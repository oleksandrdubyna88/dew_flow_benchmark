using System.Globalization;
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


/// <summary>The task pipeline's worktrees and pick-back (plan D14, D22, §4.6; E4.S2, C20), the facade over four units: <see cref="TaskReader"/> (reads), <see cref="TaskDressing"/> (the submodule, the links, the warm build), <see cref="TaskPickBack"/> (D22) and the actions below. Every git it starts is a question on <see cref="ArmGit"/> (the real path
/// <c>run-arm</c> resolved, a replaced environment, a chosen folder); every write of the arm's clone is neutralised in the context it runs in (the checkout of a worktree is a question asked IN the worktree), every file is asked for BY NAME and read through <see cref="BoundedFile"/>, every log is created new. A stop that names a task writes the task's stop line
/// (<see cref="StepLog.Stop"/>); a stop of the BATCH (the clone is not a repository, a stale <c>index.lock</c>, an abort that cannot clear the clone, an enumeration that cannot run) names none and writes one log line.
/// <para>
/// <b>Re-entry reads before it cleans</b>: every task of the batch is read first (<see cref="TaskState"/>), <see cref="TaskRecovery.Decide"/> answers for all of them, any refusal stops the batch before ANY action, and only then does each task take its one action.
/// </para>
/// <para>
/// What this does NOT do: run a task's step or decide the batches (E4.S3); serialise with a model process that still runs in the clone (E3 §8 item 17); bound a build's output in memory (E3 §8 item 18; only the log is capped); observe LFS worktrees, Windows or a git below the pilot's two (module doc).
/// </para></summary>
internal sealed class TaskWorktrees
{
    private static readonly string[] ModelLeft = ["MERGE_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply"];

    private readonly TaskContext _c;
    private readonly TaskReader _reader;
    private readonly TaskDressing _dressing;
    private readonly TaskPickBack _pickBack;

    public TaskWorktrees(ArmPaths paths, TargetFacts target, TaskTools tools, StepLog log)
        : this(paths, target, tools, log, TaskProbe.None)
    {
    }

    internal TaskWorktrees(ArmPaths paths, TargetFacts target, TaskTools tools, StepLog log, TaskProbe probe, TimeProvider? clock = null)
    {
        _c = new TaskContext(paths, target, tools, log, probe, clock ?? TimeProvider.System);
        _reader = new TaskReader(_c);
        _dressing = new TaskDressing(_c);
        _pickBack = new TaskPickBack(_c, _reader);
    }

    // ---- the batch ----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Parent §4.6 re-entry steps 1–3 for a batch without <c>batch-&lt;i&gt;.done</c>; a batch of ONE runs in the clone itself (<c>t-lib.sh:64-65</c>): no worktree, no pick, nothing asked.</summary>
    public async Task<BatchEntry> EnterAsync(IReadOnlyList<string> batch, Func<string, StepEntry> recovery, CancellationToken ct)
    {
        if (batch.Count == 1)
        {
            return new BatchEntry.Ready([new BatchPlace.Run(batch[0], _c.Paths.CloneFolder)]);
        }

        _c.Probe.Reached(TaskPoint.Entered, string.Empty);
        var places = new List<BatchPlace>();
        var halt = await EnterBatchAsync(batch, recovery, places, ct);
        if (halt is null)
        {
            return new BatchEntry.Ready(places);
        }

        if (halt.Id is { } id)
        {
            _c.Log.Stop(TaskStems.Coder(id), halt.Code, halt.Why);
            return new BatchEntry.Stopped(halt.Code, halt.Why);
        }

        var why = $"batch {string.Join(' ', batch)}: {halt.Why}";
        _c.Log.Line(why);
        return new BatchEntry.Stopped(halt.Code, why);
    }

