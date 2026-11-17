using System.Diagnostics;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Tests.Leftovers;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>E6 S5 (rule §4) — a test that made a <see cref="DatedGitRepo"/> and a scratch worktree through it and was KILLED leaves one claimed folder, and the next sweep collects it whole:
/// the repository AND the worktree lie inside it, so nothing outlives the folder. The proof kills a CHILD of this executable whose temp root is an owned folder, as the S4 kill proof does.
/// <para>What this does NOT prove: Windows (SIGKILL), and a worktree made through a seam that is not given <see cref="DatedGitRepo.ScratchRoot"/> — that one lands in the product's shared root.</para></summary>
public sealed class DatedGitRepoKillTests
{
    private const string ChildOut = "BENCH_E6S5_DATED_KILL_CHILD_OUT";

    [Fact(Timeout = 180_000)]
    public async Task A_killed_DatedGitRepo_user_is_collected_by_the_next_sweep()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the proof kills the child with SIGKILL: there is no such signal on Windows");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var report = TempDir.For("dated-kill-report");
        using var childTemp = TempDir.For("dated-kill-temp");
        var env = new Dictionary<string, string?> { [ChildOut] = report.At("child.txt"), ["TMPDIR"] = childTemp.Path, ["TEMP"] = childTemp.Path, ["TMP"] = childTemp.Path, ["TESTSTODELETE_RUN"] = null };
        using var child = System.Diagnostics.Process.Start(SuiteChild.StartInfoFor(Environment.ProcessPath!, ["--no-ansi", "--filter-method", "*KillChild_makes_a_DatedGitRepo_with_a_worktree_and_waits"], env))!;
        var drained = Task.WhenAll(child.StandardOutput.ReadToEndAsync(ct), child.StandardError.ReadToEndAsync(ct));
        try
        {
            var reported = (await ModelArms.TempFolderTests.WaitForChildFolderAsync(report.At("child.txt"), child)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var (repository, worktree) = (reported[0], reported[1]);
            var claimed = Directory.EnumerateDirectories(childTemp.Path).Where(d => Owned.FullName().IsMatch(Path.GetFileName(d)) && File.Exists(d + ".owner")).ToList();
            claimed.Should().ContainSingle("the child's repository and worktree live in ONE claimed folder of its temp root");
            var folder = claimed[0];
            var claim = folder + ".owner";
            Directory.Exists(repository).Should().BeTrue();
            Directory.Exists(worktree).Should().BeTrue();
            Path.GetFullPath(repository).Should().StartWith(folder + Path.DirectorySeparatorChar, "the repository is inside the claimed folder");
            Path.GetFullPath(worktree).Should().StartWith(folder + Path.DirectorySeparatorChar, "the worktree is inside the claimed folder");

            child.Kill();
            await child.WaitForExitAsync(ct);
            child.ExitCode.Should().Be(137, "SIGKILL: the child had no chance to dispose anything");

            var result = LeftoversSweep.Run(new SweepOptions(childTemp.Path, DateTimeOffset.UtcNow, TimeSpan.Zero, Liveness.ProbeProcess, Mine: path => (path == folder || path == claim) && Scope.IsMine(path)));

            result.Removed.Should().Equal([folder], "the sweep removed the dead child's folder, and nothing else");
            Directory.Exists(folder).Should().BeFalse();
            File.Exists(claim).Should().BeFalse();
            Directory.Exists(repository).Should().BeFalse("the repository went with the folder");
            Directory.Exists(worktree).Should().BeFalse("the worktree went with the folder");
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }

            try
            {
                await drained.WaitAsync(TimeSpan.FromSeconds(10), ct);
            }
            catch (TimeoutException)
            {
                // The killed child's pipes are not read to their end; nothing depends on them.
            }
        }
    }

    /// <summary>The child of the kill proof: makes a <see cref="DatedGitRepo"/> and a worktree under its <see cref="DatedGitRepo.ScratchRoot"/>, reports both paths (a temp file, then a move) and waits to be killed. Returns at once when the variable is unset, so every normal run is green.</summary>
    [Fact(Timeout = 180_000)]
    public async Task KillChild_makes_a_DatedGitRepo_with_a_worktree_and_waits()
    {
        if (Environment.GetEnvironmentVariable(ChildOut) is not { Length: > 0 } report)
        {
            await Task.CompletedTask;
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var repo = new DatedGitRepo(ct);
        var sha = await repo.InitAsync(("src/Policy.cs", "class Policy { }\n"), ("seed", "2026-05-01T12:00:00"));
        var worktree = (await ScratchWorktree.AddAsync(repo.Root, CommitSha.Parse(sha).Ok(), TimeSpan.FromSeconds(30), ct, repo.ScratchRoot)).Ok();
        await File.WriteAllTextAsync(report + ".part", repo.Root + "\n" + worktree + "\n", ct);
        File.Move(report + ".part", report);
        await Task.Delay(TimeSpan.FromMinutes(2), ct);
    }
}
