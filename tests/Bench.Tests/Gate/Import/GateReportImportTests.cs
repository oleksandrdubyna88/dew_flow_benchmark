using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate.Import;

/// <summary>The two report widenings the import needs: the all-tasks table (the other harness's per-model population, like
/// with like) and turn-level facts read only over the runs that recorded them.</summary>
public sealed class GateReportImportTests
{
    [Fact]
    public void The_all_tasks_table_counts_calibration_tasks_that_the_default_rows_keep_apart()
    {
        var input = Input([Run("cs2", "grok-a", 1), Run("calib1", "grok-a", 1, findings: 4), Run("calib1", "grok-a", 2, findings: 6)]);

        var table = GateReport.PerModel(Scope(PinA), StrictRubric, input);

        table.Rows.Single().Runs.Should().Be(1, "the default reading keeps the calibration task apart");
        table.Calibration.Single().Runs.Should().Be(2);
        table.AllTasks.Single().Runs.Should().Be(3, "report.py's per_model reads every task, calibration included");
        table.AllTasks.Single().FindingsPerRun.Value.Should().Be(4);
    }

    [Fact]
    public void Turn_level_columns_are_read_only_over_runs_that_recorded_them()
    {
        var recorded = Run("cs2", "arm-a", 1);
        var unrecorded = Run("cs2", "arm-a", 2);
        unrecorded = unrecorded with { Facts = unrecorded.Facts with { Turns = 0, HttpCalls = 0, Served = 0, TurnFactsCaptured = false } };

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, Input([recorded, unrecorded])).Rows.Single();

        row.TurnsMean.Value.Should().Be(2, "an import that kept no ledger is not a run of zero turns");
        row.ServedMean.Value.Should().Be(6);

        var none = GateReport.PerModel(Scope(PinA), StrictRubric, Input([unrecorded]));
        none.Rows.Single().TurnsMean.State.Should().Be(FigureState.Unknown, "nobody recorded a turn — unknown, never zero");
        none.Rows.Single().RepairRuns.State.Should().Be(FigureState.Unknown, "no run recorded its calls, so nobody knows whether one was repaired");
        none.PerTask.Single().Turns.Single().State.Should().Be(FigureState.Unknown, "the per-task table reads the same flag");
    }
}
