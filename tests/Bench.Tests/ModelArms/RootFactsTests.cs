using FluentAssertions;
using ModelArms.Adapters;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The console's reads of the world that the pure deciders of the root consume: where the enclosing git checkout is, where this
/// repository is, whether a file may be run, where a symbolic link really ends, who owns a file.
/// <para>
/// What this does NOT prove: ownership on a file system that does not report a Unix owner (a network share mapped to one user), or link
/// resolution on Windows, where creating a link needs a privilege the test host may lack; those cases skip, saying why.
/// </para></summary>
public sealed class RootFactsTests
{
    private const UnixFileMode Executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static bool AncestorHolds(string folder, string name)
    {
        for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, name)) || Directory.Exists(System.IO.Path.Combine(directory.FullName, name)))
            {
                return true;
            }
        }

        return false;
    }

    private static void CanMakeLinks(string at)
    {
        try
        {
            File.CreateSymbolicLink(System.IO.Path.Combine(at, "probe-link"), at);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"symbolic links cannot be made here: {ex.Message}");
        }
    }

    [Fact]
    public void The_git_top_of_a_path_is_its_nearest_ancestor_holding_a_git_folder_or_file_even_when_the_path_does_not_exist_yet()
    {
        using var temp = new TempFolder();
        var checkout = temp.Folder("checkout");
        Directory.CreateDirectory(System.IO.Path.Combine(checkout, ".git"));
        var worktree = temp.Folder("worktree");
        File.WriteAllText(System.IO.Path.Combine(worktree, ".git"), "gitdir: /elsewhere");

        RootFacts.GitTopOf(System.IO.Path.Combine(checkout, "not", "yet", "made")).Should().Be(checkout);
        RootFacts.GitTopOf(checkout).Should().Be(checkout, "the path itself counts");
        RootFacts.GitTopOf(System.IO.Path.Combine(worktree, "sub")).Should().Be(worktree, "a worktree's .git is a file");
    }

    [Fact]
    public void A_path_with_no_git_ancestor_has_no_git_top()
    {
        using var temp = new TempFolder();
        Assert.SkipWhen(AncestorHolds(temp.Path, ".git"), "this machine's temporary folder sits inside a git checkout");

        RootFacts.GitTopOf(temp.Combine("a", "b")).Should().BeEmpty();
    }

    [Fact]
    public void This_repository_is_the_nearest_ancestor_holding_the_solution_file()
    {
        using var temp = new TempFolder();
        var repository = temp.Folder("repo");
        File.WriteAllText(System.IO.Path.Combine(repository, "dew_flow_benchmark.slnx"), "<Solution />");
        var bin = temp.Folder("repo", "tools", "model-arms", "bin");

        RootFacts.ThisRepository(bin).Should().Be(repository);
        RootFacts.ThisRepository(AppContext.BaseDirectory).Should().Be(Cli.Repository.Root, "the test binaries sit inside this repository");
    }

    [Fact]
    public void A_folder_outside_any_repository_has_no_repository()
    {
        using var temp = new TempFolder();
        Assert.SkipWhen(AncestorHolds(temp.Path, "dew_flow_benchmark.slnx"), "this machine's temporary folder sits inside the repository");

        RootFacts.ThisRepository(temp.Path).Should().BeEmpty();
    }

    [Fact]
    public void An_executable_file_is_executable_and_a_plain_file_a_folder_or_nothing_is_not()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the executable bit is POSIX");
            return;
        }
        using var temp = new TempFolder();
        var run = temp.File("#!/bin/sh\n", "run");
        File.SetUnixFileMode(run, Executable);
        var plain = temp.File("data", "plain");
        File.SetUnixFileMode(plain, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        RootFacts.IsExecutableFile(run).Should().BeTrue();
        RootFacts.IsExecutableFile(plain).Should().BeFalse("no execute bit");
        RootFacts.IsExecutableFile(temp.Path).Should().BeFalse("a folder is not a program");
        RootFacts.IsExecutableFile(temp.Combine("absent")).Should().BeFalse();
    }

    [Fact]
    public void A_path_that_is_no_link_is_its_own_final_target()
    {
        using var temp = new TempFolder();
        var file = temp.File("x", "a", "b.txt");

        RootFacts.FinalTargetOf(file).Ok().Should().Be(file);
        RootFacts.FinalTargetOf(temp.Combine("not", "there")).Ok().Should().Be(temp.Combine("not", "there"));
    }

    [Fact]
    public void A_chain_of_links_ends_at_the_real_file_whether_the_links_are_absolute_or_relative()
    {
        using var temp = new TempFolder();
        CanMakeLinks(temp.Folder("probe"));
        var real = temp.File("x", "real", "tool");
        File.CreateSymbolicLink(temp.Combine("one"), real);
        File.CreateSymbolicLink(temp.Combine("two"), "one");
        Directory.CreateDirectory(temp.Combine("deeper"));
        File.CreateSymbolicLink(temp.Combine("deeper", "three"), System.IO.Path.Combine("..", "two"));

        RootFacts.FinalTargetOf(temp.Combine("deeper", "three")).Ok().Should().Be(real);
    }

    [Fact]
    public void A_link_in_a_folder_of_the_path_is_followed_too()
    {
        using var temp = new TempFolder();
        CanMakeLinks(temp.Folder("probe"));
        var real = temp.File("x", "real", "bin", "tool");
        File.CreateSymbolicLink(temp.Combine("shortcut"), temp.Combine("real"));

        RootFacts.FinalTargetOf(temp.Combine("shortcut", "bin", "tool")).Ok().Should().Be(real, "a PATH entry that is a link to a folder reaches the same file");
    }

    [Fact]
    public void A_link_that_loops_is_a_refusal_not_a_hang_or_a_pass()
    {
        using var temp = new TempFolder();
        CanMakeLinks(temp.Folder("probe"));
        File.CreateSymbolicLink(temp.Combine("a"), temp.Combine("b"));
        File.CreateSymbolicLink(temp.Combine("b"), temp.Combine("a"));

        RootFacts.FinalTargetOf(temp.Combine("a")).Failed().Should().BeTrue();
    }

    [Fact]
    public async Task The_current_uid_is_the_uid_this_process_runs_as()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("the independent source of the uid, /proc, is Linux-only");
            return;
        }
        var expected = File.ReadLines("/proc/self/status").First(l => l.StartsWith("Uid:", StringComparison.Ordinal))
            .Split('\t', StringSplitOptions.RemoveEmptyEntries)[1];

        (await RootFacts.CurrentUidAsync(CancellationToken.None)).Ok().Should().Be(int.Parse(expected));
    }

    [Fact]
    public async Task A_files_facts_are_its_path_its_owner_and_its_mode()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes and owners are POSIX; /proc names the expected owner");
            return;
        }
        using var temp = new TempFolder();
        var file = temp.File("{}", "experiment.json");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        var uid = (await RootFacts.CurrentUidAsync(CancellationToken.None)).Ok();

        var facts = (await RootFacts.FileFactsAsync(file, CancellationToken.None)).Ok();

        facts.Path.Should().Be(file);
        facts.OwnerUid.Should().Be(uid);
        facts.Mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
    }

    [Fact]
    public async Task A_folders_facts_are_read_the_same_way()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes and owners are POSIX");
            return;
        }
        using var temp = new TempFolder();
        File.SetUnixFileMode(temp.Path, (UnixFileMode)Convert.ToInt32("750", 8));

        var facts = (await RootFacts.FileFactsAsync(temp.Path, CancellationToken.None)).Ok();

        facts.Mode.Should().Be((UnixFileMode)Convert.ToInt32("750", 8));
    }

    [Fact]
    public async Task A_file_that_does_not_exist_has_no_facts_and_the_refusal_names_it()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes and owners are POSIX");
            return;
        }
        using var temp = new TempFolder();

        var outcome = await RootFacts.FileFactsAsync(temp.Combine("absent.json"), CancellationToken.None);

        outcome.Reason().Should().Contain("absent.json");
    }
}
