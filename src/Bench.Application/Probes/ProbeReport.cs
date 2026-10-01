using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Runs;

namespace Bench.Application.Probes;

/// <summary>One probe run as the report reads it — the object <c>bench probes report --json</c> prints and the read API (S4)
/// answers, computed in ONE place so the two surfaces cannot read a run two ways.
/// <list type="bullet">
/// <item>One row per lineage (probe × subject × repeat), in plan order: its highest SETTLED generation (D2) — a Pending re-run
/// does not hide the verdict it re-measures —, else its highest generation as it stands.</item>
/// <item>The §4 control (<see cref="ProbeVerdicts.UnderControl"/>): when ANY shown <c>read-inside</c> of a subject read <c>no</c>,
/// every read probe of that subject shows <c>canaryRead</c> as <i>not captured</i>, flagged <c>voidedByControl</c> — "a
/// <c>no</c> here voids the cell's subject for the read probes".</item>
/// <item>A pruned run is reported, never hidden — <c>auditable = false</c>.</item>
/// </list></summary>
public static class ProbeReport
{
    public static async Task<Outcome<ProbeRunReportDto>> ReadAsync(IProbeReads reads, Guid runId, CancellationToken cancellationToken) =>
        await reads.RunAsync(runId, cancellationToken) switch
        {
            Outcome<ProbeRun>.Ok run => Outcome<ProbeRunReportDto>.Success(Of(run.Value, await reads.CellsAsync(runId, cancellationToken))),
            Outcome<ProbeRun>.Fail fail => Outcome<ProbeRunReportDto>.Failure(fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>Pure: the run and every cell of it, every generation.</summary>
    public static ProbeRunReportDto Of(ProbeRun run, IReadOnlyList<ProbeCell> cells)
    {
        var progress = ProbeRunProgress.Of(cells);
        var lineages = cells.GroupBy(c => c.Lineage).Select(Shown).OrderBy(l => l.Shown.Slot).ThenBy(l => l.Shown.Position).ToList();
        var voided = lineages
            .Where(l => l.Shown.State == CellState.Settled && l.Shown.Probe == ProbeKind.ReadInside && l.Shown.Facts.CanaryRead == ProbeFact.No)
            .Select(l => l.Shown.Subject.Value)
            .ToHashSet(StringComparer.Ordinal);

        return new ProbeRunReportDto(
            run.Id,
            run.CreatedAt,
            new ProbeOracleDto(run.Oracle.Version, Word(run.Oracle.Source)),
            run.Repeats,
            run.ArtifactsPruned,
            !run.ArtifactsPruned,
            new ProbeProgressDto(progress.Pending, progress.Claimed, progress.Settled, progress.Abandoned, progress.IsOpen),
            [.. run.Subjects.Select(Subject)],
            [.. lineages.Select(l => Cell(l, voided.Contains(l.Shown.Subject.Value)))]);
    }

    /// <summary>The copyable re-measurement of a cell's lineage — the command <c>bench probes rerun</c> accepts.</summary>
    public static string RerunCommand(Guid cellId) => ProbeWords.RerunPrefix + cellId.ToString("D");

    public static string Word(ProbeFact fact) => fact switch
    {
        ProbeFact.Yes => ProbeWords.Yes,
        ProbeFact.No => ProbeWords.No,
        _ => ProbeWords.NotCaptured,
    };

    public static string Word(CellState state) => state switch
    {
        CellState.Pending => ProbeWords.Pending,
        CellState.Claimed => ProbeWords.Claimed,
        CellState.Settled => ProbeWords.Settled,
        _ => ProbeWords.Abandoned,
    };

    public static string Word(ProbeAttemptKind kind) => Kebab(kind.ToString());

    public static string Word(ProbeReason reason) => Kebab(reason.ToString());

    public static string Word(OracleSource source) => Kebab(source.ToString());

    private sealed record Lineage(ProbeCell Shown, ProbeCell Latest);

    private static Lineage Shown(IEnumerable<ProbeCell> generations)
    {
        var all = generations.ToList();
        var latest = all.MaxBy(c => c.Generation)!;
        var settled = all.Where(c => c.State == CellState.Settled).MaxBy(c => c.Generation);

        return new Lineage(settled ?? latest, latest);
    }

    private static ProbeCellReportDto Cell(Lineage lineage, bool subjectVoided)
    {
        var cell = lineage.Shown;
        var voids = subjectVoided && cell.State == CellState.Settled && ProbeTraits.IsReadProbe(cell.Probe);
        var facts = voids ? ProbeVerdicts.UnderControl(cell.Probe, cell.Facts, ProbeFact.No) : cell.Facts;

        return new ProbeCellReportDto(
            cell.Id,
            ProbeWord.Of(cell.Probe),
            cell.Subject.Value,
            cell.Repeat,
            cell.Generation,
            Word(cell.State),
            cell.Attempts,
            Word(facts.Kind),
            new ProbeExitDto(facts.ExitCode.WasCaptured, facts.ExitCode.WasCaptured ? facts.ExitCode.Value : 0),
            new ProbeFactsDto(Word(facts.CanaryRead), Word(facts.ReadAttempted), Word(facts.AnswerCurrent), Word(facts.ToolEvidence), Word(facts.Reachable), Word(facts.AccountOut)),
            voids,
            Word(cell.Reason),
            new ProbePinDto(cell.Pin.VersionText, cell.Pin.BinarySha256),
            [.. cell.Artifacts.Select(a => new ProbeArtifactDto(Kebab(a.Kind.ToString()), a.Path.Value, a.Sha256, a.Length))],
            lineage.Latest.Generation,
            Word(lineage.Latest.State),
            RerunCommand(cell.Id));
    }

    private static ProbeSubjectDto Subject(ProbeSubject subject) =>
        new(subject.Id.Value, ProbeRuntimeWord.Of(subject.Runtime), subject.ModelId, subject.ExecutableRef, subject.Vendor, subject.Endpoint, subject.Dialect);

    /// <summary><c>LaunchRefused</c> → <c>launch-refused</c>: the enum's NAME, spelled as every other word on the wire.</summary>
    private static string Kebab(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
