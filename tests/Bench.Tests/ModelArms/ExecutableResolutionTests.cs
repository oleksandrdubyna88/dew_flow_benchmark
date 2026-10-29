using Bench.Domain;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>D17's second half, pure over facts: the executable of a command is a bare name resolved from PATH, or an absolute path, and
/// the resolved file is never under the target or the experiment root, neither as written nor at the end of a symbolic link.
/// <para>
/// What this does NOT prove: that a file the rule admits is safe to run (a file outside the forbidden roots may still be bad; the rule
/// keeps only the arms' own writes out), or what a link points at when its target is renamed after validation.
/// </para></summary>
public sealed class ExecutableResolutionTests
{
    private static readonly string[] Forbidden = [.. ExecutableRule.ForbiddenRoots("/t", "/r")];

    private static Outcome<string> Same(string path) => Outcome<string>.Success(path);

    private static Outcome<string> Resolve(
        string executable, string[] path, Func<string, bool> isExecutable, Func<string, Outcome<string>>? finalTarget = null, bool windows = false, string[]? forbidden = null) =>
        ExecutableResolution.Resolve(executable, path, forbidden ?? Forbidden, isExecutable, finalTarget ?? Same, windows);

    private static void PosixPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the cases use POSIX absolute paths");
            return;
        }
    }

    [Fact]
    public void A_name_resolves_to_the_first_executable_on_path()
    {
        PosixPaths();
        var resolved = Resolve("git", ["/a", "/b", "/c"], p => p is "/b/git" or "/c/git");

        resolved.Ok().Should().Be("/b/git");
    }

    [Fact]
    public void A_resolved_path_under_the_target_is_refused()
    {
        PosixPaths();
        var resolved = Resolve("tool", ["/t/bin"], _ => true);

        resolved.Reason().Should().Contain("D17").And.Contain("/t/bin/tool");
    }

    [Fact]
    public void An_absent_name_is_refused_by_name()
    {
        PosixPaths();
        Resolve("claude", ["/a", "/b"], _ => false).Reason().Should().Be("claude is not on PATH on this host");
    }

    [Fact]
    public void Empty_path_entries_are_skipped_and_never_mean_the_working_directory()
    {
        PosixPaths();
        var seen = new List<string>();
        var resolved = Resolve("git", ["", "/a"], p => { seen.Add(p); return p == "/a/git"; });

        resolved.Ok().Should().Be("/a/git");
        seen.Should().Equal("/a/git");
    }

    [Fact]
    public void A_path_entry_under_the_experiment_root_is_refused_whatever_the_case_it_is_spelled_in()
    {
        PosixPaths();
        Resolve("tool", ["/R/base/bin"], _ => true).Reason().Should().Contain("D17", "the root is forbidden, compared ignoring case");
        Resolve("tool", ["/T/BIN"], _ => true).Reason().Should().Contain("D17", "the target is forbidden, compared ignoring case");
    }

    [Fact]
    public void A_fully_qualified_executable_must_exist_and_be_executable()
    {
        PosixPaths();
        Resolve("/usr/bin/git", ["/a"], p => p == "/usr/bin/git").Ok().Should().Be("/usr/bin/git");
        Resolve("/usr/bin/git", ["/a"], _ => false).Reason().Should().Be("/usr/bin/git does not exist or is not executable");
    }

    [Fact]
    public void A_fully_qualified_executable_under_the_target_is_refused_even_when_it_exists()
    {
        PosixPaths();
        Resolve("/t/src/bin/tool", ["/a"], _ => true).Reason().Should().Contain("D17");
    }

    [Fact]
    public void A_relative_path_is_refused_because_it_would_resolve_against_the_target()
    {
        PosixPaths();
        Resolve("./bin/tool", ["/a"], _ => true).Reason().Should().Contain("relative path").And.Contain("D17");
    }

    [Fact]
    public void On_windows_a_name_is_also_tried_with_an_exe_suffix()
    {
        PosixPaths();
        var resolved = Resolve("git", ["/a"], p => p == "/a/git.exe", windows: true);

        resolved.Ok().Should().Be("/a/git.exe");
        Resolve("git", ["/a"], p => p == "/a/git.exe", windows: false).Failed().Should().BeTrue("off Windows a name is not given a suffix");
    }

    [Fact]
    public void A_link_outside_the_target_whose_final_target_is_inside_it_is_refused()
    {
        PosixPaths();
        Outcome<string> Link(string path) => Outcome<string>.Success(path == "/usr/local/bin/t" ? "/t/bin/t" : path);

        var resolved = Resolve("t", ["/usr/local/bin"], _ => true, Link);

        resolved.Reason().Should().Contain("D17").And.Contain("/usr/local/bin/t").And.Contain("/t/bin/t").And.Contain("symbolic link");
    }

    [Fact]
    public void A_link_whose_final_target_is_under_the_experiment_root_is_refused_too()
    {
        PosixPaths();
        Outcome<string> Link(string path) => Outcome<string>.Success("/R/arm/x");

        Resolve("t", ["/usr/bin"], _ => true, Link).Reason().Should().Contain("D17").And.Contain("/R/arm/x");
    }

    [Fact]
    public void A_link_to_a_safe_place_resolves_to_the_path_as_written()
    {
        PosixPaths();
        Outcome<string> Link(string path) => Outcome<string>.Success("/opt/real/git");

        Resolve("git", ["/usr/bin"], _ => true, Link).Ok().Should().Be("/usr/bin/git", "that is the name the harness will run");
    }

    [Fact]
    public void A_link_that_cannot_be_followed_refuses_instead_of_passing()
    {
        PosixPaths();
        Outcome<string> Broken(string path) => Outcome<string>.Failure("too many levels of symbolic links");

        Resolve("t", ["/usr/bin"], _ => true, Broken).Reason().Should().Contain("too many levels of symbolic links").And.Contain("/usr/bin/t");
    }

    [Fact]
    public void The_forbidden_roots_are_the_target_source_and_the_experiment_root_itself()
    {
        ExecutableRule.ForbiddenRoots("/t/src", "/r/exp").Should().Equal("/t/src", "/r/exp");
        ExecutableRule.ForbiddenRoots(string.Empty, "/r/exp").Should().Equal(["/r/exp"], "an unread source is not a root");
    }

    [Fact]
    public void A_real_symbolic_link_into_the_target_is_refused_and_a_real_file_beside_it_is_not()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("an executable bit and symbolic links are POSIX in this test");
            return;
        }
        using var temp = new TempFolder();
        var tool = temp.File("#!/bin/sh\n", "target", "bin", "t");
        File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var outside = temp.Folder("outside");
        var honest = temp.File("#!/bin/sh\n", "outside", "honest");
        File.SetUnixFileMode(honest, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            File.CreateSymbolicLink(System.IO.Path.Combine(outside, "t"), tool);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"symbolic links cannot be made here: {ex.Message}");
        }

        var forbidden = ExecutableRule.ForbiddenRoots(temp.Combine("target"), temp.Combine("root"));
        var link = ExecutableResolution.Resolve("t", [outside], forbidden, RootFacts.IsExecutableFile, RootFacts.FinalTargetOf, windows: false);
        var control = ExecutableResolution.Resolve("honest", [outside], forbidden, RootFacts.IsExecutableFile, RootFacts.FinalTargetOf, windows: false);

        link.Reason().Should().Contain("D17").And.Contain(tool);
        control.Ok().Should().Be(System.IO.Path.Combine(outside, "honest"));
    }
}
