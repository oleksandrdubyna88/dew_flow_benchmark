using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;

namespace ModelArms.Adapters;

/// <summary>What the task pipeline launches (D17 d): the REAL path of git <c>run-arm</c> resolved, the target's build paths and the verb's parent environment — handed to <see cref="ArmGit"/> as it is (which reduces it) and, reduced by <see cref="TargetEnvironment"/>, to the warm build.</summary>
internal sealed record TaskTools(string Git, ToolPaths Build, IReadOnlyDictionary<string, string> Environment);

/// <summary>Where one task of an entered batch stands: it runs in <paramref name="Folder"/> (its worktree, or the clone for a batch of one) or it is picked back already.</summary>
internal abstract record BatchPlace
{
    private BatchPlace()
    {
    }

    public sealed record Run(string Id, string Folder) : BatchPlace;

    public sealed record Picked(string Id) : BatchPlace;
}

/// <summary>The answer of a batch's entry: ready, or the arm stops with this exit code (D16).</summary>
internal abstract record BatchEntry
{
    private BatchEntry()
    {
    }

    public sealed record Ready(IReadOnlyList<BatchPlace> Places) : BatchEntry;

    public sealed record Stopped(int ExitCode, string Why) : BatchEntry;
}

/// <summary>The places in the pipeline a test can stand in (the <see cref="StepPoint"/> pattern): a probe that throws there is a runner killed at that point. <see cref="Committed"/> is the window after the cherry-pick succeeded and before its completion line is written.</summary>
internal enum TaskPoint
{
    Entered,
    Aborted,
    Read,
    Acted,
    Added,
    SubmoduleUpdated,
    Linked,
    Built,
    Intended,
    Picked,
    Committed,
    Recorded,
    Marked,
    Dropped,
}

/// <summary>A seam production never uses: <see cref="None"/> does nothing at every point.</summary>
internal class TaskProbe
{
    public static TaskProbe None { get; } = new();

    public virtual void Reached(TaskPoint point, string id)
    {
    }
}


/// <summary>The task pipeline's worktrees and pick-back (plan D14, D22, §4.6; E4.S2, C20). Every git it starts is a question on <see cref="ArmGit"/> (the real path <c>run-arm</c> resolved, a replaced environment, a chosen folder); every write of the arm's clone is neutralised (filters; the cherry-pick also merge drivers),
/// every file is asked for BY NAME (<see cref="KernelFacts.Present"/>) and read through <see cref="BoundedFile"/>, every log is created new. A stop that names a task writes the task's stop line (<see cref="StepLog.Stop"/>); a stop of the BATCH (the clone is not a repository, a stale <c>index.lock</c>, an abort that cannot clear the
/// clone, an enumeration that cannot run) names none and writes one log line.
/// <para>
/// <b>Re-entry reads before it cleans</b>: every task of the batch is read first (<see cref="TaskState"/>), <see cref="TaskRecovery.Decide"/> answers for all of them, any refusal stops the batch before ANY action, and only then does each task take its one action. <b>The pick</b> is one commit per <c>cherry-pick</c>, an
/// INTENT line before it and a COMPLETION after it in <c>out/pick-&lt;id&gt;.json</c>, the marker <c>picked-&lt;id&gt;</c> last.
/// </para>
/// <para>
/// What this does NOT do: run a task's step or decide the batches (E4.S3); serialise with a model process that still runs in the clone (E3 §8 item 17); bound a build's output in memory (E3 §8 item 18; only the log is capped); observe LFS worktrees, Windows or a git below the pilot's two (module doc).
/// </para></summary>
internal sealed partial class TaskWorktrees
{
    private const int RecordLimit = 64 * 1024;
    private const int BeforeLimit = 4096;
    private const long BuildLogCap = 1_048_576;

    private static readonly string[] ModelLeft = ["MERGE_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply"];

    private readonly ArmPaths _paths;
    private readonly TargetFacts _target;
    private readonly TaskTools _tools;
    private readonly StepLog _log;
    private readonly TaskProbe _probe;
    private readonly TimeProvider _clock;
    private readonly ArmFiles _files;

    public TaskWorktrees(ArmPaths paths, TargetFacts target, TaskTools tools, StepLog log)
        : this(paths, target, tools, log, TaskProbe.None)
    {
    }

    internal TaskWorktrees(ArmPaths paths, TargetFacts target, TaskTools tools, StepLog log, TaskProbe probe, TimeProvider? clock = null)
    {
        _paths = paths;
        _target = target;
        _tools = tools;
        _log = log;
        _probe = probe;
        _clock = clock ?? TimeProvider.System;
        _files = new ArmFiles(paths);
    }

    /// <summary>Why the pipeline stops: the exit code (D16), the words, and the task it names (<c>null</c>: the batch).</summary>
    private sealed record Halt(int Code, string Why, string? Id);

    /// <summary>Everything one task's re-entry reads before anything is touched.</summary>
    private sealed record TaskRead(TaskState State, string BeforeSha);

    private ArmGit Clone => new(_tools.Git, _paths.CloneFolder, _tools.Environment);

    private ArmGit In(string folder) => new(_tools.Git, folder, _tools.Environment);

    private string GitFolder => Path.Combine(_paths.CloneFolder, ".git");

    private string LockFile => Path.Combine(GitFolder, "index.lock");

