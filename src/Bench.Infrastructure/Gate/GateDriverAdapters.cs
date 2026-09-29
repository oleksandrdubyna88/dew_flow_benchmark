using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Gate;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Targets;
using Bench.Infrastructure.Git;

namespace Bench.Infrastructure.Gate;

/// <summary>The files an attempt's session left, read from the real filesystem.</summary>
public sealed class FileGateAttemptFiles : IGateAttemptFiles
{
    public Outcome<string> ReadPlan(string clone, string planPath)
    {
        var full = Path.GetFullPath(Path.Combine(clone, planPath));

        return File.Exists(full) && full.StartsWith(Path.GetFullPath(clone), StringComparison.OrdinalIgnoreCase)
            ? Outcome<string>.Success(File.ReadAllText(full))
            : Outcome<string>.Failure($"the plan {planPath} is not in the checkout at the variant head — a task's plan is committed there (an untracked plan is E7's export)");
    }

    public long SizeOf(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    public bool Exists(string path) => File.Exists(path);

    public string ReadFrom(string path, long offset)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(Math.Min(offset, stream.Length), SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public string SessionConfig(string dataDir, string repoPath, string branch) => SessionConfigReader.Read(dataDir, repoPath, branch);

    public IReadOnlyList<(HttpCallFacts Facts, IReadOnlyList<string> Files)> TapCalls(string tapDirectory)
    {
        if (!Directory.Exists(tapDirectory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(tapDirectory, "call-*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .GroupBy(TapCallFacts.CallNumber)
            .Where(g => g.Key > 0)
            .OrderBy(g => g.Key)
            .Select(g => (TapCallFacts.From(Text(tapDirectory, $"call-{g.Key:00}.json"), Text(tapDirectory, $"call-{g.Key:00}.response.json")),
                (IReadOnlyList<string>)[.. g.Order(StringComparer.Ordinal)]))];
    }

    public IReadOnlyList<(string Name, byte[] Bytes)> ShimFiles(IReadOnlyList<string> promptFiles)
    {
        var folders = promptFiles.Select(Path.GetDirectoryName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists);

        return [.. folders
            .SelectMany(d => Directory.EnumerateFiles(d, "api-*"))
            .OrderBy(File.GetLastWriteTimeUtc)
            .Select((f, i) => (GateAttemptRecord.SafeName(i + 1, Path.GetFileName(f)), File.ReadAllBytes(f)))];
    }

    private static string Text(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}

/// <summary>Where a cell's secret and its reviewer's references come from on THIS machine: the environment, through the
/// one <see cref="ISecretSource"/> — and, opt-in (<c>--creds-key-from-coai-settings</c>), the vault key from the
/// machine's coai <c>settings.json</c>, read into memory and never copied, because a server run from a shell does not get
/// the panel's key.</summary>
public sealed class GateSecrets(ISecretSource environment, bool credsKeyFromCoaiSettings, string coaiSettingsFile) : IGateSecrets
{
    public static string DefaultCoaiSettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "coai-mcp", "settings.json");

    public Outcome<SecretValue> CredsKey(GateReviewer reviewer)
    {
        if (credsKeyFromCoaiSettings)
        {
            return CoaiSettingsSecrets.CredsKey(coaiSettingsFile);
        }

        var name = reviewer.Definition.CredsKeyRef;

        return name.Length == 0
            ? Outcome<SecretValue>.Failure(
                $"reviewer '{reviewer.Id}' runs on the api runtime and names no creds-key reference — the product reads the vendor key from the vault "
                + "through COAI_CREDS_KEY; add --creds-key-ref to the row, or pass --creds-key-from-coai-settings")
            : environment.Resolve(name).Match(value => SecretValue.Of(value, name), Outcome<SecretValue>.Failure);
    }

    public Outcome<ResolvedReferences> References(GateReviewer reviewer)
    {
        var d = reviewer.Definition;
        var names = new[] { d.Endpoint is ReviewerEndpoint.Reference r ? r.Name : string.Empty, d.ExecutableRef }.Where(n => n.Length > 0);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            if (environment.Resolve(name) is not Outcome<string>.Ok ok)
            {
                return Outcome<ResolvedReferences>.Failure($"reviewer '{reviewer.Id}' names {name}, and it is unset on this machine");
            }

            values[name] = ok.Value;
        }

        return Outcome<ResolvedReferences>.Success(new ResolvedReferences(values));
    }
}

/// <summary>The machine's coai settings file as a secret source for ONE value: <c>env.COAI_CREDS_KEY</c> (or the same key
/// at the top level) — the calibration harness's <c>creds_key</c>. The value is returned as a
/// <see cref="SecretValue"/>; a refusal names the file and the key, never a value.</summary>
public static class CoaiSettingsSecrets
{
    public static Outcome<SecretValue> CredsKey(string settingsFile)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(settingsFile)) as JsonObject ?? [];
            var env = root["env"] as JsonObject ?? root;
            var value = env[CoaiEnvironment.CredsKeyVariable] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

            return SecretValue.Of(value, $"{CoaiEnvironment.CredsKeyVariable} in the coai settings file");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Outcome<SecretValue>.Failure($"the coai settings file could not be read ({ex.GetType().Name}) — the creds key cannot come from it");
        }
    }
}

