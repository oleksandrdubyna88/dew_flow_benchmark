using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary><c>validate</c> (D23) and the checks <c>prep-base</c> runs first (D18): the experiment is read by the gate; this reads the HOST and says, in one
/// answer, everything that stops the experiment running on it. Exit 0 valid; 4 when any finding is a wrong configuration; 3 when every finding is the host
/// not being ready (a CLI it lacks, D16). The resolved executables are printed first, each as the REAL path the harness will launch (D17 c).</summary>
public static class ValidateVerb
{
    private const string Name = "model-arms";

    public static Task<int> RunAsync(ExperimentInput input) => RunAsync(input, GateSeams.Real);

    /// <summary>The same with the host's reads handed in — the seam the gate's tests stand on, so a test can swap <c>scenarios.json</c> where the race is.</summary>
    internal static Task<int> RunAsync(ExperimentInput input, GateSeams seams) =>
        CheckedAsync(input, seams, facts =>
        {
            input.Verb.Output.WriteLine($"{Name} validate: {input.Root.Root} is valid on this host ({facts.Host})");
            return Task.FromResult(ExitCodes.Pass);
        });

    /// <summary>The checks of D23 over the host, run for any verb that needs them: the resolved executables printed first, every problem printed, and — only when there is none — <paramref name="whenValid"/>. <c>prep-base</c> goes through here (D18: it is refused until validate passes), then launches what these resolved.</summary>
    internal static async Task<int> CheckedAsync(ExperimentInput input, GateSeams seams, Func<HostFacts, Task<int>> whenValid)
    {
        var read = await HostFactsReader.ReadAsync(input, seams);
        return await read.Match(facts => Judged(input, facts, whenValid), stop => Task.FromResult(Stopped(input, stop)));
    }

    private static int Stopped(ExperimentInput input, Passage<HostFacts>.Stopped stop) => stop.Print(input.Verb.Error);

    private static Task<int> Judged(ExperimentInput input, HostFacts facts, Func<HostFacts, Task<int>> whenValid)
    {
        PrintExecutables(input.Verb.Output, facts);
        var problems = HostChecks.Decide(input.Experiment, facts);
        if (problems.Count == 0)
        {
            return whenValid(facts);
        }

        input.Verb.Error.WriteLine($"{Name} {input.Verb.Verb}: {problems.Count} problem(s):");
        foreach (var problem in problems)
        {
            input.Verb.Error.WriteLine($"  - {problem.Text}");
        }

        return Task.FromResult(problems.Any(problem => !problem.Environment) ? ExitCodes.Configuration : ExitCodes.Environment);
    }

    /// <summary>The executables that resolved, each as <c>name → real path</c>: the path the harness launches, never the name that was configured.</summary>
    private static void PrintExecutables(TextWriter output, HostFacts facts)
    {
        output.WriteLine("resolved executables:");
        foreach (var executable in facts.Executables)
        {
            if (executable.Answer is ExecutableAnswer.Found found)
            {
                output.WriteLine($"  {executable.Needed.Name} → {found.Real}");
            }
        }
    }
}