    // ---- the batch ----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Parent §4.6 re-entry steps 1–3 for a batch without <c>batch-&lt;i&gt;.done</c>; a batch of ONE runs in the clone itself (<c>t-lib.sh:64-65</c>): no worktree, no pick, nothing asked.</summary>
    public async Task<BatchEntry> EnterAsync(IReadOnlyList<string> batch, Func<string, StepEntry> recovery, CancellationToken ct)
    {
        if (batch.Count == 1)
        {
            return new BatchEntry.Ready([new BatchPlace.Run(batch[0], _paths.CloneFolder)]);
        }

        _probe.Reached(TaskPoint.Entered, string.Empty);
        var places = new List<BatchPlace>();
        var halt = await EnterBatchAsync(batch, recovery, places, ct);
        if (halt is null)
        {
            return new BatchEntry.Ready(places);
        }

        if (halt.Id is { } id)
        {
            _log.Stop(TaskStems.Coder(id), halt.Code, halt.Why);
        }
        else
        {
            var why = $"batch {string.Join(' ', batch)}: {halt.Why}";
            _log.Line(why);
            return new BatchEntry.Stopped(halt.Code, why);
        }

        return new BatchEntry.Stopped(halt.Code, halt.Why);
    }

    private async Task<Halt?> EnterBatchAsync(IReadOnlyList<string> batch, Func<string, StepEntry> recovery, List<BatchPlace> places, CancellationToken ct)
    {
        if (await CloneStateAsync(ct) is { } stateHalt)
        {
            return stateHalt;
        }

        var listed = await Clone.WorktreeListAsync(ct);
        if (listed is Outcome<IReadOnlyList<WorktreeEntry>>.Fail listFailed)
        {
            return Failed(null, listFailed.Reason);
        }

        if (await WarnAboutLfsAsync(ct) is { } lfsHalt)
        {
            return lfsHalt;
        }

        var entries = ((Outcome<IReadOnlyList<WorktreeEntry>>.Ok)listed).Value;
        var reads = new List<TaskRead>();
        foreach (var id in batch)
        {
            var read = await ReadAsync(id, recovery(id), entries, ct);
            if (read.Halt is not null)
            {
                return read.Halt;
            }

            reads.Add(read.Read!);
        }

        _probe.Reached(TaskPoint.Read, string.Empty);
        var actions = reads.Select(read => (Read: read, Action: TaskRecovery.Decide(read.State))).ToList();
        if (actions.Select(pair => pair.Action).OfType<TaskAction.Refuse>().FirstOrDefault() is { } refused)
        {
            return new Halt(ExitCodes.Configuration, $"{refused.Id}: {refused.Why}", refused.Id);
        }

        foreach (var (read, action) in actions)
        {
            if (await ActAsync(read, action, ct) is { } actHalt)
            {
                return actHalt;
            }
        }

        _probe.Reached(TaskPoint.Acted, string.Empty);
        places.AddRange(actions.Select(pair => pair.Action is TaskAction.Drop or TaskAction.Nothing && pair.Read.State.Picked ? (BatchPlace)new BatchPlace.Picked(pair.Read.State.Id) : new BatchPlace.Run(pair.Read.State.Id, _paths.Worktree(pair.Read.State.Id))));
        return null;
    }

    /// <summary>The clone before anything about the batch is asked: a repository rooted at its own <c>.git</c>, no stale <c>index.lock</c> (§8 item 4), the model's own unfinished operations reported and left, and a cherry-pick a crash left in progress aborted — a recovery, never a conflict (D22).</summary>
    private async Task<Halt?> CloneStateAsync(CancellationToken ct)
    {
        if (Look(Path.Combine(GitFolder, "HEAD"), out var rooted) is { } lookHalt)
        {
            return lookHalt;
        }

        if (!rooted)
        {
            return new Halt(ExitCodes.Environment, $"{_paths.CloneFolder} is not a repository rooted at {GitFolder}", null);
        }

        if (LockHalt(null) is { } locked)
        {
            return locked;
        }

        foreach (var name in ModelLeft)
        {
            if (Look(Path.Combine(GitFolder, name), out var left) is { } leftHalt)
            {
                return leftHalt;
            }

            if (left)
            {
                _log.Line($"the clone has .git/{name} — left as the model left it");
            }
        }

        var inProgress = new List<string>();
        foreach (var name in new[] { "CHERRY_PICK_HEAD", "sequencer" })
        {
            if (Look(Path.Combine(GitFolder, name), out var there) is { } thereHalt)
            {
                return thereHalt;
            }

            if (there)
            {
                inProgress.Add(name == "sequencer" ? "sequencer/" : name);
            }
        }

        if (inProgress.Count > 0)
        {
            if (await Clone.CherryPickAbortAsync(ct) is Outcome<string>.Fail abortFailed)
            {
                return Failed(null, $"cherry-pick --abort failed: {abortFailed.Reason}");
            }

            _log.Line($"the clone had a cherry-pick in progress ({string.Join(", ", inProgress)}) — aborted; not a conflict");
            foreach (var name in new[] { "CHERRY_PICK_HEAD", "sequencer" })
            {
                if (Look(Path.Combine(GitFolder, name), out var still) is { } stillHalt)
                {
                    return stillHalt;
                }

                if (still)
                {
                    return new Halt(ExitCodes.Environment, $"{Path.Combine(GitFolder, name)} is still there after cherry-pick --abort — clear the clone's state by hand, then run me again", null);
                }
            }
        }

        _probe.Reached(TaskPoint.Aborted, string.Empty);
        return null;
    }

