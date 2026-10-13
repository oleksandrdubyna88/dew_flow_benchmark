using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench scouts tasks</c> against a real local repository: the cut-off guard (a recorded id admits candidates after it; an id with
/// none stops the selection, 4), the D10 draw seeded by the campaign string (a repository needs 5 admissible candidates; one main and one
/// reserve per main repository; <c>pilot-tasks.json</c> beside <c>--out</c>), P verified per merge style through the verb, each drawn tree
/// built and its hash frozen, and a leaking statement refusing the whole selection with nothing written. No database, no model.</summary>
public sealed class ScoutsTasksCommandTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Under this seed the keys <c>example-repo#1..5</c> order 4 &lt; 3 &lt; 5 &lt; 2 &lt; 1 (computed independently, see ScoutSelectionTests).</summary>
    private const string Campaign = "scouts-preliminary-2026-10-12";

    private const string Statement = "How does a cell reach its settlement?";

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private string Files => _root.Sibling("files");

    private string TasksFile => Path.Combine(Files, "tasks.json");

    private string PilotFile => Path.Combine(Files, "pilot-tasks.json");

    [Fact]
    public async Task Five_admissible_candidates_give_one_main_task_and_its_reserve_in_the_seeded_order_with_their_trees_frozen()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates((parent, fix, "c-one", 1, Statement), (parent, fix, "c-two", 2, Statement), (parent, fix, "c-three", 3, Statement), (parent, fix, "c-four", 4, Statement), (parent, fix, "c-five", 5, Statement));

        var (code, output, error) = await RunAsync(candidates, Cutoffs("2026-03-31"));

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain("the later published cut-off is 2026-03-31")
            .And.Contain("repositories   1 with at least 5 admissible candidates")
            .And.Contain("selected       main    example-repo → c-four").And.Contain("selected       reserve example-repo → c-three")
            .And.Contain("drawn          1 main + 1 reserve + 0 pilot of 5 admitted candidate(s)").And.Contain("short          the D10 set is 8 main");
        var json = File.ReadAllText(TasksFile);
        ScoutFiles.Campaign(json).Should().Be(Campaign);
        ScoutFiles.Entries(json).Ok().Select(e => (e.Id, e.Role)).Should().Equal(("c-four", ScoutTaskRole.Main), ("c-three", ScoutTaskRole.Reserve));
        var task = ScoutFiles.Tasks(json).Ok().Should().ContainSingle("the reserve never runs").Subject;
        task.Id.Value.Should().Be("c-four");
        task.GroundTruth.Should().Equal("src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs");
        task.Removals.Should().Equal("todo/PLAN_change.md");
        task.TreeHash.Should().Be(Bench.Infrastructure.Scouts.ScoutTrees.Hashed(Path.Combine(_root.Sibling("trees"), "c-four")).TreeHash);
        Directory.Exists(Path.Combine(_root.Sibling("trees"), "c-three")).Should().BeTrue("the reserve's tree is frozen too — it is part of the pre-registration");
        Directory.Exists(Path.Combine(_root.Sibling("trees"), "c-one")).Should().BeFalse("an undrawn candidate builds nothing");
        ScoutFiles.Entries(File.ReadAllText(PilotFile)).Ok().Should().BeEmpty("one repository gives no pilot; the file says so");
    }

    /// <summary>Review finding (UX): reading each candidate off its mirror is serial and slow on a cold mirror, so every candidate is
    /// announced as it starts — <c>[i/n] id</c>, in the listed order, before the selection's own lines.</summary>
    [Fact]
    public async Task Every_candidate_is_announced_with_its_place_in_the_list_as_it_is_read()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates((parent, fix, "c-one", 1, Statement), (parent, fix, "c-two", 2, Statement));

        var (_, output, _) = await RunAsync(candidates, Cutoffs("2026-03-31"));

        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Where(l => l.StartsWith("reading ", StringComparison.Ordinal)).Should().Equal("reading        [1/2] c-one …", "reading        [2/2] c-two …");
        lines.FindIndex(l => l.StartsWith("reading ", StringComparison.Ordinal)).Should().BeLessThan(lines.FindIndex(l => l.StartsWith("drawn ", StringComparison.Ordinal)),
            "the progress is printed while the candidates are read, before the draw");
    }

    [Fact]
    public async Task A_repository_with_four_admissible_candidates_is_left_out_and_nothing_is_written()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates((parent, fix, "c-one", 1, Statement), (parent, fix, "c-two", 2, Statement), (parent, fix, "c-three", 3, Statement), (parent, fix, "c-four", 4, Statement));

        var (code, output, _) = await RunAsync(candidates, Cutoffs("2026-03-31"));

        code.Should().Be(ExitCodes.NoReport, output);
        output.Should().Contain("left out: example-repo (4)").And.Contain("drawn          0 main + 0 reserve + 0 pilot of 4 admitted");
        File.Exists(TasksFile).Should().BeFalse();
    }

    [Fact]
    public async Task A_candidate_whose_merge_style_does_not_match_the_mirror_is_excluded_by_id_with_its_code()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates(
            (parent, fix, "c-one", 1, Statement), (parent, fix, "c-two", 2, Statement), (parent, fix, "c-three", 3, Statement), (parent, fix, "c-four", 4, Statement), (parent, fix, "c-five", 5, Statement),
            (parent, fix, "c-merge", 0, Statement));

        var (code, output, error) = await RunAsync(candidates, Cutoffs("2026-03-31"));

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain($"excluded       c-merge — {ScoutParentCheck.MergeCommitNeedsTwoParents}").And.Contain("selected       main    example-repo → c-four");
    }

    [Fact]
    public async Task A_cut_off_after_every_candidates_parent_admits_none()
    {
        var (parent, fix) = await HistoryAsync();

        var (code, output, _) = await RunAsync(Candidates((parent, fix, "c-one", 1, Statement)), Cutoffs("2099-01-01"));

        code.Should().Be(ExitCodes.NoReport);
        output.Should().Contain("excluded       c-one — P was committed").And.Contain("not after the later cut-off 2099-01-01");
    }

    [Fact]
    public async Task An_id_without_a_recorded_cut_off_stops_the_selection_before_any_candidate_is_read()
    {
        var (parent, fix) = await HistoryAsync();

        var (code, _, error) = await RunAsync(Candidates((parent, fix, "c-one", 1, Statement)), Cutoffs("2026-03-31"), "--sonnet-id", "claude-sonnet-6-0");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("claude-sonnet-6-0").And.Contain("selection stops");
        Directory.Exists(_root.Sibling("checkouts")).Should().BeFalse("no mirror was touched");
    }

    [Fact]
    public async Task The_campaign_string_is_the_seed_and_a_selection_without_one_is_refused_by_name()
    {
        var (parent, fix) = await HistoryAsync();

        var (code, _, error) = await RunAsync(Candidates((parent, fix, "c-one", 1, Statement)), Cutoffs("2026-03-31"), "--campaign", "");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--campaign <seed string>");
    }

    [Fact]
    public async Task A_drawn_statement_naming_the_answer_refuses_the_selection_and_writes_nothing()
    {
        var (parent, fix) = await HistoryAsync();
        const string leaking = "Why does Gamma decide the settlement?";
        var candidates = Candidates((parent, fix, "c-one", 1, leaking), (parent, fix, "c-two", 2, leaking), (parent, fix, "c-three", 3, leaking), (parent, fix, "c-four", 4, leaking), (parent, fix, "c-five", 5, leaking));

        var (code, _, error) = await RunAsync(candidates, Cutoffs("2026-03-31"));

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("Stem 'Gamma'").And.Contain("nothing was written");
        File.Exists(TasksFile).Should().BeFalse();
        File.Exists(PilotFile).Should().BeFalse();
    }

    /// <summary>Review finding (security, E2 code round): a PRIVATE candidate refused at the freeze had <c>ScoutTask.Frozen</c>'s whole reason
    /// joined into the public refusal line. The console names the label and the harness's own code only; the full reason, the statement and the
    /// leaked tokens go to the withheld file under the trees root. The planted word is on no deny-list — only the projection keeps it out.</summary>
    [Fact]
    public async Task A_private_candidate_whose_statement_leaks_is_refused_by_label_and_code_and_the_console_carries_neither_the_token_nor_the_statement()
    {
        var (parent, fix) = await HistoryAsync();
        const string leaking = "Why does Gamma decide the settlement?";
        var key = Path.Combine(_root.Sibling("keys"), "scouts.key");
        ScoutCampaignKey.Create(key).Ok();
        var privateFile = Path.Combine(_root.Sibling("private"), "tasks.private.json");

        var (code, output, error) = await RunAsync(PrivateCandidates(parent, fix, leaking), Cutoffs("2026-03-31"), "--campaign-key", key, "--private-tasks-file", privateFile);

        code.Should().Be(ExitCodes.Configuration, output + error);
        error.Should().Contain("nothing was written")
            .And.MatchRegex(@"task p0\d \(private\): the statement leaks the answer — the detail is withheld from every public writer; it is in \.scout-trees/p0\d\.withheld\.txt under the trees root");
        (output + error).Should().NotContain("Gamma").And.NotContain(leaking).And.NotContain("token(s)", "the count of leaked tokens is detail too");
        File.Exists(TasksFile).Should().BeFalse();
        File.Exists(PilotFile).Should().BeFalse();
        File.Exists(privateFile).Should().BeFalse();
        var withheld = Directory.EnumerateFiles(Path.Combine(_root.Sibling("trees"), ".scout-trees"), "p0?.withheld.txt").Select(File.ReadAllText).ToList();
        withheld.Should().HaveCount(2, "the main task and its reserve were both refused");
        withheld.Should().AllSatisfy(text => text.Should().Contain(leaking).And.Contain("Stem 'Gamma'").And.Contain("the statement leaks the answer"));
    }

    /// <summary>Review finding (security, E2 code round): the verb writes four files — <c>--out</c>, <c>pilot-</c> beside it, <c>--private-tasks-file</c>
    /// and <c>pilot-</c> beside that — and two of them naming one place (by the filesystem's own case rule) had the last write win: the private
    /// detail over the public file, or the public file over the private detail. Every output path is resolved first, and a collision is refused
    /// (4) before a mirror is read or a byte is written — naming the flags, never the private path.</summary>
    [Fact]
    public async Task Output_paths_that_name_one_file_are_refused_before_anything_is_read_or_written()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates((parent, fix, "c-one", 1, Statement), (parent, fix, "c-two", 2, Statement), (parent, fix, "c-three", 3, Statement), (parent, fix, "c-four", 4, Statement), (parent, fix, "c-five", 5, Statement));

        var same = await RunAsync(candidates, Cutoffs("2026-03-31"), "--private-tasks-file", TasksFile);

        same.Code.Should().Be(ExitCodes.Configuration, same.Output + same.Error);
        same.Error.Should().Contain("--out").And.Contain("--private-tasks-file").And.Contain("one file").And.Contain("nothing was written");
        File.Exists(TasksFile).Should().BeFalse();
        File.Exists(PilotFile).Should().BeFalse();
        Directory.Exists(_root.Sibling("checkouts")).Should().BeFalse("refused before any mirror was read");

        var pilot = await RunAsync(candidates, Cutoffs("2026-03-31"), "--private-tasks-file", Path.Combine(Files, OperatingSystem.IsWindows() ? "PILOT-TASKS.JSON" : "pilot-tasks.json"));

        pilot.Code.Should().Be(ExitCodes.Configuration, pilot.Output + pilot.Error);
        pilot.Error.Should().Contain("the pilot file beside --out").And.Contain("--private-tasks-file");
        File.Exists(TasksFile).Should().BeFalse();
        File.Exists(PilotFile).Should().BeFalse();
    }

    private async Task<(string Parent, string Fix)> HistoryAsync()
    {
        await _repo.InitAsync(("README.md", "# example\n"), ("init", "2026-05-01T10:00:00+00:00"));
        var parent = await _repo.CommitManyAsync(
            [("src/Alpha.cs", "class Alpha {}\n"), ("src/Beta.cs", "class Beta {}\n"), ("src/Gamma.cs", "class Gamma {}\n"), ("todo/PLAN_change.md", "# plan\n")],
            ("p", "2026-06-01T10:00:00+00:00"));
        var fix = await _repo.CommitManyAsync(
            [("src/Alpha.cs", "class Alpha { int Settled; }\n"), ("src/Beta.cs", "class Beta { int Settled; }\n"), ("src/Gamma.cs", "class Gamma { int Settled; }\n"), ("todo/PLAN_change.md", "# done\n"), ("package-lock.json", "{}\n")],
            ("s", "2026-06-02T10:00:00+00:00"));
        return (parent, fix);
    }

    /// <summary>A candidate with a number is a squash-merged PR (<c>example-repo#n</c>); one without is listed as a MERGE COMMIT — which the
    /// one-parent fix is not, so the parent check excludes it.</summary>
    private string Candidates(params (string Parent, string Fix, string Id, int Number, string Statement)[] candidates)
    {
        Directory.CreateDirectory(Files);
        var file = Path.Combine(Files, "candidates.json");
        File.WriteAllText(file, new System.Text.Json.Nodes.JsonObject
        {
            ["candidates"] = new System.Text.Json.Nodes.JsonArray([.. candidates.Select(c => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = c.Id, ["repository"] = "example-org/example-repo", ["parent"] = c.Parent, ["fix"] = c.Fix,
                ["kind"] = "how-end-to-end", ["statement"] = c.Statement, ["planFile"] = "todo/PLAN_change.md",
                ["mergeStyle"] = c.Number > 0 ? "pr-squash" : "merge-commit", ["number"] = c.Number,
            })]),
        }.ToJsonString());
        return file;
    }

    /// <summary>Five PRIVATE candidates on the same change (a repository needs five), labelled <c>p01..p05</c>, listed as squash-merged PRs 1..5 of
    /// the test repository's own url — a private repository is read by its local url, never through the slug template.</summary>
    private string PrivateCandidates(string parent, string fix, string statement)
    {
        Directory.CreateDirectory(Files);
        var file = Path.Combine(Files, "private-candidates.json");
        File.WriteAllText(file, new JsonObject
        {
            ["candidates"] = new JsonArray([.. Enumerable.Range(1, 5).Select(n => (JsonNode)new JsonObject
            {
                ["id"] = $"p0{n}", ["repository"] = _repo.Url, ["parent"] = parent, ["fix"] = fix,
                ["kind"] = "how-end-to-end", ["statement"] = statement, ["planFile"] = "todo/PLAN_change.md",
                ["private"] = true, ["language"] = "C#", ["mergeStyle"] = "pr-squash", ["number"] = n,
            })]),
        }.ToJsonString());
        return file;
    }

    private string Cutoffs(string opus)
    {
        Directory.CreateDirectory(Files);
        var file = Path.Combine(Files, "cutoffs.json");
        File.WriteAllText(file, $$"""
            { "models": [
              { "modelId": "claude-opus-5-5", "cutoff": "{{opus}}", "source": "https://example.invalid/models", "readOn": "2026-10-12" },
              { "modelId": "claude-sonnet-5-5", "cutoff": "2026-01-31", "source": "https://example.invalid/models", "readOn": "2026-10-12" } ] }
            """);
        return file;
    }

    private async Task<(int Code, string Output, string Error)> RunAsync(string candidates, string cutoffs, params string[] extra)
    {
        var settings = Path.Combine(Files, "settings.json");
        File.WriteAllText(settings, "{}");
        string[] args =
        [
            "scouts", "tasks", "--candidates", candidates, "--cutoffs", cutoffs, "--opus-id", "claude-opus-5-5",
            .. extra.Contains("--sonnet-id") ? Array.Empty<string>() : ["--sonnet-id", "claude-sonnet-5-5"],
            .. extra.Contains("--campaign") ? Array.Empty<string>() : ["--campaign", Campaign],
            "--out", TasksFile, "--trees-root", _root.Sibling("trees"), "--checkout-root", _root.Sibling("checkouts"),
            "--repo-url-template", _repo.Url, "--claude-settings", settings, .. extra,
        ];
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ScoutsCommand.RunAsync(CommandLine.Parse(args), ScoutVerbServices.Machine, output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }

    public void Dispose()
    {
        _repo.Dispose();
        _root.Dispose();

        foreach (var sibling in new[] { "files", "trees", "checkouts", "keys", "private" })
        {
            try
            {
                TempGitRepo.DeleteTree(_root.Sibling(sibling));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The temp folder is the operating system's to clean.
            }
        }
    }
}
