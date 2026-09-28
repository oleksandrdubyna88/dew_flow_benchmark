using Bench.Application.Gate;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;
using Bench.Tests.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Cli;

/// <summary><c>bench gate aa</c> (E7, S7.2a) through <see cref="Program.Run"/> with real arguments, over a real store, real
/// suite files and a real artefact root — the flow a person runs after the A/A campaign. The reference sits in a
/// many-task suite (the imported phase 2's shape) and the run in a one-task suite of its own stamp (the A/A's), so the
/// task is matched by its DEFINITION across two suites, as it is for real.</summary>
[Collection("postgres")]
public sealed class GateAaCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cells_differing_only_in_session_and_data_directory_have_the_references_shape_and_pass()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/srv/private-runs/a/data")), PromptCell.Api(Prompt("22222222", "/home/someone/runs/b/data"))]);

        var (code, output, error) = rig.Aa(run);

        code.Should().Be(ExitCodes.Pass, error);
        Lines(output, "same shape").Should().Be(2);
        output.Should().Contain("compared 2 of 2").And.NotContain("private-runs").And.NotContain("/home/someone", "no path ever leaves the check");
    }

    [Fact]
    public async Task A_cell_at_the_same_commit_with_other_dirty_files_is_compared_and_the_counts_are_said_as_numbers()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var dirty = ProductPin.Hashed(new string('a', 64), "0.0.0+abc1234", "abc1234", CapturedCount.Number(1), "src_mcp").Ok();
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d")) with { Pin = dirty }]);

        var (code, output, error) = rig.Aa(run);

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("same shape — 1 dirty file(s) against the reference's 0 dirty file(s)").And.NotContain("CapturedCount");
    }

    [Fact]
    public async Task A_changed_line_fails_naming_its_position_and_never_its_text()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d", body: "private body line changed"))]);

        var (code, output, _) = rig.Aa(run);

        code.Should().Be(ExitCodes.Regression);
        output.Should().Contain("differs — at lines 3 (1)").And.NotContain("private body line");
    }

    [Fact]
    public async Task An_api_reviewer_that_left_no_prompt_is_a_failure_of_the_capture_not_a_skip()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d")), PromptCell.Api(string.Empty)]);

        var (code, output, _) = rig.Aa(run);

        code.Should().Be(ExitCodes.Regression);
        output.Should().Contain("API reviewer left no prompt");
    }

    [Fact]
    public async Task A_run_of_cli_reviewers_only_compared_nothing_and_says_so_rather_than_passing()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync([PromptCell.Api(string.Empty, reviewer: "claude-fable"), PromptCell.Api(string.Empty, reviewer: "claude-fable")]);

        var (code, output, _) = rig.Aa(run);

        code.Should().Be(ExitCodes.NoReport);
        Lines(output, "CLI reviewer — no prompt by design").Should().Be(2);
        output.Should().Contain("compared 0 of 2");
    }

    [Fact]
    public async Task A_prompt_changed_on_disk_is_an_unreadable_artefact()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var (campaign, cells) = await rig.Rig.CampaignAsync([PromptCell.Api(Prompt("11111111", "/d"))], rig.AaSuite.Stamp, Ct);
        await rig.Rig.TamperAsync(campaign, cells[0], Ct);

        var (code, output, _) = rig.Aa(campaign);

        code.Should().Be(ExitCodes.Environment);
        output.Should().Contain("unreadable");
    }

    [Fact]
    public async Task Another_task_another_commit_and_a_failed_session_are_listed_and_not_compared()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var other = ProductPin.Hashed(new string('b', 64), "0.0.0+def5678", "def5678", CapturedCount.Number(0), "src_mcp").Ok();
        var run = await rig.RunAsync(
            [PromptCell.Api(Prompt("11111111", "/d")), PromptCell.Api(Prompt("33333333", "/d")) with { Pin = other }, PromptCell.Api(Prompt("44444444", "/d")) with { Failed = true }]);
        var (_, reference) = rig.Reference;
        var wide = (await rig.Rig.CampaignAsync([PromptCell.Api(Prompt("55555555", "/d"), task: "cs3")], rig.WideSuite.Stamp, Ct)).Campaign;

        var mixed = rig.Aa(run);
        var otherTask = rig.Aa(wide);

        mixed.Code.Should().Be(ExitCodes.Pass, mixed.Error);
        mixed.Output.Should().Contain("other product pin — not compared").And.Contain("session failed — not compared").And.Contain("compared 1 of 3");
        otherTask.Code.Should().Be(ExitCodes.NoReport);
        otherTask.Output.Should().Contain("other task — not compared");
    }

    [Fact]
    public async Task The_reference_inside_the_run_is_not_compared_with_itself_so_it_cannot_make_a_pass()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var other = ProductPin.Hashed(new string('b', 64), "0.0.0+def5678", "def5678", CapturedCount.Number(0), "src_mcp").Ok();
        var (campaign, cells) = await rig.Rig.CampaignAsync(
            [PromptCell.Api(Prompt("11111111", "/d")), PromptCell.Api(Prompt("22222222", "/d")) with { Pin = other }], rig.AaSuite.Stamp, Ct);

        var (code, output, _) = rig.Aa(campaign, reference: cells[0]);

        code.Should().Be(ExitCodes.NoReport, "the only readable cell is the reference itself — nothing was compared");
        output.Should().Contain("the reference — not compared").And.Contain("compared 0 of 2");
    }

    [Fact]
    public async Task An_ambiguous_reference_is_refused_rather_than_blaming_every_cell()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var twoIds = Prompt("11111111", "/d").Replace("--- end of the plan — the scope (11111111) ---", "--- end of the plan — the scope (99999999) ---", StringComparison.Ordinal);
        var reference = (await rig.Rig.CampaignAsync([PromptCell.Api(twoIds)], rig.WideSuite.Stamp, Ct)).Cells[0];
        var run = await rig.RunAsync([PromptCell.Api(Prompt("22222222", "/d"))]);

        var (code, _, error) = rig.Aa(run, reference: reference);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("two session ids or two history lines").And.Contain("pick another cell");
    }

    [Fact]
    public async Task A_cell_whose_file_is_not_the_one_its_settlement_hashed_proves_the_sites_disagree()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync(
            [PromptCell.Api(Prompt("11111111", "/d")) with { StoredHash = new string('e', 64) }, PromptCell.Api(string.Empty) with { StoredHash = new string('f', 64) }]);

        var (code, output, _) = rig.Aa(run);

        code.Should().Be(ExitCodes.Environment);
        Lines(output, "not the file its settlement hashed").Should().Be(2, "a stored hash with no file behind it is the same disagreement");
    }

    [Fact]
    public async Task One_commit_named_at_two_lengths_is_the_same_pin()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var full = ProductPin.Hashed(new string('c', 64), "0.0.0+abc1234", "abc1234" + new string('0', 33), CapturedCount.Number(0), "src_mcp").Ok();
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d")) with { Pin = full }]);

        var (code, output, error) = rig.Aa(run);

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("same shape");
    }

    [Fact]
    public async Task An_unreadable_prompt_is_said_without_its_path()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var (campaign, cells) = await rig.Rig.CampaignAsync([PromptCell.Api(Prompt("11111111", "/d"))], rig.AaSuite.Stamp, Ct);
        await rig.Rig.TamperAsync(campaign, cells[0], Ct);

        var (_, output, _) = rig.Aa(campaign);

        output.Should().Contain("unreadable").And.NotContain("runs/").And.NotContain("answers/");
    }

    [Fact]
    public async Task A_reference_prompt_changed_on_disk_is_the_machines_problem()
    {
        using var rig = await AaRig.NewAsync(postgres);
        await rig.Rig.TamperAsync(rig.Reference.Campaign, rig.Reference.Cell, Ct);
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d"))]);

        var (code, _, error) = rig.Aa(run);

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("reference cell's prompt cannot be read").And.NotContain("runs/");
    }

    [Fact]
    public async Task A_run_still_running_with_every_cell_settled_is_not_finished()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var campaign = (await rig.Rig.CampaignAsync([PromptCell.Api(Prompt("11111111", "/d"))], rig.AaSuite.Stamp, Ct, finish: false)).Campaign;

        var (code, _, error) = rig.Aa(campaign);

        code.Should().Be(ExitCodes.NoReport);
        error.Should().Contain("not Finished");
    }

    [Fact]
    public async Task A_suite_file_or_an_artefact_root_that_is_not_there_is_the_machines_problem_and_nothing_is_created()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d"))]);
        var missingRoot = Path.Combine(Path.GetTempPath(), "bench-aa-missing-" + Guid.NewGuid().ToString("N"));

        var noSuite = rig.Aa(run, suites: $"{rig.WideSuiteFile},{rig.AaSuiteFile}.gone");
        var noRoot = Run("gate", "aa", "--run", run.ToString(), "--against", rig.Reference.Cell.ToString(),
            "--suite-file", $"{rig.WideSuiteFile},{rig.AaSuiteFile}", "--artifact-root", missingRoot, "--db", postgres.ConnectionString);

        noSuite.Code.Should().Be(ExitCodes.Environment);
        noRoot.Code.Should().Be(ExitCodes.Environment);
        noRoot.Error.Should().Contain("artefact root").And.NotContain(missingRoot);
        Directory.Exists(missingRoot).Should().BeFalse("a read-only check never creates the root it was pointed at");
    }

    [Fact]
    public async Task A_run_that_has_not_finished_is_refused_naming_its_open_cells()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var (campaign, cells) = await rig.Rig.CampaignAsync(
            [PromptCell.Api(Prompt("11111111", "/d")), PromptCell.Api(Prompt("22222222", "/d"))], rig.AaSuite.Stamp, Ct, finish: false, leavePending: 1);

        var (code, _, error) = rig.Aa(campaign);

        code.Should().Be(ExitCodes.NoReport);
        error.Should().Contain("1 unsettled cell(s)").And.Contain(cells[1].ToString());
    }

    [Fact]
    public async Task What_is_asked_wrongly_is_refused_before_anything_is_read()
    {
        using var rig = await AaRig.NewAsync(postgres);
        var run = await rig.RunAsync([PromptCell.Api(Prompt("11111111", "/d"))]);
        var plan = (await rig.Rig.CampaignAsync([PromptCell.Api(Prompt("11111111", "/d"))], rig.AaSuite.Stamp, Ct, gate: GateKind.Plan)).Campaign;
        var cliReference = (await rig.Rig.CampaignAsync([PromptCell.Api(string.Empty, reviewer: "claude-fable")], rig.WideSuite.Stamp, Ct)).Cells[0];

        var otherGate = rig.Aa(plan);
        var oneSuite = rig.Aa(run, suites: rig.AaSuiteFile);
        var noPrompt = rig.Aa(run, reference: cliReference);
        var unknown = rig.Aa(Guid.CreateVersion7());
        var unknownCell = rig.Aa(run, reference: Guid.CreateVersion7());
        var noFlags = Run("gate", "aa", "--db", postgres.ConnectionString);
        var noAgainst = Run("gate", "aa", "--run", run.ToString(), "--db", postgres.ConnectionString);

        otherGate.Code.Should().Be(ExitCodes.Configuration);
        otherGate.Error.Should().Contain("differ by construction");
        oneSuite.Code.Should().Be(ExitCodes.Configuration);
        oneSuite.Error.Should().Contain(rig.WideSuite.Stamp);
        noPrompt.Code.Should().Be(ExitCodes.Configuration);
        noPrompt.Error.Should().Contain("has no turn-1 prompt");
        unknown.Code.Should().Be(ExitCodes.Configuration);
        unknown.Error.Should().Contain("there is no gate run");
        unknownCell.Code.Should().Be(ExitCodes.Configuration);
        unknownCell.Error.Should().Contain("there is no gate cell");
        noFlags.Code.Should().Be(ExitCodes.Configuration);
        noFlags.Error.Should().Contain("--run <campaign id>").And.NotContain("--against");
        noAgainst.Code.Should().Be(ExitCodes.Configuration);
        noAgainst.Error.Should().Contain("--against <reference cell id>").And.NotContain("--run <");
    }

    private static int Lines(string output, string words) => output.Split('\n').Count(l => l.Contains(words, StringComparison.Ordinal));

    /// <summary>Shaped like the product's turn-1 prompt: section fences carrying the session id, a body line (line 3), and
    /// the gate-history line naming the run's data directory. Synthetic — no private content.</summary>
    internal static string Prompt(string session, string dataDir, string body = "unchanged body line") =>
        string.Join('\n',
            "You review a feature.",
            $"--- the plan — the scope ({session}) ---",
            body,
            $"--- end of the plan — the scope ({session}) ---",
            $"--- gate history — evidence, not proof ({session}) ---",
            $"Gate history unavailable: there is no rounds database at {dataDir}{(dataDir.Contains('\\') ? '\\' : '/')}coai.db.",
            $"--- end of gate history — evidence, not proof ({session}) ---");

    internal static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>Two suite files on disk — a wide one (cs2 + cs3, the reference's) and a one-task A/A suite (cs2, the same
    /// definition, its own stamp) — and a reference cell in the wide suite's campaign.</summary>
    private sealed class AaRig : IDisposable
    {
        private readonly TempRoot _files = NewRoot();

        private AaRig(PostgresFixture postgres, PromptRig rig)
        {
            Postgres = postgres;
            Rig = rig;
            WideSuite = GateSuiteFile.Parse(WideJson).Ok();
            AaSuite = GateSuiteFile.Parse(AaJson).Ok();
            WideSuiteFile = Path.Combine(_files.Path, "wide.suite.json");
            AaSuiteFile = Path.Combine(_files.Path, "aa-cs2.suite.json");
            File.WriteAllText(WideSuiteFile, WideJson);
            File.WriteAllText(AaSuiteFile, AaJson);
        }

        public PostgresFixture Postgres { get; }

        public PromptRig Rig { get; }

        public GateSuite WideSuite { get; }

        public GateSuite AaSuite { get; }

        public string WideSuiteFile { get; }

        public string AaSuiteFile { get; }

        public (Guid Campaign, Guid Cell) Reference { get; private set; }

        public static async Task<AaRig> NewAsync(PostgresFixture postgres)
        {
            var rig = new AaRig(postgres, new PromptRig(postgres));
            var (campaign, cells) = await rig.Rig.CampaignAsync(
                [PromptCell.Api(Prompt("fb0f5dd6", "/calib/runs/p2-grok-4.7-cs2-r1/data")), PromptCell.Api(Prompt("0eaabe87", "/d"), task: "cs3")],
                rig.WideSuite.Stamp, Ct);
            rig.Reference = (campaign, cells[0]);
            return rig;
        }

        public async Task<Guid> RunAsync(IReadOnlyList<PromptCell> cells) => (await Rig.CampaignAsync(cells, AaSuite.Stamp, Ct)).Campaign;

        public (int Code, string Output, string Error) Aa(Guid run, Guid? reference = null, string? suites = null) =>
            Run("gate", "aa", "--run", run.ToString(), "--against", (reference ?? Reference.Cell).ToString(),
                "--suite-file", suites ?? $"{WideSuiteFile},{AaSuiteFile}", "--artifact-root", Rig.Root, "--db", Postgres.ConnectionString);

        public void Dispose()
        {
            Rig.Dispose();
            _files.Dispose();
        }

        private const string Cs2 = """
            { "id": "cs2", "language": "C#", "gates": ["plan", "code", "feature"], "repository": "C:/nowhere/cs2",
              "base": "1111111111111111111111111111111111111111", "variantHead": "2222222222222222222222222222222222222222",
              "planPath": "docs/plan.md", "epics": ["one"], "lessons": "", "seeds": [] }
            """;

        private const string Cs3 = """
            { "id": "cs3", "language": "C#", "gates": ["plan", "code", "feature"], "repository": "C:/nowhere/cs3",
              "base": "3333333333333333333333333333333333333333", "variantHead": "4444444444444444444444444444444444444444",
              "planPath": "docs/plan.md", "epics": ["one"], "lessons": "", "seeds": [] }
            """;

        private static readonly string WideJson = $$"""{ "id": "gate-aa-wide", "privateNames": ["contoso-orders"], "tasks": [ {{Cs2}}, {{Cs3}} ] }""";

        private static readonly string AaJson = $$"""{ "id": "gate-aa-cs2", "privateNames": ["contoso-orders"], "tasks": [ {{Cs2}} ] }""";
    }
}
