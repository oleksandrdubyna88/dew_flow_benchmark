using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Scouts;

/// <param name="Root">The trees root — every tree is <c>&lt;root&gt;/&lt;task id&gt;</c>, and nothing is written outside it.</param>
/// <param name="RepositoryUrl">How a task's PUBLIC repository slug becomes the url its mirror is cloned from — <c>https://github.com/{0}.git</c>; a task
/// whose repository is already a url (a private repository's local clone, D10) does not go through it;
/// a test points it at local repositories.</param>
public sealed record ScoutTreeSettings(string Root, string RepositoryUrl, TimeSpan GitTimeout)
{
    public const string GitHub = "https://github.com/{0}.git";

    /// <summary>The url a task's mirror is cloned from (plan D10): a repository that already IS a url is used as it is — a private repository's
    /// local <c>file://</c> clone included —, an <c>owner/name</c> slug goes through <see cref="RepositoryUrl"/>, anything else is refused by name.</summary>
    public Outcome<RepoUrl> UrlOf(string repository) =>
        (ScoutRepository.IsUrl(repository), ScoutRepository.IsSlug(repository)) switch
        {
            (true, _) => RepoUrl.Parse(repository),
            (_, true) => RepoUrl.Parse(string.Format(CultureInfo.InvariantCulture, RepositoryUrl, repository.Trim())),
            _ => Outcome<RepoUrl>.Failure(ScoutRepository.Refusal(repository)),
        };
}

/// <summary>The history-free trees of §3.2. <c>git archive P</c> from the bare mirror <see cref="GitCheckoutProvider"/> keeps (as a ZIP — a
/// symlink in it lands as a plain file, never as a link that could lead a scout out of the tree), extracted under the trees root; each
/// submodule archived at its pinned sha into its path; the PR's own plan and the project's own Claude settings
/// (<see cref="ScoutGroundTruth.ClaudeProjectSettings"/>, §3.2 as amended 2026-10-13 — they configure the scouting tool and may grant a
/// directory outside the tree) removed, and each removal recorded; then a fresh <c>git init</c>
/// and ONE commit, author and date FIXED through the environment so the commit is the same on every machine — no git config is read for
/// identity or written at all, and no hook runs (<c>--no-verify</c>). There is no later history to read.
/// <para>
/// <b>The tree hash</b> is SHA-256 over every file's repository-relative path and the SHA-256 of its bytes, sorted, <c>.git</c> excluded —
/// stable on any machine. A tree is built once, in a staging folder renamed into place, so a crash never leaves a half tree that looks
/// whole; a tree found already built is re-checked: one commit and no other ref, its own project settings granting nothing outside it.
/// A marker under <c>&lt;root&gt;/.scout-trees/</c> says this builder made it — what <c>bench scouts prune</c> deletes by.
/// </para></summary>
public sealed class ScoutTrees(ScoutTreeSettings settings, GitCheckoutProvider mirrors, ILogger<ScoutTrees> logger) : IScoutTrees
{
    public const string MarkerFolder = ".scout-trees";

    private const string Author = "bench scout tree";
    private const string Email = "scout-tree@bench.invalid";
    private const string FixedDate = "2000-01-01T00:00:00+0000";
    private const int MaxSubmoduleDepth = 3;

    public string Root => Path.GetFullPath(settings.Root);

