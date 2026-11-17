using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Tests.Leftovers;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>E6 S5 (D4) — the root of the scratch worktrees the gates build in. A benchmark run names its worktrees under the product's default root
/// (<c>&lt;temp&gt;/bench-scratch</c>) exactly as before; a TEST passes the folder it owns, so the worktree lies inside the folder that also holds its
/// repository and goes with it. The default is pinned from a CHILD of this executable whose temp root is an owned folder, so no test writes the product's shared folder.
/// <para>What this does NOT prove: that the product's other temp sites (the solver's patch file) are removed on every path — they stay the product's.</para></summary>
public sealed class ScratchRootSeamTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly GateCommand Build = new("git", ["--version"]);

    private static readonly GateCommand GrepFixed = new("git", ["grep", "-q", "FIXED", "--", "src/Policy.cs"]);

    /// <summary>Prints the worktree's top level on its way to exit 128 (<c>HEAD~999</c> names no commit): a gate command whose merged output holds the path it ran in. The error text is SHORT on purpose: a gate keeps only the last 300 characters of its output, and a long error would push the front of a long temp path out of them.</summary>
    private static readonly GateCommand PrintsItsTree = new("git", ["rev-parse", "--show-toplevel", "--verify", "HEAD~999"]);

    private const string ChildOut = "BENCH_E6S5_SCRATCH_CHILD_OUT";

    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_default_scratch_root_is_the_products_own()
    {
        ScratchWorktree.DefaultRoot.Should().Be(Path.Combine(TempDir.Root, "bench-scratch"));
    }

    [Fact]
    public void The_old_signature_still_makes_its_worktree_under_the_default_root()
    {
        using var report = TempDir.For("scratch-report");
        using var childTemp = TempDir.For("scratch-child");
        var env = new Dictionary<string, string?> { [ChildOut] = report.At("child.txt"), ["TMPDIR"] = childTemp.Path, ["TEMP"] = childTemp.Path, ["TMP"] = childTemp.Path, ["TESTSTODELETE_RUN"] = null };

        var child = SuiteChild.Run(["--no-ansi", "--filter-method", "*ChildAddsAWorktreeThroughTheOldSignature"], env, TimeSpan.FromMinutes(3));

        child.TimedOut.Should().BeFalse();
        child.ExitCode.Should().Be(0, child.StdOut + child.StdErr);
        var added = File.ReadAllText(report.At("child.txt")).Trim();
        var parent = Path.GetDirectoryName(added)!;
        parent.Should().Be(Path.Combine(childTemp.Path, "bench-scratch"), "the old signature names its worktree under the default root of ITS temp folder");
        Path.GetFileName(added).Should().MatchRegex("^[0-9a-f]{32}$");
        Directory.EnumerateFileSystemEntries(parent).Should().BeEmpty("the child removed its worktree");
    }

    /// <summary>The child of the proof above: adds a worktree through the 4-parameter signature, reports its path and removes it. Returns at once when the variable is unset, so every normal run is green.</summary>
    [Fact]
    public async Task ChildAddsAWorktreeThroughTheOldSignature()
    {
        if (Environment.GetEnvironmentVariable(ChildOut) is not { Length: > 0 } report)
        {
            return;
        }

        using var repo = new DatedGitRepo(Ct);
        var sha = await repo.InitAsync(("src/Policy.cs", "class Policy { }\n"), ("seed", "2026-05-01T12:00:00"));

        var added = (await ScratchWorktree.AddAsync(repo.Root, Sha(sha), TimeSpan.FromSeconds(30), Ct)).Ok();
        await File.WriteAllTextAsync(report, added + "\n", Ct);
        await ScratchWorktree.RemoveAsync(repo.Root, added);
    }

    [Fact]
    public async Task A_harvest_gate_worktree_lives_inside_the_tests_owned_folder()
    {
        var (baseSha, fixSha) = await FixturePairAsync();

        var report = (await HarvestGates.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), ["tests/PolicyTests.cs"], PrintsItsTree, GrepFixed, Timeout, Ct, _repo.ScratchRoot)).Ok();

        report.RedDetail.Should().Contain(_repo.ScratchRoot + Path.DirectorySeparatorChar, "the build command printed the worktree it ran in");
        await AssertWorktreeRemovedAsync("the gate");
    }

    [Fact]
    public async Task A_solution_worktree_lives_inside_the_tests_owned_folder()
    {
        var (baseSha, fixSha) = await FixturePairAsync();
        var solverDiff = await _repo.GitAsync("diff", baseSha, fixSha, "--", "src");

        var report = (await SolutionSignals.RunAsync(
            _repo.Root, Sha(baseSha), Sha(fixSha), solverDiff, ["src/Policy.cs"], ["tests/PolicyTests.cs"], Build, PrintsItsTree, Timeout, Ct, _repo.ScratchRoot)).Ok();

        report.Applies.Should().BeTrue();
        report.Builds.Should().BeTrue();
        report.TestsDetail.Should().Contain(_repo.ScratchRoot + Path.DirectorySeparatorChar, "the test command printed the worktree it ran in");
        await AssertWorktreeRemovedAsync("the signals");
    }

    /// <summary>The worktree is gone from git's registry (only the repository's own tree remains, as in `HarvestGatesTests`), and nothing is left on disk under the scratch root.</summary>
    private async Task AssertWorktreeRemovedAsync(string who)
    {
        (await _repo.GitAsync("worktree", "list", "--porcelain")).Should().NotContain(_repo.ScratchRoot, $"{who} removed its worktree on the way out");
        (await _repo.GitAsync("worktree", "list")).Trim().Split('\n').Should().HaveCount(1, "only the repository's own tree may remain");
        (!Directory.Exists(_repo.ScratchRoot) || !Directory.EnumerateFileSystemEntries(_repo.ScratchRoot).Any()).Should().BeTrue($"{who} left nothing under the scratch root");
    }

    private async Task<(string BaseSha, string FixSha)> FixturePairAsync()
    {
        var baseSha = await _repo.InitAsync(("src/Policy.cs", "class Policy { /* OLD */ }\n"), ("seed", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitManyAsync(
            [("src/Policy.cs", "class Policy { /* FIXED */ }\n"), ("tests/PolicyTests.cs", "class PolicyTests { /* expects FIXED */ }\n")],
            ("fix: the marker", "2026-08-11T09:30:00"));

        return (baseSha, fixSha);
    }

    private static CommitSha Sha(string value) => CommitSha.Parse(value).Ok();

    public void Dispose() => _repo.Dispose();
}
