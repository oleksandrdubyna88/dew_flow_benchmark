using ModelArms.Core.Steps;

namespace ModelArms.Core.Tasks;

/// <summary>Where a task's step stands, as recovery sees it (D7, D14).</summary>
public enum TaskStep { Fresh, Resume, Done, Unresumable }

/// <summary>What <c>worktree list --porcelain</c> and the folder say about <c>wt/&lt;id&gt;</c>.</summary>
public abstract record WorktreeState
{
    private WorktreeState()
    {
    }

    /// <summary>No registration for <c>wt/&lt;id&gt;</c>, no folder.</summary>
    public sealed record Gone : WorktreeState;

    /// <summary>An entry whose path is <c>wt/&lt;id&gt;</c>. <paramref name="OnBranch"/>: its <c>branch</c> line is <c>refs/heads/tmp/&lt;id&gt;</c> (false: detached, or another branch). <paramref name="FolderPresent"/>: <c>&lt;wt&gt;/.git</c> exists by name (false: the entry is prunable).</summary>
    public sealed record Registered(bool FolderPresent, bool OnBranch, bool Populated = true) : WorktreeState;

    /// <summary>A folder at <c>wt/&lt;id&gt;</c> that no registration names.</summary>
    public sealed record Stray : WorktreeState;
}

/// <summary>Everything the table reads, gathered BEFORE any cleanup (D14). <paramref name="Descends"/>: the <c>.before</c> names a commit the clone knows AND <c>tmp/&lt;id&gt;</c>'s tip descends from it; read for a resumed task with its branch and its <c>.before</c>, false otherwise.</summary>
public sealed record TaskState(string Id, TaskStep Step, bool BranchPresent, bool Picked, WorktreeState Worktree, bool BeforePresent, bool Descends, int Attempt = 1, int CommitsPastBefore = -1);

/// <summary>The one action a state takes.</summary>
public abstract record TaskAction
{
    private TaskAction()
    {
    }

    /// <summary>The session never started: remove the worktree and the branch (forcing, pruning), delete the <c>.before</c>, add anew from HEAD.</summary>
    public sealed record Recreate(string Id) : TaskAction;

    /// <summary>The session resumes and everything validates (ledger 3.1): nothing is touched.</summary>
    public sealed record Keep(string Id) : TaskAction;

    /// <summary>The folder is gone, registration and branch intact: prune, attach to the existing branch, dress it; a build only for a resumed task.</summary>
    public sealed record Reattach(string Id, bool Build) : TaskAction;

    /// <summary>Done, not picked, the folder present: nothing at entry.</summary>
    public sealed record PickBack(string Id) : TaskAction;

    /// <summary>Exit 4: a done task whose branch is gone, or a resumed task whose worktree, branch or <c>.before</c> is inconsistent.</summary>
    public sealed record Refuse(string Id, string Why) : TaskAction;

    /// <summary>Picked, and the worktree or the branch is still there.</summary>
    public sealed record Drop(string Id) : TaskAction;

    /// <summary>Picked, both gone.</summary>
    public sealed record Nothing(string Id) : TaskAction;

    /// <summary>Registered on its branch with the folder present, but the add's checkout never ran (a crash between the two): populate it, dress it, build only for a resumed task.</summary>
    public sealed record Populate(string Id, bool Build) : TaskAction;
}

/// <summary>D14's re-entry as one table (§4.6; ledger 3.1; E4.S2 spec item 11 and the review's items 27 and 29): every task is READ first and each state takes its one action. A step is DONE for <c>Skip</c> and for <c>RecoveredDone</c> alike — the earlier rule (<c>Skip</c> alone) deleted a finished task's branch. Only a session that NEVER started (<c>Fresh</c>, attempt 1)
/// is recreated; a session that ran and cannot be resumed (<c>Fresh</c>, attempt above 1) is recreated only when it left no commit past the task's <c>.before</c>.</summary>
public static class TaskRecovery
{
    public static TaskStep StepOf(StepEntry entry) => entry switch
    {
        StepEntry.Skip or StepEntry.RecoveredDone => TaskStep.Done,
        StepEntry.Resume => TaskStep.Resume,
        StepEntry.Fresh { Attempt: > 1 } => TaskStep.Unresumable,
        _ => TaskStep.Fresh,
    };

