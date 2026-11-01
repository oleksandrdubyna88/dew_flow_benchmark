using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;

namespace ModelArms.Verbs;

/// <summary>What <c>chain</c> does under its lock before its first stage (steps 3 and 4): <c>&lt;root&gt;/pids</c> made by name (E5.S1's one helper — a link or a file there is exit 4, nothing started); the PATH and configuration folders the children inherit judged (D17 b, D26 c); every executable the experiment needs on this host (<see cref="HostChecks.NeededExecutables"/>), then <c>setsid</c> (on its road) and the module, as <c>run-all</c> resolves them, each ONCE to its REAL path (D17 c, d); the paths printed under <c>resolved executables:</c> and the same lines
/// written whole to <c>chain/executables.txt</c> — staged and renamed by name, <see cref="ArmFiles.ReplaceFile"/> — BEFORE the first stage; then the isolation line and the road's one portable line. Nothing is launched here.</summary>
internal static class ChainAdmission
{
    /// <summary>The printed lines, or the stop. Exit 3 is a program the host lacks, 4 a wrong configuration.</summary>
    public static Passage<string> Admit(ExperimentInput input, string run, HostKind host)
    {
        var name = $"model-arms chain {run}";
        var pids = RootFolders.Pids(input.Root).Match<Passage<string>>(
            made => new Passage<string>.Through(made),
            stop => new Passage<string>.Stopped(stop.ExitCode, [.. stop.Lines.Select(line => $"{name}: {line}")]));
        return pids.Bind(_ => Resolved(input, host)).Bind(resolved => Announced(input, name, resolved));
    }

    private static Passage<IReadOnlyList<(string Name, string Real)>> Resolved(ExperimentInput input, HostKind host)
    {
        var environment = input.Verb.Environment;
        var roots = ExecutableRule.ForbiddenRoots(input.Experiment.Target.Source, input.Experiment.Root);
        if (LaunchAdmission.FirstRefusal(environment, roots) is { Length: > 0 } refusal)
        {
            return Passage<IReadOnlyList<(string, string)>>.Stop(ExitCodes.Configuration, $"model-arms chain: {refusal}");
        }

        var resolved = new List<(string Name, string Real)>();
        foreach (var (label, name) in Names(input, host))
        {
            var answer = LaunchAdmission.Resolve("chain", name, environment, roots);
            if (answer is Passage<string>.Stopped stopped)
            {
                return new Passage<IReadOnlyList<(string, string)>>.Stopped(stopped.ExitCode, stopped.Lines);
            }

            resolved.Add((label, ((Passage<string>.Through)answer).Value));
        }

        return new Passage<IReadOnlyList<(string, string)>>.Through(resolved);
    }

    /// <summary>The experiment's needs, then <c>setsid</c> where the detached launch uses it, then the module itself (<see cref="ModuleFiles.Executable"/> — never the process's own path, E5 §8 item 5), each once: the label printed and the name resolved.</summary>
    private static IEnumerable<(string Label, string Name)> Names(ExperimentInput input, HostKind host) =>
        HostChecks.NeededExecutables(input.Experiment, host).Select(needed => (needed.Name, needed.Name))
            .Concat(DetachedRoads.Current == DetachedRoad.Setsid ? new[] { ("setsid", "setsid") } : [])
            .Append(("model-arms", input.Verb.Module.Executable))
            .DistinctBy(pair => pair.Item1, StringComparer.Ordinal);

    private static Passage<string> Announced(ExperimentInput input, string name, IReadOnlyList<(string Name, string Real)> resolved)
    {
        List<string> lines = ["resolved executables:", .. resolved.Select(pair => $"  {pair.Name} → {pair.Real}")];
        try
        {
            ArmFiles.ReplaceFile(input.Root.Executables, string.Join('\n', lines) + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Passage<string>.Stop(ExitCodes.Environment, $"{name}: {input.Root.Executables} cannot be written: {ex.Message}");
        }

        var output = input.Verb.Output;
        foreach (var line in lines)
        {
            output.WriteLine(line);
        }

        output.WriteLine(IsolationLine.Of(IsolationFiles.State(input.Root)));
        if (RunnerLiveness.PortableLine(PlatformFactsReader.CurrentRoad) is { Length: > 0 } portable)
        {
            output.WriteLine(portable);
        }

        output.Flush();
        return new Passage<string>.Through(string.Empty);
    }
}
