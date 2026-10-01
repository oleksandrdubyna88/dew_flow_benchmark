using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Runs;

namespace Bench.Application.Probes;

/// <summary>Which lineages <c>bench probes rerun</c> appends a generation to (D2) — one cell's (<c>--cell</c>), or a subject's
/// (<c>--run --subject [--probe]</c>) — as the highest generation of each, the seed <see cref="IProbeStore.NextGenerationAsync"/>
/// numbers the next one after. Pure, so every refusal is pinned without a database.
/// <para>
/// <b>A re-run measures what it names and nothing else.</b> The campaign claims a subject's pending cells in matrix order, so a
/// re-run over a subject that still has cells Pending or Claimed — a quota stop's leftovers, a run in flight — would measure
/// those too. That is refused, naming the count and the verb that finishes them, rather than done quietly.
/// </para></summary>
public static class ProbeRerunTargets
{
    public static Outcome<IReadOnlyList<ProbeCell>> ForCell(IReadOnlyList<ProbeCell> cells, Guid cellId)
    {
        var named = cells.FirstOrDefault(c => c.Id == cellId);

        return named is null
            ? Outcome<IReadOnlyList<ProbeCell>>.Failure($"probe cell {cellId} is not a cell of this run")
            : Guarded(cells, [Seed(cells, named)]);
    }

    /// <param name="probes">The probes to re-measure; empty = every probe the subject was planned for.</param>
    public static Outcome<IReadOnlyList<ProbeCell>> ForSlice(ProbeRun run, IReadOnlyList<ProbeCell> cells, ProbeSubjectId subject, IReadOnlyList<ProbeKind> probes)
    {
        var seeds = cells
            .Where(c => c.Subject == subject && (probes.Count == 0 || probes.Contains(c.Probe)))
            .GroupBy(c => c.Lineage)
            .Select(g => g.MaxBy(c => c.Generation)!)
            .OrderBy(c => c.Slot).ThenBy(c => c.Position)
            .ToList();

        var refusal = (run.Subject(subject), seeds.Count) switch
        {
            (Outcome<ProbeSubject>.Fail f, _) => f.Reason,
            (_, 0) => $"subject '{subject}' has no cell of {string.Join(", ", probes.Select(ProbeWord.Of))} in probe run {run.Id} — the planner dropped that pair, or it was never asked for",
            _ => string.Empty,
        };

        return refusal.Length > 0 ? Outcome<IReadOnlyList<ProbeCell>>.Failure(refusal) : Guarded(cells, seeds);
    }

    private static ProbeCell Seed(IReadOnlyList<ProbeCell> cells, ProbeCell named) =>
        cells.Where(c => c.Lineage == named.Lineage).MaxBy(c => c.Generation)!;

    private static Outcome<IReadOnlyList<ProbeCell>> Guarded(IReadOnlyList<ProbeCell> cells, IReadOnlyList<ProbeCell> seeds)
    {
        var subjects = seeds.Select(s => s.Subject).ToHashSet();
        var open = cells.Where(c => subjects.Contains(c.Subject) && c.State is CellState.Pending or CellState.Claimed).ToList();

        return open.Count == 0
            ? Outcome<IReadOnlyList<ProbeCell>>.Success(seeds)
            : Outcome<IReadOnlyList<ProbeCell>>.Failure(
                $"subject(s) {string.Join(", ", open.Select(c => c.Subject.Value).Distinct(StringComparer.Ordinal))} still have "
                + $"{open.Count(c => c.State == CellState.Pending)} Pending and {open.Count(c => c.State == CellState.Claimed)} Claimed cell(s) in probe run {open[0].RunId} — "
                + $"a re-run measures only what it names; finish them first (bench probes resume --run {open[0].RunId})");
    }
}