    private async Task<Halt?> EnterBatchAsync(IReadOnlyList<string> batch, Func<string, StepEntry> recovery, List<BatchPlace> places, CancellationToken ct)
    {
        if (await CloneStateAsync(ct) is { } stateHalt)
        {
            return stateHalt;
        }

        var listed = await _c.Clone.WorktreeListAsync(ct);
        if (listed is Outcome<IReadOnlyList<WorktreeEntry>>.Fail listFailed)
        {
            return TaskContext.Failed(null, listFailed.Reason);
        }

        if (await WarnAboutLfsAsync(ct) is { } lfsHalt)
        {
            return lfsHalt;
        }

        if (!(await ReadAllAsync(batch, recovery, ((Outcome<IReadOnlyList<WorktreeEntry>>.Ok)listed).Value, ct)).TryGet(out var reads, out var readHalt))
        {
            return readHalt;
        }

        _c.Probe.Reached(TaskPoint.Read, string.Empty);
        var actions = reads!.Select(read => (Read: read, Action: TaskRecovery.Decide(read.State))).ToList();
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

        _c.Probe.Reached(TaskPoint.Acted, string.Empty);
        places.AddRange(actions.Select(pair => PlaceOf(pair.Read.State)));
        return null;
    }

    private async Task<Taken<List<TaskRead>>> ReadAllAsync(IReadOnlyList<string> batch, Func<string, StepEntry> recovery, IReadOnlyList<WorktreeEntry> entries, CancellationToken ct)
    {
        var reads = new List<TaskRead>();
        foreach (var id in batch)
        {
            if (!(await _reader.ReadAsync(id, recovery(id), entries, ct)).TryGet(out var read, out var halt))
            {
                return new Taken<List<TaskRead>>.Halted(halt!);
            }

            reads.Add(read!);
        }

        return new Taken<List<TaskRead>>.Got(reads);
    }

    private BatchPlace PlaceOf(TaskState state) =>
        state.Picked ? new BatchPlace.Picked(state.Id) : new BatchPlace.Run(state.Id, _c.Paths.Worktree(state.Id));

    // ---- the clone before the batch -----------------------------------------------------------------------------------------------------------------------------

    /// <summary>The clone before anything about the batch is asked: a repository rooted at its own <c>.git</c>, no stale <c>index.lock</c> (§8 item 4), the model's own unfinished operations reported and left, and a cherry-pick a crash left in progress aborted — a recovery, never a conflict (D22).</summary>
    private async Task<Halt?> CloneStateAsync(CancellationToken ct)
    {
        if (!TaskContext.Look(Path.Combine(_c.GitFolder, "HEAD")).TryGet(out var rooted, out var lookHalt))
        {
            return lookHalt;
        }

        if (!rooted)
        {
            return new Halt(ExitCodes.Environment, $"{_c.Paths.CloneFolder} is not a repository rooted at {_c.GitFolder}", null);
        }

        if (_c.LockHalt(null) is { } locked)
        {
            return locked;
        }

        if (ReportModelLeft() is { } leftHalt)
        {
            return leftHalt;
        }

        if (await AbortInProgressAsync(ct) is { } abortHalt)
        {
            return abortHalt;
        }

        _c.Probe.Reached(TaskPoint.Aborted, string.Empty);
        return null;
    }

    private Halt? ReportModelLeft()
    {
        foreach (var name in ModelLeft)
        {
            if (!TaskContext.Look(Path.Combine(_c.GitFolder, name)).TryGet(out var left, out var halt))
            {
                return halt;
            }

            if (left)
            {
                _c.Log.Line($"the clone has .git/{name} — left as the model left it");
            }
        }

        return null;
    }

    private async Task<Halt?> AbortInProgressAsync(CancellationToken ct)
    {
        if (!InProgress().TryGet(out var names, out var halt))
        {
            return halt;
        }

        if (names!.Count == 0)
        {
            return null;
        }

        if (await _c.Clone.CherryPickAbortAsync(ct) is Outcome<string>.Fail abortFailed)
        {
            return TaskContext.Failed(null, $"cherry-pick --abort failed: {abortFailed.Reason}");
        }

        _c.Log.Line($"the clone had a cherry-pick in progress ({string.Join(", ", names.Select(name => name == "sequencer" ? "sequencer/" : name))}) — aborted; not a conflict");
        if (!InProgress().TryGet(out var still, out var stillHalt))
        {
            return stillHalt;
        }

        return still!.Count == 0 ? null : new Halt(ExitCodes.Environment, $"{Path.Combine(_c.GitFolder, still[0])} is still there after cherry-pick --abort — clear the clone's state by hand, then run me again", null);
    }

