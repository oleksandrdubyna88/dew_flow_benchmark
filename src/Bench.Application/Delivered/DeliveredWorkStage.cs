using Bench.Delivered;
using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Application.Delivered;

/// <param name="Diff">The arm's produced diff, as git gave it.</param>
/// <param name="Endpoint">Where to ask, and what its tokens cost.</param>
public sealed record DeliveredWorkRequest(
    Guid ResultId,
    string Diff,
    ModelEndpoint Endpoint,
    Sampling Sampling,
    IReadOnlyList<Budget> Budgets);

/// <param name="Figures">Computed from the diff alone — no model touched them, which is why they survive
/// even when the model half refuses.</param>
/// <param name="Policy">The corrected scores and the trail of what changed them.</param>
/// <param name="Coverage">The gate's verdict, carried whatever it decided: a capped run must never read
/// like a clean one.</param>
public sealed record DeliveredWorkScore(
    LocFigures Figures,
    PolicyResult Policy,
    CoverageVerdict Coverage,
    string Protocol)
{
    public string Describe =>
        $"{Policy.Total} over {Policy.Applied.Count} step(s) · {Figures.Describe} · "
        + $"coverage {Coverage.Status} · {Protocol}";
}

/// <summary>The delivered-work score, end to end: decompose, gate, weigh, correct.
///
/// <para><b>This is the ONLY orchestration, and that is load-bearing rather than stylistic.</b> Every rule
/// lives in <c>Bench.Delivered</c>, a leaf that never calls a model or touches a store; this class owns the
/// IO and owns none of the rules. The property that arrangement protects is that policy and figures
/// recompute over stored payloads with zero calls — which holds only while there is exactly one place doing
/// the asking, and would quietly stop holding the day a second appeared.</para>
///
/// <para><b>The figures are computed before anything is asked</b>, so a run whose model half fails still
/// has what the diff itself says. A refusal answering nothing at all would discard evidence that cost no
/// call to produce.</para>
/// </summary>
public sealed class DeliveredWorkStage(
    IModelRuntime runtime, IStagePayloadStore payloads, TimeProvider clock)
{
    /// <summary>The gate allows exactly one re-ask, so the decomposition is asked at most twice.</summary>
    private const int MaxAttempts = 2;

    public async Task<Outcome<DeliveredWorkScore>> ScoreAsync(
        DeliveredWorkRequest request, CancellationToken cancellationToken)
    {
        var cleaned = DiffCleaner.Clean(request.Diff);
        var figures = LocCalculator.Compute(request.Diff);

        var decomposed = await DecomposeAsync(request, cleaned.Diff, figures, cancellationToken);

        return decomposed is Outcome<Decomposed>.Ok(var accepted)
            ? await WeighAsync(request, cleaned.Diff, figures, accepted, cancellationToken)
            : Outcome<DeliveredWorkScore>.Failure(decomposed.Match(_ => string.Empty, r => r));
    }

    private sealed record Decomposed(Decomposition Value, CoverageVerdict Verdict);

    /// <summary>Asks for the decomposition, gates it, and re-asks once when the gate says to.</summary>
    private async Task<Outcome<Decomposed>> DecomposeAsync(
        DeliveredWorkRequest request,
        string cleanedDiff,
        LocFigures figures,
        CancellationToken cancellationToken)
    {
        var user = DeliveredWorkPrompts.Diff(cleanedDiff);
        var last = string.Empty;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var asked = await AskAsync(
                request, DeliveredStage.Decompose, attempt,
                DeliveredWorkPrompts.DecomposeSystem, user, cancellationToken);

            if (asked is not Outcome<string>.Ok(var text))
            {
                return Outcome<Decomposed>.Failure(asked.Match(_ => string.Empty, r => r));
            }

            var read = DeliveredWorkReplies.ReadDecomposition(text);

            if (read is not Reply<Decomposition>.Ok(var decomposition))
            {
                // An unreadable reply spends the same attempt a thin one does. Not a free retry: the
                // budget is about how many times a model is ASKED, not about why an answer failed.
                last = read.Reason;
                user = $"{DeliveredWorkPrompts.Diff(cleanedDiff)}\n\n"
                    + $"Your previous answer could not be read: {read.Reason}";
                continue;
            }

            var verdict = Gate(decomposition, figures, attempt + 1);
            await RecordCoverageAsync(request, attempt, verdict, cancellationToken);

            if (verdict.Action == CoverageAction.Accept)
            {
                return Outcome<Decomposed>.Success(new Decomposed(decomposition, verdict));
            }

            if (verdict.Action == CoverageAction.Fail)
            {
                return Outcome<Decomposed>.Failure($"the decomposition was refused: {verdict.Note}");
            }

            last = verdict.Note;
            user = $"{DeliveredWorkPrompts.Diff(cleanedDiff)}\n\n{DeliveredWorkPrompts.ReAsk(verdict)}";
        }

        return Outcome<Decomposed>.Failure($"the decomposition was refused: {last}");
    }

    /// <summary>The gate's numerator is how many anchored steps the decomposition produced.
    /// <para>
    /// <b>Chosen here rather than inherited</b>, because the source divided by its own grain measure and
    /// grain is explicitly not ported — its own report says it cannot tell padding from work. Steps against
    /// cleaned churn is the honest first choice on this corpus, and it is exactly the number the
    /// recalibration arm exists to check.
    /// </para></summary>
    private static CoverageVerdict Gate(Decomposition decomposition, LocFigures figures, int attempt) =>
        CoverageDecision.Evaluate(
            accounted: decomposition.Steps.Count,
            cleanLoc: figures.Cleaned,
            capped: decomposition.Capped,
            reason: decomposition.Reason,
            attempt: attempt,
            maxAttempts: MaxAttempts,
            coverableLines: figures.Cleaned);

    private async Task<Outcome<DeliveredWorkScore>> WeighAsync(
        DeliveredWorkRequest request,
        string cleanedDiff,
        LocFigures figures,
        Decomposed decomposed,
        CancellationToken cancellationToken)
    {
        var keys = decomposed.Value.Steps.Select(s => s.Key).ToList();

        var asked = await AskAsync(
            request, DeliveredStage.Weigh, 0,
            DeliveredWorkPrompts.WeighSystem,
            $"{DeliveredWorkPrompts.Diff(cleanedDiff)}\n\n{DeliveredWorkPrompts.Steps(decomposed.Value.Steps)}",
            cancellationToken);

        if (asked is not Outcome<string>.Ok(var text))
        {
            return Outcome<DeliveredWorkScore>.Failure(asked.Match(_ => string.Empty, r => r));
        }

        var read = DeliveredWorkReplies.ReadScores(text, keys);

        return read is Reply<IReadOnlyList<UnitScore>>.Ok(var scores)
            ? Outcome<DeliveredWorkScore>.Success(new DeliveredWorkScore(
                figures, Correct(scores, decomposed.Value), decomposed.Verdict, WeightingProtocol.Protocol))
            : Outcome<DeliveredWorkScore>.Failure($"the weighing could not be read: {read.Reason}");
    }

    /// <summary>Nothing is RESCUED in this pipeline: it has no adjudicator, so every unit is matched by
    /// construction and the allowance has nothing to trim. It stays wired rather than removed, because the
    /// code lane's later arms may add one and a rule deleted is a rule re-derived.</summary>
    private static PolicyResult Correct(IReadOnlyList<UnitScore> scores, Decomposition decomposition) =>
        DeliveredWorkPolicy.Apply(new PolicyInput(
            scores,
            decomposition.Steps.ToDictionary(s => s.Key, s => s.Anchor, StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            DiffUnitCount: null));

    /// <summary>One ask, recorded before it is read.
    /// <para>
    /// The payload is stored whatever it turns out to contain — an unreadable reply is exactly the evidence
    /// somebody wants when asking why a leg scored nothing, and a stage that stored only the replies it
    /// could parse would throw that away.
    /// </para></summary>
    private async Task<Outcome<string>> AskAsync(
        DeliveredWorkRequest request,
        DeliveredStage stage,
        int ordinal,
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var answered = await runtime.AskAsync(
            new ModelRequest(request.Endpoint, request.Sampling, system, user, request.Budgets),
            cancellationToken);

        if (answered is not Outcome<ModelAnswer>.Ok(var answer))
        {
            return Outcome<string>.Failure($"the {stage} ask did not answer: {answered.Match(_ => string.Empty, r => r)}");
        }

        var text = answer.Text.Value;

        await payloads.AppendAsync(
            StagePayload.Of(
                request.ResultId, stage, ordinal, text,
                StableHash.Of(system + user), WeightingProtocol.Protocol, clock.GetUtcNow()),
            cancellationToken);

        return Outcome<string>.Success(text);
    }

    /// <summary>The gate's own verdict is a payload too, so a rescore sees what was decided without
    /// re-deriving it — and so a reader can tell a re-ask that helped from one that did not.</summary>
    private Task RecordCoverageAsync(
        DeliveredWorkRequest request,
        int ordinal,
        CoverageVerdict verdict,
        CancellationToken cancellationToken) =>
        payloads.AppendAsync(
            StagePayload.Of(
                request.ResultId,
                DeliveredStage.Coverage,
                ordinal,
                CoverageJson(verdict),
                promptHash: string.Empty,
                WeightingProtocol.Protocol,
                clock.GetUtcNow()),
            cancellationToken);

    private static string CoverageJson(CoverageVerdict verdict) =>
        $"{{\"action\":\"{verdict.Action}\",\"status\":\"{verdict.Status}\","
        + $"\"coverage\":{verdict.Coverage},\"threshold\":{verdict.Threshold},\"band\":{verdict.Band}}}";
}
