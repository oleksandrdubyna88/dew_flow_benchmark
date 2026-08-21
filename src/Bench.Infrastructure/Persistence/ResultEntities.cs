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

    /// <summary>What the machine was doing while this leg ran, as JSON.
    /// <para>
    /// <c>{}</c> is the default the migration gives every row written before a sampler existed, and it reads
    /// back as <em>not sampled</em> rather than as a leg on an idle machine — the same three-state discipline
    /// the response meta beside it already follows.
    /// </para></summary>
    public string LoadJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public List<MetricRow> Metrics { get; set; } = [];

    /// <summary>The hits retrieval surfaced for this leg, empty for the control arm.</summary>
    public List<RetrievedHitRow> Hits { get; set; } = [];

    /// <summary>Every tool call this leg made, in order. Empty for a floor leg and for a tool leg whose
    /// subject reached for nothing — which is why <see cref="ToolsOffered"/> is a separate column rather
    /// than a count of these rows.</summary>
    public List<ToolCallRow> ToolCalls { get; set; } = [];

    /// <summary>Whether this leg's lane offered tools AT ALL.
    /// <para>
    /// The one fact <c>tool_calls</c> cannot carry, because it is a statement about the rows that are not
    /// there. Without it "the lane had no tools" and "the subject ignored four of them" are the same empty
    /// set — and the second is the interesting result, the one that is evidence about the descriptions.
    /// </para></summary>
    public bool ToolsOffered { get; set; }

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

/// <summary>One tool call, as a ROW.
/// <para>
/// Rows rather than a JSON blob on the result, for the reason the metrics are rows: the question this
/// ledger exists to answer — "did subjects that located before reading score better" — is a group-by over
/// tool name and ordinal. As a blob it is a full scan that parses strings back into structure.
/// </para></summary>
public sealed class ToolCallRow
{
    public Guid Id { get; set; }

    public Guid ResultId { get; set; }

    /// <summary>Position in the whole leg, from 0 — the ordering the doctrine under test makes a claim
    /// about, and the only thing that can falsify it.</summary>
    public int Ordinal { get; set; }

    /// <summary>Which model turn asked for it. Distinct from <see cref="Ordinal"/> because one turn may
    /// request several tools at once.</summary>
    public int Turn { get; set; }

    public PhaseKind Phase { get; set; }

    public string ToolName { get; set; } = string.Empty;

    /// <summary>The arguments AS SENT. A description is measured on whether a subject can form a correct
    /// call, so the malformed ones are the evidence rather than the noise.</summary>
    public string ArgumentsJson { get; set; } = string.Empty;

    /// <summary>An expected refusal — a path outside the checkout, an argument of the wrong kind. A VALUE
    /// the subject could read and correct itself from, never an exception that ended the leg.</summary>
    public bool Refused { get; set; }

    /// <summary>Why it was refused or how it failed; empty when the call succeeded. Kept apart from
    /// <see cref="Refused"/> so "the engine said no, here is why" stays distinguishable from "the engine
    /// broke" — the distinction whose absence upstream let a false read-only guarantee stand for months.</summary>
    public string Error { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public ToolCallSource Source { get; set; }

    public ResultRow? Result { get; set; }
}
