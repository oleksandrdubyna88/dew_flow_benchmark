using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Microsoft.Extensions.Logging;

namespace Bench.Application;

/// <param name="Suite">The frozen suite this run measures. Questions are found in it by the id on the cell.</param>
/// <param name="Endpoint">Where the subject lives and what its tokens cost.</param>
/// <param name="Budgets">Only ceilings a runtime has ACCEPTED belong here.</param>
public sealed record LegPlan(
    Suite Suite,
    ModelEndpoint Endpoint,
    Sampling Sampling,
    IReadOnlyList<Budget> Budgets,
    TaskKind Kind)
{
    public static LegPlan Reading(Suite suite, ModelEndpoint endpoint, Sampling sampling) =>
        new(suite, endpoint, sampling, [], TaskKind.Reading);
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
        Guid runId, string owner, LegPlan plan, CancellationToken cancellationToken)
    {
        var claim = await runs.ClaimNextAsync(runId, owner, cancellationToken);

        if (claim is Outcome<RunCell>.Fail nothing)
        {
            return Outcome<LegResult>.Failure(nothing.Reason);
        }

        return await RunAsync(((Outcome<RunCell>.Ok)claim).Value, owner, plan, cancellationToken);
    }

    private async Task<Outcome<LegResult>> RunAsync(
        RunCell cell, string owner, LegPlan plan, CancellationToken cancellationToken)
    {
        if (await results.HasResultAsync(cell.Id, cancellationToken))
        {
            // The interrupted case: scored, never settled. Finish that rather than measure it twice.
            logger.LogInformation("Cell {Cell} was already scored — settling the leg a crash left open", cell.Id);
            await runs.SettleAsync(cell.Id, owner, new LegOutcome.Completed(), cancellationToken);
            return Outcome<LegResult>.Failure($"cell {cell.Id} was already scored; the open leg is now settled");
        }

        var question = plan.Suite.Questions.FirstOrDefault(q => q.Id == cell.QuestionId);

        if (question is null)
        {
            return await AbandonAsync(cell, owner, $"suite {plan.Suite.Stamp} has no question '{cell.QuestionId}'", cancellationToken);
        }

        var asked = await runtime.AskAsync(
            new ModelRequest(plan.Endpoint, plan.Sampling, string.Empty, question.Prompt, plan.Budgets),
            cancellationToken);

        if (asked is Outcome<ModelAnswer>.Fail failed)
        {
            // A leg that produced no answer has nothing to score. It is settled as crashed rather than
            // stored as a zero, because a subject that was never reached did not get anything wrong.
            return await AbandonAsync(cell, owner, failed.Reason, cancellationToken);
        }

        return await ScoreAsync(cell, owner, plan, question, ((Outcome<ModelAnswer>.Ok)asked).Value, cancellationToken);
    }

    private async Task<Outcome<LegResult>> ScoreAsync(
        RunCell cell,
        string owner,
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
        RunCell cell, string owner, string reason, CancellationToken cancellationToken)
    {
        await runs.SettleAsync(cell.Id, owner, new LegOutcome.Crashed(reason), cancellationToken);
        return Outcome<LegResult>.Failure(reason);
    }
}