    private Taken<List<string>> InProgress()
    {
        var names = new List<string>();
        foreach (var name in new[] { "CHERRY_PICK_HEAD", "sequencer" })
        {
            if (!TaskContext.Look(Path.Combine(_c.GitFolder, name)).TryGet(out var there, out var halt))
            {
                return new Taken<List<string>>.Halted(halt!);
            }

            if (there)
            {
                names.Add(name);
            }
        }

        return new Taken<List<string>>.Got(names);
    }

    /// <summary>Filters are neutralised, so an LFS target's worktree holds pointer files: said once, never refused (git-lfs is unobserved).</summary>
    private async Task<Halt?> WarnAboutLfsAsync(CancellationToken ct)
    {
        var drivers = await _c.Clone.FilterDriversAsync(ct);
        if (drivers is Outcome<IReadOnlyList<string>>.Fail failed)
        {
            return TaskContext.Failed(null, failed.Reason);
        }

        if (((Outcome<IReadOnlyList<string>>.Ok)drivers).Value.Contains("lfs", StringComparer.Ordinal))
        {
            _c.Log.Line("WARNING: the clone's config names an lfs filter driver — it is neutralised for the worktrees, so their LFS files are pointer files and a build that needs their content fails (unobserved)");
        }

        return null;
    }

    // ---- the actions --------------------------------------------------------------------------------------------------------------------------------------------

    private Task<Halt?> ActAsync(TaskRead read, TaskAction action, CancellationToken ct) => action switch
    {
        TaskAction.Recreate => RecreateAsync(read, ct),
        TaskAction.Keep => KeepAsync(read),
        TaskAction.Reattach reattach => ReattachAsync(read, reattach.Build, ct),
        TaskAction.Populate populate => PopulateAsync(read, populate.Build, ct),
        TaskAction.Drop => RemoveAsync(read.State.Id, IsRegistered(read.State.Worktree), read.State.BranchPresent, ct),
        _ => Task.FromResult<Halt?>(null),
    };

    private static bool IsRegistered(WorktreeState state) => state is WorktreeState.Registered or WorktreeState.Stray;

    /// <summary>A task whose session never started (or ran and left no commit): its worktree and branch removed (forcing twice, pruning, deleting), its <c>.before</c> deleted by name with them (§8 item 9), a fresh worktree from the current HEAD — added with nothing checked out, then populated IN the worktree.</summary>
    private async Task<Halt?> RecreateAsync(TaskRead read, CancellationToken ct)
    {
        var id = read.State.Id;
        if (await RecreatedLineAsync(read, ct) is { } lineHalt)
        {
            return lineHalt;
        }

        if ((read.State.BranchPresent || read.State.Worktree is not WorktreeState.Gone) && await RemoveAsync(id, IsRegistered(read.State.Worktree), read.State.BranchPresent, ct) is { } removeHalt)
        {
            return removeHalt;
        }

        File.Delete(_c.Paths.TaskBefore(id));
        if (await _c.Clone.WorktreeAddAsync(_c.Paths.Worktree(id), TaskContext.TmpPrefix + id, ct) is Outcome<string>.Fail addFailed)
        {
            return TaskContext.Failed(id, addFailed.Reason);
        }

        if (await PopulateAndDressAsync(id, build: true, ct) is { } dressHalt)
        {
            return dressHalt;
        }

        _c.Log.Line($"worktree for {id} ready");
        return null;
    }

    /// <summary>What a recreation says about what it removes. For a session that RAN and cannot be resumed (attempt above 1) the line is read BEFORE the removal, because it counts what is about to be discarded.</summary>
    private async Task<Halt?> RecreatedLineAsync(TaskRead read, CancellationToken ct)
    {
        var state = read.State;
        if (state.Step == TaskStep.Unresumable)
        {
            return await UnresumableLineAsync(state, ct);
        }

        if (state.BranchPresent || state.Worktree is not WorktreeState.Gone)
        {
            _c.Log.Line($"{state.Id}: recreated — its worktree and branch were removed (not done, not picked)");
        }

        return null;
    }

