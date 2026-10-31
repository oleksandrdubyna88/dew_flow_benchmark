using System.Text;
using Bench.Cli;
using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using ModelArms.Core.Steps;

namespace ModelArms.Verbs;

/// <summary>Everything the objective run needs once admission has let the arm in: the input, the arm and its scheme, the map (every line the verb prints goes through its <see cref="ArmMap.Scrub"/>), the arm's paths, the REAL paths of the executables (the ONLY paths launched, resolved once and printed), the baseline and the scenarios, and whether
/// this is the re-read of logs already there.</summary>
internal sealed record ChecksPlan(ExperimentInput Input, string Arm, Core.Experiment.Scheme Scheme, ArmMap Map, ArmPaths Paths, ToolPaths Tools, Baseline Baseline, IReadOnlyList<Scenario> Scenarios, bool ParseOnly)
{
    private const string Name = "model-arms checks";

    /// <summary>A line on stdout, scrubbed of every token of the map.</summary>
    public void Say(string line) => Input.Verb.Output.WriteLine(Map.Scrub(line));

    /// <summary>A line on stderr — what the operator should know and the verb goes on from — scrubbed.</summary>
    public void Warn(string line) => Input.Verb.Error.WriteLine(Map.Scrub($"{Name} {Arm}: {line}"));
}

/// <summary>Admission for <c>checks</c> (plan D17 c/d, D18, D26; E3.S5 steps 1–4): each refusal ends the verb before a program is launched — the arm's scheme (a <c>tasks</c> block is allowed: the objective checks measure what the arm left and need no step of the pipeline) and its token from <c>arms.map.json</c> read through <see cref="BoundedFile"/>; the
/// PATH, <c>HOME</c> and <c>XDG_CONFIG_HOME</c> the children inherit; <c>git</c> and — for a full run — <c>dotnet</c>, <c>lowPriority[0]</c>, <c>build[0]</c>, <c>suite[0]</c> and <c>extension[0]</c> resolved to REAL paths, <c>git --version</c> asked of the resolved git and held against the floor (D23, E4.S0: the one launch admission itself makes, from the experiment root, under the hardened environment; <c>--parse-only</c> included), and the paths printed; the arm's runner, which must not be alive; <c>baseline.json</c> and <c>scenarios.json</c>. Exit 3 is the host (a program that is not there),
/// exit 4 a wrong configuration or a refused file.</summary>
internal static class ChecksAdmission
{
    private const string Verb = "checks";
    private const string Name = "model-arms " + Verb;
    private const int PidLimit = 64;
    private const int TimingLimit = 16 * 1024 * 1024;

    public static Task<Passage<ChecksPlan>> AdmitAsync(ExperimentInput input) =>
        Task.FromResult(ArmChoices.Of(input, Verb, allowTasks: true))
            .Then(choice => Place(input, choice))
            .ThenAsync(placed => EquipAsync(input, placed))
            .Then(equipped => Idle(equipped))
            .ThenAsync(equipped => ReadAsync(equipped));

    private sealed record Located(ArmChoice Choice, ArmMap Map, ArmPaths Paths);

    private sealed record Resolved(ExperimentInput Input, Located Placed, ToolPaths Tools, bool ParseOnly);

