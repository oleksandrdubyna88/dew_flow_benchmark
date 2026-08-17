using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Variants;
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
    /// <summary>Every retrieval recipe this run measures, looked up per cell.
    /// <para>
    /// <see cref="VariantRoster.Baseline"/> by default and an <c>init</c> property rather than a positional
    /// parameter, so the retrieval lane is additive: a run planned without the catalog resolves every cell to
    /// the control arm and behaves exactly as it did before this axis existed.
    /// </para></summary>
    public VariantRoster Variants { get; init; } = VariantRoster.Baseline;

    /// <summary>How much of one hit's text reaches the prompt. Configuration rather than a constant, because
    /// it changes what the subject read and therefore belongs to the run's record.</summary>
    public RagPromptLimits Prompt { get; init; } = RagPromptLimits.Default;

    /// <summary>A single-subject reading plan — the ad-hoc shape, and what a test with one subject
    /// collapses to.</summary>
    public static LegPlan Reading(Suite suite, ModelEndpoint endpoint, Sampling sampling) =>
        new(suite, SubjectRoster.Of(endpoint, sampling), [], TaskKind.Reading);

    public static LegPlan Reading(Suite suite, SubjectRoster subjects) =>
        new(suite, subjects, [], TaskKind.Reading);
}

/// <summary>One leg's resolved context, carried as a value so the methods below take four arguments instead
/// of nine. Everything here was decided before the model was asked, and none of it changes afterwards.</summary>
internal sealed record LegWork(
    RunCell Cell,
    WorkerIdentity Owner,
    LegPlan Plan,
    Question Question,
    RosterEntry Subject,
    LegDeadline Deadline,
    RetrievedContext Retrieved);

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
    IRetriever retriever,
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

        // And the same for the other axis: the cell names its variant, so the recipe is looked up rather
        // than taken from whichever one the run resolved first.
        if (plan.Variants.For(cell.Variant) is not Outcome<VariantChoice>.Ok(var variant))
        {
            return await AbandonAsync(
                cell, owner, plan.Variants.For(cell.Variant).Match(_ => string.Empty, reason => reason), cancellationToken);
        }

        var deadline = LegDeadline.For(plan.Budgets, clock.GetUtcNow());
        var retrieved = await RetrieveAsync(question, variant, deadline, cancellationToken);

        // UnansweredAsync, not AbandonAsync: retrieval is INSIDE the leg's wall, so an engine that ran the
        // clock out is a recorded CAP and an engine that failed inside the budget is a crash. Settling both
        // as crashes would report the harness as broken over a merely slow index.
        return retrieved is Outcome<RetrievedContext>.Fail unretrieved
            ? await UnansweredAsync(cell, owner, deadline, unretrieved.Reason, cancellationToken)
            : await AnswerAsync(
                new LegWork(cell, owner, plan, question, subject, deadline, ((Outcome<RetrievedContext>.Ok)retrieved).Value),
                cancellationToken);
    }

    /// <summary>Retrieval, for the arms that have any, under what the LEG has left.
    /// <para>
    /// The control arm is not asked, and that is the whole reason the definition is a closed union rather
    /// than a recipe with retrieval switched off: a baseline leg must produce
    /// <see cref="RetrievedContext.NotPerformed"/>, which every metric downstream reads as "this arm
    /// surfaces nothing" — never as a search that came back empty.
    /// </para>
    /// <para>
    /// <b>Bounded by the leg's own deadline, not by a transport default.</b> A retrieval is a wait inside a
    /// leg, and the whole argument for one deadline per leg is that every wait inside it shares that ceiling
    /// — otherwise a hung engine adds its own timeout to the model's, and the leg costs more than the arm
    /// allowed. The remainder is what the model call then receives through <c>ForCall</c>, so slow retrieval
    /// eats into thinking time rather than extending the leg.
    /// </para></summary>
    private async Task<Outcome<RetrievedContext>> RetrieveAsync(
        Question question, VariantChoice variant, LegDeadline deadline, CancellationToken cancellationToken)
    {
        if (variant.Definition is not VariantDefinition.RetrievalRecipe recipe)
        {
            return Outcome<RetrievedContext>.Success(RetrievedContext.NotPerformed);
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (deadline.IsBounded)
        {
            budget.CancelAfter(deadline.Remaining(clock.GetUtcNow()));
        }

        try
        {
            return await retriever.RetrieveAsync(new RetrievalRequest(question.Prompt, recipe), budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our ceiling, not the operator stopping the run. A value, so the leg settles as a cap rather
            // than the campaign unwinding over one slow index.
            return Outcome<RetrievedContext>.Failure(
                $"the retrieval did not answer inside the {deadline.Describe} the leg had");
        }
    }

    /// <summary>The model half: the prompt is assembled from the question AND whatever retrieval surfaced,
    /// then sent under what the leg has left of its own deadline.
    /// <para>
    /// <b>The deadline is checked BETWEEN the two steps.</b> This is the between-turns check the deadline was
    /// designed for, arriving with the first leg that has more than one step: a leg whose wall went while
    /// retrieval was working has nothing left to think with, and asking anyway would spend a completion to
    /// produce an answer generated under no budget — measuring the ceiling and calling it a score.
    /// </para></summary>
    private async Task<Outcome<LegResult>> AnswerAsync(LegWork work, CancellationToken cancellationToken)
    {
        if (work.Deadline.Exhausted(clock.GetUtcNow()))
        {
            return await UnansweredAsync(
                work.Cell, work.Owner, work.Deadline, "retrieval used the whole leg", cancellationToken);
        }

        var prompt = RagPrompt.Assemble(work.Question, work.Retrieved, work.Plan.Prompt);

        var asked = await runtime.AskAsync(
            new ModelRequest(
                work.Subject.Endpoint,
                work.Subject.Sampling,
                string.Empty,
                prompt,
                work.Deadline.ForCall(clock.GetUtcNow())),
            cancellationToken);

        return asked is Outcome<ModelAnswer>.Fail failed
            ? await UnansweredAsync(work.Cell, work.Owner, work.Deadline, failed.Reason, cancellationToken)
            : await ScoreAsync(work, prompt, ((Outcome<ModelAnswer>.Ok)asked).Value, cancellationToken);
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

    /// <summary>Scoring and persisting one leg: the answer's metrics, retrieval's metrics, and every piece
    /// of evidence the two were computed from — in ONE write, before the cell settles.</summary>
    private async Task<Outcome<LegResult>> ScoreAsync(
        LegWork work, string prompt, ModelAnswer answer, CancellationToken cancellationToken)
    {
        var stored = await results.SaveAsync(
            LegResult.Of(work.Cell.Id, prompt, answer.Text.Value, Metrics(work, answer), clock.GetUtcNow()) with
            {
                Thinking = answer.Thinking,
                Meta = ResponseMeta.Of(answer),
                Retrieval = work.Retrieved,
            },
            cancellationToken);

        if (stored is Outcome<LegResult>.Fail unsaved)
        {
            return Outcome<LegResult>.Failure(unsaved.Reason);
        }

        var settled = await runs.SettleAsync(work.Cell.Id, work.Owner, Outcome(answer), cancellationToken);

        if (settled is Outcome<RunCell>.Fail unsettled)
        {
            // The result is already durable, so this is a report rather than a loss — and it is the exact
            // window the re-entrancy check above exists to close.
            logger.LogWarning("Cell {Cell} was scored but not settled: {Reason}", work.Cell.Id, unsettled.Reason);
        }

        return stored;
    }

    /// <summary>Both mechanical readings of one leg.
    /// <para>
    /// Anchor recall stays where it was — inside <see cref="AnswerScoring"/>, fed by an observation — so
    /// there is exactly one definition of recall in the system. The rank-sensitive metrics are additive and
    /// empty for the control arm, which keeps the no-retrieval baseline carrying precisely the metric set it
    /// carried before this lane existed.
    /// </para></summary>
    private static IReadOnlyList<StoredMetric> Metrics(LegWork work, ModelAnswer answer) =>
    [
        .. AnswerScoring.Score(work.Question, answer, RetrievalScoring.Observe(work.Question, work.Retrieved)),
        .. RetrievalScoring.Score(work.Question, work.Retrieved),
    ];

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
