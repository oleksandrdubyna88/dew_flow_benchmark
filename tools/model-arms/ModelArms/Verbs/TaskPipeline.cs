using System.Globalization;
using System.Text;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;

namespace ModelArms.Verbs;

/// <summary>The walk of a scheme's <c>tasks</c> entry (plan D14, D25, §4.6; E4.S3): the split, the checker's one fix round, <c>batches.json</c> fixed once, then each batch — its tasks at once as parallel <c>Task</c>s of THIS process, each a coder in its own worktree (a batch of ONE runs in the clone itself) — and the pick-back in task
/// order. The worktrees, the pick-back and their re-entry are <see cref="TaskWorktrees"/> (E4.S2); a step is <see cref="StepRunner"/>'s; this type only orders them.
/// <para>
/// <b>Batches fixed earlier come first</b>: when <c>work/batches.json</c> exists the checked list must produce exactly its text, before any fix round — a fix round would rewrite <c>work/tasks.json</c> under existing <c>batch-&lt;i&gt;.done</c> markers (§8 item 8). <b>A done task never gets a <c>.before</c></b>: its folder's HEAD is the tip of its branch, and a
/// <c>.before</c> written there would make the pick range empty and let the drop delete its commits. <b>A batch ends after every task ended</b> (cancelling a healthy sibling costs a paid attempt), with 4 before 3 before 5 (§8 item 7).
/// </para>
/// <para>
/// D26 (a)–(f): (a) <c>work/tasks.json</c> and <c>batches.json</c> are model-written and read through <see cref="BoundedFile"/> (1 MiB), a brief by NAME only (<see cref="KernelFacts.Present"/>, a name the checker accepted as ONE file name), every marker by name; (b) every git is a question on <see cref="ArmGit"/> by the REAL path
/// <c>run-arm</c> resolved, and the build programs are the paths admission resolved and printed; (c)–(f) are the steps' and <see cref="TaskWorktrees"/>'s own. What this does NOT do: serialise with a model process still running in the clone (E3 §8 item 17); pick or drop for a batch of one.
/// </para></summary>
internal sealed class TaskPipeline
{
    private const int ListLimit = 1024 * 1024;
    private const int BeforeLimit = 4 * 1024;
    private static readonly int[] Severity = [ExitCodes.Configuration, ExitCodes.Environment, 5];

    private readonly ArmPlan _plan;
    private readonly Func<int, StepContext> _contextAt;
    private readonly StepRunner _runner;
    private readonly StepLog _log;
    private readonly ArmFiles _files;
    private readonly TaskWorktrees _worktrees;
    private readonly TaskLimits _limits;
    private readonly ArmPaths _paths;
    private Outcome<TaskBatches>? _listing;
    private TaskBatches _batches = new([], []);

    public TaskPipeline(ArmPlan plan, Func<int, StepContext> contextAt, StepRunner runner, StepLog log, ArmFiles files)
    {
        _plan = plan;
        _contextAt = contextAt;
        _runner = runner;
        _log = log;
        _files = files;
        _paths = plan.Paths;
        _limits = plan.Input.Experiment.Tasks;
        _worktrees = new TaskWorktrees(plan.Paths, plan.Input.Experiment.Target, new TaskTools(plan.Tools.Git, plan.Tools.Build, plan.Input.Verb.Environment), log);
    }

