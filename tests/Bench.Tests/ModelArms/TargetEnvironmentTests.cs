using Bench.Domain.Probes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The environment the target's tools run under (plan D17 d, E3 §8 item 8): the parent's variables minus this repository's, every secret-named one, every <c>GIT_*</c> and the scout flags, nothing added.
/// <para>
/// What this does NOT prove: that a target's own test that needs a secret then fails or skips as it would in the arm's checks — it does, in both (§8 item 8), which is the point, and no test here holds a target; or a secret under an innocent NAME (the rule is by name).
/// </para></summary>
public sealed class TargetEnvironmentTests
{
    private static IReadOnlyDictionary<string, string> Of(params (string Name, string Value)[] parent) =>
        TargetEnvironment.Of(parent.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal));

    [Fact]
    public void Only_this_repositorys_secret_and_git_variables_are_stripped()
    {
        (string Name, string Value)[] parent =
        [
            ("HOME", "/home/op"), ("PATH", "/usr/bin:/bin"), ("TMPDIR", "/tmp/op"), ("KEEP_ME", "yes"), ("DOTNET_ROOT", "/dotnet"), ("NUGET_PACKAGES", "/nuget"),
            ("BENCH_DB", "Host=db;Password=x"), ("bench_artifact_root", "/a"), ("ConnectionStrings__bench", "Host=db"), ("COAI_PROVIDERS", "all"),
            ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"), ("ANTHROPIC_API_KEY", "key-value"), ("GITHUB_TOKEN", "ghp_value"), ("DB_PASSWORD", "pw"), ("NPM_SECRET", "s"),
            ("GIT_DIR", "/elsewhere/.git"), ("git_work_tree", "/elsewhere"), ("GIT_CONFIG_COUNT", "1"),
            (ProbeChildEnvironment.ModelGuardVariable, "1"), (ProbeChildEnvironment.BackgroundTasksVariable, "1"),
        ];

        var child = Of(parent);

        child.Keys.Should().BeEquivalentTo(["HOME", "PATH", "TMPDIR", "KEEP_ME", "DOTNET_ROOT", "NUGET_PACKAGES"], "only what a build and a suite need survives, and nothing is added");
        child["TMPDIR"].Should().Be("/tmp/op", "the target's tools keep the parent's TMPDIR: it is not an arm's to choose");
        child.Should().NotContainKey("BENCH_DB").And.NotContainKey("GIT_DIR").And.NotContainKey("GITHUB_TOKEN");
    }

    [Fact]
    public void Claudes_own_login_is_stripped_for_the_target_though_the_arms_claude_keeps_it()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CODE_OAUTH_TOKEN"] = "o", ["ANTHROPIC_API_KEY"] = "k", ["HOME"] = "/h" };

        ArmChildEnvironment.Of(parent, new ArmPaths("/e", "tok")).Keys.Should().Contain(["CLAUDE_CODE_OAUTH_TOKEN", "ANTHROPIC_API_KEY"], "the contrast: an arm's claude has a login to keep");
        TargetEnvironment.Of(parent).Keys.Should().Equal(["HOME"], "the target's code has no login to keep, and an arm WROTE that code");
    }

    [Fact]
    public void The_parent_is_left_as_it_was()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_DIR"] = "/x", ["HOME"] = "/h" };

        TargetEnvironment.Of(parent);

        parent.Should().HaveCount(2);
    }
}
