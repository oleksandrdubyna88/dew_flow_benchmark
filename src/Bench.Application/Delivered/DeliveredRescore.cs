using Bench.Delivered;
using Bench.Domain;
using Bench.Domain.Trace;

namespace Bench.Application.Delivered;

/// <param name="Recomputed">The policy applied again to the stored replies.</param>
/// <param name="Protocol">The protocol those replies were produced under — read from the payload, never
/// from whatever is current. A rescore under a different scale is a NEW measurement, not a correction.</param>
public sealed record RescoredResult(Guid ResultId, PolicyResult Recomputed, string Protocol)
{
    public string Describe => $"{ResultId} · {Recomputed.Describe} · {Protocol}";
}

/// <summary>Why one result could not be rescored. A reason rather than a silence, because "no payloads"
/// and "the payload no longer parses" send a reader to two different places.</summary>
public sealed record UnrescorableResult(Guid ResultId, string Reason);

public sealed record RescoreReport(
    IReadOnlyList<RescoredResult> Rescored,
    IReadOnlyList<UnrescorableResult> Skipped)
{
    public static RescoreReport Empty { get; } = new([], []);

    public string Describe =>
        $"{Rescored.Count} result(s) rescored, {Skipped.Count} skipped — no model was called";
}

/// <summary>Recomputes the delivered-work policy over stored payloads, with <b>no model call</b>.
///
/// <para><b>That property is the reason the payload table is permanent</b>, and this class is where it
/// becomes checkable rather than claimed: it takes no runtime, so it cannot call one. A rescore that could
/// reach a model would be a re-measurement wearing an old run's id — the same numbers would come back
/// different and nothing in the record would say why.</para>
///
/// <para>What it recomputes is the POLICY — the near-duplicate cap and the rescue allowance — not the gate.
/// The gate's verdict is a property of the decomposition that was accepted at the time, and re-deriving it
/// would need the diff's figures, which no payload carries. Its stored verdict is the record.</para>
/// </summary>
public sealed class DeliveredRescore(IStagePayloadStore payloads)
{
    public async Task<RescoreReport> ForResultsAsync(
        IReadOnlyList<Guid> resultIds, CancellationToken cancellationToken)
    {
        var rescored = new List<RescoredResult>();
        var skipped = new List<UnrescorableResult>();

        foreach (var resultId in resultIds)
        {
            var stored = await payloads.ForResultAsync(resultId, cancellationToken);
            var outcome = Recompute(resultId, stored);

            if (outcome is Outcome<RescoredResult>.Ok(var value))
            {
                rescored.Add(value);
            }
            else
            {
                skipped.Add(new UnrescorableResult(resultId, outcome.Match(_ => string.Empty, r => r)));
            }
        }

        return new RescoreReport(rescored, skipped);
    }

    /// <summary>One result, from its payloads alone.
    /// <para>
    /// The LAST decomposition attempt is the one that was scored — a re-ask replaces its predecessor rather
    /// than adding to it — so the walk takes the highest ordinal rather than the first. Reading the first
    /// would rescore a decomposition the run itself rejected.
    /// </para></summary>
    public static Outcome<RescoredResult> Recompute(Guid resultId, IReadOnlyList<StagePayload> stored)
    {
        if (stored.Count == 0)
        {
            return Outcome<RescoredResult>.Failure(
                "no stored payloads — this result was measured before they were kept, so it is not rescorable");
        }

        var decompose = Latest(stored, DeliveredStage.Decompose);
        var weigh = Latest(stored, DeliveredStage.Weigh);

        if (decompose is null || weigh is null)
        {
            return Outcome<RescoredResult>.Failure(
                $"incomplete payloads: {(decompose is null ? "no decomposition" : "no weighing")} was stored");
        }

        var read = DeliveredWorkReplies.ReadDecomposition(decompose.PayloadJson);

        if (read is not Reply<Decomposition>.Ok(var decomposition))
        {
            return Outcome<RescoredResult>.Failure($"the stored decomposition no longer reads: {read.Reason}");
        }

        var keys = decomposition.Steps.Select(s => s.Key).ToList();
        var scores = DeliveredWorkReplies.ReadScores(weigh.PayloadJson, keys);

        return scores is Reply<IReadOnlyList<UnitScore>>.Ok(var units)
            ? Outcome<RescoredResult>.Success(new RescoredResult(
                resultId, Apply(units, decomposition), decompose.Protocol))
            : Outcome<RescoredResult>.Failure($"the stored weighing no longer reads: {scores.Reason}");
    }

    private static PolicyResult Apply(IReadOnlyList<UnitScore> scores, Decomposition decomposition) =>
        DeliveredWorkPolicy.Apply(new PolicyInput(
            scores,
            decomposition.Steps.ToDictionary(s => s.Key, s => s.Anchor, StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            DiffUnitCount: null));

    private static StagePayload? Latest(IReadOnlyList<StagePayload> stored, DeliveredStage stage) =>
        stored.Where(p => p.Stage == stage).OrderByDescending(p => p.Ordinal).FirstOrDefault();
}
