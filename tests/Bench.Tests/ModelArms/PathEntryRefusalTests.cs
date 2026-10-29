using Bench.Domain;
using FluentAssertions;
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
        ExecutableResolution.PathEntryRefusal(entry, roots ?? Roots, realPath ?? Same);

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
        Func<string, Outcome<string>> kernel = path => path == "/opt" ? Outcome<string>.Success("/t/opt") : Outcome<string>.Failure("no such file or directory");

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
}
