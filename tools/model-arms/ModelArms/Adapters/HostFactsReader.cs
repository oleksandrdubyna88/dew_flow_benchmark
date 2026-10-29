using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>Reads the host for <see cref="HostChecks"/>: which machine this is, where each executable the experiment needs really is, what PATH holds, what
/// git says about the source, what <c>scenarios.json</c> holds and which brief templates the schemes would be filled from. It decides nothing — the findings
/// are <see cref="HostChecks.Decide"/>'s — except that a <c>scenarios.json</c> that is not the file the gate judged stops the walk (exit 4, D17).
/// <para>
/// The PATH it resolves against is the one in the environment map the verb was handed, never the process's: a test and a launcher say which PATH they mean.
/// The source is asked through the process's own <c>git</c> (the shared launcher), so the <c>git</c> that answers is not necessarily the one resolved.
/// </para></summary>
internal static class HostFactsReader
{
    private const string ProcVersion = "/proc/version";

    public static Task<Passage<HostFacts>> ReadAsync(ExperimentInput input, GateSeams seams) =>
        ScenariosAsync(input, seams).ThenAsync(scenarios => FactsAsync(input, scenarios));

    private static async Task<Passage<HostFacts>> FactsAsync(ExperimentInput input, Outcome<IReadOnlyList<Scenario>> scenarios)
    {
        var experiment = input.Experiment;
        var environment = input.Verb.Environment;
        var host = HostKinds.From(OperatingSystem.IsWindows(), environment.GetValueOrDefault("WSL_DISTRO_NAME", string.Empty), await ProcVersionAsync(input.Verb.Stopping));
        var entries = PathEntries(environment);
        var roots = ExecutableRule.ForbiddenRoots(experiment.Target.Source, experiment.Root);
        var executables = Executables(experiment, host, entries, roots);
        var refusals = PathRefusals(entries, roots);

        var facts = new HostFacts(host, executables, scenarios, File.Exists(experiment.Coai.Settings))
        {
            Source = await SourceAsync(input, executables, refusals),
            PathRefusals = refusals,
            Templates = Templates(input),
        };
        return new Passage<HostFacts>.Through(facts);
    }

    /// <summary>The source is asked only through a git the resolution FOUND and only while PATH is clean: git looks its own helpers up in PATH, and a refused PATH is exit 4
    /// whatever git says (and a PATH that is refused is the cause to name before git's own state). Otherwise the sentence says it was not checked, and why.</summary>
    private static async Task<SourceAnswer> SourceAsync(ExperimentInput input, IReadOnlyList<ResolvedExecutable> executables, IReadOnlyList<string> refusals)
    {
        var target = input.Experiment.Target;
        if (!Directory.Exists(target.Source))
        {
            return new SourceAnswer.Missing();
        }

        var git = executables.FirstOrDefault(executable => executable.Needed.Name == "git")?.Answer;
        return (git, refusals.Count) switch
        {
            (_, > 0) => new SourceAnswer.NotAsked("the source was not checked: PATH is refused"),
            (ExecutableAnswer.Found found, _) => await SourceFacts.ReadAsync(target, found.Real, GitEnvironment.Of(input.Verb.Environment), RootFacts.RealPathOf, input.Verb.Stopping),
            (ExecutableAnswer.Refused, _) => new SourceAnswer.NotAsked("the source was not checked: git is refused"),
            _ => new SourceAnswer.NotAsked("the source was not checked: git is absent"),
        };
    }

    private static IReadOnlyList<ResolvedExecutable> Executables(Experiment experiment, HostKind host, IReadOnlyList<string> entries, IReadOnlyList<string> roots) =>
        [.. HostChecks.NeededExecutables(experiment, host).Select(needed => new ResolvedExecutable(
            needed,
            ExecutableResolution.Resolve(needed.Name, entries, roots, RootFacts.Host, OperatingSystem.IsWindows())))];

    /// <summary>The PATH of the environment map split into its entries, an empty one included; none when the map has no PATH at all.</summary>
    private static IReadOnlyList<string> PathEntries(IReadOnlyDictionary<string, string> environment) =>
        environment.TryGetValue("PATH", out var path) ? [.. path.Split(Path.PathSeparator)] : [];

    private static IReadOnlyList<string> PathRefusals(IReadOnlyList<string> entries, IReadOnlyList<string> roots) =>
        [.. entries.Select(entry => ExecutableResolution.PathEntryRefusal(entry, roots, RootFacts.Host)).Where(refusal => refusal.Length > 0).Distinct(StringComparer.Ordinal)];

    private static IReadOnlyDictionary<StepKind, Outcome<string>> Templates(ExperimentInput input)
    {
        var files = new BriefTemplateFiles(input.Verb.Module.ShippedBriefs, input.Root.Briefs);
        return BriefRehearsal.KindsUsed(input.Experiment).ToDictionary(kind => kind, files.Read);
    }

    private static async Task<string> ProcVersionAsync(CancellationToken ct)
    {
        try
        {
            return File.Exists(ProcVersion) ? await File.ReadAllTextAsync(ProcVersion, ct) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary><c>scenarios.json</c> as the gate judged it: read at the path the gate read the facts of (<see cref="FileFacts.Path"/>, the normalised one), then stat-ed
    /// again — by the gate's own helper — and refused (exit 4) when it is no longer the file that was judged. The answer is what <see cref="ScenariosFile.Read"/> made of
    /// the text, or why the file could not be read at all.</summary>
    private static async Task<Passage<Outcome<IReadOnlyList<Scenario>>>> ScenariosAsync(ExperimentInput input, GateSeams seams)
    {
        if (input.JudgedScenarios is not [var judged])
        {
            return Passage<Outcome<IReadOnlyList<Scenario>>>.Stop(
                ExitCodes.Environment, $"model-arms {input.Verb.Verb}: the gate did not hand over the facts it judged {input.Experiment.ScenariosFile} by — refused rather than read unjudged (D17)");
        }

        return await ReadScenariosAsync(input, judged, seams);
    }

    private static async Task<Passage<Outcome<IReadOnlyList<Scenario>>>> ReadScenariosAsync(ExperimentInput input, FileFacts judged, GateSeams seams)
    {
        var text = await TextOf(judged.Path, input.Verb.Stopping);
        await seams.AfterRead(judged.Path);
        var same = ExperimentGate.StillTheSame(input.Verb, judged, seams);
        return same.Map(_ => text.Match(ScenariosFile.Read, Outcome<IReadOnlyList<Scenario>>.Failure));
    }

    private static async Task<Outcome<string>> TextOf(string path, CancellationToken ct)
    {
        try
        {
            return Outcome<string>.Success(await File.ReadAllTextAsync(path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure($"{path} cannot be read: {ex.Message}");
        }
    }
}