    /// <summary>Filters are neutralised, so an LFS target's worktree holds pointer files: said once, never refused (git-lfs is unobserved).</summary>
    private async Task<Halt?> WarnAboutLfsAsync(CancellationToken ct)
    {
        var drivers = await Clone.FilterDriversAsync(ct);
        if (drivers is Outcome<IReadOnlyList<string>>.Fail failed)
        {
            return Failed(null, failed.Reason);
        }

        if (((Outcome<IReadOnlyList<string>>.Ok)drivers).Value.Contains("lfs", StringComparer.Ordinal))
        {
            _log.Line("WARNING: the clone's config names an lfs filter driver — it is neutralised for the worktrees, so their LFS files are pointer files and a build that needs their content fails (unobserved)");
        }

        return null;
    }

    private async Task<(TaskRead? Read, Halt? Halt)> ReadAsync(string id, StepEntry entry, IReadOnlyList<WorktreeEntry> entries, CancellationToken ct)
    {
        var step = TaskRecovery.StepOf(entry);
        var tip = await Clone.BranchTipAsync(TmpPrefix + id, ct);
        if (tip is Outcome<string>.Fail tipFailed)
        {
            return (null, FailedRead(id, tipFailed.Reason));
        }

        var branch = ((Outcome<string>.Ok)tip).Value.Length > 0;
        var wt = _paths.Worktree(id);
        if (Look(_paths.Picked(id), out var picked) is { } pickedHalt)
        {
            return (null, pickedHalt);
        }

        if (Look(_paths.TaskBefore(id), out var beforePresent) is { } beforeHalt)
        {
            return (null, beforeHalt);
        }

        if (Look(Path.Combine(wt, ".git"), out var folder) is { } folderHalt)
        {
            return (null, folderHalt);
        }

        if (Look(wt, out var anything) is { } anythingHalt)
        {
            return (null, anythingHalt);
        }

        var registration = entries.FirstOrDefault(candidate => SamePath(candidate.Path, wt));
        WorktreeState worktree = registration is not null
            ? new WorktreeState.Registered(folder && !registration.Prunable, registration.Branch == "refs/heads/" + TmpPrefix + id)
            : anything ? new WorktreeState.Stray() : new WorktreeState.Gone();

        var beforeSha = beforePresent ? ReadBefore(id) : string.Empty;
        var descends = false;
        if (step == TaskStep.Resume && branch && beforePresent && beforeSha.Length > 0)
        {
            var known = await Clone.CommitPresentAsync(beforeSha, ct);
            if (known is Outcome<string>.Fail knownFailed)
            {
                return (null, FailedRead(id, knownFailed.Reason));
            }

            if (((Outcome<string>.Ok)known).Value.Length > 0)
            {
                var ancestor = await Clone.IsAncestorAsync(beforeSha, TmpPrefix + id, ct);
                if (ancestor is Outcome<bool>.Fail ancestorFailed)
                {
                    return (null, FailedRead(id, ancestorFailed.Reason));
                }

                descends = ((Outcome<bool>.Ok)ancestor).Value;
            }
        }

        if (step == TaskStep.Done && !picked && LoadRecord(id).Halt is { } recordHalt)
        {
            return (null, recordHalt);
        }

        return (new TaskRead(new TaskState(id, step, branch, picked, worktree, beforePresent, descends), beforeSha), null);
    }

    private async Task<Halt?> ActAsync(TaskRead read, TaskAction action, CancellationToken ct)
    {
        var id = read.State.Id;
        switch (action)
        {
            case TaskAction.Recreate:
                return await RecreateAsync(read, ct);
            case TaskAction.Keep:
                return await KeepAsync(read, ct);
            case TaskAction.Reattach reattach:
                return await ReattachAsync(read, reattach.Build, ct);
            case TaskAction.Drop:
                return await RemoveAsync(id, read.State.Worktree is not WorktreeState.Gone, read.State.BranchPresent, ct);
            default:
                return null;
        }
    }

    /// <summary>A task whose session never started: its worktree and branch removed (forcing twice, pruning, deleting), its <c>.before</c> deleted by name with them (§8 item 9), a fresh worktree from the current HEAD.</summary>
    private async Task<Halt?> RecreateAsync(TaskRead read, CancellationToken ct)
    {
        var id = read.State.Id;
        var wt = _paths.Worktree(id);
        if (read.State.BranchPresent || read.State.Worktree is not WorktreeState.Gone)
        {
            if (await RemoveAsync(id, read.State.Worktree is WorktreeState.Registered { FolderPresent: true } or WorktreeState.Stray, read.State.BranchPresent, ct) is { } removeHalt)
            {
                return removeHalt;
            }

            _log.Line($"{id}: recreated — its worktree and branch were removed (not done, not picked)");
        }

        File.Delete(_paths.TaskBefore(id));
        if (await Clone.WorktreeAddAsync(wt, TmpPrefix + id, ct) is Outcome<string>.Fail addFailed)
        {
            return Failed(id, addFailed.Reason);
        }

        _probe.Reached(TaskPoint.Added, id);
        if (await DressAsync(id, build: true, ct) is { } dressHalt)
        {
            return dressHalt;
        }

        _log.Line($"worktree for {id} ready");
        return null;
    }

