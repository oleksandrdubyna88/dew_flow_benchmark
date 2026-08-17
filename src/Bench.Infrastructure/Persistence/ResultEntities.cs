using Bench.Domain.Runs;

namespace Bench.Infrastructure.Persistence;

/// <summary>What one leg produced. Joined to its cell — and through it to the run — rather than
/// duplicating the measurement key, so there is exactly one place a result's target and engine come from.</summary>
public sealed class ResultRow
{
    public Guid Id { get; set; }

    public Guid CellId { get; set; }

    public string Prompt { get; set; } = string.Empty;

    /// <summary>The subject's answer, stored so a second arbiter costs its own inference and nothing else.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>The model's own reasoning, when its runtime returned any separately from the answer.
    /// <para>
    /// Kept forever with the prompt and the answer — the trio is the artefact, and a published number that
    /// cannot be re-read against the text that produced it is a number nobody can check. Its size is a
    /// budget line rather than a cleanup target.
    /// </para></summary>
    public string ThinkingText { get; set; } = string.Empty;

    /// <summary>Why there is no thinking text, when there is none. Empty exactly when the text WAS captured,
    /// which is what keeps "this model hides its reasoning" distinguishable from "it thought about nothing" —
    /// the distinction <c>Captured</c> exists for, carried through storage rather than dropped at it.</summary>
    public string ThinkingReason { get; set; } = string.Empty;

    /// <summary>Tokens in and out, latency, stop reason, response size, and the sampling AS SENT. Every one
    /// unrecoverable after the fact, since a re-run is a different call.</summary>
    public string ResponseMetaJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public List<MetricRow> Metrics { get; set; } = [];

    /// <summary>The hits retrieval surfaced for this leg, empty for the control arm.</summary>
    public List<RetrievedHitRow> Hits { get; set; } = [];

    /// <summary>The funnel of this leg's retrieval; absent for a leg that performed none. Nullable because
    /// EF models an optional one-to-one that way — the domain reads it as
    /// <c>RetrievedContext.NotPerformed</c>, which is a state rather than a null.</summary>
    public FunnelRow? Funnel { get; set; }

    public CellRow? Cell { get; set; }
}

/// <summary>One metric, as a ROW.
/// <para>
/// This is the whole reason storage is ours rather than the library's disk store. As rows, "the average
/// anchor recall per engine on this run" is a group-by. As a JSON blob — or as a composite directory name,
/// which is what the disk store's key really is — it is a full scan that parses strings back into
/// structure, and this project refuses that trade everywhere it appears.
/// </para></summary>
public sealed class MetricRow
{
    public Guid Id { get; set; }

    public Guid ResultId { get; set; }

    public string Name { get; set; } = string.Empty;

    public MetricKind Kind { get; set; }

    public string Value { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public bool Failed { get; set; }

    /// <summary>Stored as the rating's NAME. An enum kept as its ordinal changes meaning the day somebody
    /// inserts a member, and old results staying comparable is the point of this system.</summary>
    public string Rating { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public ResultRow? Result { get; set; }
}
