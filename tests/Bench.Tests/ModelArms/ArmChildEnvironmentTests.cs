using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The environment an arm's claude runs under: the parent's as the shell harness handed it on, minus this repository's
/// variables, plus the arm's own <c>TMPDIR</c> (plan D19, with the §8 item 1 correction — claude's own login travels in
/// <c>CLAUDE_*</c> and <c>ANTHROPIC_*</c> names that look like secrets and must pass).
/// <para>
/// What this does NOT prove: that claude authenticates with what is left — that is a fact about a real login on a real machine, which
/// no test here can hold. It proves the names that carry the login are not removed, and the names that carry this repository's
/// secrets are.
/// </para></summary>
public sealed class ArmChildEnvironmentTests
{
    private static readonly ArmPaths Arm = new("/e", "tok");

    private static IReadOnlyDictionary<string, string> Of(params (string Name, string Value)[] parent) =>
        ArmChildEnvironment.Of(parent.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal), Arm);

    [Fact]
    public void Home_path_locale_and_claudes_own_variables_pass_through_untouched()
    {
        (string Name, string Value)[] parent =
        [
            ("HOME", "/home/op"), ("PATH", "/usr/bin:/bin"), ("LANG", "en_US.UTF-8"), ("LC_ALL", "C.UTF-8"),
            ("CLAUDE_CONFIG_DIR", "/home/op/.claude-alt"), ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"),
            ("ANTHROPIC_API_KEY", "key-value"), ("EDITOR", "vim"),
        ];

        var child = Of(parent);

        foreach (var (name, value) in parent)
        {
            child.Should().Contain(new KeyValuePair<string, string>(name, value), $"{name} is claude's own or the shell's, and the arm needs it as the shell gave it");
        }
    }

    [Fact]
    public void Every_BENCH_and_database_variable_is_stripped_by_name()
    {
        var child = Of(
            ("HOME", "/home/op"), ("BENCH_DB", "Host=db;Password=x"), ("bench_artifact_root", "/artefacts"), ("ConnectionStrings__bench", "Host=db"),
            ("COAI_PROVIDERS", "all"), ("GITHUB_TOKEN", "ghp_value"), ("DB_PASSWORD", "pw"));

        child.Keys.Should().Equal(["HOME", ArmChildEnvironment.TmpDir], "only HOME survives besides the arm's own TMPDIR — every name of this repository, and every other secret-named one, is gone");
    }

    /// <summary>The sixteen names of the parent's E4.S0 test (plan D19, ledger 2.2). The first twelve are the ones <c>CoaiEnvironment.IsSecretName</c> alone MISSES — they are red on the shared rule — and the last four (<c>GITHUB_TOKEN</c>, <c>DB_PASSWORD</c>, <c>AWS_SECRET_ACCESS_KEY</c>, <c>AWS_SESSION_TOKEN</c>) it already strips and are rows to keep so.
    /// <c>GOOGLE_APPLICATION_CREDENTIALS</c>, <c>AWS_SECRET_ACCESS_KEY</c> and <c>AWS_SESSION_TOKEN</c> are the decided Vertex/Bedrock residual: an arm that logs in through them is not supported (D19, parent §9).</summary>
    public static TheoryData<string> SecretShapedNames() =>
    [
        "PGPASSWORD", "DATABASE_URL", "DOCKER_AUTH_CONFIG", "GITHUB_PAT", "STRIPE_APIKEY", "AWS_CREDENTIALS", "DB_PWD", "DEPLOY_PASSPHRASE", "APP_CONNECTION_STRING", "npm_config__auth", "npm_config_//registry.npmjs.org/:_authToken", "GOOGLE_APPLICATION_CREDENTIALS",
        "GITHUB_TOKEN", "DB_PASSWORD", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN",
    ];

    [Theory]
    [MemberData(nameof(SecretShapedNames))]
    public void A_secret_shaped_name_never_reaches_the_arms_session(string name)
    {
        foreach (var spelling in new[] { name, name.ToLowerInvariant(), string.Concat(name.Select((letter, index) => index % 2 == 0 ? char.ToUpperInvariant(letter) : char.ToLowerInvariant(letter))) })
        {
            var child = Of(("HOME", "/home/op"), (spelling, "hunter2"));

            child.Keys.Should().Equal(["HOME", ArmChildEnvironment.TmpDir], $"{spelling} is secret-shaped (SecretShaped.IsSecretShaped): the arm's session is the one process that runs arbitrary commands by design");
        }
    }

    /// <summary>GREEN on the code before E4.S0 — the exception already existed — so its red is the break-it that deletes the <c>CLAUDE_*</c> / <c>ANTHROPIC_*</c> clause (B2 of E4.S0): the union must not widen the exception away, or claude cannot log in. <c>CLAUDE_X_PAT</c> and <c>ANTHROPIC_CREDENTIALS</c> are names only the union catches and pass under the same exception, by D19's design.</summary>
    [Fact]
    public void Claudes_own_names_pass_even_when_secret_shaped()
    {
        (string Name, string Value)[] parent =
        [
            ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"), ("ANTHROPIC_API_KEY", "key-value"), ("ANTHROPIC_AUTH_TOKEN", "auth-value"), ("CLAUDE_X_PAT", "pat-value"), ("ANTHROPIC_CREDENTIALS", "credentials-value"),
        ];

        var child = Of([.. parent, ("CLAUDE_CODE_DISABLE_BACKGROUND_TASKS", "1")]);

        foreach (var (name, value) in parent)
        {
            child.Should().Contain(new KeyValuePair<string, string>(name, value), $"{name} starts CLAUDE_ or ANTHROPIC_: claude's login travels in such names");
        }

        child.Keys.Should().NotContain("CLAUDE_CODE_DISABLE_BACKGROUND_TASKS", "a scout flag, not a secret");
    }

    /// <summary>E6.S4 (a), the owner's decision of 2026-11-01 (ledger 3.2, D19): the ssh agent's socket is a capability and every <c>GIT_*</c> is steering — neither is a secret NAME, so neither is the secret rule's, and neither sits under its <c>CLAUDE_*</c> exception.
    /// The rows are the bench's own spellings and the ones <c>TargetEnvironmentTests</c> plants, in three cases of the socket and two of <c>GIT_DIR</c>.</summary>
    public static TheoryData<string> AgentSocketAndGitNames() =>
    [
        "SSH_AUTH_SOCK", "ssh_auth_sock", "Ssh_Auth_Sock", "GIT_DIR", "Git_Dir", "git_work_tree", "GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_VALUE_0",
        "GIT_SSH_COMMAND", "GIT_ASKPASS", "GIT_CEILING_DIRECTORIES", "GIT_CONFIG_GLOBAL", "GIT_EXEC_PATH",
    ];

    [Theory]
    [MemberData(nameof(AgentSocketAndGitNames))]
    public void The_ssh_agent_and_every_inherited_git_variable_never_reach_the_arms_session(string name)
    {
        var child = Of(("HOME", "/home/op"), ("KEEP_ME", "yes"), (name, "/run/user/1000/value"));

        child.Should().NotContainKey(name, $"{name} is the agent's socket or a GIT_* name, whatever its case: a capability and steering, stripped from the one process that runs arbitrary commands by design (ledger 3.2)");
        child.Keys.Should().Equal(["HOME", "KEEP_ME", ArmChildEnvironment.TmpDir], "nothing else moves");
    }

    [Fact]
    public void A_GIT_name_is_stripped_whatever_follows_and_claudes_own_names_holding_GIT_are_kept()
    {
        (string Name, string Value)[] kept =
        [
            ("CLAUDE_GIT_X", "a"), ("ANTHROPIC_GIT_DIR", "b"), ("CLAUDE_CODE_OAUTH_TOKEN", "oauth-value"), ("ANTHROPIC_API_KEY", "key-value"),
        ];

        var child = Of([("HOME", "/home/op"), ("GIT_CLAUDE_X", "x"), ("GIT_ANTHROPIC_DIR", "y"), .. kept]);

        child.Should().NotContainKey("GIT_CLAUDE_X", "git reads every GIT_* as its own whatever follows, and no login travels in one: the exception is the SECRET rule's alone");
        child.Should().NotContainKey("GIT_ANTHROPIC_DIR", "the same");
        foreach (var (name, value) in kept)
        {
            child.Should().Contain(new KeyValuePair<string, string>(name, value), $"{name} is not a GIT_ name (git never reads it) and claude's own names pass");
        }
    }

    /// <summary>The positive the file needs beside its negatives: the rule is a prefix and a whole name, never a search. GREEN before E6.S4 by design; its red is break-it B3.</summary>
    [Theory]
    [InlineData("GITHUB_ACTIONS")]
    [InlineData("GITLAB_CI")]
    [InlineData("MY_GIT_DIR")]
    [InlineData("DIGIT")]
    [InlineData("LEGIT_MODE")]
    [InlineData("SSH_AUTH_SOCKET")]
    [InlineData("SSH_AGENT_PID")]
    [InlineData("EMAIL")]
    [InlineData("PAGER")]
    [InlineData("XDG_CONFIG_HOME")]
    public void A_name_that_only_contains_GIT_or_resembles_the_socket_is_kept(string name)
    {
        var child = Of(("HOME", "/home/op"), (name, "kept-value"));

        child.Should().Contain(new KeyValuePair<string, string>(name, "kept-value"), $"{name} is neither the socket's whole name nor a GIT_ prefix");
    }

    /// <summary>The drift guard (<c>common.security</c> — enumerate the sites mechanically, leave the scan behind): the socket's name and the prefix are spelled in ONE file of the module (every <c>.cs</c> under <c>tools/model-arms</c>, the adapters included), so a fourth spelling is red here while every behaviour test stays green.
    /// The allow-list is the one file; no other file of the module spells either quoted literal today (<c>ArmGit</c>'s own pins spell whole names such as <c>"GIT_NO_LAZY_FETCH"</c>, which are no hit). The companion asserts EACH literal separately at the sanctioned site.</summary>
    [Fact]
    public void The_agent_socket_and_the_git_prefix_are_spelled_in_one_file_of_the_module()
    {
        var sites = SpellingSites();

        sites.SelectMany(file => file.Value.Select(line => $"{file.Key}:{line.Line}")).Where(site => !site.StartsWith(Sanctioned + ":", StringComparison.Ordinal))
            .Should().BeEmpty($"sites outside {Sanctioned}: the two literals live there alone, and every other file asks ArmChildEnvironment.IsAgentSocketOrGit");
    }

    [Theory]
    [InlineData("\"SSH_AUTH_SOCK\"")]
    [InlineData("\"GIT_\"")]
    public void The_scan_still_finds_each_literal_at_the_sanctioned_site(string literal)
    {
        SpellingSites().GetValueOrDefault(Sanctioned, []).Should().Contain(site => site.Text.Contains(literal, StringComparison.Ordinal), $"{literal} is spelled in {Sanctioned} — or the scan matches nothing and passes forever");
    }

    private const string Sanctioned = "ModelArms.Core/Experiment/ArmChildEnvironment.cs";

    private static Dictionary<string, List<(int Line, string Text)>> SpellingSites()
    {
        var module = Path.Combine(Cli.Repository.Root, "tools", "model-arms");
        var sites = new Dictionary<string, List<(int Line, string Text)>>();
        foreach (var path in Directory.EnumerateFiles(module, "*.cs", SearchOption.AllDirectories).Where(path => !Path.GetRelativePath(module, path).Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin")))
        {
            var lines = File.ReadAllLines(path);
            var hits = Enumerable.Range(0, lines.Length).Where(index => lines[index].Contains("\"SSH_AUTH_SOCK\"", StringComparison.Ordinal) || lines[index].Contains("\"GIT_\"", StringComparison.Ordinal)).Select(index => (index + 1, lines[index])).ToList();
            if (hits.Count > 0)
            {
                sites[Path.GetRelativePath(module, path).Replace('\\', '/')] = hits;
            }
        }

        return sites;
    }

    [Fact]
    public void Tmpdir_is_the_arms_own_whatever_the_parent_holds()
    {
        Of(("TMPDIR", "/tmp/claude-1000"))[ArmChildEnvironment.TmpDir].Should().Be(Arm.Tmp);
        Of(("tmpdir", "/tmp/other")).Should().ContainKey(ArmChildEnvironment.TmpDir).WhoseValue.Should().Be(Arm.Tmp);
        Of(("HOME", "/home/op"))[ArmChildEnvironment.TmpDir].Should().Be(Arm.Tmp, "a parent without one gets the arm's too");
        Of(("tmpdir", "/tmp/other")).Keys.Should().NotContain("tmpdir", "any spelling of the parent's is replaced, not kept beside");
    }

    [Fact]
    public void No_scout_flag_is_present_neither_the_model_guard_nor_background_tasks()
    {
        var child = Of(("SUBAGENT_MODEL_GUARD", "off"), ("CLAUDE_CODE_DISABLE_BACKGROUND_TASKS", "1"), ("CLAUDE_CONFIG_DIR", "/c"));

        child.Keys.Should().NotContain("SUBAGENT_MODEL_GUARD");
        child.Keys.Should().NotContain("CLAUDE_CODE_DISABLE_BACKGROUND_TASKS", "it starts CLAUDE_, but the arms run under the operator's own conditions — whatever its value");
        child.Keys.Should().Contain("CLAUDE_CONFIG_DIR", "the same prefix, a different name, still passes");
    }

    [Fact]
    public void The_parallel_tasks_of_a_batch_share_the_arms_tmpdir()
    {
        var parent = new Dictionary<string, string> { ["HOME"] = "/home/op" };

        var first = ArmChildEnvironment.Of(parent, Arm);
        var second = ArmChildEnvironment.Of(parent, Arm);
        var other = ArmChildEnvironment.Of(parent, new ArmPaths("/e", "other"));

        second[ArmChildEnvironment.TmpDir].Should().Be(first[ArmChildEnvironment.TmpDir], "the signature takes no task: one TMPDIR per arm");
        other[ArmChildEnvironment.TmpDir].Should().NotBe(first[ArmChildEnvironment.TmpDir], "another arm has its own");
    }

    [Fact]
    public void The_result_looks_names_up_case_sensitively_and_the_parent_is_left_as_it_was()
    {
        var parent = new Dictionary<string, string> { ["HOME"] = "/home/op", ["BENCH_DB"] = "x" };

        var child = ArmChildEnvironment.Of(parent, Arm);

        child.ContainsKey("home").Should().BeFalse("the comparer is ordinal");
        parent.Should().HaveCount(2, "the parent is a value, never edited");
    }
}