    private async Task<Halt?> KeepAsync(TaskRead read, CancellationToken ct)
    {
        var id = read.State.Id;
        var count = await CountPastBeforeAsync(read, ct);
        if (count.Halt is not null)
        {
            return count.Halt;
        }

        _log.Line($"{id}: kept — its session resumes in its worktree ({count.Count} commit(s) on {TmpPrefix}{id} past its .before)");
        return null;
    }

    /// <summary>The folder is gone, the registration and the branch intact: prune FIRST (a dead registration refuses the add with 128, M-t; <c>-f</c> is never used — it overrides a live one too), attach to the EXISTING branch, dress it, and build only for a resumed task.</summary>
    private async Task<Halt?> ReattachAsync(TaskRead read, bool build, CancellationToken ct)
    {
        var id = read.State.Id;
        if (await Clone.WorktreePruneAsync(ct) is Outcome<string>.Fail pruneFailed)
        {
            return Failed(id, pruneFailed.Reason);
        }

        if (await Clone.WorktreeAttachAsync(_paths.Worktree(id), TmpPrefix + id, ct) is Outcome<string>.Fail attachFailed)
        {
            return Failed(id, attachFailed.Reason);
        }

        _probe.Reached(TaskPoint.Added, id);
        if (await DressAsync(id, build, ct) is { } dressHalt)
        {
            return dressHalt;
        }

        if (!build)
        {
            _log.Line($"{id}: reattached to {TmpPrefix}{id} (its worktree was gone)");
            return null;
        }

        var count = await CountPastBeforeAsync(read, ct);
        if (count.Halt is not null)
        {
            return count.Halt;
        }

        _log.Line($"{id}: reattached to {TmpPrefix}{id} (its worktree folder was gone — its uncommitted edits are lost; its {count.Count} commit(s) are on the branch)");
        return null;
    }

    private async Task<(int Count, Halt? Halt)> CountPastBeforeAsync(TaskRead read, CancellationToken ct)
    {
        var listed = await Clone.RevListAsync(read.BeforeSha, TmpPrefix + read.State.Id, ct);
        return listed is Outcome<IReadOnlyList<string>>.Ok ok ? (ok.Value.Count, null) : (0, Failed(read.State.Id, ((Outcome<IReadOnlyList<string>>.Fail)listed).Reason));
    }

    // ---- the worktree's dressing --------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The submodule from the arm's own checkout, the <c>node_modules</c> links, the warm build — in that order (the links' parents are the submodule's folders).</summary>
    private async Task<Halt?> DressAsync(string id, bool build, CancellationToken ct)
    {
        var wt = _paths.Worktree(id);
        foreach (var entry in _target.NodeModules)
        {
            if (Path.IsPathRooted(entry) || entry.Replace('\\', '/').Split('/').Contains(".."))
            {
                return new Halt(ExitCodes.Configuration, $"{id}: target.nodeModules entry '{entry}' is rooted or climbs out of the worktree — refused", id);
            }
        }

        var listed = await Clone.SubmodulePathsAsync(ct);
        if (listed is Outcome<string>.Fail listFailed)
        {
            return Failed(id, listFailed.Reason);
        }

        var name = GitModules.NameOf(((Outcome<string>.Ok)listed).Value, _target.RulesSubmodule);
        if (name.Length == 0)
        {
            return new Halt(ExitCodes.Configuration, $"{id}: target.rulesSubmodule {_target.RulesSubmodule} is not a submodule of the arm's clone", id);
        }

        if (await Clone.SetSubmoduleUrlAsync(name, Path.Combine(_paths.CloneFolder, _target.RulesSubmodule), ct) is Outcome<string>.Fail urlFailed)
        {
            return Failed(id, urlFailed.Reason);
        }

        if (await In(wt).ArmSubmoduleUpdateAsync(_target.RulesSubmodule, ct) is Outcome<string>.Fail updateFailed)
        {
            return Failed(id, updateFailed.Reason);
        }

        _probe.Reached(TaskPoint.SubmoduleUpdated, id);
        foreach (var entry in _target.NodeModules)
        {
            if (Link(id, entry) is { } linkHalt)
            {
                return linkHalt;
            }
        }

        _probe.Reached(TaskPoint.Linked, id);
        if (build && await BuildAsync(id, ct) is { } buildHalt)
        {
            return buildHalt;
        }

        _probe.Reached(TaskPoint.Built, id);
        return null;
    }

    /// <summary>One link <c>&lt;wt&gt;/&lt;entry&gt;</c> → <c>&lt;clone&gt;/&lt;entry&gt;</c>, only for a folder the clone has, made exclusively, and only through real folders of the worktree: <c>symlink(2)</c> resolves the parent path THROUGH links (M-w) and the worktree's tree is the arm branch's — the model's —
    /// so EVERY existing component of the link's parent, from the worktree down, is read with lstat semantics and a link among them stops the arm (D26 a, §8 item 24). The clone's side is the model's own and gets no check.</summary>
    private Halt? Link(string id, string entry)
    {
        var wt = _paths.Worktree(id);
        var source = Path.Combine(_paths.CloneFolder, entry);
        if (!Directory.Exists(source))
        {
            _log.Line($"no {entry} in the clone — not linked");
            return null;
        }

        var link = Path.Combine(wt, entry);
        var parent = Path.GetDirectoryName(link)!;
        var relative = Path.GetRelativePath(wt, parent);
        var current = wt;
        var components = relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar);
        foreach (var component in new[] { string.Empty }.Concat(components))
        {
            current = component.Length == 0 ? wt : Path.Combine(current, component);
            if (!Directory.Exists(current) && !Path.Exists(current))
            {
                break;
            }

            if (RootFacts.LinkTargetOf(current) is { } target)
            {
                var label = component.Length == 0 ? $"wt/{id}" : Path.GetRelativePath(wt, current);
                return new Halt(ExitCodes.Configuration, $"{id}: {label} in wt/{id} is a symbolic link (→ {target}) — a node_modules link is made only through real folders of the worktree", id);
            }
        }