    private async Task<Halt?> UnresumableLineAsync(TaskState state, CancellationToken ct)
    {
        var id = state.Id;
        if (!state.BranchPresent)
        {
            _c.Log.Line($"{id}: recreated — its earlier attempt(s) left no {TaskContext.TmpPrefix}{id}");
            return null;
        }

        var discarded = "its worktree folder was gone";
        if (state.Worktree is WorktreeState.Registered { FolderPresent: true, Populated: true })
        {
            var left = await _c.In(_c.Paths.Worktree(id)).LeftBehindAsync(_c.Target.NodeModules, ct);
            if (left is Outcome<int>.Fail failed)
            {
                return TaskContext.Failed(id, failed.Reason);
            }

            discarded = $"{((Outcome<int>.Ok)left).Value.ToString(CultureInfo.InvariantCulture)} uncommitted change(s) in its worktree discarded";
        }

        _c.Log.Line($"{id}: recreated — attempt {(state.Attempt - 1).ToString(CultureInfo.InvariantCulture)} left no commit on {TaskContext.TmpPrefix}{id} ({discarded})");
        return null;
    }

    private Task<Halt?> KeepAsync(TaskRead read)
    {
        var id = read.State.Id;
        _c.Log.Line($"{id}: kept — its session resumes in its worktree ({read.State.CommitsPastBefore.ToString(CultureInfo.InvariantCulture)} commit(s) on {TaskContext.TmpPrefix}{id} past its .before)");
        return Task.FromResult<Halt?>(null);
    }

    /// <summary>The folder is gone, the registration and the branch intact: a registration whose folder is gone is REMOVED first (<c>remove --force --force</c> — a locked one is neither prunable nor deletable otherwise, M-ac), then pruned (§4.6), then the folder attached to the EXISTING branch with nothing checked out, populated IN
    /// the worktree, dressed; built only for a resumed task.</summary>
    private async Task<Halt?> ReattachAsync(TaskRead read, bool build, CancellationToken ct)
    {
        var id = read.State.Id;
        var wt = _c.Paths.Worktree(id);
        if (read.State.Worktree is WorktreeState.Registered && await _c.Clone.WorktreeRemoveAsync(wt, ct) is Outcome<string>.Fail removeFailed)
        {
            return TaskContext.Failed(id, removeFailed.Reason);
        }

        if (await _c.Clone.WorktreePruneAsync(ct) is Outcome<string>.Fail pruneFailed)
        {
            return TaskContext.Failed(id, pruneFailed.Reason);
        }

        if (await _c.Clone.WorktreeAttachAsync(wt, TaskContext.TmpPrefix + id, ct) is Outcome<string>.Fail attachFailed)
        {
            return TaskContext.Failed(id, attachFailed.Reason);
        }

        if (await PopulateAndDressAsync(id, build, ct) is { } dressHalt)
        {
            return dressHalt;
        }

        _c.Log.Line(build
            ? $"{id}: reattached to {TaskContext.TmpPrefix}{id} (its worktree folder was gone — its uncommitted edits are lost; its {read.State.CommitsPastBefore.ToString(CultureInfo.InvariantCulture)} commit(s) are on the branch)"
            : $"{id}: reattached to {TaskContext.TmpPrefix}{id} (its worktree was gone)");
        return null;
    }

    /// <summary>A registered worktree whose checkout never ran (a crash between the add and the populate): populated, dressed, built only for a resumed task — never kept empty (E4 §8 item 27 b).</summary>
    private async Task<Halt?> PopulateAsync(TaskRead read, bool build, CancellationToken ct)
    {
        var id = read.State.Id;
        if (await PopulateAndDressAsync(id, build, ct) is { } halt)
        {
            return halt;
        }

        _c.Log.Line($"{id}: its worktree was registered but never populated (a crash before its checkout) — populated from {TaskContext.TmpPrefix}{id}");
        return null;
    }

