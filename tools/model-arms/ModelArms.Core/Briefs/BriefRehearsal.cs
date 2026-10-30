using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Briefs;

/// <summary>Fills, once and with representative facts, every brief template the schemes use at every position they use it, through <see cref="BriefFiller"/> and its leak guard.
/// A template that could not be filled at the position a scheme places it — a field nothing earlier provides, a KNOWN text the guard would refuse — is found by <c>validate</c>,
/// never at a step's launch hours into the chain.</summary>
public static class BriefRehearsal
{
    /// <summary>The brief stems of every step the schemes place, a tasks entry counting its three steps, each once, in the order they are first placed.</summary>
    public static IReadOnlyList<string> StemsUsed(Experiment.Experiment experiment) => [];

    /// <summary>Every problem the rehearsal found, each once: a template that could not be read, then the refusal of each fill, naming the template, the scheme, the position and
    /// the step. A stem without an entry in <paramref name="templates"/> was not read and is not rehearsed.</summary>
    public static IReadOnlyList<string> Problems(
        Experiment.Experiment experiment, RootPaths root, int scenarioCount, IReadOnlyDictionary<string, Outcome<string>> templates) => [];
}
