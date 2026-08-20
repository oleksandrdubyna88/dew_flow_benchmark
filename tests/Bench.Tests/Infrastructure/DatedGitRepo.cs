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

    /// <summary>Initialises the repository with one commit and returns its sha.</summary>
    public async Task<string> InitAsync((string Path, string Content) file, (string Subject, string Date) commit)
    {
        Directory.CreateDirectory(Root);
        await GitAsync("init", "-q");
        return await CommitAsync(file, commit);
    }

    /// <summary>Writes one file, commits it with a pinned author date, returns the new sha.</summary>
    public async Task<string> CommitAsync((string Path, string Content) file, (string Subject, string Date) commit)
    {
        var full = Path.Combine(Root, file.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, file.Content, cancellationToken);

        await GitAsync("add", "-A");
        await GitAsync(
            "-c", "user.name=bench", "-c", "user.email=bench@test",
            "commit", "-q", "-m", commit.Subject, "--date", commit.Date);

        return (await GitAsync("rev-parse", "HEAD")).Trim();
    }

    public async Task<string> GitAsync(params string[] arguments)
    {
        var run = await GitCommand.RunAsync(Root, Timeout, cancellationToken, arguments);

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
