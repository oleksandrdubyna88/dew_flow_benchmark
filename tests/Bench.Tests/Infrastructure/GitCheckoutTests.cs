using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The checkout cache, tested against a real git repository.
/// <para>
/// The load-bearing test here is <see cref="The_source_repository_working_tree_is_never_touched"/>. The
/// component this replaces ran <c>git checkout</c> in place on a configured repository path, which for a
/// benchmark means rewriting whatever a developer had open to a commit they never asked for. That is not
/// a property you assert in a doc comment and hope for.
/// </para></summary>
[Collection("git")]
public sealed class GitCheckoutTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bench-cache-{Guid.NewGuid():N}");
    private TempGitRepo _source = null!;

    public async ValueTask InitializeAsync() => _source = await TempGitRepo.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _source.Dispose();
        TempGitRepo.DeleteTree(_root);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_commit_is_checked_out_with_the_content_it_had_at_that_commit()
    {
        var provider = Provider();

        var first = (await provider.EnsureAsync(Target(_source.FirstCommit), TestContext.Current.CancellationToken)).Ok();
        var second = (await provider.EnsureAsync(Target(_source.SecondCommit), TestContext.Current.CancellationToken)).Ok();

        (await File.ReadAllTextAsync(Path.Combine(first, "content.txt"), TestContext.Current.CancellationToken))
            .Trim().Should().Be("one");
        (await File.ReadAllTextAsync(Path.Combine(second, "content.txt"), TestContext.Current.CancellationToken))
            .Trim().Should().Be("two");
        first.Should().NotBe(second, "two commits are two trees and both stay available");
    }

    [Fact]
    public async Task The_source_repository_working_tree_is_never_touched()
    {
        var headBefore = await _source.HeadAsync();
        var contentBefore = await File.ReadAllTextAsync(
            Path.Combine(_source.Path, "content.txt"), TestContext.Current.CancellationToken);

        await Provider().EnsureAsync(Target(_source.FirstCommit), TestContext.Current.CancellationToken);

        (await _source.HeadAsync()).Should().Be(headBefore, "checking out an OLD commit must not move the source's HEAD");
        (await File.ReadAllTextAsync(Path.Combine(_source.Path, "content.txt"), TestContext.Current.CancellationToken))
            .Should().Be(contentBefore, "the developer's file is still the developer's file");
        (await _source.StatusAsync()).Should().BeEmpty("the source tree is left clean");
    }

    [Fact]
    public async Task Everything_it_creates_lives_under_the_cache_root()
    {
        var path = (await Provider().EnsureAsync(Target(_source.FirstCommit), TestContext.Current.CancellationToken)).Ok();

        Path.GetFullPath(path).Should().StartWith(Path.GetFullPath(_root));
        Directory.Exists(Path.Combine(_root, "bare")).Should().BeTrue("one bare mirror per repository url");
        Directory.Exists(Path.Combine(_root, "worktrees")).Should().BeTrue("one worktree per commit");
    }

    [Fact]
    public async Task Asking_twice_for_the_same_commit_returns_the_same_tree()
    {
        var provider = Provider();

        var first = (await provider.EnsureAsync(Target(_source.FirstCommit), TestContext.Current.CancellationToken)).Ok();
        var again = (await provider.EnsureAsync(Target(_source.FirstCommit), TestContext.Current.CancellationToken)).Ok();

        again.Should().Be(first, "a commit is immutable, so an existing worktree for it is already correct");
    }

    [Fact]
    public async Task Concurrent_requests_for_one_repository_do_not_race()
    {
        var provider = Provider();
        var targets = new[] { _source.FirstCommit, _source.SecondCommit, _source.FirstCommit, _source.SecondCommit };

        var results = await Task.WhenAll(targets.Select(sha =>
            provider.EnsureAsync(Target(sha), TestContext.Current.CancellationToken)));

        results.Should().AllSatisfy(r => r.Failed().Should().BeFalse());
        results.Select(r => r.Ok()).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_commit_the_repository_does_not_have_is_an_answer_not_an_exception()
    {
        var absent = CommitSha.Parse(new string('f', 40)).Ok();

        var result = await Provider().EnsureAsync(Target(absent.Value), TestContext.Current.CancellationToken);

        result.Failed().Should().BeTrue();
        result.Reason().Should().Contain("is not in").And.Contain(absent.Short);
    }

    private GitCheckoutProvider Provider() =>
        new(CheckoutCacheOptions.Under(_root) with { Timeout = TimeSpan.FromMinutes(2) },
            NullLogger<GitCheckoutProvider>.Instance);

    private MeasurementTarget Target(string sha) =>
        MeasurementTarget.At(RepoUrl.Parse(_source.Url).Ok(), CommitSha.Parse(sha).Ok());
}

/// <summary>A real repository on disk with two commits, so the cache is tested against git rather than
/// against a fake that agrees with whatever we assumed git does.</summary>
internal sealed class TempGitRepo : IDisposable
{
    private TempGitRepo(string path, string url, string first, string second)
    {
        Path = path;
        Url = url;
        FirstCommit = first;
        SecondCommit = second;
    }

    public string Path { get; }

    public string Url { get; }

    public string FirstCommit { get; }

    public string SecondCommit { get; }

    public static async Task<TempGitRepo> CreateAsync()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        await GitAsync(path, "init", "--quiet", "-b", "main");
        var first = await CommitAsync(path, "one");
        var second = await CommitAsync(path, "two");

        return new TempGitRepo(path, new Uri(path).AbsoluteUri, first, second);
    }

    public async Task<string> HeadAsync() => await GitAsync(Path, "rev-parse", "HEAD");

    public async Task<string> StatusAsync() => await GitAsync(Path, "status", "--porcelain");

    public void Dispose() => DeleteTree(Path);

    /// <summary>Git marks objects read-only, so a plain recursive delete fails on Windows.</summary>
    public static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    private static async Task<string> CommitAsync(string path, string content)
    {
        await File.WriteAllTextAsync(System.IO.Path.Combine(path, "content.txt"), content);
        await GitAsync(path, "add", "-A");
        await GitAsync(
            path, "-c", "user.name=bench", "-c", "user.email=bench@example.invalid",
            "commit", "--quiet", "-m", $"add {content}");

        return await GitAsync(path, "rev-parse", "HEAD");
    }

    private static async Task<string> GitAsync(string path, params string[] arguments)
    {
        var attempt = await ProcessRunner.RunAsync("git", arguments, path, TimeSpan.FromMinutes(1), CancellationToken.None);

        return attempt is ProcessAttempt.Completed { Result.Ok: true } completed
            ? completed.Result.Output.Trim()
            : throw new InvalidOperationException($"git {arguments[0]} failed in the fixture: {attempt.Describe}");
    }
}
