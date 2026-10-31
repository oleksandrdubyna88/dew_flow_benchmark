using System.Security.Cryptography;
using System.Text;
using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Launch;

namespace ModelArms.Verbs;

/// <summary><c>make</c> (plan §4.1, D4, D5, D27; <c>after6.sh:26-37</c>): the arms of the runs, each copied from <c>base/</c>, branched, warm-built and given its MCP files — and the arm map extended, never re-minted. It crosses the module's trust boundary twice: it launches the target's tools (<c>git</c>, the node that runs the target's lowering wrapper, the build) and it DELETES a
/// folder (an arm that crashed while being made). <b>In order:</b> (1) <see cref="LaunchAdmission"/> — the PATH, <c>HOME</c> and <c>XDG_CONFIG_HOME</c> the children inherit, then <c>git</c>, <c>lowPriority[0]</c> and <c>build[0]</c> resolved to their REAL paths and printed as <c>resolved executables:</c> — before anything is made; (2) the isolation snapshot's one line, restoring nothing;
/// (3) the arms: <see cref="RunArms.Of"/> without those that already have <c>chain/made-&lt;arm&gt;</c> (none left: <i>every arm of every run is made</i>, exit 0); (4) the arm map through <see cref="BoundedFile"/> (absent is the empty map; refused or not a map is exit 4), <see cref="ArmMap.Extend"/>ed with 8 random bytes per new arm and, when it changed, written <c>0600</c>
/// BEFORE the first folder; (5) each arm by <see cref="ArmMaker"/>, in order, the first stop ending the verb with its exit code. Exit 0 every arm is made; 3 the host or the target could not do it (a tool absent or not started, a base that will not copy or build, git that cannot answer); 4 a wrong configuration or a doubt that touches nothing (a PATH under a forbidden root, a map that does not
/// read, an arm that ran without its marker, a timing that cannot be read); 5 the operator's stop — the same command resumes: a half-made arm has no marker and is redone whole. Every line passes <see cref="ArmMap.Scrub"/>: no token is printed.
/// <para>
/// What this does NOT do: make an arm no run names (a run names it first — E3 §8 item 5), make the task pipeline's worktrees (E4), change a mode (E5), or judge the target's own code: the warm build runs it, with the operator's rights, as the shell's did (E3 §8 item 17).
/// </para></summary>
public static class MakeVerb
{
    private const string Name = "model-arms make";
    private const int MapLimit = 1024 * 1024;

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, PlatformFactsReader.CurrentRoad);

    internal static async Task<int> RunAsync(ExperimentInput input, PlatformRoad road)
    {
        var passage = await Task.FromResult(Admitted(input))
            .Then(tools => Announced(input, road, tools))
            .Then(tools => Pending(input, tools))
            .ThenAsync(pending => MadeAsync(input, road, pending));
        return passage.Match(done => done, stop => stop.Print(input.Verb.Error));
    }

    /// <summary>What step 3 hands to step 4: the real paths, and the arms still to make.</summary>
    private sealed record Plan(ToolPaths Tools, IReadOnlyList<string> Arms);

    /// <summary>Step 1: the PATH and the two configuration folders judged, then each executable resolved once to its real path. A refusal ends the verb; nothing is printed before it.</summary>
    private static Passage<ToolPaths> Admitted(ExperimentInput input)
    {
        var environment = input.Verb.Environment;
        var target = input.Experiment.Target;
        var roots = ExecutableRule.ForbiddenRoots(target.Source, input.Experiment.Root);
        if (LaunchAdmission.FirstRefusal(environment, roots) is { Length: > 0 } refusal)
        {
            return Passage<ToolPaths>.Stop(ExitCodes.Configuration, $"{Name}: {refusal}");
        }

        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "git", First(target.LowPriority), First(target.Build) }.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal))
        {
            var answer = LaunchAdmission.Resolve("make", name, environment, roots);
            if (answer is Passage<string>.Stopped stopped)
            {
                return new Passage<ToolPaths>.Stopped(stopped.ExitCode, stopped.Lines);
            }

            resolved[name] = ((Passage<string>.Through)answer).Value;
        }

        return new Passage<ToolPaths>.Through(ToolPaths.Of(target, name => resolved.GetValueOrDefault(name, string.Empty)));
    }

    private static string First(IReadOnlyList<string> command) => command.Count > 0 ? command[0] : string.Empty;

    /// <summary>The real paths printed before the first launch (D17 c), then the isolation snapshot's one line (D11): read by name, restored never.</summary>
    private static Passage<ToolPaths> Announced(ExperimentInput input, PlatformRoad road, ToolPaths tools)
    {
        var output = input.Verb.Output;
        output.WriteLine("resolved executables:");
        var target = input.Experiment.Target;
        foreach (var (name, real) in new[] { ("git", tools.Git), (First(target.LowPriority), tools.Node), (First(target.Build), tools.Build) }.DistinctBy(pair => pair.Item1))
        {
            output.WriteLine($"  {name} → {real}");
        }

        output.WriteLine(IsolationLine.Of(IsolationFiles.State(input.Root, road)));
        output.Flush();
        return new Passage<ToolPaths>.Through(tools);
    }

    /// <summary>Step 3: the arms of every run, in file order, without those whose marker is there. A marker the kernel cannot answer for is exit 3, never "not made".</summary>
    private static Passage<Plan> Pending(ExperimentInput input, ToolPaths tools)
    {
        var pending = new List<string>();
        foreach (var arm in RunArms.Of(input.Experiment.Runs))
        {
            switch (KernelFacts.Present(input.Root.Made(arm)))
            {
                case Outcome<bool>.Fail failed:
                    return Passage<Plan>.Stop(ExitCodes.Environment, $"{Name}: cannot tell whether {arm} is made: {failed.Reason}");
                case Outcome<bool>.Ok { Value: false }:
                    pending.Add(arm);
                    break;
            }
        }

        return new Passage<Plan>.Through(new Plan(tools, pending));
    }

    private static async Task<Passage<int>> MadeAsync(ExperimentInput input, PlatformRoad road, Plan plan)
    {
        if (plan.Arms.Count == 0)
        {
            input.Verb.Output.WriteLine($"{Name}: every arm of every run is made");
            return new Passage<int>.Through(ExitCodes.Pass);
        }

        return await MapExtended(input, road, plan.Arms).ThenAsync(map => EachAsync(input, road, plan, map));
    }

    /// <summary>Step 4: the map read by the bounded road, extended for exactly these arms, and — only when it changed — written private BEFORE any folder is made, so a crash after the first folder leaves a map that names it.</summary>
    private static Task<Passage<ArmMap>> MapExtended(ExperimentInput input, PlatformRoad road, IReadOnlyList<string> arms) =>
        Task.FromResult(MapOf(input, road).Bind(map => Written(input, map, ArmMap.Extend(map, arms, () => RandomNumberGenerator.GetBytes(8)))));

    private static Passage<ArmMap> MapOf(ExperimentInput input, PlatformRoad road) =>
        BoundedFile.Read(input.Root.ArmMapFile, MapLimit, road) switch
        {
            BoundedRead.Absent => new Passage<ArmMap>.Through(ArmMap.Empty),
            BoundedRead.Refused refused => Passage<ArmMap>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {ExecutableRule.Printable(refused.Reason)}"),
            BoundedRead.Bytes bytes => ArmMap.Read(Encoding.UTF8.GetString(bytes.Value)).Match(
                map => new Passage<ArmMap>.Through(map),
                reason => Passage<ArmMap>.Stop(ExitCodes.Configuration, $"{Name}: arms.map.json is refused: {string.Join('\n', reason.Split('\n').Select(ExecutableRule.Printable))}")),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private static Passage<ArmMap> Written(ExperimentInput input, ArmMap before, ArmMap after)
    {
        if (after.Equals(before))
        {
            return new Passage<ArmMap>.Through(after);
        }

        try
        {
            ArmFiles.ReplacePrivate(input.Root.ArmMapFile, after.ToJson() + "\n");
            return new Passage<ArmMap>.Through(after);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<ArmMap>.Stop(ExitCodes.Environment, $"{Name}: {input.Root.ArmMapFile} cannot be written: {ex.Message}");
        }
    }

    /// <summary>Step 5: each arm by <see cref="ArmMaker"/>, in order; the first stop ends the verb with its code and its sentence.</summary>
    private static async Task<Passage<int>> EachAsync(ExperimentInput input, PlatformRoad road, Plan plan, ArmMap map)
    {
        var environment = input.Verb.Environment;
        var maker = new ArmMaker(input.Experiment, input.Root, map, plan.Tools, new TargetTools(TargetEnvironment.Of(environment)), environment, road, text => input.Verb.Output.WriteLine($"{Name}: {text}"));
        foreach (var arm in plan.Arms)
        {
            if (await maker.MakeAsync(arm, input.Verb.Stopping) is MakeOutcome.Stopped stopped)
            {
                return Passage<int>.Stop(stopped.ExitCode, $"{Name}: {stopped.Why}");
            }
        }

        return new Passage<int>.Through(ExitCodes.Pass);
    }
}
