using Bench.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>
/// `bench run --lanes` — the join between the lane catalog and a planned run.
///
/// <para>`bench lanes add|list|retire` filled the catalog and `LegPlan.Lanes` read a roster, and until this
/// wiring NO command joined the two: a built tool loop that nothing could reach. What these tests pin is
/// mostly the refusals, and deliberately — every one of them happens before a single cell exists, because
/// the alternative is discovering it as a wall of identical leg failures three hours into a sweep.</para>
/// </summary>
[Collection("postgres")]
public sealed class LaneRunPlanningTests(PostgresFixture postgres)
{
    /// <summary>Port 1 answers nothing anywhere, so every leg fails on transport in milliseconds — which is
    /// what makes an end-to-end `bench run` affordable as a test.</summary>
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    /// <summary>A real 40-hex sha: an abbreviation is refused before the lane axis is ever looked at, and a
    /// test that trips over that refusal proves nothing about lanes.</summary>
    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    [Fact]
    public void An_unknown_lane_ends_the_run_before_a_single_cell_exists()
    {
        var (code, _, error) = Planned("no-such-lane");

        Refused(code, error).Should().Contain("no-such-lane");
    }

    [Fact]
    public void A_tool_lane_with_no_checkout_is_refused_because_its_engine_needs_a_TREE()
    {
        // Not a pedantic guard: an engine rooted at nothing still ANSWERS. Every read fails, the subject
        // writes something anyway, and the leg is scored — a tool-surface number produced by a surface that
        // could not read a single file.
        var lane = $"bridge-{Guid.NewGuid():N}"[..20];
        Run("lanes", "add", "--name", lane, "--presentation", "bridge", "--db", postgres.ConnectionString);

        var (code, _, error) = Planned(lane);

        Refused(code, error).Should().Contain("--no-checkout").And.Contain("scored");
    }

    [Fact]
    public void A_FLOOR_lane_named_explicitly_needs_no_tree_because_it_offers_nothing_to_read()
    {
        // The one lane that provably does not need a checkout. Refusing it for want of one — as the first
        // version of this wiring did, checking the tree before it knew what the lanes were — would deny
        // "no tools, but read carefully", which is a legitimate arm and the floor every tool claim is
        // measured against.
        var lane = $"floor-{Guid.NewGuid():N}"[..20];
        Run("lanes", "add", "--name", lane, "--presentation", "none",
            "--doctrine", "Read carefully.", "--db", postgres.ConnectionString);

        var (code, announcement, error) = Planned(lane);

        code.Should().Be(ExitCodes.NoReport, error);
        announcement.Should().Contain(lane).And.Contain("no tools");
    }

    [Fact]
    public void A_retired_lane_stays_LISTABLE_and_stops_being_MEASURABLE()
    {
        var lane = $"retired-{Guid.NewGuid():N}"[..20];
        Run("lanes", "add", "--name", lane, "--presentation", "bridge", "--db", postgres.ConnectionString);
        Run("lanes", "retire", "--name", lane, "--db", postgres.ConnectionString);

        var (listed, listing, _) = Run("lanes", "list", "--all", "--db", postgres.ConnectionString);
        listed.Should().Be(ExitCodes.Pass);
        listing.Should().Contain(lane, "a report over an old test still has to name the surface it ran against");

        var (code, _, error) = Planned(lane);

        Refused(code, error).Should().Contain("retired");
    }

    /// <summary>A run that gets as far as the lane axis and no further.
    /// <para>
    /// It needs a real frozen selection: the suite is chosen BEFORE the lanes are resolved, so a run against
    /// an empty bank group refuses for a reason that has nothing to do with lanes — which is how the first
    /// version of these three tests passed their assertion on the wrong message.
    /// </para></summary>
    private (int Code, string Output, string Error) Planned(string lane)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var bank = new TempReadingBank(suffix, "https://example.invalid/x.git", Sha);
        Run("questions", "import", "--file", bank.Path, "--db", postgres.ConnectionString);

        return Run(
            "run", "--db", postgres.ConnectionString, "--bank-group", $"reading-{suffix}",
            "--repo", "https://example.invalid/x.git", "--commit", Sha, "--no-checkout",
            "--model", "qwen@local", "--model-url", DeadEndpoint, "--lanes", lane);
    }

    /// <summary>Environment (3), not Configuration (4): every refusal raised while a run is PLANNED shares
    /// one exit path with the unknown-variant and unreachable-engine refusals, and reading it as
    /// "configuration" here would be this test disagreeing with the CLI rather than describing it.</summary>
    private static string Refused(int code, string error)
    {
        code.Should().Be(ExitCodes.Environment, error);
        return error;
    }

    [Fact]
    public async Task A_resolved_lane_plans_its_cells_under_the_lane_NAME_and_announces_what_it_serves()
    {
        using var repo = await TempGitRepo.CreateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lane = $"bridge-{suffix}";
        using var bank = new TempReadingBank(suffix, repo.Url, repo.FirstCommit);

        Run("questions", "import", "--file", bank.Path, "--db", postgres.ConnectionString);
        var (added, _, addError) = Run(
            "lanes", "add", "--name", lane, "--presentation", "bridge", "--max-turns", "3",
            "--doctrine", "Search before you read.", "--db", postgres.ConnectionString);
        added.Should().Be(ExitCodes.Pass, addError);

        var (code, announcement, _) = Run(
            "run", "--repo", repo.Url, "--commit", repo.FirstCommit,
            "--bank-group", $"reading-{suffix}", "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--lanes", lane);

        code.Should().Be(
            ExitCodes.NoReport,
            "the dead endpoint answers nothing, so the run measured nothing — and that is not a pass");

        // The four tools the filesystem engine serves, and the ceiling the catalog row declared: printed
        // for the reason the recipes are, because a run that measured the floor while its operator believed
        // it measured a tool surface is the failure this line exists to make impossible.
        announcement.Should().Contain(lane).And.Contain("4 tool(s)").And.Contain("3 turn(s)");

        await using var db = postgres.NewContext();
        var cell = db.Cells.Single(c => c.QuestionId == $"readq-{suffix}");
        cell.Leg.Should().Contain(lane, "the lane name is the only join a cell carries back to its surface");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>One accepted reading question against a real local tree — the minimum a lane run can freeze
    /// a selection from.</summary>
    private sealed class TempReadingBank : IDisposable
    {
        public TempReadingBank(string suffix, string repoUrl, string commit)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-lanebank-{suffix}.json");
            File.WriteAllText(Path, $$"""
            {
              "targetRepo": "{{repoUrl.Replace("\\", "\\\\")}}",
              "authoredAtCommit": "{{commit}}",
              "groups": [ { "key": "reading-{{suffix}}", "title": "Reading tasks", "ordinal": 1 } ],
              "reviewers": [],
              "questions": [
                {
                  "group": "reading-{{suffix}}", "ordinal": 1, "kind": "Reading", "state": "Accepted",
                  "source": "BugsAndTests", "authorModel": "harvest",
                  "seed": { "kind": "commit", "reference": "abc", "at": "2026-08-11T00:00:00Z" },
                  "id": "readq-{{suffix}}",
                  "prompt": "What does this repository contain?",
                  "expectations": [ { "kind": "File", "file": "one.txt" } ]
                }
              ]
            }
            """);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
