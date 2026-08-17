namespace Bench.Infrastructure.Persistence;

/// <summary>The white-box record of one leg's retrieval — and the black-box record, with its reason, when
/// the funnel could not be read. Both are data: an engine that degraded is a fact about that engine, not an
/// absence.
/// <para>
/// One row per result, enforced by a unique index rather than by a convention. Small and fixed-size per
/// cell, so it is kept forever (<c>todo/PLAN_variant_matrix.md</c> §3.5, *What grows, and who owns it*).
/// </para></summary>
public sealed class FunnelRow
{
    public Guid Id { get; set; }

    public Guid ResultId { get; set; }

    /// <summary>Taken from the PAYLOAD rather than from what the engine declared: an engine can be
    /// configured to claim <c>trace/v0</c> while a newer build behind the same endpoint answers otherwise.</summary>
    public string ContractVersion { get; set; } = string.Empty;

    public string StagesJson { get; set; } = "[]";

    /// <summary>Measured end to end by the ENGINE, independently of its stages — never their sum. The
    /// remainder is the most valuable number here, and a sum cannot show a missing part.</summary>
    public long TotalMs { get; set; }

    /// <summary>Contract stages this engine does not perform, by name. Not stored as zeroes, which would
    /// read as "it ran and found nothing".</summary>
    public string AbsentJson { get; set; } = "[]";

    public bool Degraded { get; set; }

    public string DegradationReason { get; set; } = string.Empty;

    /// <summary>Bytes of the engine's response — the part of a cell's log volume this harness can attribute
    /// honestly, unlike a daemon's own log files.</summary>
    public long PayloadBytes { get; set; }

    /// <summary>What THIS process waited, network and deserialization included. A different claim from
    /// <see cref="TotalMs"/>, and reporting one as the other turns a slow network into a slow reranker.</summary>
    public long ElapsedMs { get; set; }

    /// <summary>Which corpus answered. Two variants are two collections, so a number compared across them
    /// is a number compared across a rebuild.</summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>The axes this run asked for, and the ones the engine said it applied.
    /// <para>
    /// Both, because an engine whose contract ignores an unknown field echoes a well-formed set that merely
    /// lacks it — so the pair is the only evidence that a variant's recipe is what actually served the
    /// query. Comparing them and BLOCKING on a mismatch is build-order step 5, which is where a blocked cell
    /// gets somewhere honest to go; storing them is what makes that check possible over runs already
    /// measured.
    /// </para></summary>
    public string RequestedAxesJson { get; set; } = "{}";

    public string AppliedAxesJson { get; set; } = "{}";

    public ResultRow? Result { get; set; }
}

/// <summary>One retrieved hit, as a ROW — the same argument as the metric rows beside it: "recall at rank 3
/// per variant" is a group-by, or it is a full scan that parses JSON back into structure.
/// <para>
/// <b>This is the table that grows.</b> At a limit of 20 it is most of a cell's bytes, and its
/// <see cref="Snippet"/> is nearly all of that. Ranks, scores, paths, spans and channels are kept forever
/// because every retrieval metric recomputes from them; the snippet TEXT is kept for a window and dropped
/// after, because the corpus at the pinned commit reproduces it. The row survives the drop intact, which is
/// the whole design — see <see cref="SnippetPrunedAt"/>.
/// </para></summary>
public sealed class RetrievedHitRow
{
    public Guid Id { get; set; }

    public Guid ResultId { get; set; }

    /// <summary>1-based position in the list the engine returned. The order IS the measurement, so it is
    /// stored rather than inferred from a row order nobody guaranteed.</summary>
    public int Rank { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int EndLine { get; set; }

    /// <summary>The readable <c>Type.Member</c> identity, which is the form a suite's anchors are authored
    /// in — and therefore the only one a retrieval metric can compare.</summary>
    public string Member { get; set; } = string.Empty;

    /// <summary>The engine's own identity for this member. Stored verbatim so a later join is possible;
    /// never matched on, because its format belongs to that engine.</summary>
    public string MemberKey { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public double Score { get; set; }

    /// <summary>Which score ordered this hit — the reranker's or the fusion's. A score with no stated origin
    /// cannot be compared to anything.</summary>
    public string Ordering { get; set; } = string.Empty;

    public string ChannelsJson { get; set; } = "[]";

    public string RanksJson { get; set; } = "[]";

    /// <summary>The hit's own source text while retention keeps it; empty once dropped.</summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>What the text WEIGHED, kept in every state — including after the drop. It is the payload
    /// accounting that justified the drop, so losing it with the text would leave nothing to report.</summary>
    public long SnippetBytes { get; set; }

    /// <summary>When retention dropped the text; default while it is still here. The same
    /// "default means it has not happened" shape as <c>CellRow.ClaimedAt</c> — and it is what separates
    /// "the engine sent no text" (bytes 0, never pruned) from "we deleted it" (bytes &gt; 0, pruned at).</summary>
    public DateTimeOffset SnippetPrunedAt { get; set; }

    /// <summary>The result's creation time, copied here deliberately.
    /// <para>
    /// Denormalised so retention can select what to drop from this table alone. It is the largest table in
    /// the system by design, and a nightly prune that had to join to <c>results</c> to decide would be a
    /// scan over both.
    /// </para></summary>
    public DateTimeOffset CreatedAt { get; set; }

    public ResultRow? Result { get; set; }
}
