using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Contracts;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Probes.Driver;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>THE SCOUTS SCENARIO (plan §5 S3, as amended by D8) — the real flow, offline, catalogued in <c>research/module_tests.md</c>:
/// <c>bench scouts run</c> over a two-task fixture set with the REAL store and real history-free trees, the fake claude replaying a recorded
/// stream per (task, arm); the harness crashes mid-cell and <c>bench scouts resume</c> finishes the run; one cell goes VOID (the scouts were
/// launched on the wrong model); then <c>score</c>, <c>assess</c> (the fake codex replaying recorded verdicts) and <c>report --json</c>. The
/// report's verdict label, per-arm numbers and void count are compared with the values computed BY HAND below — the doubles are the CLI's
/// stand-ins for the two paid CLIs and nothing else.
/// <para>
/// What it does not prove: that a real claude prints these streams (E2 S4's live fixtures prove that), that a real codex judges claims the way
/// the replay does (§3.7's hand-check), or anything about the read API and the Scouts tab (E3 S8).
/// </para></summary>
[Collection("postgres")]
public sealed class ScoutsScenarioTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ScoutArmId[] W1 = [ScoutArmId.A1, ScoutArmId.B1];

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // THE FIXTURE, AND THE NUMBERS COMPUTED BY HAND
    //
    // Tasks t01, t02 — both in example-org/example-repo, G = {src/Alpha.cs, src/Beta.cs, src/Gamma.cs} (|G| = 3).
    // Arms  A1 = 1 × opus, B1 = 2 × sonnet. Scout i writes   KEY FILES: G[i], README.md
    //                                                        CLAIMS:    G[i]:1 — defines the type  ·  README.md — describes the project
    // The replayed codex calls a claim WRONG when its row names README.md or src/Beta.cs, RIGHT otherwise.
    //
    //   cell        outcome   recall          claims (wrong / decided)           team ms           team $ (tokens × the fixture's prices)
    //   t01 · A1    scored    1/3             Alpha ✓  README ✗        1 / 2      0 s → 5 s = 5000  opus team: in 4, out 150−100 = 50, cache-write 200
    //                                                                                                 → (4·5 + 50·25 + 200·6.25) / 1e6 = 0.00252
    //   t02 · A1    scored    1/3             Alpha ✓  README ✗        1 / 2      5000              0.00252
    //   t01 · B1    scored    2/3             Alpha ✓  README ✗        3 / 4      0 s → 6 s = 6000  sonnet team: in 8, out 100, cache-write 400
    //                                         Beta  ✗  README ✗                                       → (8·3 + 100·15 + 400·3.75) / 1e6 = 0.003024
    //   t02 · B1    VOID (the driver launched the scouts on opus — InputModelMismatch): counted, never measured
    //
    //   arm   recall (mean over tasks)   wrong / 10 (pooled per task, mean)   team ms   team $     $ per right file           caller load   void
    //   A1    (1/3 + 1/3) / 2 = 0.333333  (5 + 5) / 2 = 5                    5000      0.00252    0.00504 / 2 = 0.00252      2             0
    //   B1    2/3 = 0.666667              3/4 × 10 = 7.5                     6000      0.003024   0.003024 / 2 = 0.001512    3             1
    //
    //   W1    Δrecall: t01 only (t02's B1 is void) = 2/3 − 1/3 = +0.333333, 1 task, 1 repository → not estimable
    //         Δwrong:  t01 = 7.5 − 5 = +2.5
    //   The run is PRELIMINARY (the default kind): verdict "preliminary", W1 "preliminary" with reason one-repository; W2, W3 missing.
    //   Claims: A1 2 cells × 2 + B1 1 cell × 4 = 8 assessed (the void cell's are never put to the assessor).
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_run_interrupted_and_resumed_with_a_void_cell_is_scored_assessed_and_reported_with_the_hand_computed_numbers()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 2);
        setup.Answer(W1, (task, arm) => (task.Id.Value, arm) == ("t02", ScoutArmId.B1) ? CellVariant.WrongModel : CellVariant.Clean);
        using var codex = new FakeCli(new JsonObject { ["assessVerdicts"] = new JsonObject { ["README.md"] = "wrong", ["src/Beta.cs"] = "wrong" } });

        // 1. run — the harness dies once the first cell's result reached disk (a crash in flight, its owner then provably gone).
        var run = await ScoutsCampaignTests.CrashedRunAsync(setup, ScoutCheckpoint.AfterResultLine, W1);
        var interrupted = await setup.RunAsync(setup.Services(), "status", "--run", run.ToString());
        interrupted.Output.Should().Contain("1 claimed", "the crashed cell is still held by its dead owner");

        // 2. resume — the entry step settles the crashed attempt once from its stream and hands the cell back; the rest is measured.
        var resumed = await setup.RunAsync(setup.Services(), "resume", "--run", run.ToString());
        resumed.Code.Should().Be(ExitCodes.Pass, resumed.Output + resumed.Error);
        resumed.Output.Should().Contain("1 claim(s) handed back").And.Contain("1 settled from their stream");

        // 3. score, 4. assess, 5. report --json
        var scored = await setup.RunAsync(setup.Services(), "score", "--run", run.ToString());
        scored.Code.Should().Be(ExitCodes.Pass, scored.Output + scored.Error);
        var assessed = await setup.RunAsync(setup.Services(), "assess", "--run", run.ToString(), "--assessor-model", codex.Model, "--codex-exe", FakeCli.Executable);
        assessed.Code.Should().Be(ExitCodes.Pass, assessed.Output + assessed.Error);
        assessed.Output.Should().Contain("8 claim(s): 0 already assessed, 8 asked, 8 given a verdict, 0 remaining");
        var json = await setup.RunAsync(setup.Services(), "report", "--run", run.ToString(), "--json");
        json.Code.Should().Be(ExitCodes.Pass, json.Error);
        var report = JsonSerializer.Deserialize<ScoutRunReportDto>(json.Output, Web)!;

        report.Kind.Should().Be("preliminary");
        report.Verdict.Should().Be("preliminary");
        var w1 = report.Workloads.Single(w => w.Workload == "W1");
        w1.Should().Match<ScoutWorkloadDto>(w => w.ArmA == "A1" && w.ArmB == "B1" && w.Outcome == "preliminary");
        w1.Reasons.Should().Equal("one-repository");
        w1.Recall.Should().Be(new ScoutIntervalDto(true, 0.333333, 0.333333, 0.333333, 1, 1, false));
        w1.WrongPerTen.Should().Be(new ScoutIntervalDto(true, 2.5, 2.5, 2.5, 1, 1, false));
        report.Workloads.Where(w => w.Workload != "W1").Should().OnlyContain(w => w.Reasons.SequenceEqual(new[] { "workload-missing" }));

        var (a1, b1) = (report.Arms.Single(a => a.Arm == "A1"), report.Arms.Single(a => a.Arm == "B1"));
        Numbers(a1).Should().Be((0.333333, 5.0, 5000.0, 0.00252, 0.00252, 2.0));
        Numbers(b1).Should().Be((0.666667, 7.5, 6000.0, 0.003024, 0.001512, 3.0));
        (a1.Counts.Cells, a1.Counts.Scored, a1.Counts.Void, a1.Counts.WrongClaims, a1.Counts.DecidedClaims).Should().Be((2, 2, 0, 2, 4));
        (b1.Counts.Cells, b1.Counts.Scored, b1.Counts.Void, b1.Counts.WrongClaims, b1.Counts.DecidedClaims).Should().Be((2, 1, 1, 3, 4));
        (a1.WrongGap, b1.WrongGap).Should().Be(("none", "none"));

        // The money: five attempts (the crashed one settled once from its stream, then four), nothing unknown, the identity holds.
        report.Ledger.Attempts.Should().Be(5);
        report.Ledger.UnknownCharges.Should().Be(0);
        report.Ledger.SpentUsd.Should().Be(report.Ledger.SettledUsd + report.Ledger.UnknownUsd);
        await ScoutsCampaignTests.AssertLedgerIdentityAsync(setup, run, expectedSettled: 5, expectedUnknown: 0);

        // The void cell is the one the hand table names, and it was never put to the assessor.
        var cells = await setup.NewStore().CellsAsync(run, Ct);
        cells.Single(c => c.Outcome == ScoutCellOutcome.Void).Should().Match<ScoutCell>(c => c.Task.Value == "t02" && c.Arm == ScoutArmId.B1 && c.Reason == ScoutReason.InputModelMismatch);
        codex.Calls().SelectMany(c => c.Prompt.Split('\n').Where(l => l.StartsWith("{\"id\"", StringComparison.Ordinal))).Should().HaveCount(8);

        // The text report says what the JSON says, and labels the run.
        var text = await setup.RunAsync(setup.Services(), "report", "--run", run.ToString());
        text.Output.Should().Contain("verdict        preliminary — not a verdict").And.Contain("0 void").And.Contain("1 void");

        // 6. prune — the run is finished, so its history-free trees go; the artefacts (the streams) stay.
        var pruned = await setup.RunAsync(setup.Services(), "prune", "--run", run.ToString());
        pruned.Code.Should().Be(ExitCodes.Pass, pruned.Output + pruned.Error);
        pruned.Output.Should().Contain("2 history-free tree(s) of its 2 task(s) deleted");
        Directory.Exists(Path.Combine(setup.Trees, "t01")).Should().BeFalse();
        Directory.EnumerateFiles(Path.Combine(setup.ArtifactRoot, "scouts", run.ToString("D")), "stream.ndjson", SearchOption.AllDirectories).Should().HaveCount(5);
    }

    private static (double Recall, double Wrong, double TeamMs, double TeamUsd, double PerRightFile, double CallerLoad) Numbers(ScoutArmReportDto arm) =>
        (arm.Recall.Value, arm.WrongPerTen.Value, arm.TeamMs.Value, arm.TeamUsd.Value, arm.UsdPerRightFile.Value, arm.CallerLoad.Value);
}
