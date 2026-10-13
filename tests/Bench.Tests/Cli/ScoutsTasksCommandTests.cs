using Bench.Cli;
using Bench.Domain.Scouts;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench scouts tasks</c> against a real local repository: the cut-off guard (a recorded id admits candidates after it; an id with
/// none stops the selection, 4), the pilot's tasks never candidates, the seeded draw, each drawn tree built and its hash frozen, and a leaking
/// statement refusing the whole selection with nothing written. No database, no model.</summary>
public sealed class ScoutsTasksCommandTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);
    private readonly TempRoot _root = GateStoreFixtures.NewRoot();

    private string Files => _root.Sibling("files");

    [Fact]
    public async Task A_recorded_cut_off_admits_the_candidates_after_it_never_a_pilot_task_and_freezes_their_trees()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates((parent, fix, "c-one", "How does a cell reach its settlement?"), (parent, fix, "c-pilot", "How is the ledger moved?"));

        var (code, output, error) = await RunAsync(candidates, Cutoffs("2026-03-31"), "--pilot", "c-pilot");

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain("the later published cut-off is 2026-03-31").And.Contain("excluded       c-pilot — a pilot task");
        var tasks = ScoutFiles.Tasks(File.ReadAllText(Path.Combine(Files, "tasks.json"))).Ok();
        var task = tasks.Should().ContainSingle().Subject;
        task.Id.Value.Should().Be("c-one");
        task.GroundTruth.Should().Equal("src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs");
        task.Removals.Should().Equal("todo/PLAN_change.md");
        task.TreeHash.Should().Be(Bench.Infrastructure.Scouts.ScoutTrees.Hashed(Path.Combine(_root.Sibling("trees"), "c-one")).TreeHash);
    }

    /// <summary>Review finding (UX): reading each candidate off its mirror is serial and slow on a cold mirror, so every candidate is
    /// announced as it starts — <c>[i/n] id</c>, in the listed order, before the selection's own lines.</summary>
    [Fact]
    public async Task Every_candidate_is_announced_with_its_place_in_the_list_as_it_is_read()
    {
        var (parent, fix) = await HistoryAsync();
        var candidates = Candidates((parent, fix, "c-one", "How does a cell reach its settlement?"), (parent, fix, "c-pilot", "How is the ledger moved?"));

        var (code, output, error) = await RunAsync(candidates, Cutoffs("2026-03-31"), "--pilot", "c-pilot");

        code.Should().Be(ExitCodes.Pass, output + error);
        var lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Where(l => l.StartsWith("reading ", StringComparison.Ordinal)).Should().Equal("reading        [1/2] c-one …", "reading        [2/2] c-pilot …");
        lines.FindIndex(l => l.StartsWith("reading ", StringComparison.Ordinal)).Should().BeLessThan(lines.FindIndex(l => l.StartsWith("drawn ", StringComparison.Ordinal)),
            "the progress is printed while the candidates are read, before the draw");
    }

    [Fact]
    public async Task A_cut_off_after_every_candidates_parent_admits_none()
    {
        var (parent, fix) = await HistoryAsync();

        var (code, output, _) = await RunAsync(Candidates((parent, fix, "c-one", "How does a cell reach its settlement?")), Cutoffs("2099-01-01"));

        code.Should().Be(ExitCodes.NoReport);
        output.Should().Contain("excluded       c-one — P was committed").And.Contain("not after the later cut-off 2099-01-01");
    }

    [Fact]
    public async Task An_id_without_a_recorded_cut_off_stops_the_selection_before_any_candidate_is_read()
    {
        var (parent, fix) = await HistoryAsync();

        var (code, _, error) = await RunAsync(Candidates((parent, fix, "c-one", "How does it work?")), Cutoffs("2026-03-31"), "--sonnet-id", "claude-sonnet-6-0");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("claude-sonnet-6-0").And.Contain("selection stops");
        Directory.Exists(_root.Sibling("checkouts")).Should().BeFalse("no mirror was touched");
    }

    [Fact]
    public async Task A_drawn_statement_naming_the_answer_refuses_the_selection_and_writes_nothing()
    {
        var (parent, fix) = await HistoryAsync();

        var (code, _, error) = await RunAsync(Candidates((parent, fix, "c-one", "Why does Gamma decide the settlement?")), Cutoffs("2026-03-31"));

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("Stem 'Gamma'").And.Contain("nothing was written");
        File.Exists(Path.Combine(Files, "tasks.json")).Should().BeFalse();
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

    private string Candidates(params (string Parent, string Fix, string Id, string Statement)[] candidates)
    {
        Directory.CreateDirectory(Files);
        var file = Path.Combine(Files, "candidates.json");
        File.WriteAllText(file, new System.Text.Json.Nodes.JsonObject
        {
            ["candidates"] = new System.Text.Json.Nodes.JsonArray([.. candidates.Select(c => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = c.Id, ["repository"] = "example-org/example-repo", ["parent"] = c.Parent, ["fix"] = c.Fix,
                ["kind"] = "how-end-to-end", ["statement"] = c.Statement, ["planFile"] = "todo/PLAN_change.md",
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
            "--out", Path.Combine(Files, "tasks.json"), "--trees-root", _root.Sibling("trees"), "--checkout-root", _root.Sibling("checkouts"),
            "--repo-url-template", _repo.Url, "--claude-settings", settings, "--seed", "3", .. extra,
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

        foreach (var sibling in new[] { "files", "trees", "checkouts" })
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
