using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Checks;

/// <summary>The steps of a scheme that ended on their deadline (plan D8, §4.9, C17): the stems whose <c>.done</c> says <c>hard-deadline</c>, in the scheme's order — the report's <c>stepsOnDeadline</c>, so it never hides that a review or a fix was cut by its budget. A step is asked for once, by its stem; a marker that is
/// absent, refused or says anything else is not on its deadline. The scheme's <c>tasks</c> block is not a step with a marker of its own here (E3 §8 item 20: the task coders' stems are E4's to name).</summary>
public static class DeadlineSteps
{
    private static readonly string HardDeadline = new StepEnd.HardDeadline().Word;

    public static IReadOnlyList<string> Of(Scheme scheme, Func<string, Read<DoneMarker>> done) =>
        [.. scheme.Entries.OfType<SchemeEntry.Step>().Select(step => step.Name).Where(stem => done(stem) is Read<DoneMarker>.Present { Value.State: var state } && state == HardDeadline)];
}