/// <summary>The gate's own clones: one per run and task under <c>&lt;checkout-root&gt;/gate/&lt;runId&gt;/&lt;task&gt;</c>, made with
/// <c>git clone --shared --no-checkout</c> from the read-only worktree <see cref="ICheckoutProvider"/> keeps (objects
/// borrowed through alternates — a working tree, not a second object store), detached at the variant head. The run's
/// refs and the product's own worktrees are made THERE, so nothing another benchmark reads is written.</summary>
public sealed class GateCloneCheckouts(ICheckoutProvider provider, string checkoutRoot) : IGateCheckouts
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(10);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<Outcome<string>> EnsureAsync(Guid runId, GateTask task, CancellationToken cancellationToken)
    {
        var clone = Path.Combine(checkoutRoot, "gate", runId.ToString("D"), task.Id.Value);
        var gate = Gates.GetOrAdd(clone, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var made = Directory.Exists(Path.Combine(clone, ".git")) ? await VerifiedAsync(clone, task, cancellationToken) : await CloneAsync(clone, task, cancellationToken);

            return made is Outcome<string>.Ok && (HasSubmodules(clone) || task.Case.AbsentSubmodules.Count > 0) ? await SubmodulesAsync(clone, task, cancellationToken) : made;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Outcome<string>> CreateRefAsync(string clone, string branch, GateTask task, CancellationToken cancellationToken) =>
        (await GitCommand.RunAsync(clone, GitTimeout, cancellationToken, "branch", "-f", branch, task.Case.VariantHead.Value))
            .Match(_ => Outcome<string>.Success(branch), reason => Outcome<string>.Failure($"the run's ref {branch} could not be made — {reason}"));

    public Task<int> RemoveFinishedAsync(IReadOnlyCollection<Guid> finishedRuns, CancellationToken cancellationToken)
    {
        var removed = 0;

        foreach (var run in finishedRuns.Select(r => Path.Combine(checkoutRoot, "gate", r.ToString("D"))).Where(Directory.Exists))
        {
            DeleteTree(run);
            removed++;
        }

        return Task.FromResult(removed);
    }

    /// <summary>The read-only worktree the checkout provider keeps at the variant head — the same one every gate clone is
    /// cloned FROM, so it exists whenever a cell of the task ran; made when it does not.</summary>
    public Task<Outcome<string>> ReadOnlyAsync(GateTask task, CancellationToken cancellationToken) =>
        Target(task).Match(
            target => provider.EnsureAsync(target, cancellationToken),
            reason => Task.FromResult(Outcome<string>.Failure(reason)));

    /// <summary>An existing clone is reused only at the variant head. One interrupted between its clone and its checkout
    /// (a failure, a timeout, a Ctrl+C) has no working tree or the wrong one; it is checked out again, and made anew when
    /// even that fails — never handed on as it lies to fail every later cell of the task.</summary>
    private async Task<Outcome<string>> VerifiedAsync(string clone, GateTask task, CancellationToken cancellationToken)
    {
        var head = await GitCommand.ReadAsync(clone, GitTimeout, cancellationToken, "rev-parse", "HEAD");

        if (head is Outcome<string>.Ok ok && ok.Value.Trim() == task.Case.VariantHead.Value && File.Exists(Path.Combine(clone, task.Case.PlanPath)))
        {
            return Outcome<string>.Success(clone);
        }

        var repaired = await GitCommand.RunAsync(clone, GitTimeout, cancellationToken, "-c", "advice.detachedHead=false", "checkout", "--quiet", "--force", "--detach", task.Case.VariantHead.Value);

        if (repaired is Outcome<string>.Ok)
        {
            return Outcome<string>.Success(clone);
        }

        DeleteTree(clone);
        return await CloneAsync(clone, task, cancellationToken);
    }

    private async Task<Outcome<string>> CloneAsync(string clone, GateTask task, CancellationToken cancellationToken)
    {
        var source = ReadOnlyAsync(task, cancellationToken);

        if (await source is not Outcome<string>.Ok { Value: var worktree })
        {
            return Outcome<string>.Failure($"task '{task.Id}': its checkout at the variant head could not be made — {((Outcome<string>.Fail)await source).Reason}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(clone)!);
        var cloned = await GitCommand.RunAsync(checkoutRoot, GitTimeout, cancellationToken, "clone", "--shared", "--no-checkout", "--quiet", "--config", AsCommitted, worktree, clone);
        var checkedOut = cloned is Outcome<string>.Ok
            ? await GitCommand.RunAsync(clone, GitTimeout, cancellationToken, "-c", "advice.detachedHead=false", "checkout", "--quiet", "--detach", task.Case.VariantHead.Value)
            : cloned;

        return checkedOut.Match(_ => Outcome<string>.Success(clone), reason => Outcome<string>.Failure($"task '{task.Id}': the gate's clone could not be made — {reason}"));
    }

    /// <summary>The submodules the variant head pins, at the commits it pins — idempotent, so a reused clone and one
    /// interrupted before this step both end whole. The product reads a repository's rules from the tree it is handed; a
    /// submodule left empty tells the reviewer the rules are not there (E7's A/A, 2026-09-28: none of twelve against the
    /// calibration's eight). The same line-ending setting rides into the submodules' own clones; a local path is a url a
    /// suite may name, so the file transport is allowed here. Run only where the tree HAS a <c>.gitmodules</c>: the step is
    /// a git process inside the clone's lock, and paying it on every cell of a repository without submodules serialised
    /// the lanes (the campaign's concurrency tests went red on it).
    /// <para>
    /// The ones the task declares ABSENT (<see cref="GateCase.AbsentSubmodules"/>) are left uninitialised and never fetched
    /// — ts2's rules url no longer resolves, and the calibration measured it with the folder empty. A declaration the head
    /// does not bear out is refused naming the path; an undeclared submodule that cannot be fetched stays a refusal, so a
    /// failed fetch is never read as "measure it empty".
    /// </para></summary>
    private static async Task<Outcome<string>> SubmodulesAsync(string clone, GateTask task, CancellationToken cancellationToken)
    {
        var pinned = HasSubmodules(clone) ? await PinnedAsync(clone, cancellationToken) : Outcome<IReadOnlyList<string>>.Success([]);
        if (pinned is not Outcome<IReadOnlyList<string>>.Ok { Value: var paths })
        {
            return Outcome<string>.Failure($"task '{task.Id}': its .gitmodules could not be read — {((Outcome<IReadOnlyList<string>>.Fail)pinned).Reason}");
        }

        var unpinned = task.Case.AbsentSubmodules.Except(paths, StringComparer.Ordinal).ToList();
        var wanted = paths.Except(task.Case.AbsentSubmodules, StringComparer.Ordinal).ToList();

        return (unpinned.Count, wanted.Count) switch
        {
            ( > 0, _) => Outcome<string>.Failure($"task '{task.Id}' declares absent submodule '{unpinned[0]}', which its variant head does not pin — the suite describes another tree"),
            (_, 0) => Outcome<string>.Success(clone),
            _ => await UpdateAsync(clone, task, wanted, cancellationToken),
        };
    }

    private static async Task<Outcome<string>> UpdateAsync(string clone, GateTask task, IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        (await GitCommand.RunAsync(clone, GitTimeout, cancellationToken, ["-c", AsCommitted, "-c", "protocol.file.allow=always", "submodule", "update", "--init", "--recursive", "--quiet", "--", .. paths]))
            .Match(_ => Outcome<string>.Success(clone), reason => Outcome<string>.Failure($"task '{task.Id}': the submodules its variant head pins could not be checked out — {reason}"));

    /// <summary>Every submodule path <c>.gitmodules</c> names at the checked-out head, <c>/</c>-separated as git writes them.</summary>
    private static async Task<Outcome<IReadOnlyList<string>>> PinnedAsync(string clone, CancellationToken cancellationToken) =>
        (await GitCommand.ReadAsync(clone, GitTimeout, cancellationToken, "config", "--file", ".gitmodules", "--get-regexp", @"^submodule\..*\.path$"))
            .Match(
                text => Outcome<IReadOnlyList<string>>.Success([.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => l[(l.IndexOf(' ') + 1)..])]),
                Outcome<IReadOnlyList<string>>.Failure);

    private static bool HasSubmodules(string clone) => File.Exists(Path.Combine(clone, ".gitmodules"));

    /// <summary>The committed bytes, whatever the machine's global <c>core.autocrlf</c> says: a clone inheriting
    /// <c>true</c> handed the product a CRLF plan the calibration's checkout had as LF (E7's A/A, 2026-09-28).</summary>
    private const string AsCommitted = "core.autocrlf=false";

    /// <summary>The task's repository as a target the checkout provider takes: a url as it is, a local path as
    /// <c>file://</c>.</summary>
    private static Outcome<MeasurementTarget> Target(GateTask task)
    {
        var location = task.Repository.Path;
        var url = Directory.Exists(location) ? new Uri(Path.GetFullPath(location)).AbsoluteUri : location;

        return RepoUrl.Parse(url).Match(repo => Outcome<MeasurementTarget>.Success(MeasurementTarget.At(repo, task.Case.VariantHead)), Outcome<MeasurementTarget>.Failure);
    }

    /// <summary>A git tree has read-only object files on Windows; clear the attribute, then delete.</summary>
    private static void DeleteTree(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