    public async Task<Outcome<ScoutTree>> BuildAsync(ScoutTreeSpec spec, CancellationToken cancellationToken)
    {
        var tree = TreePath(spec.Task);

        if (Directory.Exists(tree))
        {
            return await FoundAsync(tree, spec, cancellationToken);
        }

        Directory.CreateDirectory(Root);
        var staging = Path.Combine(Root, $".staging-{spec.Task}-{Guid.NewGuid():N}");

        try
        {
            var built = await StageAsync(staging, spec, cancellationToken);

            return built is Outcome<IReadOnlyList<string>>.Ok removed
                ? Commit(staging, tree, spec, removed.Value)
                : Outcome<ScoutTree>.Failure(((Outcome<IReadOnlyList<string>>.Fail)built).Reason);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Probes.ProbeTrees.TryDeleteTree(staging);
            }
        }
    }

    public async Task<Outcome<ScoutTree>> PrepareAsync(ScoutTask task, CancellationToken cancellationToken)
    {
        var built = await BuildAsync(new ScoutTreeSpec(task.Id, task.Repository, task.Parent, task.Removals) { Privacy = task.Privacy }, cancellationToken);

        return built switch
        {
            Outcome<ScoutTree>.Ok ok when !string.Equals(ok.Value.TreeHash, task.TreeHash, StringComparison.Ordinal) => Outcome<ScoutTree>.Failure(
                $"the tree of task {task.Id} {ScoutTreeRefusal.HashChanged} — {task.TreeHash[..12]} frozen, {ok.Value.TreeHash[..12]} now; the cell stops"),
            _ => built,
        };
    }

    /// <summary>Whether this builder made <paramref name="task"/>'s tree — its marker is there and the folder is a real directory under the root.</summary>
    public bool Built(ScoutTaskId task) =>
        File.Exists(MarkerPath(task)) && Directory.Exists(TreePath(task)) && new DirectoryInfo(TreePath(task)).LinkTarget is null
        && ClaudeGrants.Within(TreePath(task), Root);

    /// <summary><c>prune</c>'s deletion of one tree: only a tree this builder made, path-checked under the root, never through a link.</summary>
    public Outcome<bool> Delete(ScoutTaskId task)
    {
        if (!Built(task))
        {
            return Outcome<bool>.Success(false);
        }

        return Probes.ProbeTrees.Remove(TreePath(task), Root) && TryDelete(MarkerPath(task))
            ? Outcome<bool>.Success(true)
            : Outcome<bool>.Failure($"the tree of task {task} could not be removed — the filesystem still holds it; prune again");
    }

    public string TreePath(ScoutTaskId task) => Path.Combine(Root, task.Value);

    private string MarkerPath(ScoutTaskId task) => Path.Combine(Root, MarkerFolder, task.Value + ".json");

    private async Task<Outcome<ScoutTree>> FoundAsync(string tree, ScoutTreeSpec spec, CancellationToken cancellationToken)
    {
        var commits = await GitCommand.ReadAsync(tree, settings.GitTimeout, cancellationToken, "rev-list", "--all", "--count");
        var grant = ScoutGrantGuard.ProjectRefusal(tree);

        return (commits, grant.Length > 0) switch
        {
            (Outcome<string>.Fail fail, _) => Outcome<ScoutTree>.Failure(Withheld(spec, "the tree is not a git repository any more", $"the tree of task {spec.Task} is not a git repository any more — {fail.Reason}")),
            (Outcome<string>.Ok { Value: var count }, _) when count.Trim() != "1" => Outcome<ScoutTree>.Failure(
                $"the tree of task {spec.Task} carries {count.Trim()} commits — a history-free tree has exactly one; {ScoutTreeRefusal.HashChanged}"),
            (_, true) => Outcome<ScoutTree>.Failure(Withheld(spec, "the tree's own Claude settings grant a directory outside it", $"task {spec.Task}: {grant}")),
            _ => Outcome<ScoutTree>.Success(Hashed(tree) with { Removed = Recorded(spec.Task) }),
        };
    }

    /// <summary>The archive, the submodules and the removals, under the staging folder. Answers the files removed.</summary>
    private async Task<Outcome<IReadOnlyList<string>>> StageAsync(string staging, ScoutTreeSpec spec, CancellationToken cancellationToken)
    {
        var archived = await ArchiveAsync(settings.UrlOf(spec.Repository), spec.Parent, staging, 0, spec.Privacy, cancellationToken);

        if (archived is Outcome<bool>.Fail fail)
        {
            return Outcome<IReadOnlyList<string>>.Failure(Withheld(spec, "the tree could not be archived from the mirror", $"task {spec.Task}: {fail.Reason}"));
        }

        IReadOnlyList<string> removed = [.. spec.Remove.Concat(ScoutGroundTruth.ClaudeProjectSettings).Distinct(StringComparer.Ordinal).Where(path => RemoveFile(staging, path))];
        var grant = ScoutGrantGuard.ProjectRefusal(staging);

        return grant.Length > 0
            ? Outcome<IReadOnlyList<string>>.Failure(Withheld(spec, "the tree's own Claude settings grant a directory outside it", $"task {spec.Task}: {grant}"))
            : await InitAsync(staging, spec, removed, cancellationToken);
    }

    /// <summary>A refusal about the tree as a public writer may print it: a public task's as written; a private task's by label and summary, the
    /// detail kept under <c>.scout-trees/</c> (<see cref="ScoutWithheld"/>).</summary>
    private string Withheld(ScoutTreeSpec spec, string summary, string detail) => ScoutWithheld.Refusal(Root, spec.Privacy, spec.Task.Value, summary, detail);

    private async Task<Outcome<bool>> ArchiveAsync(Outcome<RepoUrl> url, CommitSha commit, string into, int depth, ScoutTaskPrivacy privacy, CancellationToken cancellationToken)
    {
        if (url is Outcome<RepoUrl>.Fail badUrl)
        {
            return Outcome<bool>.Failure(badUrl.Reason);
        }

        var mirror = await mirrors.EnsureMirrorAsync(MeasurementTarget.At(((Outcome<RepoUrl>.Ok)url).Value, commit), cancellationToken);

        return mirror switch
        {
            Outcome<string>.Ok bare => await ExtractAsync(bare.Value, ((Outcome<RepoUrl>.Ok)url).Value.Value, commit, into, depth, privacy, cancellationToken),
            Outcome<string>.Fail fail => Outcome<bool>.Failure(fail.Reason),
            _ => Outcome<bool>.Failure("unreachable"),
        };
    }

    private async Task<Outcome<bool>> ExtractAsync(string bare, string source, CommitSha commit, string into, int depth, ScoutTaskPrivacy privacy, CancellationToken cancellationToken)
    {
        var zip = Path.Combine(Path.GetTempPath(), $"bench-scout-{Guid.NewGuid():N}.zip");

        try
        {
            // A ZIP archive applies the machine's core.autocrlf to text files — found red by the submodule test, where this machine's
            // config turned "\n" into "\r\n". Pinned per invocation (no config is written), so the bytes, and the tree hash, are P's own.
            var archived = await GitCommand.ReadAsync(bare, settings.GitTimeout, cancellationToken, "-c", "core.autocrlf=false", "-c", "core.eol=lf", "archive", "--format=zip", "-o", zip, commit.Value);

            if (archived is Outcome<string>.Fail fail)
            {
                return Outcome<bool>.Failure($"git archive {commit.Short} failed — {fail.Reason}");
            }

            Directory.CreateDirectory(into);
            ZipFile.ExtractToDirectory(zip, into, overwriteFiles: false);
            return await SubmodulesAsync(bare, source, commit, into, depth, privacy, cancellationToken);
        }
        finally
        {
            TryDelete(zip);
        }
    }

    /// <summary>Each submodule's content at the sha P pins, archived into its path from its OWN mirror — the url read from P's
    /// <c>.gitmodules</c> — or, for a LOCAL source, from the objects its clone keeps under <c>.git/modules/&lt;path&gt;</c>
    /// (<see cref="ScoutLocalModules"/>). A submodule whose url cannot be read or reached refuses the tree: a tree missing a directory is not P.</summary>
    private async Task<Outcome<bool>> SubmodulesAsync(string bare, string source, CommitSha commit, string into, int depth, ScoutTaskPrivacy privacy, CancellationToken cancellationToken)
    {
        var listed = await GitCommand.ReadAsync(bare, settings.GitTimeout, cancellationToken, "ls-tree", "-r", commit.Value);
        var pinned = listed.Match(text => ScoutSubmodules.Pinned(text), _ => []);

        if (pinned.Count == 0)
        {
            return Outcome<bool>.Success(true);
        }

        if (depth >= MaxSubmoduleDepth)
        {
            return Outcome<bool>.Failure($"submodules nest deeper than {MaxSubmoduleDepth} — refused rather than archived partly");
        }

        var modules = (await GitCommand.ReadAsync(bare, settings.GitTimeout, cancellationToken, "show", $"{commit.Value}:.gitmodules")).Match(ScoutSubmodules.Urls, _ => new Dictionary<string, string>());

        foreach (var (path, sha) in pinned)
        {
            var archived = await SubmoduleAsync(modules, source, path, sha, into, depth, privacy, cancellationToken);

            if (archived is Outcome<bool>.Fail)
            {
                return archived;
            }
        }

        return Outcome<bool>.Success(true);
    }

    private async Task<Outcome<bool>> SubmoduleAsync(
        IReadOnlyDictionary<string, string> modules, string source, string path, string sha, string into, int depth, ScoutTaskPrivacy privacy, CancellationToken cancellationToken)
    {
        var commit = CommitSha.Parse(sha);
        var target = Path.GetFullPath(Path.Combine(into, path));

        if (!modules.TryGetValue(path, out var url) || commit is Outcome<CommitSha>.Fail || !ClaudeGrants.Within(target, into))
        {
            return Outcome<bool>.Failure($"submodule '{path}' has no url in .gitmodules at P, a sha that is not one, or a path outside the tree");
        }

        logger.LogInformation("Archiving submodule {Path} at {Sha}", ScoutPublication.Path(privacy, path), privacy == ScoutTaskPrivacy.Private ? ScoutPublication.PrivateMark : sha[..12]);
        return await ArchiveAsync(RepoUrl.Parse(ScoutLocalModules.Source(source, path, url)), ((Outcome<CommitSha>.Ok)commit).Value, target, depth + 1, privacy, cancellationToken);
    }

    private static bool RemoveFile(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));

        if (!ClaudeGrants.Within(full, root) || !File.Exists(full))
        {
            return false;
        }

        File.Delete(full);
        return true;
    }

    /// <summary><c>git init</c> and ONE commit, identity and date from the environment, no hook, no config touched.</summary>
    private async Task<Outcome<IReadOnlyList<string>>> InitAsync(string staging, ScoutTreeSpec spec, IReadOnlyList<string> removed, CancellationToken cancellationToken) =>
        (await InitOneCommitAsync(staging, $"scout tree {spec.Task} at {spec.Parent.Value}", settings.GitTimeout, cancellationToken)).Match(
            _ => Outcome<IReadOnlyList<string>>.Success(removed),
            reason => Outcome<IReadOnlyList<string>>.Failure(Withheld(spec, $"{reason.Split(" — ")[0]} in the new tree", $"task {spec.Task}: {reason}")));

    /// <summary>A folder made a ONE-commit repository: <c>git init</c>, everything added, one commit — identity and date FIXED through the
    /// environment, no hook (<c>--no-verify</c>), per-invocation <c>-c</c> flags only, no config written. What every history-free tree is
    /// finished with, and what the access check (S5) builds its scratch tree with. A refusal names the git step that failed.</summary>
    public static async Task<Outcome<bool>> InitOneCommitAsync(string folder, string message, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var environment = GitEnvironment();
        (string Name, string[] Argv)[] steps =
        [
            ("init", ["-c", "init.defaultBranch=main", "init", "-q"]),
            // core.longpaths: a repository can hold a path longer than Windows' 260 characters under the trees root — found live 2026-10-13,
            // "Filename too long" from git add stopped the selection. Per invocation, nothing written; on Linux it changes nothing.
            ("add", ["-c", "core.autocrlf=false", "-c", "core.safecrlf=false", "-c", "core.longpaths=true", "add", "-A", "-f", "."]),
            ("commit", ["-c", "commit.gpgsign=false", "-c", "core.longpaths=true", "commit", "-q", "--no-verify", "--allow-empty", "-m", message]),
        ];

        foreach (var (name, argv) in steps)
        {
            var ran = await ProcessRunner.RunAsync("git", argv, folder, timeout, string.Empty, environment, cancellationToken);

            if (ran is not ProcessAttempt.Completed { Result.Ok: true })
            {
                return Outcome<bool>.Failure($"git {name} failed — {Said(ran)}");
            }
        }

        return Outcome<bool>.Success(true);
    }

    private Outcome<ScoutTree> Commit(string staging, string tree, ScoutTreeSpec spec, IReadOnlyList<string> removed)
    {
        Directory.Move(staging, tree);
        var hashed = Hashed(tree) with { Removed = removed };

        Directory.CreateDirectory(Path.Combine(Root, MarkerFolder));
        File.WriteAllText(MarkerPath(spec.Task), new JsonObject
        {
            ["task"] = spec.Task.Value,
            ["repository"] = spec.Repository,
            ["parent"] = spec.Parent.Value,
            ["treeHash"] = hashed.TreeHash,
            ["removed"] = new JsonArray([.. removed.Select(r => (JsonNode)JsonValue.Create(r))]),
        }.ToJsonString() + "\n");

        // A log line is a public writer too (§3.13): a private task's P is a digest of its history, so the line carries the label and the mark.
        logger.LogInformation("Built the history-free tree of {Task} at {Parent}: {Files} file(s), hash {Hash}", spec.Task, ScoutPublication.CommitShort(spec.Privacy, spec.Parent), hashed.Files.Count, hashed.TreeHash[..12]);
        return Outcome<ScoutTree>.Success(hashed);
    }

    private IReadOnlyList<string> Recorded(ScoutTaskId task) =>
        File.Exists(MarkerPath(task)) && JsonNode.Parse(File.ReadAllText(MarkerPath(task)))?["removed"] is JsonArray removed
            ? [.. removed.Select(r => r?.GetValue<string>() ?? string.Empty).Where(r => r.Length > 0)]
            : [];

    /// <summary>The tree's files (repository-relative, <c>/</c>, <c>.git</c> excluded, sorted) and the SHA-256 over each path and its bytes' SHA-256.</summary>
    public static ScoutTree Hashed(string tree)
    {
        var files = Directory.EnumerateFiles(tree, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
            .Select(f => Path.GetRelativePath(tree, f).Replace('\\', '/'))
            .Where(f => f != ".git" && !f.StartsWith(".git/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        var manifest = new StringBuilder();
        foreach (var file in files)
        {
            using var stream = File.OpenRead(Path.Combine(tree, file));
            manifest.Append(file).Append('\n').Append(Convert.ToHexStringLower(SHA256.HashData(stream))).Append('\n');
        }

        return new ScoutTree(tree, StableHash.Of(manifest.ToString()), files);
    }

    /// <summary>The harness's environment with the commit's identity and date fixed — the variables git reads INSTEAD of its config.</summary>
    private static IReadOnlyDictionary<string, string> GitEnvironment()
    {
        var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        environment["GIT_AUTHOR_NAME"] = Author;
        environment["GIT_AUTHOR_EMAIL"] = Email;
        environment["GIT_AUTHOR_DATE"] = FixedDate;
        environment["GIT_COMMITTER_NAME"] = Author;
        environment["GIT_COMMITTER_EMAIL"] = Email;
        environment["GIT_COMMITTER_DATE"] = FixedDate;
        return environment;
    }

    private static string Said(ProcessAttempt attempt) =>
        attempt is ProcessAttempt.Completed done ? $"exit {done.Result.ExitCode}: {done.Result.Output}" : attempt.Describe;

    private static bool TryDelete(string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>The two readings a submodule needs, pure: the gitlinks <c>git ls-tree -r P</c> lists (mode 160000), and each submodule's url
/// from <c>.gitmodules</c> by its path.</summary>
public static class ScoutSubmodules
{
    public static IReadOnlyList<(string Path, string Sha)> Pinned(string lsTree) =>
        [.. lsTree.Split('\n').Select(line => line.Split('\t', 2)).Where(parts => parts.Length == 2 && parts[0].StartsWith("160000 commit ", StringComparison.Ordinal))
            .Select(parts => (parts[1].Trim(), parts[0]["160000 commit ".Length..].Trim()))];

    public static IReadOnlyDictionary<string, string> Urls(string gitmodules)
    {
        var sections = new List<Dictionary<string, string>>();

        foreach (var line in gitmodules.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            if (line.StartsWith('[') || sections.Count == 0)
            {
                sections.Add(new Dictionary<string, string>(StringComparer.Ordinal));
            }

            if (line.Split('=', 2) is [var key, var value])
            {
                sections[^1][key.Trim()] = value.Trim();
            }
        }

        return sections.Where(s => s.ContainsKey("path") && s.ContainsKey("url"))
            .GroupBy(s => s["path"], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last()["url"], StringComparer.Ordinal);
    }
}
