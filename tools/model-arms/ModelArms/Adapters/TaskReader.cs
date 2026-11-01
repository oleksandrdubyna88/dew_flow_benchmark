using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;

namespace ModelArms.Adapters;

/// <summary>Everything one task's re-entry read before anything was touched.</summary>
internal sealed record TaskRead(TaskState State, string BeforeSha);

/// <summary>The pick record as the file holds it: its valid lines and whether its tail was torn.</summary>
internal sealed record LoadedRecord(IReadOnlyList<PickEntry> Valid, bool Torn);

/// <summary>The reading half of D14's re-entry (E4.S2): a task's step, branch, worktree, markers, <c>.before</c> and record — each fact asked by NAME of the kernel or of git, none acted on. The model-reachable files go through <see cref="BoundedFile"/>.</summary>
internal sealed partial class TaskReader(TaskContext c)
{
    private const int RecordLimit = 64 * 1024;
    private const int BeforeLimit = 4096;

    private sealed record Presence(bool Picked, bool BeforePresent, bool Folder, bool Anything);

    private sealed record Descent(bool Descends, int CommitsPast);

    public async Task<Taken<TaskRead>> ReadAsync(string id, StepEntry entry, IReadOnlyList<WorktreeEntry> entries, CancellationToken ct)
    {
        var step = TaskRecovery.StepOf(entry);
        var tip = await c.Clone.BranchTipAsync(TaskContext.TmpPrefix + id, ct);
        if (tip is Outcome<string>.Fail tipFailed)
        {
            return Halted(TaskContext.FailedRead(id, tipFailed.Reason));
        }

        var branch = ((Outcome<string>.Ok)tip).Value.Length > 0;
        if (!Presences(id).TryGet(out var seen, out var seenHalt))
        {
            return Halted(seenHalt!);
        }

        if (!(await WorktreeStateAsync(id, entries, seen, step, ct)).TryGet(out var worktree, out var worktreeHalt))
        {
            return Halted(worktreeHalt!);
        }

        var beforeSha = seen.BeforePresent ? ReadBefore(id) : string.Empty;
        var descent = new Descent(false, -1);
        if (step is TaskStep.Resume or TaskStep.Unresumable && branch && beforeSha.Length > 0 && !(await DescentAsync(id, beforeSha, ct)).TryGet(out descent, out var descentHalt))
        {
            return Halted(descentHalt!);
        }

        if (step == TaskStep.Done && !seen.Picked && !LoadRecord(id).TryGet(out _, out var recordHalt))
        {
            return Halted(recordHalt!);
        }

        return new Taken<TaskRead>.Got(new TaskRead(new TaskState(id, step, branch, seen.Picked, worktree, seen.BeforePresent, descent.Descends, AttemptOf(entry), descent.CommitsPast), beforeSha));
    }

    private static Taken<TaskRead> Halted(Halt halt) => new Taken<TaskRead>.Halted(halt);

    private static int AttemptOf(StepEntry entry) => entry switch
    {
        StepEntry.Fresh fresh => fresh.Attempt,
        StepEntry.Resume resume => resume.Attempt,
        _ => 0,
    };

    private Taken<Presence> Presences(string id)
    {
        var wt = c.Paths.Worktree(id);
        var seen = new List<bool>();
        foreach (var path in new[] { c.Paths.Picked(id), c.Paths.TaskBefore(id), Path.Combine(wt, ".git"), wt })
        {
            if (!TaskContext.Look(path).TryGet(out var there, out var halt))
            {
                return new Taken<Presence>.Halted(halt!);
            }

            seen.Add(there);
        }

        return new Taken<Presence>.Got(new Presence(seen[0], seen[1], seen[2], seen[3]));
    }

    /// <summary>The registration <c>worktree list</c> shows for <c>wt/&lt;id&gt;</c> and what the folder says. A registered folder that is present is POPULATED when the index git names for it exists: a crash between <c>worktree add --no-checkout</c> and the populate leaves a registered, empty worktree (E4 §8 item 27 b) that would otherwise read as a kept one.</summary>
    private async Task<Taken<WorktreeState>> WorktreeStateAsync(string id, IReadOnlyList<WorktreeEntry> entries, Presence seen, TaskStep step, CancellationToken ct)
    {
        var wt = c.Paths.Worktree(id);
        var registration = entries.FirstOrDefault(candidate => SamePath(candidate.Path, wt));
        if (registration is null)
        {
            return new Taken<WorktreeState>.Got(seen.Anything ? new WorktreeState.Stray() : new WorktreeState.Gone());
        }

        var folder = seen.Folder && !registration.Prunable;
        var onBranch = registration.Branch == "refs/heads/" + TaskContext.TmpPrefix + id;
        if (!folder || seen.Picked || step == TaskStep.Fresh)
        {
            return new Taken<WorktreeState>.Got(new WorktreeState.Registered(folder, onBranch));
        }

        var populated = await IsPopulatedAsync(id, ct);
        return populated.TryGet(out var yes, out var halt)
            ? new Taken<WorktreeState>.Got(new WorktreeState.Registered(true, onBranch, yes))
            : new Taken<WorktreeState>.Halted(halt!);
    }

