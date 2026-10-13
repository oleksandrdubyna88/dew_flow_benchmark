using System.Globalization;
using System.Text.Json;
using Bench.Cli;
using Bench.Contracts;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>Money before the first paid call (plan §3.10, E2 S6) through the verbs, against the fake CLI, a real database and real trees: a
/// pilot's bounds refused by name; a preliminary run taking its caps and walls from <c>schedule.json</c> only; an attempt whose
/// <c>total_cost_usd</c> ($4.10) exceeds its cap ($3) settled at $4.10 — never clipped — counted by <c>status</c> and <c>report</c>, and the next
/// reservation refused when it no longer fits; <c>bench scouts schedule</c> writing E3's caps and walls from the pilot's settled cells. Nothing
/// paid runs.</summary>
[Collection("postgres")]
public sealed class ScoutsMoneyTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly ScoutArmId[] A1 = [ScoutArmId.A1];

    [Fact]
    public async Task An_overshoot_settles_at_the_real_total_is_counted_and_the_next_reservation_is_refused_when_it_no_longer_fits()
    {
        // ceiling $7.50, cap $3 → each reservation $3.60. Cell one reports $4.10: settled at $4.10 (committed $4.10); cell two asks $3.60 and
        // $4.10 + $3.60 = $7.70 > $7.50 → refused, nothing launched. Clipped to the cap it would fit ($3 + $3.60 = $6.60) — the break-it.
        await using var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 2);
        setup.Answer(A1, (_, _) => CellVariant.Overshoot);

        var (code, output, error) = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, ceilingUsd: 7.5m, capUsd: 3m));

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("does not fit").And.Contain("nothing was launched");
        var runId = ScoutsCliSetup.RunIdOf(output);
        var attempt = (await setup.NewStore().AttemptsAsync(runId, Ct)).Should().ContainSingle("the second cell was never reserved").Subject;
        (attempt.State, attempt.SettledUsd).Should().Be((ScoutAttemptState.Settled, ScoutCellStreams.OvershootUsd), "the real total, never clipped to the $3 cap");
        (await setup.NewStore().LoadAsync(runId, Ct)).Ok().Ledger.SettledUsd.Should().Be(ScoutCellStreams.OvershootUsd);
        setup.Fake.Calls().Should().ContainSingle();

        var status = await setup.RunAsync(setup.Services(), "status", "--run", runId.ToString());
        status.Output.Should().Contain("1 overshoot(s)").And.Contain("settled at $4.1").And.Contain("over its $3");
        var report = await setup.RunAsync(setup.Services(), "report", "--run", runId.ToString(), "--json");
        var ledger = JsonSerializer.Deserialize<ScoutRunReportDto>(report.Output, Web)!.Ledger;
        (ledger.Overshoots, ledger.SettledUsd, ledger.Attempts).Should().Be((1, ScoutCellStreams.OvershootUsd, 1));
        (await setup.RunAsync(setup.Services(), "report", "--run", runId.ToString())).Output.Should().Contain("1 overshoot(s)");
    }

    [Fact]
    public async Task A_pilot_refuses_by_name_a_cap_above_3_a_wall_above_20_minutes_and_a_ceiling_above_60_and_plans_nothing()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(A1);

        var cap = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, capUsd: 3.5m));
        var wall = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, extra: ["--cell-timeout-minutes", "21"]));
        var ceiling = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, ceilingUsd: 61m));
        var allowed = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, ceilingUsd: 60m, capUsd: 3m, extra: ["--cell-timeout-minutes", "20"]));

        cap.Code.Should().Be(ExitCodes.Configuration, cap.Output + cap.Error);
        cap.Error.Should().Contain("A1").And.Contain("$3.5").And.Contain("pilot");
        wall.Code.Should().Be(ExitCodes.Configuration, wall.Output + wall.Error);
        wall.Error.Should().Contain("1260").And.Contain("20 min");
        ceiling.Code.Should().Be(ExitCodes.Configuration, ceiling.Output + ceiling.Error);
        ceiling.Error.Should().Contain("$61").And.Contain("$60");
        (cap.Output + wall.Output + ceiling.Output).Should().NotContain("planned", "nothing was planned under a refused bound");
        allowed.Code.Should().NotBe(ExitCodes.Configuration, allowed.Output + allowed.Error);
        allowed.Output.Should().Contain("planned").And.Contain("pilot");
    }

    [Fact]
    public async Task A_preliminary_run_takes_caps_and_walls_from_schedule_json_only()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(A1);
        string[] preliminary = ["--kind", "preliminary"];

        var flagged = await setup.RunAsync(setup.Services(), [.. setup.RunArgs(A1, extra: preliminary), "--cap-usd", "1"]);
        var noSchedule = await setup.RunAsync(setup.Services(), [.. setup.RunArgs(A1, extra: preliminary).Where(a => a != "--schedule" && a != setup.ScheduleFile)]);
        setup.WriteSchedule([ScoutArmId.B1], 0.5m, 600);
        var missingArm = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, extra: preliminary).Select(a => a == setup.ScheduleFile ? setup.WriteSchedule([ScoutArmId.B1], 0.5m, 600) : a).ToArray());
        var otherCampaign = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, extra: preliminary).Select(a => a == setup.ScheduleFile ? setup.WriteSchedule(A1, 0.5m, 600, campaign: "scouts-other-2027") : a).ToArray());
        var planned = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, extra: preliminary).Select(a => a == setup.ScheduleFile ? setup.WriteSchedule(A1, 0.5m, 600) : a).ToArray());

        flagged.Code.Should().Be(ExitCodes.Configuration, flagged.Output + flagged.Error);
        flagged.Error.Should().Contain("--cap-usd").And.Contain("schedule.json");
        noSchedule.Code.Should().Be(ExitCodes.Configuration, noSchedule.Output + noSchedule.Error);
        noSchedule.Error.Should().Contain("--schedule");
        missingArm.Code.Should().Be(ExitCodes.Configuration, missingArm.Output + missingArm.Error);
        missingArm.Error.Should().Contain("A1").And.Contain("schedule.json");
        otherCampaign.Code.Should().Be(ExitCodes.Configuration, otherCampaign.Output + otherCampaign.Error);
        otherCampaign.Error.Should().Contain("scouts-other-2027").And.Contain(ScoutsCliSetup.Campaign);
        planned.Code.Should().Be(ExitCodes.Pass, planned.Output + planned.Error);
        planned.Output.Should().Contain("preliminary").And.Contain("A1 $0.5 (10 min)");
        setup.Fake.Calls().Single().Argv.Should().ContainInConsecutiveOrder("--max-budget-usd", "0.5");
    }

    [Fact]
    public async Task Schedule_writes_the_next_runs_caps_and_walls_from_a_pilots_settled_cells_and_refuses_a_run_that_is_not_a_pilot()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 2);
        setup.Answer(A1);
        var pilot = await setup.RunAsync(setup.Services(), setup.RunArgs(A1));
        pilot.Code.Should().Be(ExitCodes.Pass, pilot.Output + pilot.Error);
        var pilotId = ScoutsCliSetup.RunIdOf(pilot.Output);
        var largest = setup.Tasks.Max(t => ScoutCellStreams.CostOf(setup.StreamOf(t, ScoutArmId.A1)));
        var file = Path.Combine(setup.Root.Path, "schedule.json");

        var (code, output, error) = await setup.RunAsync(setup.Services(), "schedule", "--run", pilotId.ToString(), "--out", file);

        code.Should().Be(ExitCodes.Pass, output + error);
        var schedule = ScoutFiles.Schedule(File.ReadAllText(file)).Ok();
        schedule.Campaign.Should().Be(ScoutsCliSetup.Campaign);
        schedule.FromRun.Should().Be(pilotId.ToString());
        var a1 = schedule.Budgets.Should().ContainSingle().Subject;
        a1.CapUsd.Should().Be(Math.Ceiling(largest * 1.5m * 100m) / 100m, "the largest settled cell cost × 1.5, up to the cent");
        a1.WallSeconds.Should().Be(40, "the fixture's envelope says duration_ms 20000, × 2");
        output.Should().Contain("A1").And.Contain(a1.CapUsd.ToString(CultureInfo.InvariantCulture));

        var next = await setup.RunAsync(setup.Services(), setup.RunArgs(A1, extra: ["--kind", "preliminary"]).Select(a => a == setup.ScheduleFile ? file : a).ToArray());
        next.Code.Should().Be(ExitCodes.Pass, next.Output + next.Error);
        next.Output.Should().Contain($"A1 ${a1.CapUsd.ToString(CultureInfo.InvariantCulture)} (0.7 min)");
        var notPilot = await setup.RunAsync(setup.Services(), "schedule", "--run", ScoutsCliSetup.RunIdOf(next.Output).ToString(), "--out", file);
        notPilot.Code.Should().Be(ExitCodes.Configuration, notPilot.Output + notPilot.Error);
        notPilot.Error.Should().Contain("preliminary").And.Contain("PILOT");
    }
}
