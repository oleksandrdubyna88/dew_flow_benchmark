using Bench.Application.Probes;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>One scout run as the report reads it — the object <c>bench scouts report --json</c> prints and E3 S8's read API will answer,
/// computed in ONE place so the two surfaces cannot read a run two ways (<see cref="ProbeReport"/>'s rule).
/// <list type="bullet">
/// <item>Each lineage is read at its highest SETTLED generation (<see cref="ScoutGenerations.LatestSettled"/>); only scored cells carry
/// numbers, every other outcome is counted.</item>
/// <item>The analysis (§3.6) and the decision (§3.8) are the domain's (<see cref="ScoutAnalysis"/>, <see cref="ScoutDecisions"/>); this maps
/// them to the wire, every double rounded to six decimals so the bytes do not depend on the last bit of a sum.</item>
/// <item>A PRELIMINARY run (D7) carries its intervals and the verdict word <c>preliminary</c> — never a pass or a fail.</item>
/// </list></summary>
public static class ScoutReport
{
    private const int Decimals = 6;

    public static async Task<Outcome<ScoutRunReportDto>> ReadAsync(IScoutReads reads, Guid runId, BootstrapSettings settings, CancellationToken cancellationToken) =>
        await reads.RunAsync(runId, cancellationToken) switch
        {
            Outcome<ScoutRun>.Ok run => Outcome<ScoutRunReportDto>.Success(Of(
                run.Value,
                await reads.TasksAsync(runId, cancellationToken),
                await reads.CellsAsync(runId, cancellationToken),
                await reads.ScoutRecordsAsync(runId, cancellationToken),
                await reads.VerdictsAsync(runId, cancellationToken),
                await reads.AttemptsAsync(runId, cancellationToken),
                settings)),
            Outcome<ScoutRun>.Fail fail => Outcome<ScoutRunReportDto>.Failure(fail.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>Pure: the run, its tasks, EVERY cell of it (every generation), every scout record, every verdict and every attempt.</summary>
    public static ScoutRunReportDto Of(
        ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutCellRecord> records,
        IReadOnlyList<ScoutAssessedClaim> verdicts, IReadOnlyList<ScoutAttempt> attempts, BootstrapSettings settings)
    {
        var analysis = ScoutAnalysis.Of(Evidence(tasks, cells, records, verdicts), run.Arms, settings);
        var decision = ScoutDecisions.Decide(analysis.Workloads, run.Kind);
        var progress = ScoutRunProgress.Of(cells);

        return new ScoutRunReportDto(
            run.Id,
            run.CreatedAt,
            ScoutRunKinds.Word(run.Kind),
            ProbeReport.Kebab(decision.Verdict.ToString()),
            new ScoutPinsDto(run.Pins.CliVersion, run.Pins.PluginCacheHash, run.Pins.ResolvedOpus, run.Pins.ResolvedSonnet, run.Pins.SettingsHash),
            run.Seed,
            run.Repeats,
            new ScoutProgressDto(progress.Pending, progress.Claimed, progress.Settled, progress.Abandoned, progress.IsOpen),
            Ledger(run, attempts, cells),
            new ScoutBootstrapDto(settings.Seed, settings.Resamples, ScoutBootstrap.LowerPercentile, ScoutBootstrap.UpperPercentile, ScoutDecisions.RecallMargin, ScoutDecisions.WrongMargin),
            [.. decision.Workloads.Select(d => Workload(d, analysis))],
            [.. analysis.Arms.Select(Arm)],
            [.. run.Tasks.Select(id => tasks.FirstOrDefault(t => t.Id.Value == id.Value)).OfType<ScoutTask>().Select(t => Task(t, analysis.TaskArms))]);
    }

    /// <summary>The latest settled cell of every lineage, joined to its task's repository, its records and its verdicts.</summary>
    public static IReadOnlyList<ScoutCellEvidence> Evidence(
        IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutCellRecord> records, IReadOnlyList<ScoutAssessedClaim> verdicts)
    {
        var repositories = tasks.ToDictionary(t => t.Id.Value, t => t.Repository, StringComparer.Ordinal);
        var scouts = records.ToLookup(r => r.CellId, r => r.Scout);
        var said = verdicts.ToLookup(v => v.Key.CellId, v => v.Verdict);

        return [.. ScoutGenerations.LatestSettled(cells).Select(cell => new ScoutCellEvidence(
            cell,
            repositories.GetValueOrDefault(cell.Task.Value, string.Empty),
            [.. scouts[cell.Id].OrderBy(s => s.Ordinal)],
            [.. said[cell.Id]]))];
    }

    /// <summary>The ledger with its attempts counted beside it — the unknown charges, and (S6) the overshoots: attempts settled above their
    /// arm's cap, at their real total.</summary>
    private static ScoutLedgerDto Ledger(ScoutRun run, IReadOnlyList<ScoutAttempt> attempts, IReadOnlyList<ScoutCell> cells) =>
        new(run.Ledger.CeilingUsd, run.Ledger.ReservedUsd, run.Ledger.SettledUsd, run.Ledger.UnknownUsd, run.Ledger.SpentUsd, run.Ledger.FreeUsd,
            attempts.Count, attempts.Count(a => a.State == ScoutAttemptState.ChargedUnknown), ScoutOvershoots.Of(attempts, cells, run.Budgets).Count);

    private static ScoutWorkloadDto Workload(ScoutWorkloadDecision decision, ScoutAnalysisResult analysis)
    {
        var reading = analysis.Workloads.FirstOrDefault(w => w.Workload == decision.Workload);
        var (a, b) = (DecidingArm(analysis, decision.Workload, ScoutModel.Opus), DecidingArm(analysis, decision.Workload, ScoutModel.Sonnet));

        return new ScoutWorkloadDto(
            decision.Workload.ToString(), a, b,
            ProbeReport.Kebab(decision.Outcome.ToString()),
            [.. decision.Reasons.Select(r => ProbeReport.Kebab(r.ToString()))],
            Interval(reading?.Recall ?? ScoutInterval.None),
            Interval(reading?.Wrong ?? ScoutInterval.None));
    }

    private static string DecidingArm(ScoutAnalysisResult analysis, ScoutWorkload workload, ScoutModel model) =>
        analysis.Arms.Select(s => s.Arm).FirstOrDefault(arm => arm.Workload == workload && arm.Model == model && !arm.IsReplication && arm.Id != ScoutArmId.C3)?.Word ?? string.Empty;

    private static ScoutIntervalDto Interval(ScoutInterval interval) =>
        new(interval.Known, Round(interval.Point), Round(interval.Lower), Round(interval.Upper), interval.Tasks, interval.Clusters, interval.Estimable);

    private static ScoutArmReportDto Arm(ScoutArmSummary s) =>
        new(
            s.Arm.Word, s.Arm.Workload.ToString(), s.Arm.ModelAlias, s.Arm.Count, s.Arm.AgentType,
            Wire(s.Recall), Wire(s.WrongPerTen), ProbeReport.Kebab(s.WrongGap.ToString()),
            Wire(s.TeamMs), Wire(s.SlowestScoutMs), Wire(s.ScoutMs), Wire(s.SessionMs),
            new ScoutTokensDto(Wire(s.Tokens.TeamInput), Wire(s.Tokens.TeamOutput), Wire(s.Tokens.TeamCacheRead), Wire(s.Tokens.TeamCacheWrite),
                Wire(s.Tokens.ScoutInput), Wire(s.Tokens.ScoutOutput)),
            Wire(s.TeamUsd), Wire(s.SessionUsd), Wire(s.UsdPerRightFile), Wire(s.CallerLoad),
            new ScoutCountsDto(
                s.Counts.Cells, s.Counts.Scored, s.Counts.Void, s.Counts.Stopped, s.Counts.Aborted, s.Counts.SilentCells, s.Counts.SilentScouts,
                s.Counts.UnparseableClaims, s.Counts.Wrong, s.Counts.Decided, s.Counts.Unresolved, s.Counts.Unassessed, s.Counts.TeamTimeBelowSlowestScout));

    /// <summary>The task on the wire — through the one projection: a private task's repository is its label (<see cref="ScoutPublication"/>).</summary>
    private static ScoutTaskReportDto Task(ScoutTask task, IReadOnlyList<ScoutTaskArm> taskArms) =>
        new(task.Id.Value, ScoutPublication.Repository(task), ScoutTaskPrivacies.Word(task.Privacy), task.Language, ProbeReport.Kebab(task.Kind.ToString()), task.GroundTruth.Count,
            [.. taskArms.Where(t => t.Task == task.Id.Value).Select(t => new ScoutTaskArmDto(
                ScoutArms.Word(t.Arm), t.Cells, t.Scored, Wire(t.Recall),
                t.Wrong.Measured ? Wire(Domain.Gate.Figure.Of((double)t.Wrong.PerTen)) : Wire(Domain.Gate.Figure.Unknown),
                Wire(t.TeamMs), Wire(t.TeamUsd)))]);

    /// <summary>A domain figure on the wire: known with its value, <c>not-measured</c> for the §3.6 rule (carried as <c>Withheld</c>), else
    /// <c>not-captured</c> — never a zero for an absence.</summary>
    public static ScoutFigureDto Wire(Figure figure) => figure switch
    {
        { Known: true } => new ScoutFigureDto(true, Round(figure.Value), ScoutFigureDto.KnownState),
        { State: FigureState.Withheld } => new ScoutFigureDto(false, 0, ScoutFigureDto.NotMeasuredState),
        _ => new ScoutFigureDto(false, 0, ScoutFigureDto.NotCapturedState),
    };

    private static double Round(double value) => Math.Round(value, Decimals, MidpointRounding.ToEven);
}
