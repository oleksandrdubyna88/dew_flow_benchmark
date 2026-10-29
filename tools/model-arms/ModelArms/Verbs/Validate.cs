using Bench.Cli;
using Bench.Domain;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary><c>validate</c> (D23) and the guard in front of <c>prep-base</c> (D18): the experiment is read by the gate; this reads the HOST and says, in one
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
            return ExitCodes.Pass;
        });

    /// <summary><c>prep-base</c>'s guard: the same checks, printed the same way. Passing them is not yet a baseline — that measurement is story E4.S4 — so the
    /// verb says so and exits 4.</summary>
    public static Task<int> PrepBaseGuardAsync(ExperimentInput input) => PrepBaseGuardAsync(input, GateSeams.Real);

    internal static Task<int> PrepBaseGuardAsync(ExperimentInput input, GateSeams seams) =>
        CheckedAsync(input, seams, _ =>
        {
            input.Verb.Error.WriteLine($"{Name} prep-base: validated; the baseline measurement is not built yet — story E4.S4 of todo/PLAN_model_arms.md");
            return ExitCodes.Configuration;
        });

    private static async Task<int> CheckedAsync(ExperimentInput input, GateSeams seams, Func<HostFacts, int> whenValid)
    {
        var read = await HostFactsReader.ReadAsync(input, seams);
        return read.Match(facts => Judged(input, facts, whenValid), stop => Stopped(input, stop));
    }

    private static int Stopped(ExperimentInput input, Passage<HostFacts>.Stopped stop)
    {
        foreach (var line in stop.Lines)
        {
            input.Verb.Error.WriteLine(line);
        }

        return stop.ExitCode;
    }

    private static int Judged(ExperimentInput input, HostFacts facts, Func<HostFacts, int> whenValid)
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

        return problems.Any(problem => !problem.Environment) ? ExitCodes.Configuration : ExitCodes.Environment;
    }

    /// <summary>The executables that resolved, each as <c>name → real path</c>: the path the harness launches, never the name that was configured.</summary>
    private static void PrintExecutables(TextWriter output, HostFacts facts)
    {
        output.WriteLine("resolved executables:");
        foreach (var executable in facts.Executables)
        {
            if (executable.Path is Outcome<string>.Ok real)
            {
                output.WriteLine($"  {executable.Name} → {real.Value}");
            }
        }
    }
}
