using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>D17 for the PATH the harness's children inherit, pure over the kernel's answers: an entry the children could be made to run a file from is
/// refused — empty, relative, under a forbidden root as written, or really under one — and an entry that is merely absent is not.
/// <para>
/// What this does NOT prove: the real kernel's answers (<c>ValidateVerbTests</c> makes real links), a link made after the check, or that the children are given
/// a filtered PATH at launch (E2): this refuses a PATH, it does not rewrite one.
/// </para></summary>
public sealed class PathEntryRefusalTests
{
    private static readonly string[] Roots = [.. ExecutableRule.ForbiddenRoots("/t", "/r")];

    private static Outcome<string> Same(string path) => Outcome<string>.Success(path);

    private static string Refusal(string entry, Func<string, Outcome<string>>? realPath = null, string[]? roots = null) =>
        ExecutableResolution.PathEntryRefusal(entry, roots ?? Roots, ExecutableResolutionTests.Fake(_ => true, realPath));

    private static void PosixPaths() => Assert.SkipWhen(OperatingSystem.IsWindows(), "the cases use POSIX absolute paths");

    [Fact]
    public void An_entry_under_a_forbidden_root_as_written_is_refused_naming_it_the_root_and_D17()
    {
        PosixPaths();

        Refusal("/t/bin").Should().Contain("PATH entry '/t/bin'").And.Contain("'/t'").And.Contain("D17");
        Refusal("/r/pids").Should().Contain("'/r'");
        Refusal("/T/BIN").Should().Contain("'/t'", "the comparison ignores case and fails closed");
    }

    [Fact]
    public void An_entry_that_really_ends_under_a_forbidden_root_is_refused_naming_both_spellings()
    {
        PosixPaths();

        var refusal = Refusal("/opt/shortcut", path => Outcome<string>.Success(path == "/opt/shortcut" ? "/t/x/bin" : path));

        refusal.Should().Contain("PATH entry '/opt/shortcut'").And.Contain("really '/t/x/bin'").And.Contain("'/t'").And.Contain("D17");
    }

    [Fact]
    public void An_entry_that_does_not_exist_is_judged_by_its_nearest_existing_ancestor()
    {
        PosixPaths();
        Func<string, Outcome<string>> kernel = path => path switch
        {
            "/opt" => Outcome<string>.Success("/t/opt"),
            "/" => Outcome<string>.Success("/"),
            _ => Outcome<string>.Failure("No such file or directory"),
        };

        Refusal("/opt/not/there", kernel).Should().Contain("really '/t/opt/not/there'");
        Refusal("/elsewhere/not/there", kernel).Should().BeEmpty("an absent entry outside every root is skipped by every shell");
    }

    [Fact]
    public void An_empty_or_relative_entry_is_refused_naming_what_it_is()
    {
        PosixPaths();

        Refusal(string.Empty).Should().Contain("empty entry").And.Contain("current directory").And.Contain("D17");
        Refusal("bin").Should().Contain("'bin'").And.Contain("relative").And.Contain("D17");
        Refusal("./bin").Should().Contain("'./bin'");
    }

    [Fact]
    public void A_clean_absolute_entry_outside_every_root_is_not_refused()
    {
        PosixPaths();

        Refusal("/usr/bin").Should().BeEmpty();
        Refusal("/tmp/bin", roots: ["/t/"]).Should().BeEmpty();
        Refusal("/tx/bin").Should().BeEmpty("/tx is not under /t");
    }

    [Fact]
    public void A_NUL_in_an_entry_or_a_root_is_a_refusal_never_a_thrown_exception()
    {
        PosixPaths();

        Refusal("/usr/\0bin").Should().Contain("NUL").And.Contain("D17").And.NotContain("\0");
        Refusal("/usr/bin", roots: ["/t\0"]).Should().Contain("NUL").And.NotContain("\0");
    }

    [Fact]
    public void An_entry_whose_existing_component_cannot_be_resolved_is_refused_not_climbed_past()
    {
        PosixPaths();
        var host = new HostPaths(
            path => path is "/home/u/link" or "/home/u/link/bin" ? Outcome<string>.Failure("No such file or directory") : Same(path),
            path => Outcome<bool>.Success(path != "/home/u/link/bin"),
            _ => true);

        var refusal = ExecutableResolution.PathEntryRefusal("/home/u/link/bin", Roots, host);

        refusal.Should().Contain("'/home/u/link'").And.Contain("cannot be resolved").And.Contain("D17", "climbing to /home/u would judge the entry by a place it will not lead to");
    }

    [Fact]
    public void A_dangling_link_in_an_entry_is_refused_until_the_target_exists_and_then_judged_by_where_it_leads()
    {
        PosixPaths();
        using var temp = new TempFolder();
        temp.RequireLinks();
        var roots = ExecutableRule.ForbiddenRoots(temp.Combine("target"), temp.Combine("root"));
        temp.Folder("home");
        File.CreateSymbolicLink(temp.Combine("home", "link"), temp.Combine("target", "notyet"));
        var entry = temp.Combine("home", "link", "bin");

        ExecutableResolution.PathEntryRefusal(entry, roots, RootFacts.Host).Should().Contain("cannot be resolved").And.Contain(temp.Combine("home", "link"));

        temp.Folder("target", "notyet", "bin");

        ExecutableResolution.PathEntryRefusal(entry, roots, RootFacts.Host).Should().Contain("is really").And.Contain(temp.Combine("target", "notyet", "bin"), "once an arm makes the target, the entry leads into it");
    }

    [Fact]
    public void A_link_loop_in_an_entry_is_refused_naming_the_os_reason()
    {
        PosixPaths();
        using var temp = new TempFolder();
        temp.RequireLinks();
        File.CreateSymbolicLink(temp.Combine("l1"), temp.Combine("l2"));
        File.CreateSymbolicLink(temp.Combine("l2"), temp.Combine("l1"));
        var roots = ExecutableRule.ForbiddenRoots(temp.Combine("target"), temp.Combine("root"));

        ExecutableResolution.PathEntryRefusal(temp.Combine("l1", "bin"), roots, RootFacts.Host).Should().Contain("Too many levels").And.Contain("D17");
    }
}
