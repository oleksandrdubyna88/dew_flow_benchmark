using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>S3's verbs through <c>bench scouts …</c>, against a run the real <c>run</c> settled with the fake claude, a real database and real
/// history-free trees: <c>status</c> (cells and the ledger), <c>score</c> (recall recomputed from the streams and stored; the hand-check draw
/// fixed by its seed), <c>assess</c> (the fake codex replaying recorded verdicts; idempotent — a re-run asks only what has no verdict), and
/// <c>report --json</c> byte-identical across two invocations. Nothing paid runs.</summary>
[Collection("postgres")]
public sealed class ScoutsAnalysisCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ScoutArmId[] TwoArms = [ScoutArmId.A1, ScoutArmId.B1];

    /// <summary>Two tasks × A1/B1, every cell clean: 4 scored cells; A1 has one scout, B1 two; every scout makes 2 claims → 12 claims.</summary>
    private static async Task<(ScoutsCliSetup Setup, Guid Run)> SettledAsync(PostgresFixture postgres)
    {
        var setup = await ScoutsCliSetup.StartAsync(postgres, tasks: 2);
        setup.Answer(TwoArms);
        var (code, output, error) = await setup.RunAsync(setup.Services(), setup.RunArgs(TwoArms));
        code.Should().Be(ExitCodes.Pass, output + error);
        return (setup, ScoutsCliSetup.RunIdOf(output));
    }

    [Fact]
    public async Task Status_lists_every_cell_by_state_and_the_ledger_with_the_ceiling_left()
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;

        var (code, output, error) = await setup.RunAsync(setup.Services(), "status", "--run", run.ToString());

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("preliminary").And.Contain("0 pending · 0 claimed · 4 settled · 0 abandoned — finished").And.Contain("4 scored · 0 void");
        output.Should().MatchRegex(@"ledger +\$0(\.0+)? reserved · \$[0-9.]+ settled · \$0(\.0+)? unknown \(0 charge\(s\)\) · \$[0-9.]+ spent · \$[0-9.]+ of the \$50(\.0+)? ceiling left · 4 attempt\(s\)");
        output.Split('\n').Count(l => l.StartsWith("settled ", StringComparison.Ordinal)).Should().Be(4);
        (await setup.RunAsync(setup.Services(), "status", "--run", Guid.NewGuid().ToString())).Code.Should().Be(ExitCodes.Configuration);
    }

    [Fact]
    public async Task Score_recomputes_recall_from_the_streams_and_stores_it_over_a_tampered_row()
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;
        var before = (await setup.NewStore().CellsAsync(run, Ct)).ToDictionary(c => c.Id, c => c.Facts.RecallFound.Value);
        await using (var db = PostgresFixture.Context(setup.Connection))
        {
            await db.ScoutCells.Where(c => c.RunId == run).ExecuteUpdateAsync(s => s.SetProperty(c => c.RecallFound, 0L), Ct);
        }

        var (code, output, error) = await setup.RunAsync(setup.Services(), "score", "--run", run.ToString());

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain("4 scored cell(s) rescored from their streams, 4 changed");
        (await setup.NewStore().CellsAsync(run, Ct)).ToDictionary(c => c.Id, c => c.Facts.RecallFound.Value).Should().Equal(before,
            "the recall settlement computed and the recall score recomputes from the same stream are the same numbers");
        before.Values.Should().OnlyContain(found => found == 1 || found == 2, "A1 names one file of G, B1 two");
    }

    [Fact]
    public async Task The_hand_check_draw_is_fixed_by_its_seed_and_gives_every_item_an_agree_and_a_disagree_box()
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;
        var (first, again, other) = (Path.Combine(setup.Root.Path, "h1.md"), Path.Combine(setup.Root.Path, "h2.md"), Path.Combine(setup.Root.Path, "h3.md"));

        (await setup.RunAsync(setup.Services(), "score", "--run", run.ToString(), "--handcheck", "3", "--seed", "5", "--out", first)).Code.Should().Be(ExitCodes.Pass);
        (await setup.RunAsync(setup.Services(), "score", "--run", run.ToString(), "--handcheck", "3", "--seed", "5", "--out", again)).Code.Should().Be(ExitCodes.Pass);
        (await setup.RunAsync(setup.Services(), "score", "--run", run.ToString(), "--handcheck", "3", "--seed", "6", "--out", other)).Code.Should().Be(ExitCodes.Pass);

        var markdown = File.ReadAllText(first);
        File.ReadAllText(again).Should().Be(markdown, "the same seed over the same run draws the same items");
        File.ReadAllText(other).Should().NotBe(markdown);
        markdown.Should().Contain("### F01").And.Contain("### N03").And.Contain("- [ ] agree\n- [ ] disagree\n- note: ");
        markdown.Should().NotContain("### C01", "no claim has a verdict before assess");
        (await setup.RunAsync(setup.Services(), "score", "--run", run.ToString(), "--handcheck", "3")).Code.Should().Be(ExitCodes.Configuration, "a draw needs its seed");
    }

    /// <summary>Review finding (security): a deny-list path that is SUPPLIED and does not load — a typo, a folder — refuses the verb (4)
    /// naming the path, and no hand-check file is written: a silently empty deny-list would publish the very names it exists to hide.</summary>
    [Theory]
    [InlineData("no-such-deny-list.txt")]
    [InlineData("a-folder-not-a-file")]
    public async Task A_supplied_deny_list_that_does_not_load_refuses_the_hand_check_and_writes_nothing(string name)
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;
        var (draw, denyList) = (Path.Combine(setup.Root.Path, "h.md"), Path.Combine(setup.Root.Path, name));
        if (name.StartsWith("a-folder", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(denyList);
        }

        var (code, output, error) = await setup.RunAsync(setup.Services(), "score", "--run", run.ToString(), "--handcheck", "3", "--seed", "5", "--out", draw, "--deny-list", denyList);

        code.Should().Be(ExitCodes.Configuration, output + error);
        error.Should().Contain(denyList).And.Contain("deny-list");
        File.Exists(draw).Should().BeFalse("a deny-list that did not load must not let the hand-check file be written");
    }

    [Fact]
    public async Task Assess_gives_every_claim_a_verdict_once_and_a_re_run_asks_nothing()
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;
        using var codex = new FakeCli(new JsonObject { ["assessVerdicts"] = new JsonObject { ["README.md"] = "wrong" } });

        var first = await setup.RunAsync(setup.Services(), Assess(run, codex));

        first.Code.Should().Be(ExitCodes.Pass, first.Output + first.Error);
        first.Output.Should().Contain("12 claim(s): 0 already assessed, 12 asked, 12 given a verdict, 0 remaining");
        var verdicts = await Reads(setup).VerdictsAsync(run, Ct);
        verdicts.Should().HaveCount(12);
        verdicts.Count(v => v.Verdict == ScoutClaimVerdict.Wrong).Should().Be(6, "every scout's second claim names README.md");
        verdicts.Should().OnlyContain(v => v.AssessorModel == codex.Model && v.RubricSha256 == ScoutAssessment.RubricSha256);
        var calls = codex.Calls();
        calls.Should().HaveCount(2, "one batch per task — the task's claims across both arms, blinded together");
        calls.Select(c => Path.GetFileName(c.Cwd)).Should().BeEquivalentTo(["t01", "t02"], "the assessor reads the task's own history-free tree");
        calls.Should().OnlyContain(c => c.Argv.Contains("read-only") && c.Argv.Contains("--output-schema") && c.Argv.Contains("-o") && c.Argv.Contains(codex.Model));
        calls.Should().OnlyContain(c => !c.Prompt.Contains("A1", StringComparison.Ordinal) && !c.Prompt.Contains("opus", StringComparison.Ordinal), "the rows are blinded");

        var again = await setup.RunAsync(setup.Services(), Assess(run, codex));

        again.Code.Should().Be(ExitCodes.Pass, again.Output + again.Error);
        again.Output.Should().Contain("12 claim(s): 12 already assessed, 0 asked, 0 given a verdict, 0 remaining");
        codex.Calls().Should().HaveCount(2, "nothing already assessed is asked again");
        (await Reads(setup).VerdictsAsync(run, Ct)).Select(v => (v.Key, v.Verdict)).Should().Equal(verdicts.Select(v => (v.Key, v.Verdict)));
    }

    [Fact]
    public async Task Assess_after_an_unanswered_batch_asks_exactly_the_claims_still_without_a_verdict()
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;
        using var codex = new FakeCli(new JsonObject { ["assessVerdicts"] = new JsonObject(), ["assessSkip"] = "README.md" });

        var partial = await setup.RunAsync(setup.Services(), Assess(run, codex));

        partial.Code.Should().Be(ExitCodes.Environment, "claims remain without a verdict — resumable");
        partial.Output.Should().Contain("12 asked, 6 given a verdict, 6 remaining");
        codex.Rewrite(new JsonObject { ["assessVerdicts"] = new JsonObject { ["README.md"] = "wrong" } });

        var rest = await setup.RunAsync(setup.Services(), Assess(run, codex));

        rest.Code.Should().Be(ExitCodes.Pass, rest.Output + rest.Error);
        rest.Output.Should().Contain("12 claim(s): 6 already assessed, 6 asked, 6 given a verdict, 0 remaining");
        codex.Calls().Skip(2).SelectMany(c => c.Prompt.Split('\n').Where(l => l.StartsWith("{\"id\"", StringComparison.Ordinal)))
            .Should().HaveCount(6).And.OnlyContain(row => row.Contains("README.md"), "only the unanswered claims are put again");
    }

    [Fact]
    public async Task Report_json_is_byte_identical_across_two_invocations_and_says_preliminary()
    {
        var (setup, run) = await SettledAsync(postgres);
        await using var _ = setup;

        var first = await setup.RunAsync(setup.Services(), "report", "--run", run.ToString(), "--json");
        var second = await setup.RunAsync(setup.Services(), "report", "--run", run.ToString(), "--json");
        var text = await setup.RunAsync(setup.Services(), "report", "--run", run.ToString());

        first.Code.Should().Be(ExitCodes.Pass, first.Error);
        second.Output.Should().Be(first.Output);
        first.Output.Should().Contain("\"kind\":\"preliminary\"").And.Contain("\"verdict\":\"preliminary\"");
        text.Output.Should().Contain(ScoutReportText.PreliminaryLabel).And.Contain("claims-unassessed", "no claim has a verdict before assess");
        (await setup.RunAsync(setup.Services(), "report", "--run", run.ToString(), "--resamples", "5")).Code.Should().Be(ExitCodes.Configuration);
    }

    private static string[] Assess(Guid run, FakeCli codex) =>
        ["assess", "--run", run.ToString(), "--assessor-model", codex.Model, "--codex-exe", FakeCli.Executable];

    private static PostgresScoutReads Reads(ScoutsCliSetup setup) => new(PostgresFixture.Context(setup.Connection));
}
