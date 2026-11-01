using System.Globalization;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Tasks;

namespace ModelArms.Adapters;

/// <summary>D22 (plan §4.6, E4.S2 spec item 8 and the review's items 28 and 31): the task's commits (<c>task-&lt;id&gt;.before..tmp/&lt;id&gt;</c>) onto the arm branch, ONE commit per <c>cherry-pick</c>, an INTENT line before it and a COMPLETION line after it in <c>out/pick-&lt;id&gt;.json</c>, the marker <c>picked-&lt;id&gt;</c> LAST. Every git is a question on
/// <see cref="ArmGit"/> (the cherry-pick neutralised for filters and merge drivers, the committer pinned); a record line is believed only for what the pipeline itself made.</summary>
internal sealed class TaskPickBack(TaskContext c, TaskReader reader)
{
    private const int SubjectCut = 72;

    private sealed record Lists(IReadOnlyList<string> Task, IReadOnlyList<string> Branch, IReadOnlyDictionary<string, PickSource> ById);

    /// <summary><c>null</c>: carried.</summary>
    public async Task<Halt?> PickAsync(string id, CancellationToken ct)
    {
        if (!TaskContext.Look(c.Paths.Picked(id)).TryGet(out var picked, out var pickedHalt))
        {
            return pickedHalt;
        }

        if (picked)
        {
            return null;
        }

        var before = Before(id);
        if (!before.TryGet(out var beforeSha, out var beforeHalt))
        {
            return beforeHalt;
        }

        if (await WarnAboutWorktreeAsync(id, ct) is { } warnHalt)
        {
            return warnHalt;
        }

        return await CarryAsync(id, beforeSha, ct);
    }

    private Taken<string> Before(string id)
    {
        if (!TaskContext.Look(c.Paths.TaskBefore(id)).TryGet(out var there, out var halt))
        {
            return new Taken<string>.Halted(halt!);
        }

        var sha = there ? reader.ReadBefore(id) : string.Empty;
        return sha.Length == 0
            ? new Taken<string>.Halted(new Halt(ExitCodes.Configuration, $"{id}: out/task-{id}.before is missing — the pick has no range", id))
            : new Taken<string>.Got(sha);
    }

    private async Task<Halt?> CarryAsync(string id, string before, CancellationToken ct)
    {
        if (!reader.LoadRecord(id).TryGet(out var loaded, out var loadHalt))
        {
            return loadHalt;
        }

        var record = Path.GetFileName(c.Paths.PickRecord(id));
        var entries = loaded!.Valid.ToList();
        if (loaded.Torn)
        {
            c.Log.Line($"{record}: the last line is torn — ignored and removed");
            c.Files.Replace(c.Paths.PickRecord(id), PickRecord.Write(entries));
        }

        if (!(await ListsAsync(id, before, ct)).TryGet(out var lists, out var listHalt))
        {
            return listHalt;
        }

        if (await SettleOpenIntentAsync(id, entries, lists!.ById, ct) is { } intentHalt)
        {
            return intentHalt;
        }

        c.Probe.Reached(TaskPoint.Read, id);
        var plan = PickBackOrder.Decide([.. lists.Task.Select(sha => lists.ById[sha])], entries, lists.Branch);
        foreach (var unbelieved in plan.NotBelieved)
        {
            c.Log.Line($"{record} line {entries.IndexOf(unbelieved) + 1}: {unbelieved.PickedSha} is not on the arm branch — not believed");
        }

        return await RunPlanAsync(id, plan, entries, ct);
    }

    private async Task<Taken<Lists>> ListsAsync(string id, string before, CancellationToken ct)
    {
        var sources = await c.Clone.RevListAsync(before, TaskContext.TmpPrefix + id, ct);
        var onBranch = await c.Clone.RevListAsync(before, ct);
        if (sources is not Outcome<IReadOnlyList<string>>.Ok task || onBranch is not Outcome<IReadOnlyList<string>>.Ok branch)
        {
            return Stopped(TaskContext.Failed(id, sources is Outcome<IReadOnlyList<string>>.Fail failedSources ? failedSources.Reason : ((Outcome<IReadOnlyList<string>>.Fail)onBranch).Reason));
        }

        var ids = await c.Clone.PatchIdsAsync(task.Value, ct);
        return ids is Outcome<IReadOnlyList<PickSource>>.Ok ok
            ? new Taken<Lists>.Got(new Lists(task.Value, branch.Value, ok.Value.ToDictionary(source => source.Sha, StringComparer.Ordinal)))
            : Stopped(TaskContext.Failed(id, ((Outcome<IReadOnlyList<PickSource>>.Fail)ids).Reason));
    }

    private static Taken<Lists> Stopped(Halt halt) => new Taken<Lists>.Halted(halt);