    private async Task<Taken<bool>> IsPopulatedAsync(string id, CancellationToken ct)
    {
        var index = await c.In(c.Paths.Worktree(id)).IndexPathAsync(ct);
        return index is Outcome<string>.Ok ok
            ? TaskContext.Look(ok.Value)
            : new Taken<bool>.Halted(TaskContext.FailedRead(id, ((Outcome<string>.Fail)index).Reason));
    }

    /// <summary>For a task whose session started: does the <c>.before</c> name a commit the clone knows that <c>tmp/&lt;id&gt;</c> descends from (two questions, so that an unknown sha is a "no", never git's 128 read as the host, M-u), and how many commits the branch holds past it.</summary>
    private async Task<Taken<Descent>> DescentAsync(string id, string beforeSha, CancellationToken ct)
    {
        var known = await c.Clone.CommitPresentAsync(beforeSha, ct);
        if (known is Outcome<string>.Fail knownFailed)
        {
            return Failed(id, knownFailed.Reason);
        }

        if (((Outcome<string>.Ok)known).Value.Length == 0)
        {
            return new Taken<Descent>.Got(new Descent(false, -1));
        }

        var ancestor = await c.Clone.IsAncestorAsync(beforeSha, TaskContext.TmpPrefix + id, ct);
        if (ancestor is Outcome<bool>.Fail ancestorFailed)
        {
            return Failed(id, ancestorFailed.Reason);
        }

        return ((Outcome<bool>.Ok)ancestor).Value ? await CountAsync(id, beforeSha, ct) : new Taken<Descent>.Got(new Descent(false, -1));
    }

    private async Task<Taken<Descent>> CountAsync(string id, string beforeSha, CancellationToken ct)
    {
        var listed = await c.Clone.RevListAsync(beforeSha, TaskContext.TmpPrefix + id, ct);
        return listed is Outcome<IReadOnlyList<string>>.Ok ok
            ? new Taken<Descent>.Got(new Descent(true, ok.Value.Count))
            : Failed(id, ((Outcome<IReadOnlyList<string>>.Fail)listed).Reason);
    }

    private static Taken<Descent> Failed(string id, string reason) => new Taken<Descent>.Halted(TaskContext.FailedRead(id, reason));

    /// <summary>The record as the file holds it: the valid lines and whether the tail is torn; an unreadable line before the last is exit 4 naming the file and the line.</summary>
    public Taken<LoadedRecord> LoadRecord(string id)
    {
        var path = c.Paths.PickRecord(id);
        var name = Path.GetFileName(path);
        return BoundedFile.Read(path, RecordLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Absent => new Taken<LoadedRecord>.Got(new LoadedRecord([], false)),
            BoundedRead.Refused refused => Stop(id, $"{id}: {name} cannot be read: {refused.Reason} — restore the file or start the arm again from a new clone"),
            BoundedRead.Bytes bytes => Parsed(id, name, new UTF8Encoding(false).GetString(bytes.Value)),
            _ => throw new InvalidOperationException("a closed set"),
        };
    }

    private static Taken<LoadedRecord> Parsed(string id, string name, string text) =>
        PickRecord.Parse(text) switch
        {
            PickRecordRead.Lines lines => new Taken<LoadedRecord>.Got(new LoadedRecord(lines.Valid, lines.TornTail)),
            PickRecordRead.Unreadable bad => Stop(id, $"{id}: {name} line {bad.LineNumber.ToString(CultureInfo.InvariantCulture)} cannot be read: {bad.Why} — appending cannot repair it; restore the file or start the arm again from a new clone"),
            _ => throw new InvalidOperationException("a closed set"),
        };

    private static Taken<LoadedRecord> Stop(string id, string why) => new Taken<LoadedRecord>.Halted(new Halt(ExitCodes.Configuration, why, id));

    /// <summary>The commit id <c>task-&lt;id&gt;.before</c> holds, or the empty text when the file is not one commit id (it is model-reachable: read bounded, never trusted).</summary>
    public string ReadBefore(string id) =>
        BoundedFile.Read(c.Paths.TaskBefore(id), BeforeLimit, PlatformFactsReader.CurrentRoad) is BoundedRead.Bytes bytes && new UTF8Encoding(false).GetString(bytes.Value).Trim() is var text && ShaText().IsMatch(text) ? text : string.Empty;

    [GeneratedRegex(@"^[0-9a-f]{40}([0-9a-f]{24})?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ShaText();

    public static bool SamePath(string left, string right) => Canonical(left) == Canonical(right);

    /// <summary>The path with its PARENT resolved by the kernel (the folder itself may be gone): git prints the real path of a worktree, the paths object may spell a link.</summary>
    private static string Canonical(string path)
    {
        var full = Path.GetFullPath(path);
        return Path.GetDirectoryName(full) is { } parent && RootFacts.RealPathOf(parent) is Outcome<string>.Ok real ? Path.Combine(real.Value, Path.GetFileName(full)) : full;
    }
}
