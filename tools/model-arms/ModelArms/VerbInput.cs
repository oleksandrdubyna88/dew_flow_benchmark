using Bench.Cli;
using ModelArms.Core.Experiment;

namespace ModelArms;

/// <summary>Where the files that travel with the binary are: the worked example and the shipped briefs, both beside the executable.</summary>
public sealed record ModuleFiles(string ExampleFile, string ShippedBriefs)
{
    public static ModuleFiles Beside(string baseDirectory) =>
        new(Path.Combine(baseDirectory, "examples", "part6.experiment.json"), Path.Combine(baseDirectory, "briefs"));
}

/// <summary>Everything a verb is given: its name, its positional words, its flags, the two writers, the environment as a map (never read from the
/// process by a verb, so a test hands its own) and the token a stop signal cancels.</summary>
public sealed record VerbInput(
    string Verb,
    IReadOnlyList<string> Positionals,
    CommandLine Flags,
    TextWriter Output,
    TextWriter Error,
    IReadOnlyDictionary<string, string> Environment,
    ModuleFiles Module,
    CancellationToken Stopping);

/// <summary>What a verb that reads an experiment is given once <see cref="ExperimentGate"/> has let it in: the verb's input, the root's paths and
/// the experiment, read once.</summary>
public sealed record ExperimentInput(VerbInput Verb, RootPaths Root, Experiment Experiment);