    /// <summary>Null: every batch is done and the walk goes on to the next entry; else the stop (D16).</summary>
    public async Task<StepOutcome.Stopped?> RunAsync(int position, CancellationToken stopping)
    {
        try
        {
            return await WalkAsync(position, stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return _log.Stop("tasks", 5, "the operator stopped the arm");
        }
    }

    private async Task<StepOutcome.Stopped?> WalkAsync(int position, CancellationToken stopping)
    {
        if (await _runner.RunAsync(Block(position, TaskStems.Split, _limits.Split, string.Empty), stopping) is StepOutcome.Stopped splitStopped)
        {
            return splitStopped;
        }

        if (Check() is { } checkStopped)
        {
            return checkStopped;
        }

        if (await FixedAsync(position, stopping) is { } fixStopped)
        {
            return fixStopped;
        }

        return await BatchesAsync(position, stopping);
    }

    // ---- the list: check, compare, one fix round, fix once ------------------------------------------------------------------------------------------------------

    /// <summary>Step 2: <c>work/tasks.json</c> checked into <see cref="_listing"/>, <c>out/tasks-plan.log</c> replaced with what the checker said, one log line. A file the road refuses (a link, a FIFO, past 1 MiB) is exit 4.</summary>
    private StepOutcome.Stopped? Check()
    {
        var text = BoundedFile.Read(_paths.TasksJson, ListLimit, PlatformFactsReader.CurrentRoad);
        if (text is BoundedRead.Refused refused)
        {
            return _log.Stop(TaskStems.Split, ExitCodes.Configuration, $"work/tasks.json is refused: {refused.Reason}");
        }

        var json = text is BoundedRead.Bytes bytes ? Encoding.UTF8.GetString(bytes.Value) : string.Empty;
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in TasksPlan.BriefNames(json))
        {
            var there = KernelFacts.Present(Path.Combine(_paths.Work, name));
            if (there is Outcome<bool>.Fail failed)
            {
                return _log.Stop(TaskStems.Split, ExitCodes.Environment, failed.Reason);
            }

            if (there is Outcome<bool>.Ok { Value: true })
            {
                present.Add(name);
            }
        }

        _listing = TasksPlan.Validate(json, _plan.Input.Experiment.Target.Plan, _plan.ScenarioIds, _limits, present, _paths.Work);
        var said = _listing.Match(TasksPlan.Ok, reason => reason);
        _files.Replace(_paths.TasksPlanLog, said + "\n");
        _log.Line(_listing is Outcome<TaskBatches>.Ok ? said : "the task list has problems — one fix round: " + OneLine(said));
        return null;
    }

