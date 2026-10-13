using Bench.Domain.Scouts;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>E2 S8's second instrument correction (S5's live finding): a driver shaped as a blind verbatim relay — "launch exactly these N
/// Agent calls, these prompts verbatim, then reply DONE" — was refused by the API's safeguards on one launch and declined by the fallback
/// model; the access check passed once its prompt said what the check was for. So every cell prompt (the driver AND each focus prompt) now
/// says plainly what the run is, while every pinned property stays: the exact call count, the agent type and model alias, the prompts frozen
/// per cell (their hashes are what the read-back compares), and the KEY FILES / CLAIMS contract. The wording is a new VERSION, and no v1
/// hash can match a v2 prompt. Written RED first against the v1 wording.</summary>
public sealed class ScoutPromptsTests
{
    private const string Statement = "How does a cell reach its settlement after a crash?";

    /// <summary>The SHA-256s of the v1 focus prompts F1..F3 for <see cref="Statement"/> — computed outside the code (node) from the v1 wording,
    /// and confirmed RED against the v1 code: those were the hashes it produced.</summary>
    private static readonly string[] V1FocusHashes =
    [
        "cda25b639dc219f472b0f46c1c85974fac7d5d55299ed9007d9646417e802859",
        "7ceb5d4729d30bca2374d8d3385d2a1d66d47e0655303b6eb86e67e3731e3d13",
        "362400891112fc50e5b18fbf017b39c2e9cb24cd2b0024628ee97846db745f5b",
    ];

    private static readonly ScoutTask Task = ScoutTask.Frozen(
        ScoutTaskId.Parse("t01").Ok(), "example-org/example-repo", Bench.Domain.Targets.CommitSha.Parse(new string('a', 40)).Ok(),
        Bench.Domain.Targets.CommitSha.Parse(new string('b', 40)).Ok(), ScoutQuestionKind.HowEndToEnd, Statement,
        ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"], [], [], new string('c', 64)).Ok();

    public static TheoryData<ScoutArmId> Arms() => [.. Enum.GetValues<ScoutArmId>()];

    [Fact]
    public void The_purpose_says_what_the_run_is_in_plain_words()
    {
        ScoutPrompts.Purpose.Should().Contain("benchmark")
            .And.Contain("read-only")
            .And.Contain("code-exploring subagents")
            .And.Contain("files relevant to a described past change")
            .And.Contain("repository snapshot")
            .And.Contain("Nothing is modified");
    }

    [Theory]
    [MemberData(nameof(Arms))]
    public void The_driver_states_the_purpose_and_still_pins_the_count_the_agent_the_model_and_DONE(ScoutArmId id)
    {
        var arm = ScoutArms.Of(id);
        var prompts = ScoutPrompts.ForCell(Task, arm);

        using var _ = new AssertionScope();
        prompts.Driver.Should().StartWith(ScoutPrompts.Purpose, "the purpose comes first, before any instruction");
        prompts.Driver.Should().Contain($"exactly {arm.Count} Agent call(s)")
            .And.Contain($"subagent_type \"{arm.AgentType}\"")
            .And.Contain($"model \"{arm.ModelAlias}\"")
            .And.Contain("reply DONE");
        prompts.Driver.Should().NotContain("VERBATIM", "the blind-relay shape S5 saw refused is the refuted wording");
        prompts.Scouts.Should().HaveCount(arm.Count).And.OnlyHaveUniqueItems("the focus areas are distinct, never copies");
        prompts.Scouts.Should().OnlyContain(p => prompts.Driver.Contains(p), "each frozen prompt rides the driver as it is, between its markers");
    }

    [Theory]
    [MemberData(nameof(Arms))]
    public void Every_focus_prompt_states_the_purpose_and_keeps_the_output_contract(ScoutArmId id)
    {
        var prompts = ScoutPrompts.ForCell(Task, ScoutArms.Of(id));

        prompts.Scouts.Should().OnlyContain(p =>
            p.StartsWith(ScoutPrompts.Purpose, StringComparison.Ordinal)
            && p.Contains(Statement)
            && p.Contains($"{ScoutOutput.KeyFilesHeader}\n- at most {ScoutOutput.MaxLines} repository-relative paths")
            && p.Contains($"{ScoutOutput.ClaimsHeader}\n- at most {ScoutOutput.MaxLines} lines, each `path[:line] — one factual statement about that file`")
            && p.IndexOf(ScoutOutput.KeyFilesHeader, StringComparison.Ordinal) < p.IndexOf(ScoutOutput.ClaimsHeader + "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void The_v2_wording_is_a_new_version_and_no_v1_hash_matches_it()
    {
        var b2 = ScoutPrompts.ForCell(Task, ScoutArms.Of(ScoutArmId.B2));

        using var _ = new AssertionScope();
        ScoutPrompts.Version.Should().Be("scout-prompts-v2");
        b2.Sha256s.Should().NotIntersectWith(V1FocusHashes, "a v1 prompt hash must never read back as a v2 cell's frozen prompt");
        ScoutPrompts.ForCell(Task, ScoutArms.Of(ScoutArmId.B2)).Sha256s.Should().Equal(b2.Sha256s, "the prompts stay a pure function of statement, focus and arm");
    }

    /// <summary>The alias watch launches the same kind of session as a cell, so it carries the same correction: its driver says what the run is
    /// and is no blind verbatim relay, while it still asks for exactly two calls on the two aliases with the one fixed prompt.</summary>
    [Fact]
    public void The_alias_check_driver_states_its_purpose_and_is_no_blind_relay()
    {
        var driver = ScoutAliasCheck.Driver();

        using var _ = new AssertionScope();
        driver.Should().StartWith(ScoutAliasCheck.Purpose, "the purpose comes first, as in a cell's driver");
        driver.Should().NotContainEquivalentOf("verbatim", "the relay shape is what S5 saw refused");
        driver.Should().Contain("exactly 2 Agent calls").And.Contain("model \"sonnet\"").And.Contain("model \"opus\"");
        driver.Should().Contain(ScoutAliasCheck.ScoutPrompt, "the scouts still get the one fixed prompt");
    }
}
