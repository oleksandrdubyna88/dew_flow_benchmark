using FluentAssertions;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S2, D14 (pure): the re-entry state table of §4.6 as rows of <see cref="TaskRecovery.Decide"/> — every task is read first and each state takes its one action. A resumed session keeps its worktree (ledger 3.1); only a task whose session never started is recreated.
/// <para>
/// What this does NOT prove: that the facts are read correctly (<c>TaskWorktreesTests</c> over real git).
/// </para></summary>
public sealed class TaskRecoveryTests
{
    private static readonly WorktreeState Folder = new WorktreeState.Registered(FolderPresent: true, OnBranch: true);

    private static TaskState State(TaskStep step, bool picked, bool branch, WorktreeState worktree, bool before = true, bool descends = true) =>
        new("t1", step, branch, picked, worktree, before, descends);

    private const string Resumes = "t1: ";

    public static TheoryData<TaskState, TaskAction> Rows() => new()
    {
        { State(TaskStep.Fresh, false, true, Folder), new TaskAction.Recreate("t1") },
        { State(TaskStep.Fresh, false, false, new WorktreeState.Gone()), new TaskAction.Recreate("t1") },
        { State(TaskStep.Fresh, false, true, new WorktreeState.Stray(), before: false), new TaskAction.Recreate("t1") },
        { State(TaskStep.Resume, false, true, Folder), new TaskAction.Keep("t1") },
        { State(TaskStep.Resume, false, true, new WorktreeState.Registered(false, true)), new TaskAction.Reattach("t1", Build: true) },
        { State(TaskStep.Done, false, true, Folder), new TaskAction.PickBack("t1") },
        { State(TaskStep.Done, false, true, new WorktreeState.Registered(true, false)), new TaskAction.PickBack("t1") },
        { State(TaskStep.Done, false, true, new WorktreeState.Registered(false, true)), new TaskAction.Reattach("t1", Build: false) },
        { State(TaskStep.Done, false, true, new WorktreeState.Gone()), new TaskAction.Reattach("t1", Build: false) },
        { State(TaskStep.Done, false, true, new WorktreeState.Stray()), new TaskAction.Reattach("t1", Build: false) },
        { State(TaskStep.Done, true, true, Folder), new TaskAction.Drop("t1") },
        { State(TaskStep.Done, true, false, Folder), new TaskAction.Drop("t1") },
        { State(TaskStep.Done, true, true, new WorktreeState.Gone()), new TaskAction.Drop("t1") },
        { State(TaskStep.Done, true, false, new WorktreeState.Stray()), new TaskAction.Drop("t1") },
        { State(TaskStep.Done, true, false, new WorktreeState.Gone()), new TaskAction.Nothing("t1") },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Every_task_is_read_before_any_cleanup_and_each_state_takes_its_one_action(TaskState state, TaskAction expected)
    {
        TaskRecovery.Decide(state).Should().Be(expected);
    }

    public static TheoryData<TaskState, string> Refusals() => new()
    {
        { State(TaskStep.Resume, false, false, Folder), "tmp/t1 is gone" },
        { State(TaskStep.Resume, false, true, new WorktreeState.Registered(true, false)), "its worktree is on another branch or a detached HEAD, not tmp/t1" },
        { State(TaskStep.Resume, false, true, new WorktreeState.Stray()), "wt/t1 is not a registered worktree" },
        { State(TaskStep.Resume, false, true, new WorktreeState.Gone()), "its worktree and its registration are both gone" },
        { State(TaskStep.Resume, false, true, Folder, before: false), "out/task-t1.before is missing" },
        { State(TaskStep.Resume, false, true, Folder, descends: false), "tmp/t1 does not descend from its out/task-t1.before" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void A_resuming_task_that_does_not_validate_is_refused_with_its_clause_and_the_recovery(TaskState state, string clause)
    {
        var refusal = TaskRecovery.Decide(state).Should().BeOfType<TaskAction.Refuse>().Subject;

        refusal.Id.Should().Be("t1");
        refusal.Why.Should().StartWith("its session resumes but " + clause + " — ");
        refusal.Why.Should().Contain("remove wt/t1, tmp/t1 and notes/t1-code.md to start the task again");
    }

    [Fact]
    public void A_done_task_whose_branch_is_gone_is_refused_whatever_its_worktree_says()
    {
        foreach (var worktree in new WorktreeState[] { Folder, new WorktreeState.Gone(), new WorktreeState.Stray() })
        {
            TaskRecovery.Decide(State(TaskStep.Done, false, false, worktree)).Should().Be(new TaskAction.Refuse("t1", "its step is done but tmp/t1 is gone — its commits exist nowhere else, and a task is never paid for again silently"));
        }
    }

    [Fact]
    public void A_step_is_done_for_skip_and_for_a_recovered_done_alike_and_a_resume_is_not()
    {
        TaskRecovery.StepOf(new StepEntry.Skip()).Should().Be(TaskStep.Done);
        TaskRecovery.StepOf(new StepEntry.RecoveredDone(new StepEnd.Finished(), 1)).Should().Be(TaskStep.Done);
        TaskRecovery.StepOf(new StepEntry.Resume(Guid.NewGuid(), 2, 60)).Should().Be(TaskStep.Resume);
        TaskRecovery.StepOf(new StepEntry.Fresh(1, 60)).Should().Be(TaskStep.Fresh);
        TaskRecovery.IsDone(new StepEntry.RecoveredDone(new StepEnd.HardDeadline(), 1)).Should().BeTrue();
        TaskRecovery.IsDone(new StepEntry.Resume(Guid.NewGuid(), 2, 60)).Should().BeFalse();
    }
}
