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

    [Theory]
    [InlineData(@"\\?\C:\t\x.exe")]
    [InlineData(@"\\.\C:\t\x.exe")]
    [InlineData("//?/C:/t/x.exe")]
    [InlineData("//./C:/t/x.exe")]
    public void A_verbatim_or_device_prefix_is_refused_as_a_spelling_on_every_platform(string spelled)
    {
        var refusal = ExecutableRule.Refusal(spelled, [@"C:\t"]);

        refusal.Should().Contain("verbatim or device prefix").And.Contain("D17");
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
    public void A_link_outside_the_target_whose_real_path_is_inside_it_is_refused()
    {
        PosixPaths();
        Outcome<string> Link(string path) => Outcome<string>.Success(path == "/usr/local/bin/t" ? "/t/bin/t" : path);

        var resolved = Resolve("t", ["/usr/local/bin"], _ => true, Link);

        resolved.Reason().Should().Contain("D17").And.Contain("/usr/local/bin/t").And.Contain("is really").And.Contain("/t/bin/t");
    }

    [Fact]
    public void A_link_whose_real_path_is_under_the_experiment_root_is_refused_too()
    {
        PosixPaths();
        Outcome<string> Link(string path) => Outcome<string>.Success(path == "/usr/bin/t" ? "/R/arm/x" : path);

        Resolve("t", ["/usr/bin"], _ => true, Link).Reason().Should().Contain("D17").And.Contain("/R/arm/x");
    }

    [Fact]
    public void The_result_is_the_real_path_the_kernel_would_open()
    {
        PosixPaths();
        Outcome<string> Link(string path) => Outcome<string>.Success(path == "/usr/bin/git" ? "/opt/real/git" : path);

        Resolve("git", ["/usr/bin"], _ => true, Link).Ok().Should().Be("/opt/real/git", "judging the real path and launching the written one leaves the link for an arm to re-point");
    }

    [Fact]
    public void The_real_result_of_a_real_link_is_the_file_it_ends_at()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var real = temp.Executable("opt", "real", "git");
        temp.Folder("outside");
        File.CreateSymbolicLink(temp.Combine("outside", "git"), real);

        var resolved = ExecutableResolution.Resolve("git", [temp.Combine("outside")], Roots(temp), RootFacts.IsExecutableFile, RootFacts.RealPathOf, windows: false);

        resolved.Ok().Should().Be(real);
    }

    [Fact]
    public void A_link_that_cannot_be_followed_refuses_instead_of_passing()
    {
        PosixPaths();
        Outcome<string> Broken(string path) => Outcome<string>.Failure("too many levels of symbolic links");

        Resolve("t", ["/usr/bin"], _ => true, Broken).Reason().Should().Contain("too many levels of symbolic links").And.Contain("/usr/bin/t");
    }

    [Fact]
    public void A_root_that_does_not_exist_yet_is_resolved_through_its_nearest_existing_ancestor()
    {
        PosixPaths();
        Outcome<string> Real(string path) => path switch
        {
            "/mnt/d/exp/new" => Outcome<string>.Failure("No such file or directory"),
            "/mnt/d/exp" => Outcome<string>.Success("/real/exp"),
            _ => Outcome<string>.Success(path),
        };

        var refusal = Resolve("/real/exp/new/bin/t", [], _ => true, Real, forbidden: ["/mnt/d/exp/new"]);

        refusal.Reason().Should().Contain("D17").And.Contain("/real/exp/new");
    }

    [Fact]
    public void The_forbidden_roots_are_the_target_source_and_the_experiment_root_itself()
    {
        ExecutableRule.ForbiddenRoots("/t/src", "/r/exp").Should().Equal("/t/src", "/r/exp");
        ExecutableRule.ForbiddenRoots(string.Empty, "/r/exp").Should().Equal(["/r/exp"], "an unread source is not a root");
    }

    private const string NoLinks = "symbolic links need a privilege on Windows; the Windows spellings are their own tests";

    private static string[] Roots(TempFolder temp) => [.. ExecutableRule.ForbiddenRoots(temp.Combine("target"), temp.Combine("root"))];

    private static Outcome<string> Real(TempFolder temp, string executable, string[] path) =>
        ExecutableResolution.Resolve(executable, path, Roots(temp), RootFacts.IsExecutableFile, RootFacts.RealPathOf, windows: false);

    [Fact]
    public void A_real_symbolic_link_into_the_target_is_refused_and_a_real_file_beside_it_is_not()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var tool = temp.Executable("target", "bin", "t");
        var outside = temp.Folder("outside");
        temp.Executable("outside", "honest");
        File.CreateSymbolicLink(System.IO.Path.Combine(outside, "t"), tool);

        var link = Real(temp, "t", [outside]);
        var control = Real(temp, "honest", [outside]);

        link.Reason().Should().Contain("D17").And.Contain(tool);
        control.Ok().Should().Be(System.IO.Path.Combine(outside, "honest"), "the real path of a real file is itself");
    }

    [Fact]
    public void A_link_whose_target_climbs_through_a_linked_folder_is_judged_where_the_kernel_lands()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var claude = temp.Executable("target", "bin", "claude");
        temp.Folder("target", "x");
        temp.Folder("home", "c");
        Directory.CreateSymbolicLink(temp.Combine("home", "c", "sub"), temp.Combine("target", "x"));
        File.CreateSymbolicLink(temp.Combine("home", "c", "claude"), "sub/../bin/claude");
        temp.Executable("home", "c", "bin", "claude");

        var resolved = Real(temp, "claude", [temp.Combine("home", "c")]);

        resolved.Reason().Should().Contain("D17").And.Contain("is really").And.Contain(claude);
    }

    [Fact]
    public void A_dot_dot_after_a_linked_folder_is_applied_to_the_folder_the_link_points_to()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var claude = temp.Executable("target", "bin", "claude");
        temp.Folder("target", "x");
        temp.Folder("home");
        Directory.CreateSymbolicLink(temp.Combine("home", "l"), temp.Folder("target", "x"));
        temp.Executable("home", "bin", "claude");
        var viaPath = temp.Combine("home", "l", "..", "bin");

        Real(temp, "claude", [viaPath]).Reason().Should().Contain("D17").And.Contain(claude, "a PATH entry spelled through a link and '..'");
        Real(temp, System.IO.Path.Combine(viaPath, "claude"), []).Reason().Should().Contain("D17").And.Contain(claude, "the same spelled as the executable");
    }

    [Fact]
    public void A_forbidden_root_spelled_through_a_link_is_compared_by_its_real_path()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var tool = temp.Executable("mnt", "git", "proj", "bin", "t");
        temp.Folder("home", "bin");
        Directory.CreateSymbolicLink(temp.Combine("home", "git"), temp.Folder("mnt", "git"));
        File.CreateSymbolicLink(temp.Combine("home", "bin", "t"), tool);
        temp.Executable("home", "bin", "honest");
        var roots = ExecutableRule.ForbiddenRoots(temp.Combine("home", "git", "proj"), temp.Combine("root"));

        var refused = ExecutableResolution.Resolve("t", [temp.Combine("home", "bin")], roots, RootFacts.IsExecutableFile, RootFacts.RealPathOf, windows: false);
        var control = ExecutableResolution.Resolve("honest", [temp.Combine("home", "bin")], roots, RootFacts.IsExecutableFile, RootFacts.RealPathOf, windows: false);

        refused.Reason().Should().Contain("D17").And.Contain(temp.Combine("mnt", "git", "proj"));
        control.Ok().Should().Be(temp.Combine("home", "bin", "honest"));
    }

    [Fact]
    public void A_dangling_link_on_path_is_skipped_and_the_search_moves_on()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        temp.Folder("bin1");
        File.CreateSymbolicLink(temp.Combine("bin1", "claude"), "nowhere");
        var good = temp.Executable("bin2", "claude");

        Real(temp, "claude", [temp.Combine("bin1"), temp.Combine("bin2")]).Ok().Should().Be(good);
    }

    [Fact]
    public void A_looping_link_on_path_is_skipped_too()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        temp.Folder("bin0");
        File.CreateSymbolicLink(temp.Combine("bin0", "claude"), "other");
        File.CreateSymbolicLink(temp.Combine("bin0", "other"), "claude");
        var good = temp.Executable("bin2", "claude");

        Real(temp, "claude", [temp.Combine("bin0"), temp.Combine("bin2")]).Ok().Should().Be(good);
    }

    [Fact]
    public void A_spelled_executable_that_is_a_dangling_link_is_refused_naming_the_reason()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        temp.Folder("bin1");
        File.CreateSymbolicLink(temp.Combine("bin1", "claude"), "nowhere");

        Real(temp, temp.Combine("bin1", "claude"), []).Reason().Should().Contain("cannot be followed").And.Contain("No such file").And.Contain("D17");
    }

    /// <summary>A guard against dropping the WRITTEN side of the judgement: a link INSIDE the target that points out of it must still be refused by its
    /// spelling, because D17 forbids the spelling and not only the bytes. This is green before the fix too (the written check already exists); it is here
    /// so that judging the real path alone goes red.</summary>
    [Fact]
    public void A_link_inside_the_target_that_points_out_of_it_is_still_refused_by_its_spelling()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        temp.Executable("outside", "git");
        temp.Folder("target");
        Directory.CreateSymbolicLink(temp.Combine("target", "sub"), temp.Combine("outside"));

        Real(temp, temp.Combine("target", "sub", "git"), []).Reason().Should().Contain("D17").And.Contain(temp.Combine("target", "sub", "git"));
    }

    /// <summary>Windows only, reasoned and not observed on a Linux host: an 8.3 short name is another spelling of a path under the target that a text
    /// comparison cannot see; the handle the real-path call opens answers the long one.</summary>
    [Fact]
    public void An_8_3_short_name_spelling_of_the_target_is_refused()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("8.3 short names exist only on Windows");
            return;
        }

        using var temp = new TempFolder();
        var longFolder = temp.Folder("target-with-a-long-name");
        File.Copy(System.IO.Path.Combine(Environment.SystemDirectory, "cmd.exe"), System.IO.Path.Combine(longFolder, "x.exe"));
        var buffer = new char[260];
        var length = ShortNames.GetShortPathNameW(longFolder, buffer, buffer.Length);
        var shortFolder = new string(buffer, 0, length);
        if (string.Equals(shortFolder, longFolder, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Skip("8.3 names are disabled on this volume");
            return;
        }

        var roots = ExecutableRule.ForbiddenRoots(longFolder, temp.Combine("root"));
        var refused = ExecutableResolution.Resolve(System.IO.Path.Combine(shortFolder, "x.exe"), [], roots, RootFacts.IsExecutableFile, RootFacts.RealPathOf, windows: true);

        refused.Reason().Should().Contain("D17").And.Contain(longFolder);
    }
}

internal static class ShortNames
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "GetShortPathNameW")]
    internal static extern int GetShortPathNameW(string path, char[] buffer, int length);
}
