using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Leftovers.SweepPlants;

namespace Bench.Tests.Leftovers;

/// <summary>
/// Case 15 of the leftovers sweep (E6 S2, row E6-c): a killed run's git-repository folder, with a linked worktree INSIDE it, is collected whole.
/// The sweep reads only a candidate's OWN <c>.git</c>, and only when it is a regular file (<c>LeftoversSweep.HasGitFile</c>), so the <c>other clone</c> rule does not fire for an
/// owned folder that HOLDS a clone and its linked worktree: the folder is quarantined and removed by <c>Removal.Quietly</c> (<c>Directory.Delete</c>, recursive). These tests turn that reading into a run.
/// <para>Every scenario plants in a PRIVATE root made by the class's <see cref="OwnedScope"/>, never in the machine's temp root, and calls <see cref="LeftoversSweep.Run"/> with the REAL
/// kernel-owner key (<see cref="Scope.IsMine"/>), not the <c>_ =&gt; true</c> seam. Every <c>git</c> call is an argument array through <see cref="ProcessRunner"/>, never a shell string.</para>
/// <para><b>Windows.</b> All three skip there: git marks its object files read-only and the copied <c>Removal</c> does not clear that flag on Windows, so neither the sweep nor the scope could
/// remove the folder (by reading; the template-note N1 of the E6 S2 brief). Benchmark's CI has no Windows leg.</para>
/// </summary>
public sealed class LeftoversSweepCase15Tests : IDisposable
{
    private const string WindowsReason =
        "git marks its object files read-only, and the copied Removal (Directory.Delete recursive) does not clear that flag on Windows, so neither the sweep nor the scope could remove the folder " +
        "(by reading; coai's TempDir note measured it); benchmark's CI has no Windows leg";

    private readonly OwnedScope _owned = DarwinLayout.FreshOwnedScope();

    public void Dispose() => _owned.Dispose();

    [Fact]
    public async Task Case15_ADeadClaimedGitRepoFolder_WithALinkedWorktreeInside_IsRemovedWhole()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsReason);
        var root = _owned.Dir("case15a-root");
        var dead = Dead();
        var dir = await PlantGitRepoFolder(root, Name("gitrepo"), Rec(dead));

        var result = LeftoversSweep.Run(Options(root, mine: Scope.IsMine));

        result.Removed.Should().Equal(dir);
        Directory.Exists(dir).Should().BeFalse("the whole folder, the clone and its linked worktree with it, is removed");
        File.Exists(dir + ".owner").Should().BeFalse("and its claim");
        Directory.GetFileSystemEntries(root).Should().BeEmpty("no quarantine is left in the root");
        result.Errors.Should().BeEmpty();
        result.Preserved.Should().NotContain(p => p.Path == dir, "the folder was not kept as `other clone`: only its own `.git` FILE decides that");
        UnmatchablePid.AssertNoSuchProcess(dead);
    }

    [Fact]
    public async Task Case15_ALiveClaimedGitRepoFolder_IsKept()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsReason);
        var root = _owned.Dir("case15b-root");
        var dir = await PlantGitRepoFolder(root, Name("gitrepo"), Rec(Environment.ProcessId));

        var result = LeftoversSweep.Run(Options(root, mine: Scope.IsMine));

        ReasonOf(result, dir).Should().Be("live");
        result.Removed.Should().BeEmpty();
        Directory.Exists(Path.Combine(dir, "repo", ".git")).Should().BeTrue();
        Directory.Exists(Path.Combine(dir, "scratch")).Should().BeTrue();
        Directory.EnumerateFiles(Path.Combine(dir, "scratch"), ".git", SearchOption.AllDirectories).Should().ContainSingle("the linked worktree is still there");
        File.Exists(dir + ".owner").Should().BeTrue();
    }

    [Fact]
    public async Task Case15_AFolderWhoseOwnGitIsAFile_IsKeptAsOtherClone()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), WindowsReason);
        var root = _owned.Dir("case15c-root");
        var home = _owned.Dir("case15c-home");
        var repo = await MakeRepo(home);
        var dead = Dead();
        var worktree = Path.Combine(root, Name("wt"));
        // testsToDelete-exempt: raw-worktree — case 15c makes this linked worktree at the top of its own private sweep root, beside a repo planted in its own owned scope folder
        await Git(repo, "worktree", "add", "--detach", worktree, "HEAD");
        File.Exists(Path.Combine(worktree, ".git")).Should().BeTrue("a linked worktree's own `.git` is a regular FILE: the premise of the `other clone` rule");
        File.WriteAllText(worktree + ".owner", Rec(dead));
        AgeFolder(worktree);
        File.SetLastWriteTimeUtc(worktree + ".owner", DateTime.UtcNow - Old);

        var result = LeftoversSweep.Run(Options(root, mine: Scope.IsMine));

        ReasonOf(result, worktree).Should().Be("other clone");
        result.Removed.Should().BeEmpty();
        Directory.Exists(worktree).Should().BeTrue("a linked worktree is git's to remove, never the sweep's");
        File.Exists(Path.Combine(worktree, ".git")).Should().BeTrue();
        File.Exists(worktree + ".owner").Should().BeTrue();
        UnmatchablePid.AssertNoSuchProcess(dead);
    }

    /// <summary>
    /// A claimed, aged folder that holds a clone (<c>repo</c>, with a real commit, so real read-only object files exist) and a linked worktree of it (<c>scratch/&lt;id&gt;</c>, INSIDE the folder:
    /// no prefix and no lock of its own, rule §1). Aged LAST: the git commands made the folder new again.
    /// </summary>
    private static async Task<string> PlantGitRepoFolder(string root, string name, string claim)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        var repo = await MakeRepo(dir);
        var id = "wt1";
        var scratch = Path.Combine(dir, "scratch", id);
        // testsToDelete-exempt: raw-worktree — case 15 makes this linked worktree inside the owned gitrepo folder it plants, in a private root of its own scope
        await Git(repo, "worktree", "add", "--detach", scratch, "HEAD");
        File.WriteAllText(dir + ".owner", claim);
        AgeFolder(dir);
        File.SetLastWriteTimeUtc(dir + ".owner", DateTime.UtcNow - Old);

        File.Exists(Path.Combine(scratch, ".git")).Should().BeTrue("the linked worktree's own `.git` is a FILE");
        Directory.Exists(Path.Combine(repo, ".git", "worktrees")).Should().BeTrue("the clone knows its linked worktree");
        (await Git(repo, "worktree", "list", "--porcelain")).Should().Contain(Path.Combine("scratch", id));
        return dir;
    }

    /// <summary>A repository <c>repo</c> in <paramref name="parent"/> with one real commit.</summary>
    private static async Task<string> MakeRepo(string parent)
    {
        var repo = Path.Combine(parent, "repo");
        await Git(parent, "-c", "init.defaultBranch=main", "init", "-q", "repo");
        await File.WriteAllTextAsync(Path.Combine(repo, "file.txt"), "one\n");
        await Git(repo, "add", "file.txt");
        await Git(repo, "-c", "user.name=bench-tests", "-c", "user.email=bench-tests@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "one");
        return repo;
    }

    private static async Task<string> Git(string cwd, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync("git", arguments, cwd, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        return attempt is ProcessAttempt.Completed { Result.Ok: true } completed
            ? completed.Result.StandardOutput
            : throw new InvalidOperationException($"git {arguments[0]} failed in case 15's plant: {attempt.Describe}");
    }
}
