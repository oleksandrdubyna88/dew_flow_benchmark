using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Verbs;

/// <summary>The arm's scheme and name, once the experiment has said it runs here.</summary>
internal sealed record ArmChoice(string Arm, Scheme Scheme);

/// <summary>Where the arm lives: the map that holds its token and the paths that token names.</summary>
internal sealed record ArmPlaced(ArmChoice Choice, ArmMap Map, ArmPaths Paths);

/// <summary>The two executables <c>run-arm</c> resolved: the REAL paths, printed and launched as one value (D17 c, d).</summary>
internal sealed record ArmTools(string Claude, string Git);

/// <summary>Everything the walk needs, once admission has let the arm in.</summary>
internal sealed record ArmPlan(ExperimentInput Input, string Arm, Scheme Scheme, ArmMap Map, ArmPaths Paths, ArmTools Tools, int ScenarioCount);

/// <summary>Admission for <c>run-arm</c>: each refusal ends the walk, so a later check never runs on what an earlier one refused and nothing is launched before all of them have passed — the scheme (the arm's letters must be among the experiment's
/// schemes, and a <c>tasks</c> block waits for E4), the arm's token from <c>arms.map.json</c>, the PATH the child programs inherit and the two executables that PATH resolves, and <c>scenarios.json</c> as the gate judged it. The PATH is judged before the
/// executables are looked up in it: git and claude look their own helpers up there for hours, and <c>validate</c>'s judgement is not carried over by magic. <c>HOME</c> and <c>XDG_CONFIG_HOME</c> are judged next, by the same helper <c>validate</c> uses (D26 c: git reads its global config there and a claude its settings and hooks). Exit 3 is the host (a CLI that is not there, a file that cannot be read), exit 4 a wrong configuration.</summary>
internal static class RunArmAdmission
{
    private const string Verb = "run-arm";
    private const string Name = "model-arms " + Verb;
    private static readonly TimeSpan LockPoll = TimeSpan.FromSeconds(2);

    public static Task<Passage<ArmPlan>> AdmitAsync(ExperimentInput input) =>
        Task.FromResult(Choose(input))
            .Then(choice => Place(input, choice))
            .Then(placed => Equip(input, placed))
            .ThenAsync(plan => CountedAsync(plan));

    /// <summary>The root's own gate lock, or the one a test handed in; a host that cannot lock is exit 3 before anything is taken.</summary>
    public static Outcome<IGateLock> LockFor(RunArmSeams seams) =>
        seams.Lock is { } given
            ? Outcome<IGateLock>.Success(given)
            : FileGateLock.Create(TimeProvider.System, LockPoll).Match(created => Outcome<IGateLock>.Success(created), Outcome<IGateLock>.Failure);

    private static Passage<ArmChoice> Choose(ExperimentInput input) => ArmChoices.Of(input, Verb, allowTasks: false);

    private static Passage<ArmPlaced> Place(ExperimentInput input, ArmChoice choice)
    {
        try
        {
            return MapOf(input, choice, File.ReadAllText(input.Root.ArmMapFile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<ArmPlaced>.Stop(ExitCodes.Environment, $"{Name}: {input.Root.ArmMapFile} cannot be read: {ex.Message}");
        }
    }

    private static Passage<ArmPlaced> MapOf(ExperimentInput input, ArmChoice choice, string json) =>
        ArmMap.Read(json).Match(
            map => map.TokenOf(choice.Arm).Match(
                token => new Passage<ArmPlaced>.Through(new ArmPlaced(choice, map, input.Root.Arm(token))),
                reason => Passage<ArmPlaced>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")),
            reason => Passage<ArmPlaced>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {reason}"));

    private static Passage<ArmPlan> Equip(ExperimentInput input, ArmPlaced placed)
    {
        var environment = input.Verb.Environment;
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        var refusal = LaunchAdmission.FirstRefusal(environment, roots);
        return refusal.Length > 0
            ? Passage<ArmPlan>.Stop(ExitCodes.Configuration, $"{Name}: {refusal}")
            : LaunchAdmission.Resolve(Verb, "claude", environment, roots).Bind(claude => LaunchAdmission.Resolve(Verb, "git", environment, roots).Map(git => Planned(input, placed, new ArmTools(claude, git))));
    }

    private static ArmPlan Planned(ExperimentInput input, ArmPlaced placed, ArmTools tools) =>
        new(input, placed.Choice.Arm, placed.Choice.Scheme, placed.Map, placed.Paths, tools, ScenarioCount: 0);

    /// <summary><c>scenarios.json</c> as the gate judged it (the same road <c>validate</c> reads it by): its count is what the briefs say. A file that does not read is exit 4 naming why.</summary>
    private static async Task<Passage<ArmPlan>> CountedAsync(ArmPlan plan)
    {
        var read = await HostFactsReader.ScenariosAsync(plan.Input, GateSeams.Real);
        return read.Bind(scenarios => scenarios.Match(
            list => new Passage<ArmPlan>.Through(plan with { ScenarioCount = list.Count }),
            reason => Passage<ArmPlan>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")));
    }
}
