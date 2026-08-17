using Bench.Domain.Retrieval;

namespace Bench.Infrastructure.Persistence;

/// <summary>How far along one corpus is toward being measurable, as a row.
/// <para>
/// Keyed by <c>(CommitSha, RecipeHash, EngineEndpoint)</c> under a unique index, which is the whole
/// concurrency story: two runs that want the same index at the same moment must reach ONE row, or the matrix
/// holds two readiness answers for one collection with nothing able to say which is current.
/// </para>
/// <para>
/// This table is <b>bounded by the matrix</b>, not by the campaign: one row per corpus, not per cell or per
/// run — the plan's own projection is 96 corpora. So it has no retention rule and needs none, which is a
/// statement made here deliberately rather than an omission (`reliability.md` § Everything that grows has an
/// owner).
/// </para></summary>
public sealed class PreparationRow
{
    public Guid Id { get; set; }

    public string CommitSha { get; set; } = string.Empty;

    /// <summary>The CORPUS recipe's hash, never the whole variant's: two variants differing only in their
    /// result limit share one index, and keying by the variant would build it twice.</summary>
    public string RecipeHash { get; set; } = string.Empty;

    /// <summary>Two daemons are two indexes, even at one commit and one recipe.</summary>
    public string EngineEndpoint { get; set; } = string.Empty;

    public PreparationState State { get; set; }

    /// <summary>The owner's label, host and pid — the same triple the cell queue records, and for the same
    /// reason: a pid without its machine is a sweep reaching a confident wrong answer.</summary>
    public string Owner { get; set; } = string.Empty;

    public string OwnerHost { get; set; } = string.Empty;

    public int OwnerPid { get; set; }

    /// <summary>The engine's own id for the pass building this corpus, so a poll asks about THAT pass rather
    /// than the newest one and hopes.</summary>
    public string PassId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Refreshed while the pass runs; default until one starts. What separates a worker still
    /// watching a long pass from one that died early.</summary>
    public DateTimeOffset Heartbeat { get; set; }
}