    private static string OneLine(string problems) => string.Join(' ', problems.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Steps 3–5: the batches fixed earlier win over everything; else one fix round, then the batches are written once.</summary>
    private async Task<StepOutcome.Stopped?> FixedAsync(int position, CancellationToken stopping)
    {
        var there = KernelFacts.Present(_paths.BatchesJson);
        if (there is Outcome<bool>.Fail failed)
        {
            return _log.Stop(TaskStems.Split, ExitCodes.Environment, failed.Reason);
        }

        if (there is Outcome<bool>.Ok { Value: true })
        {
            return Compared();
        }

        if (_listing is Outcome<TaskBatches>.Fail refusedFirst)
        {
            if (await _runner.RunAsync(Block(position, TaskStems.SplitFix, _limits.SplitFix, refusedFirst.Reason), stopping) is StepOutcome.Stopped fixStopped)
            {
                return fixStopped;
            }

            if (Check() is { } checkStopped)
            {
                return checkStopped;
            }

            if (_listing is Outcome<TaskBatches>.Fail stillRefused)
            {
                return _log.Stop(TaskStems.SplitFix, ExitCodes.Configuration, $"the task list is still invalid: {OneLine(stillRefused.Reason)}");
            }
        }

        _batches = ((Outcome<TaskBatches>.Ok)_listing!).Value;
        _files.WriteOnce(_paths.BatchesJson, BatchesFile.Write(_batches, _paths.Work));
        return null;
    }

    /// <summary>Step 3: <c>work/batches.json</c> exists. The list just checked must write exactly its text; a list the check refused, one whose text differs, and a file that does not read are all "changed after the batches were fixed" — <c>batches.json</c> is written only from a list the check passed.</summary>
    private StepOutcome.Stopped? Compared()
    {
        var fixedText = BoundedFile.Read(_paths.BatchesJson, ListLimit, PlatformFactsReader.CurrentRoad);
        if (fixedText is not BoundedRead.Bytes bytes)
        {
            return _log.Stop(TaskStems.Split, ExitCodes.Configuration, $"work/batches.json cannot be read: {(fixedText as BoundedRead.Refused)?.Reason ?? "it vanished"} — start the arm again from a new clone");
        }

        var text = Encoding.UTF8.GetString(bytes.Value);
        if (_listing is not Outcome<TaskBatches>.Ok ok || BatchesFile.Write(ok.Value, _paths.Work) != text)
        {
            return _log.Stop(TaskStems.Split, ExitCodes.Configuration, "work/tasks.json changed after the batches were fixed in work/batches.json — restore it, or start the arm again from a new clone");
        }

        _batches = ok.Value;
        return null;
    }

    // ---- the batches --------------------------------------------------------------------------------------------------------------------------------------------

    private async Task<StepOutcome.Stopped?> BatchesAsync(int position, CancellationToken stopping)
    {
        await WarnAboutTheCloneAsync(stopping);
        for (var index = 0; index < _batches.Batches.Count; index++)
        {
            if (await BatchAsync(position, index, stopping) is { } stopped)
            {
                return stopped;
            }
        }

        return null;
    }

    /// <summary><c>t-lib.sh:88</c>: a split that left changes in the clone is said; nothing else happens.</summary>
    private async Task WarnAboutTheCloneAsync(CancellationToken stopping)
    {
        var count = await new ArmGit(_plan.Tools.Git, _paths.CloneFolder, _plan.Input.Verb.Environment).UncommittedCountAsync(stopping);
        if (count is Outcome<int>.Ok { Value: > 0 } changed)
        {
            _log.Line($"WARNING: the split left {changed.Value.ToString(CultureInfo.InvariantCulture)} uncommitted change(s) in the clone — the coders' worktrees are cut from its HEAD, so they do not see them");
        }
    }

    private async Task<StepOutcome.Stopped?> BatchAsync(int position, int index, CancellationToken stopping)
    {
        var batch = _batches.Batches[index];
        var number = index.ToString(CultureInfo.InvariantCulture);
        if (Look(_paths.BatchDone(index), out var isDone) is { } doneStop)
        {
            return doneStop;
        }

        if (isDone)
        {
            _log.Line($"batch {number} already done — skipped");
            return null;
        }

        var kept = new Dictionary<string, StepEntry>(StringComparer.Ordinal);
        if (batch.Count == 1)
        {
            kept[batch[0]] = _runner.Recover(Coder(position, batch[0], _paths.CloneFolder));
        }

        var entry = await _worktrees.EnterAsync(batch, id => kept[id] = _runner.Recover(Coder(position, id, _paths.Worktree(id))), stopping);
        if (entry is BatchEntry.Stopped entryStopped)
        {
            return new StepOutcome.Stopped(entryStopped.ExitCode, entryStopped.Why);
        }

        var runs = ((BatchEntry.Ready)entry).Places.OfType<BatchPlace.Run>().ToList();
        foreach (var run in runs.Where(run => !TaskRecovery.IsDone(kept[run.Id])))
        {
            if (await BeforeAsync(run, stopping) is { } beforeStopped)
            {
                return beforeStopped;
            }
        }

        if (await RunTogetherAsync(position, number, runs, stopping) is { } stopped)
        {
            return stopped;
        }

        return batch.Count == 1 ? Marked(index) : await CarriedAsync(batch, runs, index, stopping);
    }

    /// <summary>Whether <paramref name="path"/> is there, asked by NAME; a kernel that cannot say is the host's stop (exit 3).</summary>
    private StepOutcome.Stopped? Look(string path, out bool there)
    {
        var asked = KernelFacts.Present(path);
        there = asked is Outcome<bool>.Ok { Value: true };
        return asked is Outcome<bool>.Fail failed ? _log.Stop("tasks", ExitCodes.Environment, failed.Reason) : null;
    }

    /// <summary><c>out/task-&lt;id&gt;.before</c> once, from the HEAD of the folder the task runs in, BEFORE the coder's first launch (the pick range starts there). Only for a task whose step is not done — see the class summary.</summary>
    private async Task<StepOutcome.Stopped?> BeforeAsync(BatchPlace.Run run, CancellationToken stopping)
    {
        if (Look(_paths.TaskBefore(run.Id), out var exists) is { } stop)
        {
            return stop;
        }

        if (exists)
        {
            return null;
        }

        var head = await new ArmGit(_plan.Tools.Git, run.Folder, _plan.Input.Verb.Environment).HeadAsync(stopping);
        if (head is Outcome<string>.Fail failed)
        {
            return _log.Stop(TaskStems.Coder(run.Id), ExitCodes.Environment, $"{run.Id}: HEAD of {run.Folder} cannot be read: {failed.Reason}");
        }

        _files.WriteOnce(_paths.TaskBefore(run.Id), ((Outcome<string>.Ok)head).Value + "\n");
        return null;
    }

    /// <summary>All the batch's coders at once; none is cancelled because another stopped. After the last ended: the stops, most severe first.</summary>
    private async Task<StepOutcome.Stopped?> RunTogetherAsync(int position, string number, IReadOnlyList<BatchPlace.Run> runs, CancellationToken stopping)
    {
        var outcomes = await Task.WhenAll(runs.Select(run => _runner.RunAsync(Coder(position, run.Id, run.Folder), stopping)));
        var stops = runs.Zip(outcomes, (run, outcome) => (run.Id, Outcome: outcome as StepOutcome.Stopped)).Where(each => each.Outcome is not null).ToList();
        if (stops.Count == 0)
        {
            return null;
        }

        var code = Severity.First(severe => stops.Any(each => each.Outcome!.ExitCode == severe));
        var why = code == 5
            ? $"a task of batch {number} was interrupted — run me again to resume it"
            : string.Join("; ", stops.Where(each => each.Outcome!.ExitCode == code).Select(each => $"{each.Id}: {each.Outcome!.Why}"));
        return new StepOutcome.Stopped(code, why);
    }

    /// <summary>A batch of many, every coder done: each task picked back in task order, then every worktree dropped, then the marker.</summary>
    private async Task<StepOutcome.Stopped?> CarriedAsync(IReadOnlyList<string> batch, IReadOnlyList<BatchPlace.Run> runs, int index, CancellationToken stopping)
    {
        foreach (var run in runs)
        {
            if (await _worktrees.PickBackAsync(run.Id, stopping) is { } pickStopped)
            {
                return pickStopped;
            }
        }

        foreach (var id in batch)
        {
            if (await _worktrees.DropAsync(id, stopping) is { } dropStopped)
            {
                return dropStopped;
            }
        }

        return Marked(index);
    }

    private StepOutcome.Stopped? Marked(int index)
    {
        _files.Replace(_paths.BatchDone(index), string.Empty);
        return null;
    }

    // ---- the contexts -------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>One of the block's own steps (the split, its fix): the block's position, the step the stem names, the clone as the folder.</summary>
    private StepContext Block(int position, string stem, StepId id, string problems) =>
        _contextAt(position) with { Placed = new SchemeEntry.Step(stem, id), Problems = problems };

    /// <summary>The coder of <paramref name="id"/> in <paramref name="folder"/>: the shell's values (<c>t-lib.sh:51-52</c>) — the task, its brief by absolute path, its files in back quotes, and <c>BEFORE</c> as the SHA the <c>.before</c> holds (not its path), the empty text before it exists.</summary>
    private StepContext Coder(int position, string id, string folder)
    {
        var task = _batches.Task(id);
        return _contextAt(position) with
        {
            Placed = new SchemeEntry.Step(TaskStems.Coder(id), _limits.TaskCode),
            WorkingDirectory = folder,
            Task = new TaskFacts(id, _paths.Work + "/" + task.Brief, TaskBriefFields.Files(task), BeforeText(id)),
        };
    }

    private string BeforeText(string id) =>
        BoundedFile.Read(_paths.TaskBefore(id), BeforeLimit, PlatformFactsReader.CurrentRoad) is BoundedRead.Bytes bytes ? Encoding.UTF8.GetString(bytes.Value).Trim() : string.Empty;
}
