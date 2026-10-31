using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The git floor as arithmetic over a text (plan D23, E4.S0): <c>git --version</c>'s first line parsed by a fixed grammar, a pre-release refused, and the number held against 2.45.0 or the patched maintenance lines. The grammar's every row is a case here; where the text comes from, and what each verb does with a refusal, are the verb tests'.
/// <para>
/// What this does NOT prove: that a git carrying a number carries the fix — the floor reads a NUMBER, and a vendor build with the number and without the fix passes (parent §9); that the version text a real git prints parses — the host's own is a row of <c>ArmGitWideningTests</c>, and the pilot runs the suite on 2.39.4 and 2.45.0.
/// </para></summary>
public sealed class GitFloorTests
{
    /// <summary>300 characters: a suffix longer than the 200 a sentence may name.</summary>
    private const string Long = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    /// <summary>The text git printed, and the verdict: <c>ok:&lt;numbers&gt;</c> admitted and read as those three numbers, <c>below</c> refused naming the version and the floor, <c>rc</c> refused as a release candidate.</summary>
    [Theory]
    [InlineData("git version 2.45.0", "ok:2.45.0"), InlineData("git version 2.53.0", "ok:2.53.0"), InlineData("git version 2.44.1", "ok:2.44.1"), InlineData("git version 2.44.2", "ok:2.44.2"), InlineData("git version 2.43.4", "ok:2.43.4"), InlineData("git version 2.42.2", "ok:2.42.2"), InlineData("git version 2.41.1", "ok:2.41.1"), InlineData("git version 2.40.2", "ok:2.40.2"), InlineData("git version 2.39.4", "ok:2.39.4")]
    [InlineData("git version 2.39.5 (Apple Git-154)", "ok:2.39.5"), InlineData("git version 2.45.1.windows.1", "ok:2.45.1"), InlineData("git version 2.46.0.GIT", "ok:2.46.0"), InlineData("git version 2.46.0.12.gabcdef0", "ok:2.46.0"), InlineData("git version 3.0.0", "ok:3.0.0"), InlineData("git version 2.44.1\r\n", "ok:2.44.1"), InlineData("  git version 2.44.1  ", "ok:2.44.1")]
    [InlineData("git version 2.44.0", "below"), InlineData("git version 2.43.3", "below"), InlineData("git version 2.42.1", "below"), InlineData("git version 2.41.0", "below"), InlineData("git version 2.40.1", "below"), InlineData("git version 2.39.3", "below"), InlineData("git version 2.38.5", "below"), InlineData("git version 2.31.0", "below"), InlineData("git version 1.9.0", "below"), InlineData("git version 2.44.0\r\n", "below")]
    [InlineData("git version 2.45.0.rc1", "rc"), InlineData("git version 2.46.0.rc0", "rc"), InlineData("git version 2.46.0.RC2", "rc"), InlineData("git version 2.45.0.rc1." + Long, "rc")]
    public void The_floor_admits_2_45_and_the_patched_maintenance_lines_and_refuses_the_rest(string text, string verdict)
    {
        var answer = GitFloor.Admit(text);

        if (verdict.StartsWith("ok:", StringComparison.Ordinal))
        {
            answer.Should().BeOfType<Outcome<GitVersion>.Ok>($"Admit(\"{text}\") is at or above the floor");
            ((Outcome<GitVersion>.Ok)answer).Value.ToString().Should().Be(verdict[3..], "the three numbers, the suffix omitted");
            return;
        }

        answer.Should().BeOfType<Outcome<GitVersion>.Fail>($"Admit(\"{text}\") is refused ({verdict})");
        var reason = ((Outcome<GitVersion>.Fail)answer).Reason;
        if (verdict == "rc")
        {
            reason.Should().Contain("release candidate").And.NotContain(new string('x', 201), "the suffix is named through Printable and cut at 200 characters");
            return;
        }

        reason.Should().Contain(text.Trim().Split(' ')[2]).And.Contain(GitFloor.Sentence, "the refusal names the version read and the floor");
    }

    /// <summary>The text git (or a wrapper) printed, and what the refusal must name of it: the empty text is <c>printed nothing</c>, a NUL is <c>\0</c>.</summary>
    public static TheoryData<string, string> Unparseable() => new()
    {
        { string.Empty, "printed nothing" },
        { "git version", "git version" },
        { "git version two", "git version two" },
        { "git version 2.45", "git version 2.45" },
        { "git version 2.45.0-rc0", "2.45.0-rc0" },
        { "v2.45.0", "v2.45.0" },
        { "fatal: not a git repository", "fatal: not a git repository" },
        { "git version 2.45\0.0.windows.1", "2.45\\0.0.windows.1" },
        { "git version 2\0.45.0", "2\\0.45.0" },
        { "git version 2.45.0.windows.1\0", "windows.1\\0" },
        { "git version 2.45.0.\u001b[31mred", "2.45.0.\\u001B[31mred" },
        { "git version 2.45.0.rc1\u001b[31m", "rc1\\u001B[31m" },
        { new string('x', 1000), new string('x', 200) },
    };

    [Theory]
    [MemberData(nameof(Unparseable))]
    public void An_unparseable_version_is_refused_naming_the_text_read(string text, string named)
    {
        var answer = GitFloor.Admit(text);

        answer.Should().BeOfType<Outcome<GitVersion>.Fail>($"Admit({ExecutableRule.Printable(text)}) is not a text this harness can read");
        var reason = ((Outcome<GitVersion>.Fail)answer).Reason;
        reason.Should().Contain(named).And.Contain("not a version this harness can read").And.NotContain("floor").And.NotContain("\0").And.NotContain("\u001b");
        if (text.Length > 200)
        {
            reason.Should().NotContain(text[..201], "at most 200 characters of a long line are named");
        }
    }
}
