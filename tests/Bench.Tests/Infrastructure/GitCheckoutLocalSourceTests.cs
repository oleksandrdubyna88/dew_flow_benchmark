using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The mirror of a LOCAL working clone (D10: a private task is read from the operator's clone through <c>file://</c>).
/// <para>
/// A working clone keeps its default branch's history in <c>refs/remotes/origin/*</c> — its local branch is usually behind — and
/// <c>clone --bare</c> / <c>fetch origin</c> copy only <c>refs/heads</c> and tags. Seen live 2026-10-13: the selection stopped on
/// "commit 3f51… is not in file:////…". The fixture reproduces exactly that shape: commit X exists in the source ONLY behind
/// <c>refs/remotes/origin/&lt;branch&gt;</c>.
/// </para></summary>
[Collection("git")]
public sealed class GitCheckoutLocalSourceTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DatedGitRepo _upstream = new(TestContext.Current.CancellationToken);
    private readonly DatedGitRepo _clone = new(TestContext.Current.CancellationToken);
    private readonly string _cache = Path.Combine(Path.GetTempPath(), $"bench-cache-{Guid.NewGuid():N}");

    [Fact]
    public async Task A_commit_only_on_the_local_clones_remote_tracking_branch_is_mirrored()
    {
        var (_, onlyTracked) = await LocalCloneBehindItsRemoteAsync();
        var provider = Provider();

        var mirrored = await provider.EnsureMirrorAsync(Target(onlyTracked), Ct);

        mirrored.Failed().Should().BeFalse(
            "a commit reachable from the source's refs/remotes/* is in the operator's clone; the mirror must carry it ({0})",
            mirrored.Failed() ? mirrored.Reason() : "ok");

        var again = await provider.EnsureMirrorAsync(Target(onlyTracked), Ct);

        again.Failed().Should().BeFalse("the refresh path keeps the remote-tracking refs as well");
    }

    [Fact]
    public async Task A_refresh_brings_a_commit_that_reached_the_local_clones_remote_tracking_branch_later()
    {
        var head = (await _upstream.InitAsync(("content.txt", "one"), ("one", "2026-01-01T00:00:00Z"))).Trim();
        await CloneUpstreamAsync();
        var provider = Provider();
        (await provider.EnsureMirrorAsync(Target(head), Ct)).Failed().Should().BeFalse("the mirror exists before the source moves");

        var later = await AdvanceUpstreamAndFetchAsync();
        var refreshed = await provider.EnsureMirrorAsync(Target(later), Ct);

        refreshed.Failed().Should().BeFalse(
            "a refresh fetches the source's refs/remotes/* too ({0})", refreshed.Failed() ? refreshed.Reason() : "ok");
    }

    [Fact]
    public async Task The_local_clone_is_only_read()
    {
        var (localHead, onlyTracked) = await LocalCloneBehindItsRemoteAsync();
        var refsBefore = await _clone.GitAsync("for-each-ref", "--format=%(refname) %(objectname)");

        await Provider().EnsureMirrorAsync(Target(onlyTracked), Ct);

        (await _clone.GitAsync("for-each-ref", "--format=%(refname) %(objectname)")).Should().Be(refsBefore);
        (await _clone.GitAsync("rev-parse", "HEAD")).Trim().Should().Be(localHead);
        (await _clone.GitAsync("status", "--porcelain")).Trim().Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://github.com/owner/name.git")]
    [InlineData("ssh://git@github.com/owner/name.git")]
    [InlineData("git@github.com:owner/name.git")]
    public void A_network_source_keeps_the_old_fetch_with_no_remote_tracking_refspec(string repository)
    {
        GitCheckoutProvider.IsLocalSource(repository).Should().BeFalse();
        GitCheckoutProvider.SourceRemoteFetches(repository).Should().BeEmpty("https/ssh sources behave exactly as before");
    }

    [Theory]
    [InlineData("file:///C:/Users/u/git/repo")]
    [InlineData("file:////wsl.localhost/Ubuntu/home/u/git/repo")]
    [InlineData("FILE:///home/u/git/repo")]
    public void A_local_source_also_fetches_its_remote_tracking_refs_into_a_separate_namespace(string repository)
    {
        GitCheckoutProvider.IsLocalSource(repository).Should().BeTrue();
        GitCheckoutProvider.SourceRemoteFetches(repository).Should().ContainSingle()
            .Which.Should().Equal("fetch", "--quiet", "--prune", "origin", "+refs/remotes/*:refs/source-remotes/*");
    }

    [Fact]
    public void A_plain_local_path_is_a_local_source() =>
        GitCheckoutProvider.IsLocalSource(Path.GetTempPath()).Should().BeTrue();

    public void Dispose()
    {
        _upstream.Dispose();
        _clone.Dispose();
        TempGitRepo.DeleteTree(_cache);
    }

    /// <summary>A → B (clone), then a new commit X in A and a <c>fetch</c> in B: B's local branch stays at the old head while
    /// <c>refs/remotes/origin/&lt;branch&gt;</c> carries X.</summary>
    private async Task<(string LocalHead, string OnlyTracked)> LocalCloneBehindItsRemoteAsync()
    {
        await _upstream.InitAsync(("content.txt", "one"), ("one", "2026-01-01T00:00:00Z"));
        await CloneUpstreamAsync();
        var onlyTracked = await AdvanceUpstreamAndFetchAsync();

        return ((await _clone.GitAsync("rev-parse", "HEAD")).Trim(), onlyTracked);
    }

    private async Task CloneUpstreamAsync()
    {
        Directory.CreateDirectory(_clone.Root);
        await _clone.GitAsync("clone", "--quiet", _upstream.Url, ".");
    }

    private async Task<string> AdvanceUpstreamAndFetchAsync()
    {
        var x = await _upstream.CommitAsync(("content.txt", Guid.NewGuid().ToString("N")), ("advance", "2026-01-02T00:00:00Z"));
        await _clone.GitAsync("fetch", "--quiet", "origin");

        (await _clone.GitAsync("rev-parse", "HEAD")).Trim().Should().NotBe(x, "the fixture's point: the local branch is behind");
        (await _clone.GitAsync("for-each-ref", "--contains", x, "--format=%(refname)", "refs/heads"))
            .Trim().Should().BeEmpty("X must not be reachable from any local branch of the source");

        return x;
    }

    private GitCheckoutProvider Provider() =>
        new(CheckoutCacheOptions.Under(_cache) with { Timeout = TimeSpan.FromMinutes(2) },
            NullLogger<GitCheckoutProvider>.Instance);

    private MeasurementTarget Target(string sha) =>
        MeasurementTarget.At(RepoUrl.Parse(_clone.Url).Ok(), CommitSha.Parse(sha).Ok());
}
