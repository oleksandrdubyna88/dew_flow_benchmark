using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Experiment;
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
    public void The_git_top_of_a_path_spelled_through_a_link_is_asked_of_where_the_kernel_puts_it()
    {
        using var temp = new TempFolder();
        temp.RequireLinks();
        var checkout = temp.Folder("checkout");
        Directory.CreateDirectory(System.IO.Path.Combine(checkout, ".git"));
        Directory.CreateDirectory(System.IO.Path.Combine(checkout, "a"));
        var link = temp.Combine("L");
        File.CreateSymbolicLink(link, System.IO.Path.Combine(checkout, "a"));

        RootFacts.GitTopOf(System.IO.Path.Combine(link, "not", "yet", "made")).Should().Be(RootFacts.RealPathOf(checkout).Ok(), "the text has no checkout above it; the kernel's path does");
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

    private const string NoLinks = "symbolic links need a privilege on Windows; the Windows spellings are their own tests";

    [Fact]
    public void The_real_path_of_a_path_is_the_kernels_answer()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var real = temp.File("x", "real", "bin", "tool");
        File.CreateSymbolicLink(temp.Combine("one"), real);
        File.CreateSymbolicLink(temp.Combine("two"), "one");
        temp.Folder("deeper");
        File.CreateSymbolicLink(temp.Combine("deeper", "three"), System.IO.Path.Combine("..", "two"));
        Directory.CreateSymbolicLink(temp.Combine("shortcut"), temp.Combine("real"));
        var claude = temp.File("x", "target", "bin", "claude");
        temp.Folder("target", "x");
        temp.Folder("home", "c");
        Directory.CreateSymbolicLink(temp.Combine("home", "c", "sub"), temp.Combine("target", "x"));
        File.CreateSymbolicLink(temp.Combine("home", "c", "claude"), "sub/../bin/claude");
        temp.File("decoy", "home", "c", "bin", "claude");
        File.CreateSymbolicLink(temp.Combine("dangling"), "nowhere");
        File.CreateSymbolicLink(temp.Combine("a"), "b");
        File.CreateSymbolicLink(temp.Combine("b"), "a");

        RootFacts.RealPathOf(real).Ok().Should().Be(real, "a real file is its own real path");
        RootFacts.RealPathOf(temp.Combine("deeper", "three")).Ok().Should().Be(real, "a chain of absolute and relative links ends at the file");
        RootFacts.RealPathOf(temp.Combine("shortcut", "bin", "tool")).Ok().Should().Be(real, "a link in a folder of the path is followed too");
        RootFacts.RealPathOf(temp.Combine("home", "c", "claude")).Ok().Should().Be(claude, "'..' after a linked folder is applied to the folder the link points to, not to the text");
        RootFacts.RealPathOf(temp.Combine("dangling")).Reason().Should().Contain("No such file");
        RootFacts.RealPathOf(temp.Combine("a")).Reason().Should().Contain("Too many levels");
        RootFacts.RealPathOf(temp.Combine("absent")).Failed().Should().BeTrue("a path that does not exist has no real path");
    }

    [Fact]
    public void IsExecutableFile_answers_false_for_a_dangling_link_a_loop_and_a_folder_instead_of_throwing()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(NoLinks);
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var tool = temp.Executable("real", "tool");
        File.CreateSymbolicLink(temp.Combine("dangling"), "nowhere");
        File.CreateSymbolicLink(temp.Combine("a"), "b");
        File.CreateSymbolicLink(temp.Combine("b"), "a");
        File.CreateSymbolicLink(temp.Combine("tool-link"), tool);

        RootFacts.IsExecutableFile(temp.Combine("dangling")).Should().BeFalse();
        RootFacts.IsExecutableFile(temp.Combine("a")).Should().BeFalse();
        RootFacts.IsExecutableFile(temp.Path).Should().BeFalse();
        RootFacts.IsExecutableFile(temp.Combine("tool-link")).Should().BeTrue("a real executable file behind a link is executable");
    }

    [Fact]
    public void A_trust_path_that_is_a_symbolic_link_is_refused_naming_where_it_points()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("links, owners and modes are read on Linux here");
            return;
        }

        using var temp = new TempFolder();
        temp.RequireLinks();
        var other = temp.File("{}", "other", "experiment.json");
        File.SetUnixFileMode(other, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        temp.Folder("root");
        File.CreateSymbolicLink(temp.Combine("experiment.json"), other);
        Directory.CreateSymbolicLink(temp.Combine("rootlink"), temp.Combine("root"));
        File.CreateSymbolicLink(temp.Combine("dangling.json"), temp.Combine("nowhere"));

        foreach (var (link, target) in new[]
        {
            (temp.Combine("experiment.json"), other),
            (temp.Combine("rootlink"), temp.Combine("root")),
            (temp.Combine("rootlink") + System.IO.Path.DirectorySeparatorChar, temp.Combine("root")),
            (temp.Combine("rootlink") + "//", temp.Combine("root")),
            (temp.Combine("rootlink") + "/.", temp.Combine("root")),
            (temp.Combine("dangling.json"), temp.Combine("nowhere")),
        })
        {
            var reason = RootFacts.FileFacts(link).Reason();
            reason.Should().Contain("symbolic link", link).And.Contain(target, link).And.Contain("D17", link);
        }

        RootFacts.FileFacts(other).Ok().Path.Should().Be(other, "the file itself is judged");
    }

    [Fact]
    public void Owner_and_mode_come_from_one_file_even_when_a_dot_dot_climbs_out_of_a_linked_folder()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("links, owners and modes are read on Linux here");
            return;
        }

        // The whole shape lives inside the temporary folder, so it means the same at any TMPDIR depth. 'W/dl2/../x/f.json' is W/x/f.json to .NET
        // (the '..' cancels 'dl2' as text) and W/K/a/b/c/x/f.json to the kernel (the '..' climbs from the folder 'dl2' points to).
        using var temp = new TempFolder();
        temp.RequireLinks();
        var decoy = temp.File("decoy", "W", "K", "a", "b", "c", "x", "f.json");
        File.SetUnixFileMode(decoy, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var textual = temp.File("textual", "W", "x", "f.json");
        File.SetUnixFileMode(textual, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        Directory.CreateSymbolicLink(temp.Combine("W", "dl2"), temp.Combine("W", "K", "a", "b", "c", "e"));
        temp.Folder("W", "K", "a", "b", "c", "e");
        var climbing = temp.Combine("W", "dl2", "..", "x", "f.json");

        var facts = RootFacts.FileFacts(climbing).Ok();

        facts.Path.Should().Be(textual, "the facts are of the one normalised path");
        facts.Mode.Should().Be(File.GetUnixFileMode(textual), "the mode is of the textual file (644), not of the decoy the kernel's reading of '..' reaches (600)");
        File.ReadAllText(facts.Path).Should().Be("textual");
    }

    [Fact]
    public void A_nul_in_a_trust_path_is_a_refusal_never_a_throw()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("owners and modes are read on Linux here");
            return;
        }

        var outcome = RootFacts.FileFacts("/tmp/experiment\0.json");

        outcome.Reason().Should().Contain("NUL");
    }

    [Theory]
    [InlineData(0x81a4, FileKind.Regular, "644")]
    [InlineData(0xa1ff, FileKind.Link, "777")]
    [InlineData(0x41ed, FileKind.Directory, "755")]
    [InlineData(0x1180, FileKind.Fifo, "600")]
    [InlineData(0xc1ff, FileKind.Socket, "777")]
    [InlineData(0x21b6, FileKind.CharacterDevice, "666")]
    [InlineData(0x61b0, FileKind.BlockDevice, "660")]
    [InlineData(0x0000, FileKind.Unknown, "000")]
    [InlineData(0x8fff, FileKind.Regular, "7777")]
    public void The_type_bits_of_a_raw_mode_name_the_kind_and_the_permission_bits_the_mode(int raw, FileKind kind, string octalMode)
    {
        var mode = new RootFacts.RawMode(raw);

        mode.Kind.Should().Be(kind, "the kind is decided on the type bits, never on a word");
        ((int)mode.Permissions).Should().Be(Convert.ToInt32(octalMode, 8), "the type bits are not part of the mode");
    }

    [Fact]
    public async Task A_link_made_and_removed_again_moves_the_change_time_of_a_real_file_though_its_inode_stays()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("change times are read with GNU stat here");
            return;
        }
        using var temp = new TempFolder();
        var file = temp.File("{}", "experiment.json");
        var before = RootFacts.FileFacts(file).Ok();
        var keep = temp.Combine(".keep");
        await ExperimentRoot.HardLinkAsync(file, keep);
        File.Delete(keep);
        var after = RootFacts.FileFacts(file).Ok();

        after.Identity.Inode.Should().Be(before.Identity.Inode, "the swap and swap back leaves the same inode under the name");
        RootTrust.Changes(before, after).Should().Equal("ctime");
    }

    [Fact]
    public void A_real_files_identity_is_the_inode_of_the_file_and_changes_when_another_file_takes_its_name()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("inodes are read with GNU stat here");
            return;
        }
        using var temp = new TempFolder();
        var file = temp.File("{}", "experiment.json");
        var before = RootFacts.FileFacts(file).Ok();
        File.Move(temp.File("{}", "other.json"), file, overwrite: true);
        var after = RootFacts.FileFacts(file).Ok();

        before.Identity.Kind.Should().Be(FileKind.Regular);
        before.Identity.Inode.Should().BeGreaterThan(0UL);
        RootTrust.Changes(before, after).Should().Contain("inode");
    }

    [Fact]
    public void A_file_of_another_kind_than_the_one_wanted_is_refused_naming_both()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file kinds are read with GNU stat here");
            return;
        }
        using var temp = new TempFolder();
        var file = temp.File("{}", "experiment.json");

        var asFolder = RootFacts.ReadFacts(file, FileKind.Directory);
        var asFile = RootFacts.ReadFacts(file, FileKind.Regular);
        var folderAsFile = RootFacts.ReadFacts(temp.Path, FileKind.Regular);

        asFolder.Should().BeOfType<FactsRead.Refused>().Which.Reason.Should().Contain("is a regular file, not a directory").And.Contain(file);
        folderAsFile.Should().BeOfType<FactsRead.Refused>().Which.Reason.Should().Contain("is a directory, not a regular file");
        asFile.Should().BeOfType<FactsRead.Read>();
    }

    [Fact]
    public void A_files_facts_are_its_path_its_owner_and_its_mode()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes and owners are POSIX; /proc names the expected owner");
            return;
        }
        using var temp = new TempFolder();
        var file = temp.File("{}", "experiment.json");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        var uid = RootFacts.CurrentUid();

        var facts = RootFacts.FileFacts(file).Ok();

        facts.Path.Should().Be(file);
        facts.OwnerUid.Should().Be(uid);
        facts.Mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
    }

    [Fact]
    public void A_folders_facts_are_read_the_same_way()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes and owners are POSIX");
            return;
        }
        using var temp = new TempFolder();
        File.SetUnixFileMode(temp.Path, (UnixFileMode)Convert.ToInt32("750", 8));

        var facts = RootFacts.FileFacts(temp.Path).Ok();

        facts.Mode.Should().Be((UnixFileMode)Convert.ToInt32("750", 8));
    }

    [Fact]
    public void A_file_that_does_not_exist_has_no_facts_and_the_refusal_names_it()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("file modes and owners are POSIX");
            return;
        }
        using var temp = new TempFolder();

        var outcome = RootFacts.FileFacts(temp.Combine("absent.json"));

        outcome.Reason().Should().Contain("absent.json").And.Contain("No such file", "the reason carries the operating system's own words");
    }
}
