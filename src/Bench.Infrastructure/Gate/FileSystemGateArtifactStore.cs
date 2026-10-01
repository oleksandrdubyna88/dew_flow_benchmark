using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Gate;

/// <summary>The artefact root on the local filesystem — <see cref="IGateArtifactStore"/>'s one adapter.
/// <para>
/// Opened through <see cref="Open"/>, which refuses a root inside any git checkout: the files here are private,
/// and a root under a working tree is one <c>git add -A</c> away from a public repository. The root is resolved
/// once, links included, and every later path is resolved the same way and must land inside it — and inside the
/// writing attempt's own roots (<see cref="CellPaths.WritableRoots"/>), so a link planted in one cell's folder
/// cannot carry a write into another's or into the shared data directory.
/// </para></summary>
public sealed class FileSystemGateArtifactStore : IGateArtifactStore
{
    private readonly string _root;
    private readonly TimeProvider _clock;
    private readonly ArtifactProbe _probe;

    private FileSystemGateArtifactStore(string root, TimeProvider clock, ArtifactProbe probe)
    {
        _root = root;
        _clock = clock;
        _probe = probe;
    }

    /// <summary>The canonical root every path is resolved against.</summary>
    public string Root => _root;

    public static Outcome<FileSystemGateArtifactStore> Open(string? root, TimeProvider clock, ArtifactProbe probe)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return Outcome<FileSystemGateArtifactStore>.Failure("the gate needs an artefact root — pass --artifact-root; it holds private text and has no default guess");
        }

        return ArtifactContainment.Resolve(root).Match(
            canonical => GitCheckoutAbove(canonical) is { Length: > 0 } checkout
                ? Outcome<FileSystemGateArtifactStore>.Failure(
                    $"the artefact root {canonical} is inside the git checkout at {checkout} — prompts, answers and findings that quote "
                    + "private code must never sit where a 'git add' can reach them; choose a root outside every repository")
                : Created(canonical, clock, probe),
            Outcome<FileSystemGateArtifactStore>.Failure);
    }

    public Task<Outcome<ArtifactPath>> BeginAttemptAsync(ArtifactScope scope, CancellationToken cancellationToken)
    {
        var root = CellPaths.AttemptRoot(scope);

        return Task.FromResult(Unlinked(root).Match(
            full => Directory.Exists(full)
                ? Outcome<ArtifactPath>.Failure(
                    $"{root} already exists — an attempt directory is created once; a later attempt of the cell gets attempt-{scope.Attempt + 1}, never this one again")
                : Begin(scope, root, full),
            Outcome<ArtifactPath>.Failure));
    }

    public async Task<Outcome<ArtifactRef>> WriteAsync(
        ArtifactScope scope, ArtifactClass kind, ArtifactPath path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (!CellPaths.Allows(scope, path))
        {
            return Outcome<ArtifactRef>.Failure(
                $"{path} is outside cell {scope.CellId}'s own root for attempt {scope.Attempt} ({scope.Run.Mode} data) — a cell writes under its "
                + "attempt directory and its own data directory, never another cell's, another attempt's or the other mode's");
        }

        return await WithinScope(scope, path).Match(
            full => CommitAsync(scope, kind, path, full, bytes, cancellationToken),
            reason => Task.FromResult(Outcome<ArtifactRef>.Failure(reason)));
    }

    public async Task<Outcome<ArtifactRef>> AdoptAsync(ArtifactScope scope, ArtifactClass kind, ArtifactPath path, CancellationToken cancellationToken)
    {
        if (!CellPaths.Allows(scope, path))
        {
            return Outcome<ArtifactRef>.Failure($"{path} is outside cell {scope.CellId}'s own root for attempt {scope.Attempt} — only a file inside it is adopted");
        }

        return await WithinScope(scope, path).Match(
            full => AdoptFileAsync(scope, kind, path, full, cancellationToken),
            reason => Task.FromResult(Outcome<ArtifactRef>.Failure(reason)));
    }

    private static async Task<Outcome<ArtifactRef>> AdoptFileAsync(
        ArtifactScope scope, ArtifactClass kind, ArtifactPath path, string full, CancellationToken cancellationToken)
    {
        if (!File.Exists(full))
        {
            return Outcome<ArtifactRef>.Failure($"{path} does not exist — nothing to adopt");
        }

        await using var stream = new FileStream(full, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        stream.Flush(flushToDisk: true);
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));

        return ArtifactRef.Of(scope, kind, path, sha256, stream.Length);
    }

    public async Task<Outcome<ReadOnlyMemory<byte>>> ReadAsync(ArtifactRef artifact, CancellationToken cancellationToken) =>
        await Contained(artifact.Path).Match(
            async full => File.Exists(full)
                ? Verified(artifact, await File.ReadAllBytesAsync(full, cancellationToken))
                : Outcome<ReadOnlyMemory<byte>>.Failure($"{artifact.Path} is gone — the ref outlived its file"),
            reason => Task.FromResult(Outcome<ReadOnlyMemory<byte>>.Failure(reason)));

    public Task<IReadOnlyList<AttemptOnDisk>> AttemptsAsync(Guid runId, Guid cellId, CancellationToken cancellationToken)
    {
        var cellRoot = CellPaths.CellRoot(runId, cellId);

        var attempts = Contained(cellRoot).Match<IReadOnlyList<AttemptOnDisk>>(
            full => [.. TapPruner.AttemptDirectories(full).Select(a => new AttemptOnDisk(a.Number, StateOf(a.Path), AttemptPath(cellRoot, a.Number)))],
            _ => []);

        return Task.FromResult(attempts);
    }

    public Task<ArtifactFootprint> FootprintAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(Contained(CellPaths.RunRoot(runId)).Match(Footprint, ArtifactFootprint.Unknown));

    public Task<IReadOnlyList<Guid>> RunsAsync(CancellationToken cancellationToken)
    {
        var runs = Path.Combine(_root, CellPaths.RunsFolder);

        IReadOnlyList<Guid> ids = Directory.Exists(runs)
            ? [.. Directory.EnumerateDirectories(runs)
                .Select(d => Guid.TryParse(Path.GetFileName(d), out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Order()]
            : [];

        return Task.FromResult(ids);
    }

    public Task<FileHashKeyRead> ReadFileHashKeyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(FileHashKeyFile.Read(_root));

    public Task<Outcome<FileHashKey>> CreateFileHashKeyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(FileHashKeyFile.Create(_root));

    public Task<TapPruneReport> PruneTapAsync(DateTimeOffset cutoff, bool dryRun, CancellationToken cancellationToken) =>
        Task.FromResult(new TapPruner(_root, _probe).Prune(cutoff, dryRun, _clock.GetUtcNow()));

    private static Outcome<FileSystemGateArtifactStore> Created(string canonical, TimeProvider clock, ArtifactProbe probe)
    {
        Directory.CreateDirectory(canonical);
        return Outcome<FileSystemGateArtifactStore>.Success(new FileSystemGateArtifactStore(canonical, clock, probe));
    }

    /// <summary>The nearest directory at or above <paramref name="directory"/> holding a <c>.git</c> folder or file
    /// (a worktree's pointer), or empty when there is none.</summary>
    private static string GitCheckoutAbove(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var git = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(git) || File.Exists(git))
            {
                return current.FullName;
            }
        }

        return string.Empty;
    }

    private Outcome<ArtifactPath> Begin(ArtifactScope scope, ArtifactPath root, string full)
    {
        foreach (var earlier in TapPruner.AttemptDirectories(Path.GetDirectoryName(full)!).Where(a => a.Number < scope.Attempt))
        {
            MarkInterrupted(earlier.Path, earlier.Number, scope.Attempt);
        }

        Directory.CreateDirectory(full);

        return Unlinked(CellPaths.DataDirFor(scope)).Match(
            data =>
            {
                Directory.CreateDirectory(data);
                return Outcome<ArtifactPath>.Success(root);
            },
            Outcome<ArtifactPath>.Failure);
    }

    /// <summary>Leaves a marker in an attempt a later one replaced. Kept whole; only the marker is added, once.</summary>
    private void MarkInterrupted(string attemptDirectory, int attempt, int replacedBy)
    {
        var marker = Path.Combine(attemptDirectory, CellPaths.InterruptedFile);

        if (File.Exists(marker))
        {
            return;
        }

        var body = string.Create(CultureInfo.InvariantCulture,
            $"{{\"attempt\":{attempt},\"replacedBy\":{replacedBy},\"markedAt\":\"{_clock.GetUtcNow().UtcDateTime:O}\"}}\n");

        Stage(marker, Encoding.UTF8.GetBytes(body));
    }

    /// <summary>Stage → flush → hash → rename is <see cref="ArtifactCommit"/>'s — one protocol, shared with the probes' store
    /// (S2); what stays here is the containment above and the ref below.</summary>
    private async Task<Outcome<ArtifactRef>> CommitAsync(
        ArtifactScope scope, ArtifactClass kind, ArtifactPath path, string full, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
        (await ArtifactCommit.CommitAsync(full, bytes, _probe, cancellationToken)).Match(
            committed => ArtifactRef.Of(scope, kind, path, committed.Sha256, committed.Length),
            reason => Outcome<ArtifactRef>.Failure($"{path} {reason}"));

    /// <summary>The synchronous twin for the small markers the store writes itself.</summary>
    private static void Stage(string full, byte[] bytes) => ArtifactCommit.Stage(full, bytes);

    /// <summary>The target on disk, resolved, and inside the artefact root AND one of the attempt's writable roots.
    /// A link INSIDE a writable root is followed and judged by where it leads; a writable root that is itself reached
    /// through a link does not count as one at all — <c>cells/&lt;a&gt;</c> made a link to <c>cells/&lt;b&gt;</c> would
    /// otherwise let cell a write into cell b's folder while every path still spelled cell a.</summary>
    private Outcome<string> WithinScope(ArtifactScope scope, ArtifactPath path) =>
        Contained(path).Match(
            full => CellPaths.WritableRoots(scope)
                .Select(Unlinked)
                .Any(root => root is Outcome<string>.Ok ok && ArtifactContainment.IsWithin(full, ok.Value))
                ? Outcome<string>.Success(full)
                : Outcome<string>.Failure(
                    $"{path} resolves outside cell {scope.CellId}'s own root — a link on the way leads elsewhere, and a write through it is refused"),
            Outcome<string>.Failure);

    /// <summary>A directory the store is about to own — an attempt root, a data directory — resolved, and refused
    /// unless it is exactly where its ids say: no link anywhere between the artefact root and it.</summary>
    private Outcome<string> Unlinked(ArtifactPath path)
    {
        var lexical = Path.TrimEndingDirectorySeparator(Path.Combine([_root, .. path.Segments]));

        return Contained(path).Match(
            full => ArtifactContainment.Same(full, lexical)
                ? Outcome<string>.Success(full)
                : Outcome<string>.Failure($"{path} is reached through a link — a cell's own root is a real directory where its ids say, or it is refused"),
            Outcome<string>.Failure);
    }

    /// <summary>A relative path, resolved on the real filesystem, refused unless it stays under the root.</summary>
    private Outcome<string> Contained(ArtifactPath path) =>
        ArtifactContainment.Resolve(Path.Combine([_root, .. path.Segments])).Match(
            full => ArtifactContainment.IsWithin(full, _root)
                ? Outcome<string>.Success(full)
                : Outcome<string>.Failure($"{path} resolves outside the artefact root — a link on the way leads elsewhere"),
            Outcome<string>.Failure);

    private static Outcome<ReadOnlyMemory<byte>> Verified(ArtifactRef artifact, byte[] bytes)
    {
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        return string.Equals(sha256, artifact.Sha256, StringComparison.Ordinal) && bytes.LongLength == artifact.Length
            ? Outcome<ReadOnlyMemory<byte>>.Success(bytes)
            : Outcome<ReadOnlyMemory<byte>>.Failure(
                $"{artifact.Path} no longer matches its ref — committed {HashText.Short(artifact.Sha256)} ({artifact.Length} B), on disk "
                + $"{HashText.Short(sha256)} ({bytes.LongLength} B); the evidence changed after it was recorded");
    }

    private static AttemptState StateOf(string attemptDirectory) =>
        (File.Exists(Path.Combine(attemptDirectory, CellPaths.InterruptedFile)), File.Exists(Path.Combine(attemptDirectory, CellPaths.RunRecordFile))) switch
        {
            (true, _) => AttemptState.Interrupted,
            (_, true) => AttemptState.Recorded,
            _ => AttemptState.Open,
        };

    private static ArtifactPath AttemptPath(ArtifactPath cellRoot, int attempt) =>
        cellRoot.Then(CellPaths.AttemptFolder(attempt)).Match(p => p, _ => cellRoot);

    /// <summary>What is on disk under a run, without following links — a link is counted as itself, never as the
    /// tree it points at. Any failure to read is an UNKNOWN footprint, never a smaller one.</summary>
    private static ArtifactFootprint Footprint(string runDirectory)
    {
        if (!Directory.Exists(runDirectory))
        {
            return new ArtifactFootprint(CapturedCount.Number(0), CapturedCount.Number(0), CapturedCount.Number(0));
        }

        try
        {
            // Aggregated while enumerating — a run is thousands of files, and holding every FileInfo to sum them
            // afterwards is memory spent on nothing.
            var (bytes, count, tap) = new DirectoryInfo(runDirectory)
                .EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
                .Aggregate((Bytes: 0L, Count: 0L, Tap: 0L), (sum, file) => (sum.Bytes + file.Length, sum.Count + 1, sum.Tap + (IsTapBody(file) ? file.Length : 0)));

            return new ArtifactFootprint(CapturedCount.Number(bytes), CapturedCount.Number(count), CapturedCount.Number(tap));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ArtifactFootprint.Unknown($"{ex.GetType().Name} while measuring the run's directory");
        }
    }

    private static bool IsTapBody(FileInfo file) =>
        string.Equals(file.Directory?.Name, CellPaths.TapFolder, StringComparison.Ordinal)
        && (file.Name.EndsWith(".request.json", StringComparison.Ordinal) || file.Name.EndsWith(".response.json", StringComparison.Ordinal));
}