    private async Task<Halt?> PopulateAndDressAsync(string id, bool build, CancellationToken ct)
    {
        if (await _c.In(_c.Paths.Worktree(id)).PopulateAsync(ct) is Outcome<string>.Fail populateFailed)
        {
            return TaskContext.Failed(id, populateFailed.Reason);
        }

        _c.Probe.Reached(TaskPoint.Added, id);
        return await _dressing.DressAsync(id, build, ct);
    }

    // ---- the pick-back and the drop -----------------------------------------------------------------------------------------------------------------------------

    /// <summary>D22: the task's commits onto the arm branch, an intent and a completion line per commit, the marker LAST. <c>null</c>: carried.</summary>
    public async Task<StepOutcome.Stopped?> PickBackAsync(string id, CancellationToken ct)
    {
        var halt = await _pickBack.PickAsync(id, ct);
        return halt is null ? null : _c.Log.Stop(TaskStems.Coder(id), halt.Code, halt.Why);
    }

    /// <summary>After the pick-back: <c>remove --force --force</c> whenever the worktree is registered or a folder with a <c>.git</c> is there → <c>prune</c> → <c>branch -D</c> (only when the branch is there). Each a no-op by READING, never by parsing git's words; a stray non-worktree folder is git's 128 <i>is not a working tree</i> → exit 3 naming the folder.</summary>
    public async Task<StepOutcome.Stopped?> DropAsync(string id, CancellationToken ct)
    {
        var halt = await DropCoreAsync(id, ct);
        return halt is null ? null : _c.Log.Stop(TaskStems.Coder(id), halt.Code, halt.Why);
    }

    private async Task<Halt?> DropCoreAsync(string id, CancellationToken ct)
    {
        var wt = _c.Paths.Worktree(id);
        var listed = await _c.Clone.WorktreeListAsync(ct);
        if (listed is Outcome<IReadOnlyList<WorktreeEntry>>.Fail listFailed)
        {
            return TaskContext.Failed(id, listFailed.Reason);
        }

        if (!TaskContext.Look(Path.Combine(wt, ".git")).TryGet(out var folder, out var halt))
        {
            return halt;
        }

        var registered = ((Outcome<IReadOnlyList<WorktreeEntry>>.Ok)listed).Value.Any(entry => TaskReader.SamePath(entry.Path, wt));
        return await RemoveAsync(id, registered || folder, branchPresent: null, ct);
    }

    /// <summary>The three removals. What the batch's entry already read (<paramref name="remove"/>, <paramref name="branchPresent"/>) is not asked again — a read after the first removal would break "every task is read before any cleanup"; the public drop reads and passes <c>null</c> for the branch.</summary>
    private async Task<Halt?> RemoveAsync(string id, bool remove, bool? branchPresent, CancellationToken ct)
    {
        var wt = _c.Paths.Worktree(id);
        if (remove && await _c.Clone.WorktreeRemoveAsync(wt, ct) is Outcome<string>.Fail removeFailed)
        {
            return TaskContext.Failed(id, $"{removeFailed.Reason} (worktree {wt})");
        }

        if (await _c.Clone.WorktreePruneAsync(ct) is Outcome<string>.Fail pruneFailed)
        {
            return TaskContext.Failed(id, pruneFailed.Reason);
        }

        var branch = await BranchPresentAsync(id, branchPresent, ct);
        if (!branch.TryGet(out var there, out var branchHalt))
        {
            return branchHalt;
        }

        if (there && await _c.Clone.DeleteBranchAsync(TaskContext.TmpPrefix + id, ct) is Outcome<string>.Fail deleteFailed)
        {
            return TaskContext.Failed(id, deleteFailed.Reason);
        }

        _c.Probe.Reached(TaskPoint.Dropped, id);
        return null;
    }

    private async Task<Taken<bool>> BranchPresentAsync(string id, bool? known, CancellationToken ct)
    {
        if (known is { } already)
        {
            return new Taken<bool>.Got(already);
        }

        var tip = await _c.Clone.BranchTipAsync(TaskContext.TmpPrefix + id, ct);
        return tip is Outcome<string>.Ok ok
            ? new Taken<bool>.Got(ok.Value.Length > 0)
            : new Taken<bool>.Halted(TaskContext.Failed(id, ((Outcome<string>.Fail)tip).Reason));
    }
}
