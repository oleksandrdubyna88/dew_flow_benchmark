using Bench.Domain.Runs;

namespace Bench.Domain.Probes;

/// <summary>The one reading of a lineage's generations a report uses (D2): the highest SETTLED generation per
/// (probe, subject, repeat) — a Pending re-run does not hide the verdict it is re-measuring, and an Abandoned later
/// generation does not either. Pure, so the read adapter and the page cannot disagree.</summary>
public static class ProbeGenerations
{
    /// <summary>One cell per lineage: its highest settled generation; lineages with no settled generation are left out.
    /// Ordered as the plan is — slot, then position — so a table reads in run order.</summary>
    public static IReadOnlyList<ProbeCell> LatestSettled(IEnumerable<ProbeCell> cells) =>
        [.. cells.Where(c => c.State == CellState.Settled)
            .GroupBy(c => c.Lineage)
            .Select(lineage => lineage.MaxBy(c => c.Generation)!)
            .OrderBy(c => c.Slot).ThenBy(c => c.Position)];

    /// <summary>The highest generation a lineage has, settled or not — what <c>rerun</c> numbers the next one after.</summary>
    public static int Highest(IEnumerable<ProbeCell> lineage) => lineage.Select(c => c.Generation).DefaultIfEmpty(0).Max();
}