    private async Task<Halt?> RunPlanAsync(string id, PickPlan plan, List<PickEntry> entries, CancellationToken ct)
    {
        int byRecord = 0, made = 0;
        foreach (var step in plan.Steps)
        {
            if (step is PickStep.Carried)
            {
                byRecord++;
            }
            else if (await PickOneAsync(id, (PickStep.Pick)step, entries, ct) is { } halt)
            {
                return halt;
            }
            else
            {
                made++;
            }
        }

        c.Files.WriteOnce(c.Paths.Picked(id), PickRecord.Write(entries));
        c.Probe.Reached(TaskPoint.Marked, id);
        c.Log.Line($"{id}: {made} commit(s) carried over ({byRecord} already carried by the record)");
        return null;
    }

    /// <summary>The worktree, when it is there: a branch other than <c>tmp/&lt;id&gt;</c> and uncommitted changes are WARNINGS (<c>t-lib.sh:29</c>) — only the branch is carried.</summary>
    private async Task<Halt?> WarnAboutWorktreeAsync(string id, CancellationToken ct)
    {
        var wt = c.Paths.Worktree(id);
        if (!TaskContext.Look(Path.Combine(wt, ".git")).TryGet(out var there, out var halt))
        {
            return halt;
        }

        if (!there)
        {
            return null;
        }

        var branch = await c.In(wt).CurrentBranchAsync(ct);
        if (branch is Outcome<string>.Fail branchFailed)
        {
            return TaskContext.Failed(id, branchFailed.Reason);
        }

        var name = ((Outcome<string>.Ok)branch).Value;
        if (name != TaskContext.TmpPrefix + id)
        {
            c.Log.Line($"WARNING: {id}'s worktree is on {(name.Length == 0 ? "a detached HEAD" : name)}, not {TaskContext.TmpPrefix}{id} — only {TaskContext.TmpPrefix}{id} is carried");
        }

        return await WarnAboutLeftBehindAsync(id, wt, ct);
    }

    private async Task<Halt?> WarnAboutLeftBehindAsync(string id, string wt, CancellationToken ct)
    {
        var left = await c.In(wt).LeftBehindAsync(c.Target.NodeModules, ct);
        if (left is Outcome<int>.Fail leftFailed)
        {
            return TaskContext.Failed(id, leftFailed.Reason);
        }

        if (((Outcome<int>.Ok)left).Value is var count and > 0)
        {
            c.Log.Line($"WARNING: {id} left {count.ToString(CultureInfo.InvariantCulture)} uncommitted change(s) in its worktree — not carried over");
        }

        return null;
    }

    /// <summary>An INTENT that is the record's last entry: the pick it announces either did not happen (HEAD is still <c>headBefore</c>: the source stays a pick, a fresh intent precedes its retry) or HEAD is exactly the pipeline's own commit for it — one commit past <c>headBefore</c> whose parent is <c>headBefore</c>, whose author, author date and message are the
    /// source's and whose committer is the pin's (a cherry-pick keeps the first three, an empty source included: M-ab) — or the branch moved under the pick (exit 4). A commit by another hand has the same count and the same parent (M-ab) and is never taken for the pick (E4 §8 item 28).</summary>
    private async Task<Halt?> SettleOpenIntentAsync(string id, List<PickEntry> entries, IReadOnlyDictionary<string, PickSource> byCommit, CancellationToken ct)
    {
        if (PickRecord.Open(entries) is not { } open)
        {
            return null;
        }

        var head = await c.Clone.HeadAsync(ct);
        if (head is Outcome<string>.Fail headFailed)
        {
            return TaskContext.Failed(id, headFailed.Reason);
        }

        var current = ((Outcome<string>.Ok)head).Value;
        if (current == open.HeadBefore)
        {
            return null;
        }

        var range = await c.Clone.RevListAsync(open.HeadBefore, ct);
        if (range is Outcome<IReadOnlyList<string>>.Fail rangeFailed)
        {
            return TaskContext.Failed(id, rangeFailed.Reason);
        }

        var past = ((Outcome<IReadOnlyList<string>>.Ok)range).Value;
        if (past.Count != 1 || past[0] != current)
        {
            return new Halt(ExitCodes.Configuration, $"{id}: {Path.GetFileName(c.Paths.PickRecord(id))} intends to pick {open.Source} from {open.HeadBefore} but HEAD is {current}, {past.Count.ToString(CultureInfo.InvariantCulture)} commit(s) past it — the arm branch moved under the pick; restore it to {open.HeadBefore} or start the arm again from a new clone", id);
        }

        return await CloseIfOursAsync(id, open, current, entries, byCommit, ct);
    }