    /// <summary>Step 1: the token, from the map read by name through the bounded road (a link, a FIFO or a file past the limit is refused — the root is the operator's, but under the lock an arm can still create a file by name in it).</summary>
    private static Passage<Located> Place(ExperimentInput input, ArmChoice choice) =>
        ExperimentGate.TextOf(input.Root.ArmMapFile, PlatformFactsReader.CurrentRoad.Bounded).Match(
            json => ArmMap.Read(json).Match(
                map => map.TokenOf(choice.Arm).Match(
                    token => new Passage<Located>.Through(new Located(choice, map, input.Root.Arm(token))),
                    reason => Passage<Located>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")),
                reason => Passage<Located>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {reason}")),
            reason => Passage<Located>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {reason}"));

    /// <summary>Step 2: the PATH and the two configuration folders judged, then each executable resolved ONCE to the real path that is printed and launched (D17 c, d); the isolation line printed — nothing is restored.</summary>
    private static async Task<Passage<Resolved>> EquipAsync(ExperimentInput input, Located placed)
    {
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        var refusal = LaunchAdmission.FirstRefusal(input.Verb.Environment, roots);
        var parseOnly = input.Verb.Flags.Has("parse-only");
        if (refusal.Length > 0)
        {
            return Passage<Resolved>.Stop(ExitCodes.Configuration, $"{Name}: {refusal}");
        }

        return await Task.FromResult(Resolve(input, roots, Needed(input.Experiment.Target, parseOnly)))
            .ThenAsync(resolved => FlooredAsync(input, resolved))
            .Then(resolved => Printed(input, placed, resolved, parseOnly));
    }

    /// <summary>D23, E4.S0: the resolved git's version against the floor, <c>--parse-only</c> INCLUDED (git is resolved for it too and the report's git facts are asked of it): the one launch admission itself makes, from the experiment root, under the hardened environment. Nothing is printed before a refusal.</summary>
    private static async Task<Passage<Dictionary<string, string>>> FlooredAsync(ExperimentInput input, Dictionary<string, string> resolved) =>
        (await LaunchAdmission.FloorAsync(Verb, resolved["git"], input.Verb.Environment, input.Root.Root, input.Verb.Stopping)).Map(_ => resolved);

    /// <summary>Each name resolved in turn, once; the first that does not resolve ends the walk with its sentence (exit 3 absent, 4 refused).</summary>
    private static Passage<Dictionary<string, string>> Resolve(ExperimentInput input, IReadOnlyList<string> roots, IEnumerable<string> names)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var answer = LaunchAdmission.Resolve(Verb, name, input.Verb.Environment, roots);
            if (answer is Passage<string>.Stopped stop)
            {
                return new Passage<Dictionary<string, string>>.Stopped(stop.ExitCode, stop.Lines);
            }

            resolved[name] = ((Passage<string>.Through)answer).Value;
        }

        return new Passage<Dictionary<string, string>>.Through(resolved);
    }

    private static Passage<Resolved> Printed(ExperimentInput input, Located placed, Dictionary<string, string> resolved, bool parseOnly)
    {
        var output = input.Verb.Output;
        output.WriteLine("resolved executables:");
        foreach (var (name, real) in resolved)
        {
            output.WriteLine(placed.Map.Scrub($"  {name} → {real}"));
        }

        output.WriteLine(placed.Map.Scrub(Core.Isolation.IsolationLine.Of(IsolationFiles.State(input.Root))));
        output.Flush();
        return new Passage<Resolved>.Through(new Resolved(input, placed, ToolPaths.Of(input.Experiment.Target, name => resolved.GetValueOrDefault(name, string.Empty)), parseOnly));
    }

    /// <summary>The names to resolve, each once, in the order they are printed: <c>git</c> always (the report's git facts are asked of it, parse-only too); the target's tools only when stages are to run.</summary>
    private static IEnumerable<string> Needed(TargetFacts target, bool parseOnly) =>
        parseOnly ? new[] { "git" } : new[] { "git", "dotnet", First(target.LowPriority), First(target.Build), First(target.Suite.Command), First(target.Extension.Command) }.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal);

    private static string First(IReadOnlyList<string> command) => command.Count > 0 ? command[0] : string.Empty;

