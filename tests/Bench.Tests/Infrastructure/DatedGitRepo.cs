using Bench.Domain;
using Bench.Infrastructure.Git;

namespace Bench.Tests.Infrastructure;

/// <summary>A disposable real git repository whose commits the TEST authors — arbitrary paths, pinned
/// author dates, branches and merges — driven through <see cref="GitCommand"/>, the same launcher
/// production uses, because the failure modes worth testing (a root commit, a merge, an unknown ref)
/// are git's answers, not ours.
/// <para>
/// Deliberately beside <see cref="TempGitRepo"/> rather than merged into it: that fixture is the
/// checkout cache's — a fixed two-commit shape whose value is being boringly identical across its
/// tests — while harvest tests need to compose histories. The shared half, deleting a tree whose
/// object files git marked read-only, is one method and it is reused, not copied.
/// </para></summary>
public sealed class DatedGitRepo(CancellationToken cancellationToken) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public string Root { get; } = Path.Combine(
        Path.GetTempPath(), "bench-dated-git", Guid.CreateVersion7().ToString("N"));

    /// <summary>The repository as a clonable url — what a checkout provider takes.</summary>
    public string Url => new Uri(Root).AbsoluteUri;

    /// <summary>Initialises the repository with one commit and returns its sha. Line endings are pinned
    /// to LF regardless of the machine's autocrlf, so a diff captured here applies to a worktree checked
    /// out here — a fixture at the mercy of global git config is a test that fails per machine.</summary>
    public async Task<string> InitAsync((string Path, string Content) file, (string Subject, string Date) commit)
    {
        Directory.CreateDirectory(Root);
        await GitAsync("init", "-q");
        await GitAsync("config", "core.autocrlf", "false");
        return await CommitAsync(file, commit);
    }

    /// <summary>Writes one file, commits it with a pinned author date, returns the new sha.</summary>
    public Task<string> CommitAsync((string Path, string Content) file, (string Subject, string Date) commit) =>
        CommitManyAsync([file], commit);

    /// <summary>Several files in ONE commit — the shape a real fix has: the change and its test land
    /// together, and a gate test needs exactly that.</summary>
    public async Task<string> CommitManyAsync(
        IReadOnlyList<(string Path, string Content)> files, (string Subject, string Date) commit)
    {
        foreach (var file in files)
        {
            var full = Path.Combine(Root, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, file.Content, cancellationToken);
        }

        await GitAsync("add", "-A");
        await GitAsync(
            "-c", "user.name=bench", "-c", "user.email=bench@test",
            "commit", "-q", "-m", commit.Subject, "--date", commit.Date);

        return (await GitAsync("rev-parse", "HEAD")).Trim();
    }

    public async Task<string> GitAsync(params string[] arguments)
    {
        // ReadAsync: a test capturing a diff or a sha must get the PAYLOAD, not git's stderr beside it.
        var run = await GitCommand.ReadAsync(Root, Timeout, cancellationToken, arguments);

        return run is Outcome<string>.Ok ok
            ? ok.Value
            : throw new InvalidOperationException(((Outcome<string>.Fail)run).Reason);
    }

    public void Dispose()
    {
        try
        {
            TempGitRepo.DeleteTree(Root);
        }
        catch (IOException)
        {
            // A leaked temp directory is a nuisance; a failed test run over it would be a mystery.
        }
    }
}