        if (!Directory.Exists(parent))
        {
            _log.Line($"{id}: {relative} is not in the worktree — {entry} not linked");
            return null;
        }

        try
        {
            File.CreateSymbolicLink(link, source);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Halt(ExitCodes.Environment, $"{id}: cannot link {entry}: {ex.Message}", id);
        }
    }

    /// <summary>The warm build (<c>t-lib.sh:19</c>) through the target's own wrapper and tools, under the target's environment, into <c>out/wt-build-&lt;id&gt;.log</c>: the old log deleted by name, the new one created exclusively before the child starts and capped where it is written (§8 item 19). A failure, a kill at the wall or a start
    /// failure stops the arm with exit 3 and leaves the worktree (D16 — the shell only warned).</summary>
    private async Task<Halt?> BuildAsync(string id, CancellationToken ct)
    {
        var path = _paths.WorktreeBuildLog(id);
        Directory.CreateDirectory(_paths.Out);
        File.Delete(path);
        if (ToolLog.Create(path) is not Outcome<ToolLog>.Ok created)
        {
            return new Halt(ExitCodes.Environment, $"{id}: {((Outcome<ToolLog>.Fail)ToolLog.Create(path)).Reason}", id);
        }

        using var log = created.Value;
        var capped = new CappedStream(log.Stream, BuildLogCap);
        var run = await new TargetTools(TargetEnvironment.Of(_tools.Environment), _clock).RunAsync(TargetArgv.Build(_target, _tools.Build, _paths.Worktree(id)), capped, ct);
        capped.Finish();
        return run is { Started: true, KilledAtWall: false, Exit: { WasCaptured: true, Value: 0 } }
            ? null
            : new Halt(ExitCodes.Environment, $"the warm build of {id}'s worktree failed (see out/wt-build-{id}.log)", id);
    }

    // ---- the drop -----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>After the pick-back: <c>remove --force --force</c> (only when the folder is there) → <c>prune</c> → <c>branch -D</c> (only when the branch is there). Each a no-op by READING, never by parsing git's words; a stray non-worktree folder is git's 128 <i>is not a working tree</i> → exit 3 naming the folder.</summary>
    public async Task<StepOutcome.Stopped?> DropAsync(string id, CancellationToken ct)
    {
        var halt = await RemoveAsync(id, folderIsThere: null, branchPresent: null, ct);
        return halt is null ? null : _log.Stop(TaskStems.Coder(id), halt.Code, halt.Why);
    }

    /// <summary>The three removals. What the batch's entry already read (<paramref name="folderIsThere"/>, <paramref name="branchPresent"/>) is not asked again — a read after the first removal would break "every task is read before any cleanup"; the public drop passes <c>null</c> and reads.</summary>
    private async Task<Halt?> RemoveAsync(string id, bool? folderIsThere, bool? branchPresent, CancellationToken ct)
    {
        var wt = _paths.Worktree(id);
        if (folderIsThere is null)
        {
            if (Look(Path.Combine(wt, ".git"), out var present) is { } lookHalt)
            {
                return lookHalt;
            }

            folderIsThere = present;
        }

        if (folderIsThere == true && await Clone.WorktreeRemoveAsync(wt, ct) is Outcome<string>.Fail removeFailed)
        {
            return Failed(id, $"{removeFailed.Reason} (worktree {wt})");
        }

        if (await Clone.WorktreePruneAsync(ct) is Outcome<string>.Fail pruneFailed)
        {
            return Failed(id, pruneFailed.Reason);
        }

        if (branchPresent is null)
        {
            var tip = await Clone.BranchTipAsync(TmpPrefix + id, ct);
            if (tip is Outcome<string>.Fail tipFailed)
            {
                return Failed(id, tipFailed.Reason);
            }

            branchPresent = ((Outcome<string>.Ok)tip).Value.Length > 0;
        }

        if (branchPresent == true && await Clone.DeleteBranchAsync(TmpPrefix + id, ct) is Outcome<string>.Fail deleteFailed)
        {
            return Failed(id, deleteFailed.Reason);
        }

        _probe.Reached(TaskPoint.Dropped, id);
        return null;
    }

    // ---- the pick-back ------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>D22: the task's commits (<c>task-&lt;id&gt;.before..tmp/&lt;id&gt;</c>) onto the arm branch, ONE commit per <c>cherry-pick</c>, an intent line before it and a completion after it, the marker <c>picked-&lt;id&gt;</c> LAST. <c>null</c>: carried.</summary>
    public async Task<StepOutcome.Stopped?> PickBackAsync(string id, CancellationToken ct)
    {
        var halt = await PickAsync(id, ct);
        return halt is null ? null : _log.Stop(TaskStems.Coder(id), halt.Code, halt.Why);
    }

    private async Task<Halt?> PickAsync(string id, CancellationToken ct)
    {
        if (Look(_paths.Picked(id), out var picked) is { } pickedHalt)
        {
            return pickedHalt;
        }

        if (picked)
        {
            return null;
        }

        if (Look(_paths.TaskBefore(id), out var hasBefore) is { } beforeHalt)
        {
            return beforeHalt;
        }

        var before = hasBefore ? ReadBefore(id) : string.Empty;
        if (before.Length == 0)
        {
            return new Halt(ExitCodes.Configuration, $"{id}: out/task-{id}.before is missing — the pick has no range", id);
        }

        if (await WarnAboutWorktreeAsync(id, ct) is { } warnHalt)
        {
            return warnHalt;
        }

        var loaded = LoadRecord(id);
        if (loaded.Halt is not null)
        {
            return loaded.Halt;
        }

        var record = Path.GetFileName(_paths.PickRecord(id));
        var entries = loaded.Valid.ToList();
        if (loaded.Torn)
        {
            _log.Line($"{record}: the last line is torn — ignored and removed");
            _files.Replace(_paths.PickRecord(id), PickRecord.Write(entries));
        }

        var sources = await Clone.RevListAsync(before, TmpPrefix + id, ct);
        var onBranch = await Clone.RevListAsync(before, ct);
        if (sources is Outcome<IReadOnlyList<string>>.Fail sourcesFailed || onBranch is Outcome<IReadOnlyList<string>>.Fail)
        {
            return Failed(id, sources is Outcome<IReadOnlyList<string>>.Fail failedSources ? failedSources.Reason : ((Outcome<IReadOnlyList<string>>.Fail)onBranch).Reason);
        }

        var taskShas = ((Outcome<IReadOnlyList<string>>.Ok)sources).Value;
        var branchShas = ((Outcome<IReadOnlyList<string>>.Ok)onBranch).Value;
        var ids = await Clone.PatchIdsAsync([.. taskShas.Concat(branchShas).Distinct(StringComparer.Ordinal)], ct);
        if (ids is Outcome<IReadOnlyList<PickSource>>.Fail idsFailed)
        {
            return Failed(id, idsFailed.Reason);
        }

        var byCommit = ((Outcome<IReadOnlyList<PickSource>>.Ok)ids).Value.ToDictionary(source => source.Sha, StringComparer.Ordinal);
        if (await SettleOpenIntentAsync(id, entries, byCommit, ct) is { } intentHalt)
        {
            return intentHalt;
        }

        _probe.Reached(TaskPoint.Read, id);
        var plan = PickBackOrder.Decide([.. taskShas.Select(sha => byCommit[sha])], entries, [.. branchShas.Select(sha => byCommit[sha])]);
        foreach (var unbelieved in plan.NotBelieved)
        {
            _log.Line($"{record} line {entries.IndexOf(unbelieved) + 1}: {unbelieved.PickedSha} is not on the arm branch — not believed");
        }

        int byRecord = 0, byPatch = 0, made = 0;
        foreach (var step in plan.Steps)
        {
            if (step is PickStep.Carried carried)
            {
                _ = carried.By == "the record" ? byRecord++ : byPatch++;
                continue;
            }

            var pick = (PickStep.Pick)step;
            if (await PickOneAsync(id, pick, entries, ct) is { } pickHalt)
            {
                return pickHalt;
            }

            made++;
        }

        _files.WriteOnce(_paths.Picked(id), PickRecord.Write(entries));
        _probe.Reached(TaskPoint.Marked, id);
        _log.Line($"{id}: {made} commit(s) carried over ({byRecord} already carried by the record, {byPatch} by patch-id)");
        return null;
    }

    /// <summary>The worktree, when it is there: a branch other than <c>tmp/&lt;id&gt;</c> and uncommitted changes are WARNINGS (<c>t-lib.sh:29</c>) — only the branch is carried.</summary>
    private async Task<Halt?> WarnAboutWorktreeAsync(string id, CancellationToken ct)
    {
        var wt = _paths.Worktree(id);
        if (Look(Path.Combine(wt, ".git"), out var there) is { } lookHalt)
        {
            return lookHalt;
        }

        if (!there)
        {
            return null;
        }

        var branch = await In(wt).CurrentBranchAsync(ct);
        if (branch is Outcome<string>.Fail branchFailed)
        {
            return Failed(id, branchFailed.Reason);
        }

        var name = ((Outcome<string>.Ok)branch).Value;
        if (name != TmpPrefix + id)
        {
            _log.Line($"WARNING: {id}'s worktree is on {(name.Length == 0 ? "a detached HEAD" : name)}, not {TmpPrefix}{id} — only {TmpPrefix}{id} is carried");
        }

        var left = await In(wt).LeftBehindAsync(_target.NodeModules, ct);
        if (left is Outcome<int>.Fail leftFailed)
        {
            return Failed(id, leftFailed.Reason);
        }

        if (((Outcome<int>.Ok)left).Value is var count and > 0)
        {
            _log.Line($"WARNING: {id} left {count.ToString(CultureInfo.InvariantCulture)} uncommitted change(s) in its worktree — not carried over");
        }

        return null;
    }

    /// <summary>An INTENT that is the record's last entry: the pick it announces either did not happen (HEAD is still <c>headBefore</c>: the source stays a pick, a fresh intent precedes its retry) or is HEAD (exactly one commit past <c>headBefore</c>, the single cherry-pick this pipeline makes: recorded as the completion that was never written),
    /// or the branch moved under the pick (exit 4).</summary>
    private async Task<Halt?> SettleOpenIntentAsync(string id, List<PickEntry> entries, Dictionary<string, PickSource> byCommit, CancellationToken ct)
    {
        if (PickRecord.Open(entries) is not { } open)
        {
            return null;
        }

        var head = await Clone.HeadAsync(ct);
        if (head is Outcome<string>.Fail headFailed)
        {
            return Failed(id, headFailed.Reason);
        }

        var current = ((Outcome<string>.Ok)head).Value;
        if (current == open.HeadBefore)
        {
            return null;
        }

        var range = await Clone.RevListAsync(open.HeadBefore, ct);
        if (range is Outcome<IReadOnlyList<string>>.Fail rangeFailed)
        {
            return Failed(id, rangeFailed.Reason);
        }

        var past = ((Outcome<IReadOnlyList<string>>.Ok)range).Value;
        if (past.Count != 1 || past[0] != current)
        {
            return new Halt(ExitCodes.Configuration, $"{id}: {Path.GetFileName(_paths.PickRecord(id))} intends to pick {open.Source} from {open.HeadBefore} but HEAD is {current}, {past.Count.ToString(CultureInfo.InvariantCulture)} commit(s) past it — the arm branch moved under the pick; restore it to {open.HeadBefore} or start the arm again from a new clone", id);
        }

        var completion = new PickEntry.Picked(open.Source, current, byCommit.TryGetValue(open.Source, out var known) ? known.PatchId : string.Empty);
        _files.AppendLine(_paths.PickRecord(id), PickRecord.Line(completion));
        entries.Add(completion);
        _log.Line($"{Path.GetFileName(_paths.PickRecord(id))}: {open.Source} was picked as {current} before the crash — recorded");
        return null;
    }

    private async Task<Halt?> PickOneAsync(string id, PickStep.Pick pick, List<PickEntry> entries, CancellationToken ct)
    {
        if (LockHalt(id) is { } locked)
        {
            return locked;
        }

        var head = await Clone.HeadAsync(ct);
        if (head is Outcome<string>.Fail headFailed)
        {
            return Failed(id, headFailed.Reason);
        }

        var intent = new PickEntry.Intent(pick.Source, ((Outcome<string>.Ok)head).Value);
        _files.AppendLine(_paths.PickRecord(id), PickRecord.Line(intent));
        entries.Add(intent);
        _probe.Reached(TaskPoint.Intended, id);
        _probe.Reached(TaskPoint.Picked, id);
        var picked = await Clone.CherryPickAsync(pick.Source, ct);
        if (picked is Outcome<string>.Fail failed)
        {
            return await PickFailedAsync(id, failed.Reason, ct);
        }

        _probe.Reached(TaskPoint.Committed, id);
        var after = await Clone.HeadAsync(ct);
        if (after is Outcome<string>.Fail afterFailed)
        {
            return Failed(id, afterFailed.Reason);
        }

        var completion = new PickEntry.Picked(pick.Source, ((Outcome<string>.Ok)after).Value, pick.PatchId);
        _files.AppendLine(_paths.PickRecord(id), PickRecord.Line(completion));
        entries.Add(completion);
        _probe.Reached(TaskPoint.Recorded, id);
        return null;
    }

    /// <summary>A failed cherry-pick: an enumeration that could not run is the host's (exit 3); otherwise git's whole stderr goes to <c>pick-&lt;id&gt;.log</c>, what the pick left in progress is aborted (a failure of that is exit 3) and the arm stops on the conflict (exit 4, <c>t-lib.sh:32-34</c>; a merge commit's 128 takes the same road).</summary>
    private async Task<Halt?> PickFailedAsync(string id, string reason, CancellationToken ct)
    {
        if (reason.StartsWith(ArmGit.EnumerationFailure, StringComparison.Ordinal))
        {
            return Failed(id, reason);
        }

        _files.Replace(_paths.PickLog(id), reason.EndsWith('\n') ? reason : reason + "\n");
        if (Look(Path.Combine(GitFolder, "CHERRY_PICK_HEAD"), out var head) is { } headHalt || Look(Path.Combine(GitFolder, "sequencer"), out var sequencer) is { } sequencerHalt)
        {
            return Failed(id, "the clone's cherry-pick state cannot be looked at");
        }

        if ((head || sequencer) && await Clone.CherryPickAbortAsync(ct) is Outcome<string>.Fail abortFailed)
        {
            return Failed(id, $"cherry-pick --abort failed: {abortFailed.Reason}");
        }

        return new Halt(ExitCodes.Configuration, $"{id}: its commits conflict with the arm branch — stopping (see pick-{id}.log)", id);
    }

    // ---- reading ------------------------------------------------------------------------------------------------------------------------------------------------

    private const string TmpPrefix = "tmp/";

    /// <summary>The record as the file holds it: the valid lines and whether the tail is torn; an unreadable line before the last is exit 4 naming the file and the line.</summary>
    private (IReadOnlyList<PickEntry> Valid, bool Torn, Halt? Halt) LoadRecord(string id)
    {
        var path = _paths.PickRecord(id);
        var name = Path.GetFileName(path);
        switch (BoundedFile.Read(path, RecordLimit, PlatformFactsReader.CurrentRoad))
        {
            case BoundedRead.Absent:
                return ([], false, null);
            case BoundedRead.Refused refused:
                return ([], false, new Halt(ExitCodes.Configuration, $"{id}: {name} cannot be read: {refused.Reason} — restore the file or start the arm again from a new clone", id));
            case BoundedRead.Bytes bytes:
                return PickRecord.Parse(new UTF8Encoding(false).GetString(bytes.Value)) switch
                {
                    PickRecordRead.Lines lines => (lines.Valid, lines.TornTail, null),
                    PickRecordRead.Unreadable bad => ([], false, new Halt(ExitCodes.Configuration, $"{id}: {name} line {bad.LineNumber.ToString(CultureInfo.InvariantCulture)} cannot be read: {bad.Why} — appending cannot repair it; restore the file or start the arm again from a new clone", id)),
                    _ => throw new InvalidOperationException("a closed set"),
                };
            default:
                throw new InvalidOperationException("a closed set");
        }
    }

    /// <summary>The commit id <c>task-&lt;id&gt;.before</c> holds, or the empty text when the file is not one commit id (it is model-reachable: read bounded, never trusted).</summary>
    private string ReadBefore(string id) =>
        BoundedFile.Read(_paths.TaskBefore(id), BeforeLimit, PlatformFactsReader.CurrentRoad) is BoundedRead.Bytes bytes && new UTF8Encoding(false).GetString(bytes.Value).Trim() is var text && ShaText().IsMatch(text) ? text : string.Empty;

    [GeneratedRegex(@"^[0-9a-f]{40}([0-9a-f]{24})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ShaText();

    /// <summary>Whether a directory entry is there, asked by NAME of the kernel; a kernel that cannot say is the host's failure (exit 3).</summary>
    private static Halt? Look(string path, out bool present)
    {
        var answer = KernelFacts.Present(path);
        present = answer is Outcome<bool>.Ok { Value: true };
        return answer is Outcome<bool>.Fail failed ? new Halt(ExitCodes.Environment, failed.Reason, null) : null;
    }

    /// <summary>The stale <c>index.lock</c> (§8 item 4): git's own text says only a person may remove it once no git runs; reading it as a conflict names the wrong cause. Checked by name before the sequencer abort and before every cherry-pick — M-r: the only two writers of the clone's index. Never removed.</summary>
    private Halt? LockHalt(string? id)
    {
        if (Look(LockFile, out var there) is { } lookHalt)
        {
            return lookHalt;
        }

        return there
            ? new Halt(ExitCodes.Environment, $"{(id is null ? string.Empty : id + ": ")}{LockFile} exists — a git was killed while writing, or one still runs in {_paths.CloneFolder}; remove it once none does, then run me again", id)
            : null;
    }

    /// <summary>A git that could not answer: an enumeration of the clone's config that cannot run is the BATCH's stop and no question is asked with fewer pairs (§8 item 25); anything else is the task's (or the batch's) exit 3.</summary>
    private static Halt Failed(string? id, string reason) =>
        reason.StartsWith(ArmGit.EnumerationFailure, StringComparison.Ordinal) && id is null
            ? new Halt(ExitCodes.Environment, $"the clone's config cannot be enumerated: {reason}", null)
            : new Halt(ExitCodes.Environment, id is null ? reason : $"{id}: {reason}", id);

    /// <summary>A failure while the batch is being READ: an enumeration that cannot run names no task (it is the clone's), any other git failure names the one being read.</summary>
    private static Halt FailedRead(string id, string reason) => Failed(reason.StartsWith(ArmGit.EnumerationFailure, StringComparison.Ordinal) ? null : id, reason);

    private static bool SamePath(string left, string right) => Canonical(left) == Canonical(right);

    /// <summary>The path with its PARENT resolved by the kernel (the folder itself may be gone): git prints the real path of a worktree, the paths object may spell a link.</summary>
    private static string Canonical(string path)
    {
        var full = Path.GetFullPath(path);
        return Path.GetDirectoryName(full) is { } parent && RootFacts.RealPathOf(parent) is Outcome<string>.Ok real ? Path.Combine(real.Value, Path.GetFileName(full)) : full;
    }

    /// <summary>The first <c>cap</c> bytes of what is written, then the number dropped: the warm build's log (§8 item 19). The build is not stopped by the cap; its wall stops it.</summary>
    private sealed class CappedStream(Stream inner, long cap) : Stream
    {
        private long _kept;
        private long _dropped;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var room = (int)Math.Min(buffer.Length, Math.Max(0, cap - _kept));
            inner.Write(buffer[..room]);
            _kept += room;
            _dropped += buffer.Length - room;
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Flush() => inner.Flush();

        /// <summary>The one line that says how much was not kept.</summary>
        public void Finish()
        {
            if (_dropped > 0)
            {
                inner.Write(new UTF8Encoding(false).GetBytes($"[model-arms: the build's output past 1 MiB was not kept — {_dropped.ToString(CultureInfo.InvariantCulture)} bytes dropped]\n"));
                inner.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
