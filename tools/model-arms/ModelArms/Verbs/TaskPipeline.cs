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
    private const int InterruptedCode = 5;
    private static readonly int[] Severity = [ExitCodes.Configuration, ExitCodes.Environment, InterruptedCode];

    private readonly ArmPlan _plan;
    private readonly Func<int, StepContext> _contextAt;
    private readonly StepRunner _runner;
    private readonly StepLog _log;
    private readonly ArmFiles _files;
    private readonly TaskWorktrees _worktrees;
    private readonly TaskLimits _limits;
    private readonly ArmPaths _paths;
    private Outcome<TaskBatches> _listing = Outcome<TaskBatches>.Failure("work/tasks.json was not checked");
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

    /// <summary>The split, the check, the list fixed (compared, or one fix round and written), the batches: the first stop ends the walk.</summary>
    private async Task<StepOutcome.Stopped?> WalkAsync(int position, CancellationToken stopping)
    {
        Func<Task<StepOutcome.Stopped?>>[] stages =
        [
            () => SplitAsync(position, stopping),
            () => Task.FromResult(Check()),
            () => FixedAsync(position, stopping),
            () => BatchesAsync(position, stopping),
        ];
        foreach (var stage in stages)
        {
            if (await stage() is { } stopped)
            {
                return stopped;
            }
        }

        return null;
    }

    private static readonly Task<StepOutcome.Stopped?> Continue = Task.FromResult<StepOutcome.Stopped?>(null);

    private StepOutcome.Stopped HostStop(string reason) => _log.Stop("tasks", ExitCodes.Environment, reason);

    private Task<StepOutcome.Stopped?> HostHalt(string reason) => Task.FromResult<StepOutcome.Stopped?>(HostStop(reason));

    private async Task<StepOutcome.Stopped?> SplitAsync(int position, CancellationToken stopping) =>
        await _runner.RunAsync(Block(position, TaskStems.Split, _limits.Split, string.Empty), stopping) as StepOutcome.Stopped;

    // ---- the list: check, compare, one fix round, fix once ------------------------------------------------------------------------------------------------------

    /// <summary>Step 2: <c>work/tasks.json</c> checked into <see cref="_listing"/>, <c>out/tasks-plan.log</c> replaced with what the checker said. A file the road refuses (a link, a FIFO, past 1 MiB) is exit 4.</summary>
    private StepOutcome.Stopped? Check() =>
        ListText().Match(
            json => Judge(json),
            reason => _log.Stop(TaskStems.Split, ExitCodes.Configuration, $"work/tasks.json is refused: {reason}"));

    private Outcome<string> ListText() =>
        BoundedFile.Read(_paths.TasksJson, ListLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Bytes bytes => Outcome<string>.Success(Encoding.UTF8.GetString(bytes.Value)),
            BoundedRead.Refused refused => Outcome<string>.Failure(refused.Reason),
            _ => Outcome<string>.Success(string.Empty),
        };

    private StepOutcome.Stopped? Judge(string json) =>
        BriefsThere(TasksPlan.BriefNames(json)).Match(
            present => Validated(json, present),
            reason => _log.Stop(TaskStems.Split, ExitCodes.Environment, reason));

    /// <summary>The names among <paramref name="names"/> that are there, each asked by NAME; a kernel that cannot say fails the whole question.</summary>
    private Outcome<IReadOnlySet<string>> BriefsThere(IEnumerable<string> names)
    {
        var there = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var asked = KernelFacts.Present(Path.Combine(_paths.Work, name));
            if (asked is Outcome<bool>.Fail failed)
            {
                return Outcome<IReadOnlySet<string>>.Failure(failed.Reason);
            }

            if (asked is Outcome<bool>.Ok { Value: true })
            {
                there.Add(name);
            }
        }

        return Outcome<IReadOnlySet<string>>.Success(there);
    }

    private StepOutcome.Stopped? Validated(string json, IReadOnlySet<string> present)
    {
        _listing = TasksPlan.Validate(json, _plan.Input.Experiment.Target.Plan, _plan.ScenarioIds, _limits, present, _paths.Work);
        var said = _listing.Match(TasksPlan.Ok, reason => reason);
        _files.Replace(_paths.TasksPlanLog, said + "\n");
        if (_listing is Outcome<TaskBatches>.Ok)
        {
            _log.Line(said);
        }

        return null;
    }

    private static string OneLine(string problems) => string.Join(' ', problems.Split('\n', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Steps 3–5: the batches fixed earlier win over everything; else the fix round (when one is due), then the batches are written once.</summary>
    private Task<StepOutcome.Stopped?> FixedAsync(int position, CancellationToken stopping) =>
        KernelFacts.Present(_paths.BatchesJson).Match(
            fixedAlready => fixedAlready ? Task.FromResult(Compared()) : RoundAndWriteAsync(position, stopping),
            reason => HostHalt(reason));

    private async Task<StepOutcome.Stopped?> RoundAsync(int position, CancellationToken stopping)
    {
        var entry = _runner.Recover(Block(position, TaskStems.SplitFix, _limits.SplitFix, string.Empty));
        var refused = _listing.Match(_ => string.Empty, reason => reason);
        if (refused.Length == 0 && entry is not (StepEntry.Resume or StepEntry.RecoveredDone))
        {
            return null;
        }

        if (refused.Length > 0 && entry is not StepEntry.Skip)
        {
            _log.Line("the task list has problems — one fix round: " + OneLine(refused));
        }

        return await _runner.RunAsync(Block(position, TaskStems.SplitFix, _limits.SplitFix, refused), stopping) as StepOutcome.Stopped ?? Check() ?? StillInvalid();
    }

    private async Task<StepOutcome.Stopped?> RoundAndWriteAsync(int position, CancellationToken stopping) =>
        await RoundAsync(position, stopping) ?? Written();

    /// <summary>A list the one fix round left refused ends the arm (exit 4).</summary>
    private StepOutcome.Stopped? StillInvalid() =>
        _listing.Match<StepOutcome.Stopped?>(_ => null, problems => _log.Stop(TaskStems.SplitFix, ExitCodes.Configuration, $"the task list is still invalid: {OneLine(problems)}"));

    /// <summary>The list that passed, as the batches: <c>work/batches.json</c> written ONCE.</summary>
    private StepOutcome.Stopped? Written() =>
        _listing.Match<StepOutcome.Stopped?>(
            batches =>
            {
                _batches = batches;
                _files.WriteOnce(_paths.BatchesJson, BatchesFile.Write(batches, _paths.Work));
                return null;
            },
            problems => _log.Stop(TaskStems.SplitFix, ExitCodes.Configuration, $"the task list is invalid: {OneLine(problems)}"));

    /// <summary>Step 3: <c>work/batches.json</c> exists. The list just checked must write exactly its text — unless every batch is done already, when nothing is left to run and a later edit of <c>tasks.json</c> (a fixer's, a reviewer's) changes nothing; a list the check refused, one whose text differs, and a file
    /// that does not read are all "changed after the batches were fixed" — <c>batches.json</c> is written only from a list the check passed.</summary>
    private StepOutcome.Stopped? Compared() =>
        FixedText().Match(
            text => BatchesFile.Read(text, _paths.Work).Match(
                fixedBatches => Accepted(fixedBatches, text),
                reason => _log.Stop(TaskStems.Split, ExitCodes.Configuration, $"work/batches.json is refused: {reason}")),
            reason => _log.Stop(TaskStems.Split, ExitCodes.Configuration, $"work/batches.json cannot be read: {reason} — start the arm again from a new clone"));

    private Outcome<string> FixedText() =>
        BoundedFile.Read(_paths.BatchesJson, ListLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Bytes bytes => Outcome<string>.Success(Encoding.UTF8.GetString(bytes.Value)),
            BoundedRead.Refused refused => Outcome<string>.Failure(refused.Reason),
            _ => Outcome<string>.Failure("it vanished"),
        };

    private StepOutcome.Stopped? Accepted(TaskBatches fixedBatches, string text)
    {
        _batches = fixedBatches;
        return EveryBatchDone(fixedBatches) || _listing.Match(now => BatchesFile.Write(now, _paths.Work) == text, _ => false)
            ? null
            : _log.Stop(TaskStems.Split, ExitCodes.Configuration, "work/tasks.json changed after the batches were fixed in work/batches.json — restore it, or start the arm again from a new clone");
    }

    private bool EveryBatchDone(TaskBatches batches) =>
        Enumerable.Range(0, batches.Batches.Count).All(index => KernelFacts.Present(_paths.BatchDone(index)) is Outcome<bool>.Ok { Value: true });

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

    private Task<StepOutcome.Stopped?> BatchAsync(int position, int index, CancellationToken stopping) =>
        KernelFacts.Present(_paths.BatchDone(index)).Match(
            done => done ? Skipped(index) : EnteredAsync(position, index, stopping),
            reason => HostHalt(reason));

    private Task<StepOutcome.Stopped?> Skipped(int index)
    {
        _log.Line($"batch {index.ToString(CultureInfo.InvariantCulture)} already done — skipped");
        return Continue;
    }

    /// <summary>Re-entry reads before it cleans (E4.S2), the step of every task is read ONCE, and the batch's coders run on what it answered.</summary>
    private async Task<StepOutcome.Stopped?> EnteredAsync(int position, int index, CancellationToken stopping)
    {
        var batch = _batches.Batches[index];
        var kept = new Dictionary<string, StepEntry>(StringComparer.Ordinal);
        if (batch.Count == 1)
        {
            kept[batch[0]] = _runner.Recover(Coder(position, batch[0], _paths.CloneFolder));
        }

        var entry = await _worktrees.EnterAsync(batch, id => kept[id] = _runner.Recover(Coder(position, id, _paths.Worktree(id))), stopping);
        return entry switch
        {
            BatchEntry.Ready ready => await RanAsync(position, index, ready.Places.OfType<BatchPlace.Run>().ToList(), kept, stopping),
            BatchEntry.Stopped halted => new StepOutcome.Stopped(halted.ExitCode, halted.Why),
            _ => throw new InvalidOperationException("a closed set"),
        };
    }

    private async Task<StepOutcome.Stopped?> RanAsync(int position, int index, IReadOnlyList<BatchPlace.Run> runs, IReadOnlyDictionary<string, StepEntry> kept, CancellationToken stopping) =>
        await BeforeAllAsync(runs.Where(run => !TaskRecovery.IsDone(kept[run.Id])), stopping)
        ?? await RunTogetherAsync(position, index, runs, stopping)
        ?? (_batches.Batches[index].Count == 1 ? Marked(index) : await CarriedAsync(_batches.Batches[index], runs, index, stopping));

    private async Task<StepOutcome.Stopped?> BeforeAllAsync(IEnumerable<BatchPlace.Run> runs, CancellationToken stopping)
    {
        foreach (var run in runs)
        {
            if (await BeforeAsync(run, stopping) is { } stopped)
            {
                return stopped;
            }
        }

        return null;
    }

    /// <summary><c>out/task-&lt;id&gt;.before</c> once, from the HEAD of the folder the task runs in, BEFORE the coder's first launch (the pick range starts there). Only for a task whose step is not done — see the class summary.</summary>
    private Task<StepOutcome.Stopped?> BeforeAsync(BatchPlace.Run run, CancellationToken stopping) =>
        KernelFacts.Present(_paths.TaskBefore(run.Id)).Match(
            exists => exists ? Continue : WriteBeforeAsync(run, stopping),
            reason => HostHalt(reason));

    private async Task<StepOutcome.Stopped?> WriteBeforeAsync(BatchPlace.Run run, CancellationToken stopping) =>
        (await new ArmGit(_plan.Tools.Git, run.Folder, _plan.Input.Verb.Environment).HeadAsync(stopping)).Match<StepOutcome.Stopped?>(
            head =>
            {
                _files.WriteOnce(_paths.TaskBefore(run.Id), head + "\n");
                return null;
            },
            reason => _log.Stop(TaskStems.Coder(run.Id), ExitCodes.Environment, $"{run.Id}: HEAD of {run.Folder} cannot be read: {reason}"));

    /// <summary>All the batch's coders at once; none is cancelled because another stopped. After the last ended: the stops, most severe first.</summary>
    private async Task<StepOutcome.Stopped?> RunTogetherAsync(int position, int index, IReadOnlyList<BatchPlace.Run> runs, CancellationToken stopping)
    {
        var outcomes = await Task.WhenAll(runs.Select(run => _runner.RunAsync(Coder(position, run.Id, run.Folder), stopping)));
        var stops = runs.Zip(outcomes, (run, outcome) => (run.Id, Outcome: outcome as StepOutcome.Stopped)).Where(each => each.Outcome is not null).Select(each => (each.Id, Stopped: each.Outcome!)).ToList();
        return stops.Count == 0 ? null : Verdict(index, stops);
    }

    /// <summary>The batch's stop: the most severe code among the tasks' (4, then 3, then 5). A code the pipeline does not rank is taken as the most severe of all, and said.</summary>
    private StepOutcome.Stopped Verdict(int index, IReadOnlyList<(string Id, StepOutcome.Stopped Stopped)> stops)
    {
        var worst = MostSevere(stops.Select(each => each.Stopped.ExitCode));
        if (Rank(worst) < 0)
        {
            _log.Line($"a task stopped with exit {worst.ToString(CultureInfo.InvariantCulture)}, which the pipeline does not rank — taken as the most severe");
        }

        return new StepOutcome.Stopped(worst, worst == InterruptedCode
            ? $"a task of batch {index.ToString(CultureInfo.InvariantCulture)} was interrupted — run me again to resume it"
            : string.Join("; ", stops.Where(each => each.Stopped.ExitCode == worst).Select(each => $"{each.Id}: {each.Stopped.Why}")));
    }

    /// <summary>4 outranks 3 outranks 5; a code none of them is outranks all three (a total function: the runner's codes are those three today, and a fourth must not turn a stopped batch into an exception).</summary>
    internal static int MostSevere(IEnumerable<int> codes) => codes.MinBy(Rank);

    private static int Rank(int code) => Array.IndexOf(Severity, code);

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
