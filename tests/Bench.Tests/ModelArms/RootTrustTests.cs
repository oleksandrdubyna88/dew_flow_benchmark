using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>D17's first half: a root and the files that name executables must be the operator's own and closed to group and others. The
/// decision is pure over facts; the live test reads the facts of a real folder through the console's adapter.
/// <para>
/// What this does NOT prove: that a refused root cannot be abused (this declines to run; it does not defend), or the check on Windows,
/// which is declared not performed and never passed.
/// </para></summary>
public sealed class RootTrustTests
{
    private const UnixFileMode Own = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static IReadOnlyList<string> ProblemsOf(TrustAnswer answer) =>
        answer.Should().BeOfType<TrustAnswer.Refused>().Subject.Problems;

    [Fact]
    public async Task A_root_or_file_writable_by_group_or_others_is_refused_naming_the_file_and_the_mode()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes are POSIX; the Windows answer is its own test");
            return;
        }
        using var temp = new TempFolder();
        var root = temp.Path;
        var experiment = temp.File("{}", "experiment.json");
        File.SetUnixFileMode(root, (UnixFileMode)Convert.ToInt32("775", 8));
        File.SetUnixFileMode(experiment, (UnixFileMode)Convert.ToInt32("664", 8));

        var uid = (await RootFacts.CurrentUidAsync(CancellationToken.None)).Ok();
        var facts = new[] { (await RootFacts.FileFactsAsync(root, CancellationToken.None)).Ok(), (await RootFacts.FileFactsAsync(experiment, CancellationToken.None)).Ok() };
        var problems = ProblemsOf(RootTrust.Decide(true, uid, facts, root));

        problems.Should().HaveCount(2);
        problems.Should().ContainSingle(p => p.Contains(root, StringComparison.Ordinal) && p.Contains("775", StringComparison.Ordinal) && p.Contains("writable by group or others", StringComparison.Ordinal));
        problems.Should().ContainSingle(p => p.Contains(experiment, StringComparison.Ordinal) && p.Contains("664", StringComparison.Ordinal) && p.Contains("chmod go-w", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_root_closed_to_group_and_others_and_owned_by_the_operator_is_trusted()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes are POSIX");
            return;
        }
        using var temp = new TempFolder();
        var experiment = temp.File("{}", "experiment.json");
        File.SetUnixFileMode(temp.Path, (UnixFileMode)Convert.ToInt32("755", 8));
        File.SetUnixFileMode(experiment, (UnixFileMode)Convert.ToInt32("644", 8));

        var uid = (await RootFacts.CurrentUidAsync(CancellationToken.None)).Ok();
        var facts = new[] { (await RootFacts.FileFactsAsync(temp.Path, CancellationToken.None)).Ok(), (await RootFacts.FileFactsAsync(experiment, CancellationToken.None)).Ok() };

        RootTrust.Decide(true, uid, facts, temp.Path).Should().BeOfType<TrustAnswer.Trusted>("readable by others is fine; writable is not");
    }

    [Fact]
    public void A_file_not_owned_by_the_current_user_is_refused()
    {
        var answer = RootTrust.Decide(true, 1000, [new FileFacts("/r/experiment.json", 0, Own)], "/r");

        var problems = ProblemsOf(answer);
        problems.Should().ContainSingle().Which.Should().Contain("/r/experiment.json").And.Contain("uid 0").And.Contain("uid 1000").And.Contain("D17");
    }

    [Fact]
    public void A_file_both_foreign_and_writable_is_refused_for_both_reasons_on_separate_lines()
    {
        var answer = RootTrust.Decide(true, 1000, [new FileFacts("/r/scenarios.json", 0, Own | UnixFileMode.OtherWrite)], "/r");

        var problems = ProblemsOf(answer);
        problems.Should().HaveCount(2);
        problems.Should().Contain(p => p.Contains("uid 0", StringComparison.Ordinal)).And.Contain(p => p.Contains("602", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_file_is_judged_not_only_the_first_that_fails()
    {
        var answer = RootTrust.Decide(
            true, 1000, [new FileFacts("/r", 0, Own), new FileFacts("/r/experiment.json", 1000, Own), new FileFacts("/r/scenarios.json", 7, Own)], "/r");

        ProblemsOf(answer).Should().HaveCount(2).And.Contain(p => p.Contains("/r ", StringComparison.Ordinal)).And.Contain(p => p.Contains("scenarios.json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_group_writable_mode_alone_is_refused_with_its_three_digit_octal_form()
    {
        var answer = RootTrust.Decide(true, 1000, [new FileFacts("/r/experiment.json", 1000, Own | UnixFileMode.GroupWrite)], "/r");

        ProblemsOf(answer).Should().ContainSingle().Which.Should().Contain("is mode 620");
    }

    [Fact]
    public void A_low_mode_is_padded_to_three_digits()
    {
        var answer = RootTrust.Decide(true, 1000, [new FileFacts("/r/x", 1000, UnixFileMode.OtherWrite)], "/r");

        ProblemsOf(answer).Should().ContainSingle().Which.Should().Contain("is mode 002");
    }

    [Fact]
    public void On_windows_the_check_is_declared_not_performed_never_passed_silently()
    {
        var answer = RootTrust.Decide(false, 0, [new FileFacts("/r/experiment.json", 0, UnixFileMode.OtherWrite)], "/r");

        var line = answer.Should().BeOfType<TrustAnswer.NotPerformed>("a check that was not performed is not a check that passed").Subject.Line;
        line.Should().Contain("not performed").And.Contain("/r").And.Contain("POSIX only");
        answer.Should().NotBeOfType<TrustAnswer.Trusted>();
    }

    private static FileFacts Facts(ulong device = 1, ulong inode = 2, FileKind kind = FileKind.Regular, int owner = 1000, UnixFileMode mode = Own) =>
        new("/r/experiment.json", owner, mode) { Identity = new FileIdentity(device, inode, kind) };

    [Fact]
    public void Two_reads_of_one_file_that_agree_on_everything_have_no_changes()
    {
        RootTrust.Changes(Facts(), Facts()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("device")]
    [InlineData("inode")]
    [InlineData("type")]
    [InlineData("owner")]
    [InlineData("mode")]
    public void Each_fact_that_differs_between_two_reads_is_named(string fact)
    {
        var after = fact switch
        {
            "device" => Facts(device: 2),
            "inode" => Facts(inode: 3),
            "type" => Facts(kind: FileKind.Fifo),
            "owner" => Facts(owner: 0),
            _ => Facts(mode: Own | UnixFileMode.GroupWrite),
        };

        RootTrust.Changes(Facts(), after).Should().Equal(fact);
    }

    [Fact]
    public void Every_fact_that_differs_is_named_not_only_the_first()
    {
        RootTrust.Changes(Facts(), Facts(device: 2, inode: 3, owner: 0)).Should().Equal("device", "inode", "owner");
    }
}
