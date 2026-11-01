using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using ModelArms.Core.Tasks;

namespace ModelArms.Core.Checks;

/// <summary>The steps of a scheme that ended on their deadline (plan D8, §4.9, C17): the stems whose <c>.done</c> says <c>hard-deadline</c>, in the scheme's order — the report's <c>stepsOnDeadline</c>, so it never hides that a review or a fix was cut by its budget. A step is asked for once, by its stem; a marker that is
/// absent, refused or says anything else is not on its deadline. A scheme's <c>tasks</c> entry stands where it is in the scheme as the pipeline's stems (E4.S3, E3 §8 item 20): the split, its fix, then one coder per task of <c>work/batches.json</c> in the ids' order — the caller reads the ids and passes none when the batches are not fixed yet.</summary>
public static class DeadlineSteps
{
    private static readonly string HardDeadline = new StepEnd.HardDeadline().Word;

    public static IReadOnlyList<string> Of(Scheme scheme, IReadOnlyList<string> taskIds, Func<string, Read<DoneMarker>> done) =>
        [.. scheme.Entries.SelectMany(entry => StemsOf(entry, taskIds)).Where(stem => done(stem) is Read<DoneMarker>.Present { Value.State: var state } && state == HardDeadline)];

    private static IEnumerable<string> StemsOf(SchemeEntry entry, IReadOnlyList<string> taskIds) =>
        entry is SchemeEntry.Step step ? [step.Name] : [TaskStems.Split, TaskStems.SplitFix, .. taskIds.Select(TaskStems.Coder)];
}
