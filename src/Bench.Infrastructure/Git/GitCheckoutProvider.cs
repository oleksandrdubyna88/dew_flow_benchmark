using System.Collections.Concurrent;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Targets;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Git;

/// <param name="Root">Everything this provider creates lives under here, and it never writes outside it.</param>
/// <param name="Timeout">Per git invocation. A clone of a large repository is minutes, not seconds.</param>
public sealed record CheckoutCacheOptions(string Root, TimeSpan Timeout)
{
    public static CheckoutCacheOptions Under(string root) => new(root, TimeSpan.FromMinutes(20));
}

/// <summary>A pinned, read-only tree per commit: one bare clone per repository url, one worktree per
/// commit, both under a cache root this provider owns.
/// <para>
/// <b>The working tree of the repository being measured is never touched.</b> That is the whole design
/// constraint, and it is not hypothetical caution: the equivalent component in the previous system ran
/// <c>git checkout</c> in place on a configured repository path, which for a benchmark means silently
/// rewriting whatever a developer had open to a commit they never asked for. Here the source is only
/// ever read — <c>clone --bare</c> and <c>fetch</c> — and every write lands under
/// <see cref="CheckoutCacheOptions.Root"/>.
/// </para>
/// <para>
/// Directory names come from a hash of the url rather than the url itself. That is a safety property,
/// not tidiness: a url is operator input, and a url pasted into a path is how <c>../</c> gets a say in
/// where files land.
/// </para></summary>
public sealed class GitCheckoutProvider(CheckoutCacheOptions options, ILogger<GitCheckoutProvider> logger)
    : ICheckoutProvider
{
    private readonly Dictionary<string, RepositoryGate> _gates = new(StringComparer.Ordinal);

    /// <summary>How many per-repository gates are held right now. Zero between calls: a gate exists while
    /// somebody is inside it and not one moment longer, which is what stops a campaign against many
    /// repositories from accumulating one undisposed semaphore per url for the life of the process.</summary>
    public int Gates
    {
        get
        {
            lock (_gates)
            {
                return _gates.Count;
            }
        }
    }

    public async Task<Outcome<string>> EnsureAsync(MeasurementTarget target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Root);

        var key = StableHash.Of(target.Repo.Value)[..16];
        var bare = Path.Combine(options.Root, "bare", $"{key}.git");
        var worktree = Path.Combine(options.Root, "worktrees", key, target.Commit.Value);

        if (!IsInsideRoot(bare) || !IsInsideRoot(worktree))
        {
            return Outcome<string>.Failure($"refusing to work outside the cache root '{options.Root}'");
        }

        var gate = Rent(key);

        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken);

            try
            {
                return await EnsureUnderLockAsync(target, bare, worktree, cancellationToken);
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        finally
        {
            Return(key, gate);
        }
    }

    /// <summary>Takes a share of this repository's gate, creating it if this caller is the first.
    /// <para>
    /// Counted rather than cached, because the alternative was a dictionary that only ever grew: one
    /// <see cref="SemaphoreSlim"/> per distinct url, never removed and never disposed. One repository makes
    /// that a footnote; a campaign whose whole premise is measuring many repositories over weeks makes it a
    /// leak with a shape.
    /// </para>
    /// <para>
    /// The share is taken BEFORE the wait, which is what makes disposal safe: a gate cannot reach zero
    /// users while anybody is still queued on it.
    /// </para></summary>
    private RepositoryGate Rent(string key)
    {
        lock (_gates)
        {
            if (!_gates.TryGetValue(key, out var gate))
            {
                gate = new RepositoryGate();
                _gates[key] = gate;
            }

            gate.Users++;
            return gate;
        }
    }

    private void Return(string key, RepositoryGate gate)
    {
        lock (_gates)
        {
            if (--gate.Users > 0)
            {
                return;
            }

            _gates.Remove(key);
            gate.Semaphore.Dispose();
        }
    }

    /// <summary>One repository's serialization point, and how many callers are currently holding it.</summary>
    private sealed class RepositoryGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Users { get; set; }
    }

    private async Task<Outcome<string>> EnsureUnderLockAsync(
        MeasurementTarget target, string bare, string worktree, CancellationToken cancellationToken)
    {
        if (Directory.Exists(worktree) && Directory.EnumerateFileSystemEntries(worktree).Any())
        {
            // A commit is immutable, so a worktree that already exists for one is already correct.
            return Outcome<string>.Success(worktree);
        }

        var mirror = await EnsureMirrorAsync(target, bare, cancellationToken);

        return mirror is Outcome<string>.Fail fail
            ? Outcome<string>.Failure(fail.Reason)
            : await AddWorktreeAsync(target, bare, worktree, cancellationToken);
    }

    private async Task<Outcome<string>> EnsureMirrorAsync(
        MeasurementTarget target, string bare, CancellationToken cancellationToken)
    {
        if (Directory.Exists(Path.Combine(bare, "objects")))
        {
            logger.LogDebug("Refreshing mirror of {Repo}", target.Repo.Value);

            // A fetch failure is not fatal on its own: the commit may already be present, which is the
            // common case for a re-run and the only case that works offline. The commit check decides.
            var fetched = await GitAsync(bare, cancellationToken, "fetch", "--quiet", "--prune", "origin");

            if (fetched is Outcome<string>.Fail fetchFail)
            {
                logger.LogWarning("Fetch failed for {Repo}: {Reason}", target.Repo.Value, fetchFail.Reason);
            }

            return await VerifyCommitAsync(target, bare, cancellationToken);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(bare) ?? options.Root);
        logger.LogInformation("Mirroring {Repo}", target.Repo.Value);

        var cloned = await GitAsync(
            options.Root, cancellationToken, "clone", "--bare", "--quiet", target.Repo.Value, bare);

        return cloned is Outcome<string>.Fail cloneFail
            ? Outcome<string>.Failure($"could not mirror {target.Repo.Value}: {cloneFail.Reason}")
            : await VerifyCommitAsync(target, bare, cancellationToken);
    }

    private async Task<Outcome<string>> VerifyCommitAsync(
        MeasurementTarget target, string bare, CancellationToken cancellationToken)
    {
        var exists = await GitAsync(bare, cancellationToken, "cat-file", "-e", $"{target.Commit.Value}^{{commit}}");

        return exists is Outcome<string>.Fail
            ? Outcome<string>.Failure(
                $"commit {target.Commit.Short} is not in {target.Repo.Value} — it may be unpushed, on a fork, or garbage-collected")
            : Outcome<string>.Success(bare);
    }

    private async Task<Outcome<string>> AddWorktreeAsync(
        MeasurementTarget target, string bare, string worktree, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(worktree) ?? options.Root);

        var added = await GitAsync(
            bare, cancellationToken, "worktree", "add", "--detach", "--force", worktree, target.Commit.Value);

        if (added is Outcome<string>.Fail fail)
        {
            return Outcome<string>.Failure($"could not check out {target.Commit.Short}: {fail.Reason}");
        }

        logger.LogInformation("Checked out {Repo} at {Commit}", target.Repo.Value, target.Commit.Short);
        return Outcome<string>.Success(worktree);
    }

    // The runner is GitCommand's, shared with the history reader: one launcher and one refusal shape for every
    // git call this repository makes.
    private Task<Outcome<string>> GitAsync(
        string workingDirectory, CancellationToken cancellationToken, params string[] arguments) =>
        GitCommand.RunAsync(workingDirectory, options.Timeout, cancellationToken, arguments);

    private bool IsInsideRoot(string candidate)
    {
        var root = Path.GetFullPath(options.Root);
        var full = Path.GetFullPath(candidate);

        return full.StartsWith(
            root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }
}
