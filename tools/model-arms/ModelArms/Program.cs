using System.Collections;
using System.Text;
using Bench.Cli;
using ModelArms.Core.Experiment;
using ModelArms.Verbs;

namespace ModelArms;

/// <summary>The console's entry point: split the line (D20), parse its flags, dispatch to the verb, and turn what happened into an exit code
/// (D16). The verbs that read an experiment reach it through <see cref="ExperimentGate"/> only.</summary>
public static class Program
{
    private const string Name = "model-arms";

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Ctrl+C and SIGTERM become the root token every verb runs under, so a planned stop leaves a state the same command resumes.
        using var shutdown = ShutdownSignal.Install(Console.Error);
        try
        {
            return await RunAsync(args, Console.Out, Console.Error, Snapshot(), shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"{Name}: stopped — run the same command again to resume");
            return ExitCodes.NoReport;
        }
    }

    /// <summary>The testable seam: the writers, the environment and the stop token are handed in, so a test never touches the process's own.</summary>
    public static Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, IReadOnlyDictionary<string, string> environment, CancellationToken stopping) =>
        RunAsync(args, output, error, environment, stopping, Dispatch);

    /// <summary>The same with the dispatch handed in: the only way to reach the exit-code mapping below (a flag the verb cannot read, a fault, a stop)
    /// before a verb that can fail exists. The one caller outside the tests is the overload above.</summary>
    internal static Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken stopping,
        Func<VerbInput, Task<int>> dispatch) =>
        ModelArmsCommandLine.Split(args).Match(
            line => RunLineAsync(line, new VerbInput(line.Verb, line.Positionals, CommandLine.Parse(line.Rest), output, error, environment, ModuleFiles.Beside(AppContext.BaseDirectory), stopping), dispatch),
            reason => Refused(error, reason));

    private static Task<int> Refused(TextWriter error, string reason)
    {
        error.WriteLine($"{Name}: {reason}");
        return Task.FromResult(ExitCodes.Configuration);
    }

    private static async Task<int> RunLineAsync(VerbLine line, VerbInput input, Func<VerbInput, Task<int>> dispatch)
    {
        try
        {
            return await dispatch(input);
        }
        catch (CommandLineException refused)
        {
            input.Error.WriteLine($"{Name} {line.Verb}: {refused.Message}");
            return ExitCodes.Configuration;
        }
        catch (Exception ex) when (IsFault(ex, input.Stopping))
        {
            input.Error.WriteLine($"{Name} {line.Verb}: unexpected {ex.GetType().Name}: {ex.Message}");
            return ExitCodes.Environment;
        }
    }

    /// <summary>A stop the operator asked for is not a fault: it travels up to <see cref="Main"/>, which maps it to the resumable exit.</summary>
    private static bool IsFault(Exception ex, CancellationToken stopping) =>
        ex is not OperationCanceledException || !stopping.IsCancellationRequested;

    private static Task<int> Dispatch(VerbInput input)
    {
        return input.Verb switch
        {
            "init" => InitVerb.RunAsync(input),
            "validate" => ExperimentGate.RunAsync(input, ValidateVerb.RunAsync),
            "prep-base" => ExperimentGate.RunAsync(input, ValidateVerb.PrepBaseGuardAsync),
            "make" => NotBuilt(input, "E4.S4"),
            "lock" => NotBuilt(input, "E4.S1"),
            "unlock" => NotBuilt(input, "E4.S1"),
            "run-arm" => ExperimentGate.RunAsync(input, RunArmVerb.RunAsync),
            "run-all" => NotBuilt(input, "E4.S3"),
            "chain" => NotBuilt(input, "E4.S5"),
            "checks" => NotBuilt(input, "E5.S2"),
            "status" => NotBuilt(input, "E4.S4"),
            "gate-lock" => ExperimentGate.RunAsync(input, GateLockVerb.RunAsync),
            "version" => Help.Version(input),
            "help" => Help.Usage(input),
            _ => Help.Unknown(input),
        };
    }

    private static Task<int> NotBuilt(VerbInput input, string story)
    {
        input.Error.WriteLine($"{Name} {input.Verb}: not built yet — story {story} of todo/PLAN_model_arms.md");
        return Task.FromResult(ExitCodes.Configuration);
    }

    /// <summary>The process's variables as a map, taken once: a verb reads this map and never the process, so a test hands its own.</summary>
    private static Dictionary<string, string> Snapshot()
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var map = new Dictionary<string, string>(comparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            map[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
        }

        return map;
    }
}
