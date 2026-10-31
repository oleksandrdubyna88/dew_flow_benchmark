using Bench.Domain.Probes;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The environment the target's tools run under (plan D17 d, E3 §8 item 8): the parent's variables minus this repository's, every secret-named one, every <c>GIT_*</c> and the scout flags, nothing added.
/// <para>
/// What this does NOT prove: that a target's own test that needs a secret then fails or skips as it would in the arm's checks — it does, in both (§8 item 8), which is the point, and no test here holds a target; that <c>HOME</c>'s files are unreadable — they are not, by design (E3 §8 item 17); or a secret under an innocent NAME (the rule is by name: <c>KEEP_ME=hunter2</c> passes, and the doc of <c>TargetEnvironment</c> says so).
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
            ("SSH_AUTH_SOCK", "/run/user/1000/keyring/ssh"), ("GIT_DIR", "/elsewhere/.git"), ("git_work_tree", "/elsewhere"), ("GIT_CONFIG_COUNT", "1"),
            (ProbeChildEnvironment.ModelGuardVariable, "1"), (ProbeChildEnvironment.BackgroundTasksVariable, "1"),
        ];

        var child = Of(parent);

        child.Keys.Should().BeEquivalentTo(["HOME", "PATH", "TMPDIR", "KEEP_ME", "DOTNET_ROOT", "NUGET_PACKAGES"], "only what a build and a suite need survives, and nothing is added");
        child["TMPDIR"].Should().Be("/tmp/op", "the target's tools keep the parent's TMPDIR: it is not an arm's to choose");
        child.Should().NotContainKey("BENCH_DB").And.NotContainKey("GIT_DIR").And.NotContainKey("GITHUB_TOKEN");
    }

    /// <summary>The names the shared <c>CoaiEnvironment.IsSecretName</c> suffix list (<c>_KEY</c>, <c>_TOKEN</c>, <c>_SECRET</c>, <c>_PASSWORD</c>) does not end with, and that a database client, a forge, npm or a cloud SDK reads a secret from; <c>NPM_TOKEN</c>, <c>NODE_AUTH_TOKEN</c> and the <c>_API_KEY</c> names are rows too — the shared list already covers them, and the rows keep it so.</summary>
    public static TheoryData<string> SecretShapedNames() =>
    [
        "PGPASSWORD", "pgpassword", "DATABASE_URL", "database_url",
        "GITHUB_PAT", "AZURE_DEVOPS_PAT", "my_pat",
        "STRIPE_APIKEY", "stripe_apikey", "OPENAI_API_KEY", "openai_api_key",
        "AWS_CREDENTIALS", "GOOGLE_CREDENTIAL", "google_credentials",
        "REGISTRY_AUTH", "NPM_AUTHTOKEN", "FEED_AUTH_TOKEN", "NODE_AUTH_TOKEN", "NPM_TOKEN",
        "npm_config__auth", "npm_config__authToken", "npm_config__authtoken", "NPM_CONFIG__AUTH", "npm_config_always_auth", "npm_config_//registry.npmjs.org/:_authToken", "npm_config_//registry.example/:_auth", "npm_config_registry_auth_scope",
    ];

    [Theory]
    [MemberData(nameof(SecretShapedNames))]
    public void A_secret_shaped_name_the_shared_suffix_list_misses_is_stripped_whatever_its_case(string name)
    {
        var child = Of((name, "hunter2"), ("KEEP_ME", "yes"), ("PATH", "/usr/bin:/bin"), ("HOME", "/home/op"), ("DOTNET_ROOT", "/dotnet"), ("NUGET_PACKAGES", "/nuget"));

        child.Should().NotContainKey(name, "the parent held a secret under this name and the target's tools must not inherit it");
        child.Keys.Should().BeEquivalentTo(["KEEP_ME", "PATH", "HOME", "DOTNET_ROOT", "NUGET_PACKAGES"], "nothing else goes with it");
    }

    [Theory]
    [InlineData("npm_config_cache"), InlineData("npm_config_registry"), InlineData("npm_config_prefix"), InlineData("NODE_PATH"), InlineData("PATHEXT"), InlineData("MY_PATH"), InlineData("AUTHOR"), InlineData("OAUTH_ENABLED"), InlineData("DATABASE_HOST"), InlineData("DOTNET_CLI_TELEMETRY_OPTOUT"), InlineData("NUGET_HTTP_CACHE_PATH")]
    public void A_name_that_only_looks_like_one_of_them_is_kept(string name)
    {
        Of((name, "v")).Should().ContainKey(name, "the rule ends with a name, it does not search one: a variable a build needs is not stripped for a look-alike");
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