    public static bool IsDone(StepEntry entry) => StepOf(entry) == TaskStep.Done;

    public static TaskAction Decide(TaskState state)
    {
        var id = state.Id;
        if (state.Picked)
        {
            return state.Worktree is WorktreeState.Registered or WorktreeState.Stray || state.BranchPresent ? new TaskAction.Drop(id) : new TaskAction.Nothing(id);
        }

        return state.Step switch
        {
            TaskStep.Fresh => new TaskAction.Recreate(id),
            TaskStep.Resume => Resumed(state),
            TaskStep.Unresumable => Unresumable(state),
            _ => Finished(state),
        };
    }

    private static TaskAction Finished(TaskState state) => state.Worktree switch
    {
        _ when !state.BranchPresent => new TaskAction.Refuse(state.Id, $"its step is done but tmp/{state.Id} is gone — its commits exist nowhere else, and a task is never paid for again silently"),
        WorktreeState.Registered { FolderPresent: true, Populated: false } => new TaskAction.Populate(state.Id, Build: false),
        WorktreeState.Registered { FolderPresent: true } => new TaskAction.PickBack(state.Id),
        _ => new TaskAction.Reattach(state.Id, Build: false),
    };

    private static TaskAction Resumed(TaskState state)
    {
        var id = state.Id;
        if (InconsistentClause(state) is { Length: > 0 } clause)
        {
            return new TaskAction.Refuse(id, $"its session resumes but {clause} — recreating it would lose the task's work and resume a session over files that are not there; remove wt/{id}, tmp/{id} and notes/{id}-code.md to start the task again, or restore what is missing, then run me again");
        }

        return state.Worktree switch
        {
            WorktreeState.Registered { FolderPresent: true, Populated: false } => new TaskAction.Populate(id, Build: true),
            WorktreeState.Registered { FolderPresent: true } => new TaskAction.Keep(id),
            _ => new TaskAction.Reattach(id, Build: true),
        };
    }

    /// <summary>A session that RAN and cannot be resumed (its file is unreadable, claude's transcript is gone): its commits are paid work. Recreated only when it left none, or no branch; refused — exit 4, the commit count named — when recreating would delete them, or when the commits cannot be told from the branch's (<c>.before</c>).</summary>
    private static TaskAction Unresumable(TaskState state)
    {
        var id = state.Id;
        if (!state.BranchPresent)
        {
            return new TaskAction.Recreate(id);
        }

        var cannot = state switch
        {
            { BeforePresent: false } => $"out/task-{id}.before is missing",
            { Descends: false } => $"tmp/{id} does not descend from its out/task-{id}.before",
            _ => string.Empty,
        };
        if (cannot.Length > 0)
        {
            return new TaskAction.Refuse(id, $"its session cannot be resumed and {cannot} — the task's commits cannot be told from the branch's; remove wt/{id}, tmp/{id} and notes/{id}-code.md to start the task again, or restore what is missing, then run me again");
        }

        return state.CommitsPastBefore == 0
            ? new TaskAction.Recreate(id)
            : new TaskAction.Refuse(id, $"its session cannot be resumed (attempt {state.Attempt}: the session file or claude's transcript is unreadable) and tmp/{id} holds {state.CommitsPastBefore} commit(s) past its .before — recreating would delete them; pick them back by hand, or remove wt/{id}, tmp/{id} and notes/{id}-code.md to start the task again, then run me again");
    }

    private static string InconsistentClause(TaskState state) => state switch
    {
        { BranchPresent: false } => $"tmp/{state.Id} is gone",
        { Worktree: WorktreeState.Registered { OnBranch: false } } => $"its worktree is on another branch or a detached HEAD, not tmp/{state.Id}",
        { Worktree: WorktreeState.Stray } => $"wt/{state.Id} is not a registered worktree",
        { Worktree: WorktreeState.Gone } => "its worktree and its registration are both gone",
        { BeforePresent: false } => $"out/task-{state.Id}.before is missing",
        { Descends: false } => $"tmp/{state.Id} does not descend from its out/task-{state.Id}.before",
        _ => string.Empty,
    };
}
