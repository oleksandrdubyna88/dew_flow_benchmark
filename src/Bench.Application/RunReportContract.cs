using Bench.Contracts;
using Bench.Domain.Runs;

namespace Bench.Application;

/// <summary>Flattening the typed report onto the wire.
/// <para>
/// It lives here rather than in a surface because both surfaces need the SAME answer:
/// <c>bench report --json</c> serialises what this returns, and so does the API. Written once, the promise
/// <see cref="RunPlanDto"/>'s comment makes — <em>an agent reading the CLI and a browser reading the API
/// never see different truths</em> — holds by construction instead of by two renderers agreeing until
/// somebody edits one.
/// </para>
/// <para>
/// The typed view stays for the human rendering, where a <c>switch</c> over <c>ProofState</c> is a compiler
/// error when a member is added and a switch over a string is not.
/// </para></summary>
public static class RunReportContract
{
    public static RunReportDto From(RunReportView view) => new(
        view.RunId,
        view.Label,
        view.TargetCanonical,
        view.SuiteStamp,
        view.EngineCanonical,
        view.MetricName,
        view.Scoreboard.Scored,
        view.Scoreboard.Passed,
        view.SelectionQuestions,
        view.HeldOutQuestions,
        [.. view.Dimensions.Select(From)],
        From(view.Discrimination),
        view.Warnings,
        From(view.Machine),
        view.Load.Describe);

    private static MachineDto From(Bench.Domain.Trace.MachineFacts machine) => new(
        machine.Recorded,
        machine.Hostname,
        machine.Fingerprint,
        machine.Os.Describe,
        machine.Cpu.Describe,
        machine.TotalRamBytes,
        [.. machine.Adapters.Select(a => a.Describe)],
        [.. machine.UnhealthyAdapters.Select(a => a.Describe)],
        machine.Volume.Describe);

    public static RunSummaryDto From(BenchRun run) => new(
        run.Id,
        run.Label,
        run.Target.Canonical,
        run.SuiteStamp,
        run.Engine.Canonical,
        run.Engine.Backend is Bench.Domain.Retrieval.BackendDeclaration.Declared declared
            ? declared.Backend.Canonical
            : ComputeArmLabel.NotDeclared,
        run.Status.ToString(),
        run.CreatedAt);

    private static DimensionReportDto From(DimensionReport dimension) => new(
        dimension.Dimension.ToString(),
        [.. dimension.Arms.Select(From)],
        dimension.Baseline,
        dimension.RankingRefusal);

    private static ArmReadingDto From(ArmReading arm) => new(
        arm.Arm,
        arm.Average,
        arm.Legs,
        From(arm.Selection),
        From(arm.HeldOut),
        arm.Proof.ToString(),
        arm.Margin);

    private static HalfReadingDto From(HalfReading half) => new(half.Measured, half.Average, half.Legs);

    private static DiscriminationDto From(DiscriminationReport report) => new(
        report.Discriminating,
        report.EveryonePasses,
        report.NobodyPasses,
        report.TooClose,
        report.Unusable,
        report.Describe);
}
