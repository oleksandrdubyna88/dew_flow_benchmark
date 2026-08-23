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

    private readonly string _id = Guid.CreateVersion7().ToString("N");

    public string Root => Path.Combine(Path.GetTempPath(), "bench-dated-git", _id);

    /// <summary>The repository as a clonable url — what a checkout provider takes.</summary>
    public string Url => new Uri(Root).AbsoluteUri;

    /// <summary>Initialises the repository with one commit and returns its sha. Line endings are pinned
    /// to LF regardless of the machine's autocrlf, so a diff captured here applies to a worktree checked
    /// out here — a fixture at the mercy of global git config is a test that fails per machine.
    /// <para>
    /// <b>The identity is configured on the REPOSITORY, not passed per command.</b> It used to ride on
    /// <c>-c</c> flags at the commit call only, which left every other verb — <c>merge</c> above all —
    /// depending on whoever ran the test having a global git identity. That is the very hazard the
    /// paragraph above names, and it cost three days of red CI: the harvest tests passed on every
    /// developer machine and failed on the runner with <em>"Committer identity unknown"</em>. Configured
    /// once here, every verb this fixture will ever gain is covered by construction.
    /// </para></summary>
    public async Task<string> InitAsync((string Path, string Content) file, (string Subject, string Date) commit)
    {
        Directory.CreateDirectory(Root);
        await GitAsync("init", "-q");
        await GitAsync("config", "core.autocrlf", "false");
        await GitAsync("config", "user.name", "bench");

        // PER-REPOSITORY, and that is the second half of the fix. Four test files build a byte-identical
        // fixture — same paths, same content, same messages, same pinned author dates — and `--date` pins
        // only the AUTHOR date, so the sole thing separating their commit shas was the committer timestamp.
        // Two landing in the same second produced the same sha, hence the same `fix-<sha12>` question id,
        // and the second insert into the shared bank was refused by name. The identity enters the commit
        // object but NOT the tree, so every diff a test asserts on is untouched.
        await GitAsync("config", "user.email", $"bench+{_id}@test");

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

        // No -c flags: the identity is on the repository (see InitAsync). Two mechanisms for one fact is
        // how the merge path came to have none.
        await GitAsync("commit", "-q", "-m", commit.Subject, "--date", commit.Date);

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