    /// <summary>Step 3: a live runner is a refusal — <c>checks</c> measures a finished or stopped arm and would run the build and the tests under a runner that is still writing the clone. A pid file that cannot be read, or holds no pid, is a refusal too: whether the runner is alive cannot be told.</summary>
    private static Passage<Resolved> Idle(Resolved equipped)
    {
        var (input, placed) = (equipped.Input, equipped.Placed);
        var pidFile = input.Root.PidFile(placed.Choice.Arm);
        var refusal = BoundedFile.Read(pidFile, PidLimit, PlatformFactsReader.CurrentRoad) switch
        {
            BoundedRead.Absent => string.Empty,
            BoundedRead.Refused refused => $"{Name}: {pidFile} cannot be read — whether {placed.Choice.Arm}'s runner is alive cannot be told: {ExecutableRule.Printable(refused.Reason)}",
            BoundedRead.Bytes bytes => RunnerPid.Parse(Encoding.UTF8.GetString(bytes.Value).Trim()) is { } pid
                ? WorkerLiveness.ProcessIsAlive(pid) ? $"{Name}: {placed.Choice.Arm}'s runner (pid {pid}) is alive — checks measures a finished or stopped arm" : string.Empty
                : $"{Name}: {pidFile} holds no pid — whether {placed.Choice.Arm}'s runner is alive cannot be told",
            _ => string.Empty,
        };
        return refusal.Length > 0 ? Passage<Resolved>.Stop(ExitCodes.Configuration, refusal) : new Passage<Resolved>.Through(equipped);
    }

    /// <summary>Step 4: <c>baseline.json</c> through the bounded road and <c>BaselineFile.Read</c>, and <c>scenarios.json</c> as the gate judged it; the arm's timing is looked at last — an arm with no <c>arm-done</c> line is one warning, never a refusal.</summary>
    private static async Task<Passage<ChecksPlan>> ReadAsync(Resolved equipped)
    {
        var (input, placed) = (equipped.Input, equipped.Placed);
        var baseline = input.Root.Of(input.Experiment.BaselineFile).Match(
            path => ExperimentGate.TextOf(path, PlatformFactsReader.CurrentRoad.Bounded).Match(
                text => BaselineFile.Read(text).Match(read => Outcome<Baseline>.Success(read), reason => Outcome<Baseline>.Failure($"{path} is refused: {reason}")),
                Outcome<Baseline>.Failure),
            Outcome<Baseline>.Failure);
        if (baseline is Outcome<Baseline>.Fail failed)
        {
            return Passage<ChecksPlan>.Stop(ExitCodes.Configuration, $"{Name}: baseline.json is refused: {failed.Reason}");
        }

        var scenarios = await HostFactsReader.ScenariosAsync(input, GateSeams.Real);
        return scenarios.Bind(list => list.Match(
            read => Planned(equipped, ((Outcome<Baseline>.Ok)baseline).Value, read),
            reason => Passage<ChecksPlan>.Stop(ExitCodes.Configuration, $"{Name}: {reason}")));
    }

    private static Passage<ChecksPlan> Planned(Resolved equipped, Baseline baseline, IReadOnlyList<Scenario> scenarios)
    {
        var plan = new ChecksPlan(equipped.Input, equipped.Placed.Choice.Arm, equipped.Placed.Choice.Scheme, equipped.Placed.Map, equipped.Placed.Paths, equipped.Tools, baseline, scenarios, equipped.ParseOnly);
        if (!ArmDone(plan))
        {
            plan.Warn("its timing carries no arm-done line — it did not finish its scheme; the checks measure what it left");
        }

        return new Passage<ChecksPlan>.Through(plan);
    }

    /// <summary>Whether the arm's <c>out/timing.jsonl</c> says it finished its scheme; a timing that is absent or refused says nothing, so the arm is not known to be done.</summary>
    private static bool ArmDone(ChecksPlan plan) =>
        BoundedFile.Read(plan.Paths.Timing, TimingLimit, PlatformFactsReader.CurrentRoad) is BoundedRead.Bytes bytes
        && TimingLines.IsArmDone(TimingLines.Read(Encoding.UTF8.GetString(bytes.Value)), plan.Arm);
}
