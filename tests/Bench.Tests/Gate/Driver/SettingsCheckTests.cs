using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.5 — what a run ASKED for against the session config the product wrote to disk, scoped to this run's own
/// session. A setting accepted and ignored looks exactly like one that worked; only the disk can tell them apart.</summary>
public sealed class SettingsCheckTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), "bench-gate-settings", Guid.NewGuid().ToString("N"));

    [Fact]
    public void An_accepted_and_ignored_knob_is_a_mismatch_and_an_unobservable_one_is_unchecked()
    {
        var asked = new Dictionary<string, string>
        {
            ["COAI_ROUNDS_PLANCRITIQUE"] = "2",
            ["COAI_THRESHOLD_PLANCRITIQUE"] = "6",
            ["COAI_ON_EXHAUSTED"] = "good_enough",
            ["COAI_LOCAL_REASONING_EFFORT"] = "medium",
        };
        const string config = """{"roles":{"PlanCritique":{"maxRounds":1,"threshold":6,"enabled":true}},"onExhausted":"GoodEnough"}""";

        var applied = SettingsCheck.Compare(asked, config);

        applied.Mismatches.Should().Equal(["PlanCritique rounds: asked 2, the session says 1"]);
        applied.Checked.Should().BeEquivalentTo(["PlanCritique rounds", "PlanCritique threshold", "on-exhausted"],
            "good_enough in the environment and GoodEnough in the state are one decision spelled twice");
        applied.Unchecked.Should().Contain("COAI_LOCAL_REASONING_EFFORT", "a knob whose effect the session file cannot show is unchecked, never passing");
    }

    [Fact]
    public void No_session_file_leaves_everything_unchecked_rather_than_passing()
    {
        var applied = SettingsCheck.Compare(new Dictionary<string, string> { ["COAI_ROUNDS_PLANCRITIQUE"] = "2" }, string.Empty);

        applied.Mismatches.Should().BeEmpty();
        applied.Checked.Should().BeEmpty();
        applied.Unchecked.Should().Contain("COAI_ROUNDS_PLANCRITIQUE");
    }

    [Fact]
    public void Only_this_runs_own_session_is_read_never_a_neighbours_in_the_same_directory()
    {
        Session("a.json", repo: "C:/checkouts/gate/run/cs2", branch: "bench/gate/other/rev/cs2-r1-a1", rounds: 9);
        Session("b.json", repo: "C:\\checkouts\\gate\\run\\cs2\\", branch: "bench/gate/mine/rev/cs2-r1-a1", rounds: 2);

        var mine = SessionConfigReader.Read(_data, "c:/checkouts/gate/run/cs2", "bench/gate/mine/rev/cs2-r1-a1");

        mine.Should().Contain("\"maxRounds\":2", "the repo path is compared with its separators and case normalised, the branch exactly");
        SessionConfigReader.Read(_data, "c:/checkouts/gate/run/cs2", "bench/gate/nobody").Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_data))
        {
            Directory.Delete(_data, recursive: true);
        }
    }

    private void Session(string name, string repo, string branch, int rounds)
    {
        var sessions = Path.Combine(_data, "sessions");
        Directory.CreateDirectory(sessions);
        var state = new System.Text.Json.Nodes.JsonObject
        {
            ["state"] = new System.Text.Json.Nodes.JsonObject
            {
                ["repoPath"] = repo,
                ["branch"] = branch,
                ["config"] = new System.Text.Json.Nodes.JsonObject { ["roles"] = new System.Text.Json.Nodes.JsonObject { ["PlanCritique"] = new System.Text.Json.Nodes.JsonObject { ["maxRounds"] = rounds } } },
            },
        };
        File.WriteAllText(Path.Combine(sessions, "session-" + name), state.ToJsonString());
    }
}
