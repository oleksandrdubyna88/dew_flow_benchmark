using Bench.Cli;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>The one reading of "which arm is this, and which scheme does it run" that every verb taking an arm asks (<c>run-arm</c>, <c>checks</c>; plan D16): the arm's letters must be among the experiment's schemes. A scheme with a <c>tasks</c> block is refused for a verb that WALKS it (<c>run-arm</c>, until the task pipeline lands in E4.S3) and
/// allowed for one that only measures what the arm left (<c>checks</c>: the objective report needs no step of the pipeline). Extracted from <c>RunArmAdmission</c>, which calls it (<c>common.reuse-first</c>); the sentences are prefixed <c>model-arms &lt;verb&gt;:</c>.</summary>
internal static class ArmChoices
{
    public static Passage<ArmChoice> Of(ExperimentInput input, string verb, bool allowTasks)
    {
        var name = $"model-arms {verb}";
        var arm = input.Verb.Positionals[0];
        return ArmNames.SchemeOf(arm).Match(scheme => Known(input, name, arm, scheme, allowTasks), reason => Passage<ArmChoice>.Stop(ExitCodes.Configuration, $"{name}: {reason}"));
    }

    private static Passage<ArmChoice> Known(ExperimentInput input, string name, string arm, string schemeName, bool allowTasks)
    {
        var schemes = input.Experiment.Schemes;
        if (schemes.FirstOrDefault(scheme => scheme.Name == schemeName) is not { } scheme)
        {
            return Passage<ArmChoice>.Stop(ExitCodes.Configuration, $"{name}: {arm} belongs to scheme {schemeName}, which is not among the schemes ({string.Join(", ", schemes.Select(each => each.Name))})");
        }

        return !allowTasks && scheme.Entries.Any(entry => entry is SchemeEntry.Tasks)
            ? Passage<ArmChoice>.Stop(ExitCodes.Configuration, $"{name}: {arm}'s scheme runs the task pipeline — not built yet, story E4.S3 of todo/PLAN_model_arms.md")
            : new Passage<ArmChoice>.Through(new ArmChoice(arm, scheme));
    }
}
