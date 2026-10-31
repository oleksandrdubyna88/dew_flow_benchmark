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
    /// <summary>Printed text after <c>git version </c>, and the verdict: <c>ok:&lt;numbers&gt;</c> admitted and read as those three numbers, <c>below</c> refused naming the version and the floor, <c>rc</c> refused as a release candidate.</summary>
    [Theory]
    [InlineData("2.45.0", "ok:2.45.0"), InlineData("2.53.0", "ok:2.53.0"), InlineData("2.44.1", "ok:2.44.1"), InlineData("2.44.2", "ok:2.44.2"), InlineData("2.43.4", "ok:2.43.4"), InlineData("2.42.2", "ok:2.42.2"), InlineData("2.41.1", "ok:2.41.1"), InlineData("2.40.2", "ok:2.40.2"), InlineData("2.39.4", "ok:2.39.4")]
    [InlineData("2.39.5 (Apple Git-154)", "ok:2.39.5"), InlineData("2.45.1.windows.1", "ok:2.45.1"), InlineData("2.46.0.GIT", "ok:2.46.0"), InlineData("2.46.0.12.gabcdef0", "ok:2.46.0"), InlineData("3.0.0", "ok:3.0.0"), InlineData("2.44.1\r\n", "ok:2.44.1")]
    [InlineData("2.44.0", "below"), InlineData("2.43.3", "below"), InlineData("2.42.1", "below"), InlineData("2.41.0", "below"), InlineData("2.40.1", "below"), InlineData("2.39.3", "below"), InlineData("2.38.5", "below"), InlineData("2.31.0", "below"), InlineData("1.9.0", "below")]
    [InlineData("2.45.0.rc1", "rc"), InlineData("2.46.0.rc0", "rc"), InlineData("2.46.0.RC2", "rc")]
    public void The_floor_admits_2_45_and_the_patched_maintenance_lines_and_refuses_the_rest(string printed, string verdict)
    {
        var text = "git version " + printed;

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
            reason.Should().Contain("release candidate");
            return;
        }

        reason.Should().Contain(printed.Split(' ')[0]).And.Contain(GitFloor.Sentence, "the refusal names the version read and the floor");
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
        { new string('x', 1000), new string('x', 200) },
    };

    [Theory]
    [MemberData(nameof(Unparseable))]
    public void An_unparseable_version_is_refused_naming_the_text_read(string text, string named)
    {
        var answer = GitFloor.Admit(text);

        answer.Should().BeOfType<Outcome<GitVersion>.Fail>($"Admit({ExecutableRule.Printable(text)}) is not a text this harness can read");
        var reason = ((Outcome<GitVersion>.Fail)answer).Reason;
        reason.Should().Contain(named).And.Contain("not a version this harness can read").And.NotContain("floor").And.NotContain("\0");
        if (text.Length > 200)
        {
            reason.Should().NotContain(text[..201], "at most 200 characters of a long line are named");
        }
    }
}
