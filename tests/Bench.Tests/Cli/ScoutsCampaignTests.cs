using System.Text.Json.Nodes;
using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Process;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>S2 through the verbs, against the fake CLI, a real database and real history-free trees: <c>run</c> settles a two-task replay
/// with one void and one aborted cell; a pin change stops the run before anything is claimed; the next reservation that does not fit stops it
/// with exit 3; a changed tree stops the cell; a hung CLI is killed at the wall and its attempt charged; <c>rerun</c> appends a generation.
/// Every launch is the fake; nothing paid runs.</summary>
[Collection("postgres")]
public sealed class ScoutsCampaignTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ScoutArmId[] TwoArms = [ScoutArmId.A1, ScoutArmId.B1];

    [Fact]
    public async Task Run_settles_a_two_task_replay_with_one_void_and_one_aborted_cell_and_the_ledger_identity_holds()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 2);
        var schedule = setup.Schedule(TwoArms);
        var (voided, aborted) = (schedule[0], schedule[^1]);
        setup.Answer(TwoArms, (task, arm) =>
            (task.Id.Value, arm) == (voided.Task.Value, voided.Arm) ? CellVariant.Void
            : (task.Id.Value, arm) == (aborted.Task.Value, aborted.Arm) ? CellVariant.BudgetExhausted
            : CellVariant.Clean);

        var (code, output, error) = await setup.RunAsync(setup.Services(), setup.RunArgs(TwoArms));

        code.Should().Be(ExitCodes.Environment, "the aborted cell stops the run with a resumable exit — " + output + error);
        error.Should().Contain("spend limit");
        var runId = ScoutsCliSetup.RunIdOf(output);
        var cells = await setup.NewStore().CellsAsync(runId, Ct);
        cells.Should().HaveCount(4).And.OnlyContain(c => c.State == CellState.Settled);
        cells.Select(c => c.Outcome).Should().BeEquivalentTo([ScoutCellOutcome.Void, ScoutCellOutcome.Scored, ScoutCellOutcome.Scored, ScoutCellOutcome.Aborted]);
        cells.Single(c => c.Outcome == ScoutCellOutcome.Void).Reason.Should().Be(ScoutReason.SubagentsNotCompleted);
        cells.Where(c => c.Outcome == ScoutCellOutcome.Scored).Should().OnlyContain(c => c.Facts.RecallFound.Value == 1 || c.Facts.RecallFound.Value == 2);

        await AssertLedgerIdentityAsync(setup, runId, expectedSettled: 4, expectedUnknown: 0);
        var mcp = Path.Combine(setup.ArtifactRoot, ScoutArtifacts.Folder, "empty-mcp.json");
        string[] launch =
        [
            "-p", "--model", "opus", "--output-format", "stream-json", "--verbose", "--tools", "Task", "Read", "Glob", "Grep",
            "--strict-mcp-config", "--mcp-config", mcp, "--forward-subagent-text", "--max-budget-usd", "1", "--no-session-persistence",
        ];
        setup.Fake.Calls().Should().HaveCount(4, "one launch per cell, in schedule order");
        setup.Fake.Calls().Should().OnlyContain(c => c.Argv.SequenceEqual(launch), "the scout launch of §3.4, exactly");
        File.ReadAllText(mcp).Should().Be(ScoutLaunch.EmptyMcpConfig);
        setup.Fake.Calls().Should().OnlyContain(c => c.EnvironmentNames.Contains("FAKE_CLI_HOME") && !c.EnvironmentNames.Any(n => n.StartsWith("BENCH_", StringComparison.Ordinal)),
            "the child runs under the probes' minimal environment, plus the test's own home and nothing else");
        setup.Fake.Calls().Select(c => Path.GetFileName(c.Cwd)).Should().BeEquivalentTo(["t01", "t01", "t02", "t02"], "every cell runs in its task's history-free tree");
    }

    [Fact]
    public async Task A_moved_pin_stops_the_run_before_anything_is_claimed()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(TwoArms);
        var crashed = await CrashedRunAsync(setup, ScoutCheckpoint.BeforeLaunchingMark);
        File.WriteAllText(Path.Combine(setup.PluginCache, "code-explorer.md"), "# the plugin updated itself\n");

        var (code, output, error) = await setup.RunAsync(setup.Services(), "resume", "--run", crashed.ToString());

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("plugin cache hash").And.Contain("a pin moved");
        (await setup.NewStore().CellsAsync(crashed, Ct)).Should().OnlyContain(c => c.State == CellState.Pending, "nothing was claimed under the moved pin");
        setup.Fake.Calls().Should().BeEmpty("nothing was launched");
    }

    [Fact]
    public async Task The_next_reservation_that_does_not_fit_stops_the_run_with_exit_3_and_launches_nothing()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(TwoArms);
        var firstCost = ScoutCellStreams.CostOf(setup.StreamOf(setup.Tasks[0], setup.Schedule(TwoArms)[0].Arm));
        var ceiling = decimal.Round(1.2m + (firstCost / 2m), 6);

        var (code, output, error) = await setup.RunAsync(setup.Services(), setup.RunArgs(TwoArms, ceilingUsd: ceiling, capUsd: 1m));

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("does not fit").And.Contain("nothing was launched");
        var runId = ScoutsCliSetup.RunIdOf(output);
        (await setup.NewStore().CellsAsync(runId, Ct)).Count(c => c.State == CellState.Settled).Should().Be(1);
        (await setup.NewStore().AttemptsAsync(runId, Ct)).Should().ContainSingle("the second cell was never reserved");
        setup.Fake.Calls().Should().ContainSingle();
    }

    [Fact]
    public async Task A_tree_that_changed_since_it_was_frozen_stops_the_cell_and_the_run()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(TwoArms);
        var crashed = await CrashedRunAsync(setup, ScoutCheckpoint.BeforeLaunchingMark);
        File.AppendAllText(Path.Combine(setup.Trees, "t01", "src", "Alpha.cs"), "// edited after the freeze\n");

        var (code, output, error) = await setup.RunAsync(setup.Services(), "resume", "--run", crashed.ToString());

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain(ScoutTreeRefusal.HashChanged);
        var stopped = (await setup.NewStore().CellsAsync(crashed, Ct)).Single(c => c.State == CellState.Settled);
        stopped.Should().Match<ScoutCell>(c => c.Outcome == ScoutCellOutcome.Stopped && c.Reason == ScoutReason.PinMoved);
        (await setup.NewStore().AttemptsAsync(crashed, Ct)).Should().OnlyContain(a => a.State == ScoutAttemptState.Released, "a stop before the launch spends nothing");
        setup.Fake.Calls().Should().BeEmpty();
    }

    [Fact]
    public async Task A_hung_CLI_is_killed_at_the_wall_and_its_attempt_settles_from_what_reached_disk()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer([ScoutArmId.A1]);
        setup.Fake.Script(new JsonObject { ["holdAfterLine"] = 3 });

        var (code, output, error) = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1], extra: ["--cell-timeout-seconds", "20"]));

        code.Should().Be(ExitCodes.Pass, output + error);
        var runId = ScoutsCliSetup.RunIdOf(output);
        var cell = (await setup.NewStore().CellsAsync(runId, Ct)).Single();
        cell.Should().Match<ScoutCell>(c => c.Outcome == ScoutCellOutcome.Void && c.Reason == ScoutReason.StreamIncomplete);
        var attempt = (await setup.NewStore().AttemptsAsync(runId, Ct)).Single();
        attempt.State.Should().Be(ScoutAttemptState.ChargedUnknown, "no result reached the stream, so the whole reservation is charged");
        File.ReadAllLines(new ScoutArtifacts(setup.ArtifactRoot).PathOf(attempt, ScoutArtifactKind.Stream)).Should().HaveCount(3, "the lines printed before the hang are on disk");
        WorkerLiveness.ProcessIsAlive(setup.Fake.Calls().Single().Pid).Should().BeFalse("the wall killed the CLI's whole tree");
        await AssertLedgerIdentityAsync(setup, runId, expectedSettled: 0, expectedUnknown: 1);
    }

    [Fact]
    public async Task Rerun_appends_the_next_generation_of_a_void_cell_and_measures_only_it()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer([ScoutArmId.A1], (_, _) => CellVariant.Void);
        var run = await setup.RunAsync(setup.Services(), setup.RunArgs([ScoutArmId.A1]));
        var runId = ScoutsCliSetup.RunIdOf(run.Output);
        var first = (await setup.NewStore().CellsAsync(runId, Ct)).Single();
        setup.Answer([ScoutArmId.A1]);

        var (code, output, error) = await setup.RunAsync(setup.Services(), "rerun", "--cell", first.Id.ToString());

        code.Should().Be(ExitCodes.Pass, output + error);
        var lineage = (await setup.NewStore().CellsAsync(runId, Ct)).OrderBy(c => c.Generation).ToList();
        lineage.Select(c => (c.Generation, c.Outcome)).Should().Equal((1, ScoutCellOutcome.Void), (2, ScoutCellOutcome.Scored));
        await AssertLedgerIdentityAsync(setup, runId, expectedSettled: 2, expectedUnknown: 0);
    }

    [Fact]
    public async Task Alias_check_exits_non_zero_naming_the_new_id_when_a_resolved_model_moved()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Fake.Answer(ScoutAliasCheck.Driver(), ScoutStreams.Cell([new ChildSpec(ScoutAliasCheck.ScoutPrompt, "sonnet", "claude-sonnet-6-0"), new ChildSpec(ScoutAliasCheck.ScoutPrompt, "opus", ScoutStreams.Opus, ScoutStreams.Opus)]));
        var work = setup.Root.Sibling("alias");

        var moved = await setup.RunAsync(setup.Services(), "alias-check", "--opus-id", ScoutStreams.Opus, "--sonnet-id", ScoutStreams.Sonnet, "--work-root", work);

        moved.Code.Should().Be(ExitCodes.Environment, moved.Output + moved.Error);
        moved.Error.Should().Contain("sonnet: claude-sonnet-5-5 → claude-sonnet-6-0");
        setup.Fake.Calls().Single().Argv.Should().ContainInConsecutiveOrder("--max-budget-usd", "1");

        setup.Fake.Answer(ScoutAliasCheck.Driver(), ScoutStreams.Cell([new ChildSpec(ScoutAliasCheck.ScoutPrompt, "sonnet", ScoutStreams.Sonnet), new ChildSpec(ScoutAliasCheck.ScoutPrompt, "opus", ScoutStreams.Opus, ScoutStreams.Opus)]));
        var same = await setup.RunAsync(setup.Services(), "alias-check", "--opus-id", ScoutStreams.Opus, "--sonnet-id", ScoutStreams.Sonnet, "--work-root", work);
        same.Code.Should().Be(ExitCodes.Pass, same.Output + same.Error);
    }

    /// <summary>The ledger identity (§3.10): spent = Σ settled <c>total_cost_usd</c> + Σ unknown charges — the run row against its attempts —
    /// with nothing left reserved.</summary>
    internal static async Task AssertLedgerIdentityAsync(ScoutsCliSetup setup, Guid runId, int expectedSettled, int expectedUnknown)
    {
        var run = (await setup.NewStore().LoadAsync(runId, Ct)).Ok();
        var attempts = await setup.NewStore().AttemptsAsync(runId, Ct);

        attempts.Count(a => a.State == ScoutAttemptState.Settled).Should().Be(expectedSettled);
        attempts.Count(a => a.State == ScoutAttemptState.ChargedUnknown).Should().Be(expectedUnknown);
        run.Ledger.ReservedUsd.Should().Be(0m, "no attempt is left open");
        run.Ledger.SettledUsd.Should().Be(attempts.Where(a => a.State == ScoutAttemptState.Settled).Sum(a => a.SettledUsd));
        run.Ledger.UnknownUsd.Should().Be(attempts.Where(a => a.State == ScoutAttemptState.ChargedUnknown).Sum(a => a.ReservedUsd));
        run.Ledger.SpentUsd.Should().Be(attempts.Sum(a => a.ChargedUsd));
    }

    /// <summary>A run whose harness crashed at <paramref name="point"/> while claiming as an owner that is now provably gone — the state a
    /// killed bench leaves. Answers the run id.</summary>
    internal static async Task<Guid> CrashedRunAsync(ScoutsCliSetup setup, ScoutCheckpoint point, IReadOnlyList<ScoutArmId>? arms = null)
    {
        var crash = new CrashAt(point);
        var output = new StringWriter();
        var run = async () => await ScoutsCommand.RunAsync(
            CommandLine.Parse(["scouts", .. setup.WithDefaults(setup.RunArgs(arms ?? TwoArms))]), setup.Services(crash, TestWorkers.Dead("crashed-bench")), output, new StringWriter(), Ct);

        await run.Should().ThrowAsync<ScoutCrash>();
        crash.Fired.Should().BeTrue();
        return ScoutsCliSetup.RunIdOf(output.ToString());
    }
}