    private async Task<Halt?> CloseIfOursAsync(string id, PickEntry.Intent open, string current, List<PickEntry> entries, IReadOnlyDictionary<string, PickSource> byCommit, CancellationToken ct)
    {
        var headFacts = await c.Clone.CommitFactsAsync(current, ct);
        var sourceFacts = await c.Clone.CommitFactsAsync(open.Source, ct);
        if (headFacts is not Outcome<CommitFacts>.Ok made || sourceFacts is not Outcome<CommitFacts>.Ok source)
        {
            return TaskContext.Failed(id, headFacts is Outcome<CommitFacts>.Fail headFailed ? headFailed.Reason : ((Outcome<CommitFacts>.Fail)sourceFacts).Reason);
        }

        if (!IsThePick(made.Value, source.Value, open.HeadBefore))
        {
            return new Halt(ExitCodes.Configuration, $"{id}: {Path.GetFileName(c.Paths.PickRecord(id))} intends to pick {open.Source} ({Describe(source.Value)}) from {open.HeadBefore}, but HEAD {current} ({Describe(made.Value)}) is not that pick — the arm branch moved under the pick; restore it to {open.HeadBefore}, or start the arm again from a new clone", id);
        }

        var completion = new PickEntry.Picked(open.Source, current, byCommit.TryGetValue(open.Source, out var known) ? known.PatchId : string.Empty);
        c.Files.AppendLine(c.Paths.PickRecord(id), PickRecord.Line(completion));
        entries.Add(completion);
        c.Log.Line($"{Path.GetFileName(c.Paths.PickRecord(id))}: {open.Source} was picked as {current} before the crash — recorded");
        return null;
    }

    private static bool IsThePick(CommitFacts made, CommitFacts source, string headBefore) =>
        made.Parents.SequenceEqual([headBefore])
        && (made.AuthorName, made.AuthorEmail, made.AuthorDate, made.Message) == (source.AuthorName, source.AuthorEmail, source.AuthorDate, source.Message)
        && (made.CommitterName, made.CommitterEmail) == (ArmGit.CommitterName, ArmGit.CommitterEmail);

    private static string Describe(CommitFacts facts)
    {
        var subject = facts.Message.Split('\n')[0];
        return $"{facts.AuthorName} <{facts.AuthorEmail}>, \"{(subject.Length > SubjectCut ? subject[..SubjectCut] : subject)}\"";
    }

    private async Task<Halt?> PickOneAsync(string id, PickStep.Pick pick, List<PickEntry> entries, CancellationToken ct)
    {
        if (c.LockHalt(id) is { } locked)
        {
            return locked;
        }

        var head = await c.Clone.HeadAsync(ct);
        if (head is Outcome<string>.Fail headFailed)
        {
            return TaskContext.Failed(id, headFailed.Reason);
        }

        Append(id, entries, new PickEntry.Intent(pick.Source, ((Outcome<string>.Ok)head).Value));
        c.Probe.Reached(TaskPoint.Intended, id);
        c.Probe.Reached(TaskPoint.Picked, id);
        if (await c.Clone.CherryPickAsync(pick.Source, ct) is Outcome<string>.Fail failed)
        {
            return await PickFailedAsync(id, failed.Reason, ct);
        }

        c.Probe.Reached(TaskPoint.Committed, id);
        var after = await c.Clone.HeadAsync(ct);
        if (after is Outcome<string>.Fail afterFailed)
        {
            return TaskContext.Failed(id, afterFailed.Reason);
        }

        Append(id, entries, new PickEntry.Picked(pick.Source, ((Outcome<string>.Ok)after).Value, pick.PatchId));
        c.Probe.Reached(TaskPoint.Recorded, id);
        return null;
    }

    private void Append(string id, List<PickEntry> entries, PickEntry entry)
    {
        c.Files.AppendLine(c.Paths.PickRecord(id), PickRecord.Line(entry));
        entries.Add(entry);
    }

    /// <summary>A failed cherry-pick: an enumeration that could not run is the host's (exit 3); otherwise git's whole output goes to <c>pick-&lt;id&gt;.log</c>, what the pick left in progress is aborted (a failure of that is exit 3) and the arm stops on the conflict (exit 4, <c>t-lib.sh:32-34</c>; a merge commit's 128 takes the same road).</summary>
    private async Task<Halt?> PickFailedAsync(string id, string reason, CancellationToken ct)
    {
        if (reason.StartsWith(ArmGit.EnumerationFailure, StringComparison.Ordinal))
        {
            return TaskContext.Failed(id, reason);
        }

        c.Files.Replace(c.Paths.PickLog(id), reason.EndsWith('\n') ? reason : reason + "\n");
        if (!InProgress().TryGet(out var inProgress, out var halt))
        {
            return halt;
        }

        if (inProgress && await c.Clone.CherryPickAbortAsync(ct) is Outcome<string>.Fail abortFailed)
        {
            return TaskContext.Failed(id, $"cherry-pick --abort failed: {abortFailed.Reason}");
        }

        return new Halt(ExitCodes.Configuration, $"{id}: its commits conflict with the arm branch — stopping (see pick-{id}.log)", id);
    }

    private Taken<bool> InProgress()
    {
        foreach (var name in new[] { "CHERRY_PICK_HEAD", "sequencer" })
        {
            var seen = TaskContext.Look(Path.Combine(c.GitFolder, name));
            if (seen is not Taken<bool>.Got { Value: false })
            {
                return seen;
            }
        }

        return new Taken<bool>.Got(false);
    }
}
