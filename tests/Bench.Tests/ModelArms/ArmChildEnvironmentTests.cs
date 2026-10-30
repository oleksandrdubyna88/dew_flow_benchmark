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
