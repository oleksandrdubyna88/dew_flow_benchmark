using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Microsoft.Extensions.Logging;

namespace Bench.Application;

/// <param name="Suite">The frozen suite this run measures. Questions are found in it by the id on the cell.</param>
/// <param name="Subjects">Every subject of this run, and where each one's legs are sent. A LIST, because a
/// run has always been able to plan several while the runner held one endpoint — which would have sent
/// every leg to the first model and labelled the results with the cell's subject.</param>
/// <param name="Budgets">Only ceilings a runtime has ACCEPTED belong here.</param>
public sealed record LegPlan(
    Suite Suite,
    SubjectRoster Subjects,
    IReadOnlyList<Budget> Budgets,
    TaskKind Kind)
{
    /// <summary>A single-subject reading plan — the ad-hoc shape, and what a test with one subject
    /// collapses to.</summary>
    public static LegPlan Reading(Suite suite, ModelEndpoint endpoint, Sampling sampling) =>
        new(suite, SubjectRoster.Of(endpoint, sampling), [], TaskKind.Reading);

    public static LegPlan Reading(Suite suite, SubjectRoster subjects) =>
        new(suite, subjects, [], TaskKind.Reading);
}

/// <summary>One leg, end to end: claim a cell, run its phases, score the answer, store it, settle.
/// <para>
/// Every piece it uses existed separately before this class and none of them were connected. This is the
/// assembly, and it is where the design is actually tested — a seam that looked fine in isolation is only
/// proved by something driving all of them in order.
/// </para>
/// <para>
/// <b>Result first, settle second, and re-entrant across the gap.</b> A crash between the two leaves the
/// cell claimed rather than settled, so the sweep hands it back and a retry runs again — at which point the
/// result store refuses the duplicate. That refusal must not deadlock the retry, so the runner asks whether
/// the leg was already scored and, if it was, finishes the job it interrupted instead of starting over.
/// </para></summary>
public sealed class LegRunner(
    IRunStore runs,
    IResultStore results,
    IModelRuntime runtime,
    TimeProvider clock,
    ILogger<LegRunner> logger)
{
    /// <summary>Claims one cell and runs it. "Nothing to claim" is a VALUE — several workers draining a
    /// queue is the expected shape, and a loser must not treat losing as a fault.</summary>
    public async Task<Outcome<LegResult>> RunNextAsync(
        Guid runId, WorkerIdentity owner, LegPlan plan, CancellationToken cancellationToken)
    {
        var claim = await runs.ClaimNextAsync(runId, owner, cancellationToken);

        if (claim is Outcome<RunCell>.Fail nothing)
        {
            return Outcome<LegResult>.Failure(nothing.Reason);
        }

        return await RunAsync(((Outcome<RunCell>.Ok)claim).Value, owner, plan, cancellationToken);
    }

    private async Task<Outcome<LegResult>> RunAsync(
        RunCell cell, WorkerIdentity owner, LegPlan plan, CancellationToken cancellationToken)
    {
        if (await results.HasResultAsync(cell.Id, cancellationToken))
        {
            // The interrupted case: scored, never settled. Finish that rather than measure it twice.
            logger.LogInformation("Cell {Cell} was already scored — settling the leg a crash left open", cell.Id);
            await runs.SettleAsync(cell.Id, owner, new LegOutcome.Completed(), cancellationToken);
            return Outcome<LegResult>.Failure($"cell {cell.Id} was already scored; the open leg is now settled");
        }

        var question = plan.Suite.Questions.FirstOrDefault(q => q.Id == cell.QuestionId);

        return question is null
            ? await AbandonAsync(cell, owner, $"suite {plan.Suite.Stamp} has no question '{cell.QuestionId}'", cancellationToken)
            : await AskAsync(cell, owner, plan, question, cancellationToken);
    }

    /// <summary>The measured part of the leg, under ONE deadline.
    /// <para>
    /// The deadline is created here rather than per call, and that placement is the point: a lane that
    /// answers in one completion and a lane that loops twenty-five times must cost the same wall clock
    /// ceiling. When the loop lands it turns here, reading <see cref="LegDeadline.Exhausted"/> between
    /// turns and passing <see cref="LegDeadline.ForCall"/> down — never a fresh budget per turn.
    /// </para></summary>
    private async Task<Outcome<LegResult>> AskAsync(
        RunCell cell, WorkerIdentity owner, LegPlan plan, Question question, CancellationToken cancellationToken)
    {
        // The cell says which subject this leg is FOR, so the endpoint is looked up rather than assumed.
        // A miss is settled, never defaulted: a leg sent to another subject's endpoint would carry this
        // cell's label and be invisible in every number built afterwards.
        if (plan.Subjects.For(cell.SubjectModelId) is not Outcome<RosterEntry>.Ok(var subject))
        {
            return await AbandonAsync(
                cell, owner, plan.Subjects.For(cell.SubjectModelId).Match(_ => string.Empty, reason => reason), cancellationToken);
        }

        var deadline = LegDeadline.For(plan.Budgets, clock.GetUtcNow());

        var asked = await runtime.AskAsync(
            new ModelRequest(subject.Endpoint, subject.Sampling, string.Empty, question.Prompt, deadline.ForCall(clock.GetUtcNow())),
            cancellationToken);

        return asked is Outcome<ModelAnswer>.Fail failed
            ? await UnansweredAsync(cell, owner, deadline, failed.Reason, cancellationToken)
            : await ScoreAsync(cell, owner, plan, question, ((Outcome<ModelAnswer>.Ok)asked).Value, cancellationToken);
    }

    /// <summary>A leg that produced no answer: a CEILING when its own wall ran out, a crash otherwise.
    /// <para>
    /// The distinction is the reason this method exists. Both cases used to settle as
    /// <see cref="LegOutcome.Crashed"/>, which reads as "the harness is broken" — so a campaign against a
    /// merely slow endpoint would report its own instrument as faulty, and a genuinely broken one would
    /// look identical to it. Neither is scored: a subject that was never reached, and one cut off at a
    /// ceiling, did not get anything wrong.
    /// </para></summary>
    private async Task<Outcome<LegResult>> UnansweredAsync(
        RunCell cell, WorkerIdentity owner, LegDeadline deadline, string reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        if (!deadline.Exhausted(now))
        {
            return await AbandonAsync(cell, owner, reason, cancellationToken);
        }

        await runs.SettleAsync(cell.Id, owner, deadline.Cap(now), cancellationToken);
        logger.LogInformation("Cell {Cell} spent its {Budget} leg wall budget", cell.Id, deadline.Describe);

        return Outcome<LegResult>.Failure($"{reason} — the leg spent its {deadline.Describe} wall budget");
    }

    private async Task<Outcome<LegResult>> ScoreAsync(
        RunCell cell,
        WorkerIdentity owner,
        LegPlan plan,
        Question question,
        ModelAnswer answer,
        CancellationToken cancellationToken)
    {
        var metrics = AnswerScoring.Score(question, answer, Retrieval(plan));

        var stored = await results.SaveAsync(
            LegResult.Of(cell.Id, question.Prompt, answer.Text.Value, metrics, clock.GetUtcNow()),
            cancellationToken);

        if (stored is Outcome<LegResult>.Fail unsaved)
        {
            return Outcome<LegResult>.Failure(unsaved.Reason);
        }

        var settled = await runs.SettleAsync(cell.Id, owner, Outcome(answer), cancellationToken);

        if (settled is Outcome<RunCell>.Fail unsettled)
        {
            // The result is already durable, so this is a report rather than a loss — and it is the exact
            // window the re-entrancy check above exists to close.
            logger.LogWarning("Cell {Cell} was scored but not settled: {Reason}", cell.Id, unsettled.Reason);
        }

        return stored;
    }

    /// <summary>What this lane could surface. A lane with no tools surfaces nothing, and that is a fact
    /// about the ARM — the scorer must not read it as the subject missing an anchor.</summary>
    private static RetrievalObservation Retrieval(LegPlan plan) => RetrievalObservation.None;

    /// <summary>An answer cut off at a ceiling ends the leg as a CAP, not as a completion. Scored as a
    /// wrong answer it would measure the ceiling, and a capped leg is excluded from paired deltas — which
    /// is only possible if the ceiling is recorded here rather than inferred from the text later.</summary>
    private static LegOutcome Outcome(ModelAnswer answer) =>
        answer.WasCutOff
            ? new LegOutcome.CapExceeded(BudgetKind.Context, BudgetScope.Question, 0, 0)
            : new LegOutcome.Completed();

    private async Task<Outcome<LegResult>> AbandonAsync(
        RunCell cell, WorkerIdentity owner, string reason, CancellationToken cancellationToken)
    {
        await runs.SettleAsync(cell.Id, owner, new LegOutcome.Crashed(reason), cancellationToken);
        return Outcome<LegResult>.Failure(reason);
    }
}
