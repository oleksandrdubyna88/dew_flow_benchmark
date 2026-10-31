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
/// The source is asked only through the <c>git</c> that <c>Resolve</c> found, launched by its real path under <see cref="GitEnvironment.Of"/> with the source as its working
/// directory, and only while PATH is clean and <c>HOME</c> and <c>XDG_CONFIG_HOME</c> are not under a forbidden root (git reads its global config from there); otherwise
/// the answer says the source was not checked, and why.
/// </para>
/// <para>
/// <b>The git's version (D23, E4.S0)</b> is asked of the same git under the same guard, BEFORE the source and from the experiment root (<c>--version</c> reads no repository; the root exists for every verb once the gate let it in): the source is asked only of a git whose version was read and passes <see cref="GitFloor.Admit"/>, so a git whose guards are unknown is never run over the operator's
/// checkout and every refusal is still printed at once. D26 (a)–(f) for this fact: (a) no path is judged here — the resolution already did; (b) the real path, <see cref="ArmGit"/>'s environment, the root as cwd; (c) the same guard as the source; (d)–(f) as the source's.
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
        var entries = ExecutableResolution.PathEntries(environment);
        var roots = ExecutableRule.ForbiddenRoots(experiment.Target.Source, experiment.Root);
        var executables = Executables(experiment, host, entries, roots);
        var refusals = PathRefusals(entries, roots);
        var homes = ConfigFolders.Refusals(environment, roots);

        var version = await VersionAsync(input, executables, refusals, homes);
        var facts = new HostFacts(host, executables, scenarios, File.Exists(experiment.Coai.Settings))
        {
            Source = await SourceAsync(input, executables, refusals, homes, version),
            Version = version,
            PathRefusals = refusals,
            EnvironmentRefusals = [.. homes.Select(home => home.Refusal)],
            Templates = Templates(input),
        };
        return new Passage<HostFacts>.Through(facts);
    }

    /// <summary>Why git is not asked anything, or nothing when it may be: PATH refused (git looks its own helpers up there, and a refused PATH is exit 4 whatever git says — the cause to name before git's own state), then <c>HOME</c> or <c>XDG_CONFIG_HOME</c> refused (git reads its global config there), then git itself refused or absent.
    /// One answer for the version and the source, so the two are unasked for the same reason.</summary>
    private static string? Unasked(IReadOnlyList<ResolvedExecutable> executables, IReadOnlyList<string> refusals, IReadOnlyList<(string Variable, string Refusal)> homes) =>
        (executables.FirstOrDefault(executable => executable.Needed.Name == "git")?.Answer, refusals.Count) switch
        {
            (_, > 0) => "PATH is refused",
            _ when homes.Count > 0 => $"{string.Join(" and ", homes.Select(home => home.Variable))} {(homes.Count > 1 ? "are" : "is")} refused",
            (ExecutableAnswer.Found, _) => null,
            (ExecutableAnswer.Refused, _) => "git is refused",
            _ => "git is absent",
        };

    /// <summary>D23: <c>git --version</c> of the git the resolution FOUND, under the same guard as the source, from the experiment root (it reads no repository, and the root exists for every verb once the gate let it in), through <see cref="ArmGit.VersionAsync"/>: the resolved real path, pinned, sealed, one launch.</summary>
    private static async Task<GitVersionAnswer> VersionAsync(
        ExperimentInput input, IReadOnlyList<ResolvedExecutable> executables, IReadOnlyList<string> refusals, IReadOnlyList<(string Variable, string Refusal)> homes)
    {
        if (Unasked(executables, refusals, homes) is { } cause)
        {
            return new GitVersionAnswer.NotAsked($"git's version was not checked: {cause}");
        }

        var real = ((ExecutableAnswer.Found)executables.First(executable => executable.Needed.Name == "git").Answer).Real;
        return (await new ArmGit(real, input.Root.Root, input.Verb.Environment).VersionAsync(input.Verb.Stopping)).Match<GitVersionAnswer>(
            text => new GitVersionAnswer.Read(text),
            reason => new GitVersionAnswer.Unanswered($"git --version at {real} could not be read: {reason}"));
    }

    /// <summary>The source is asked only through a git the resolution FOUND, whose version was read and passes <see cref="GitFloor.Admit"/> (a git whose guards are unknown is never run over the operator's checkout), and only while PATH is clean: git looks its own helpers up in PATH, and a refused PATH is exit 4
    /// whatever git says (and a PATH that is refused is the cause to name before git's own state). Otherwise the sentence says it was not checked, and why.</summary>
    private static async Task<SourceAnswer> SourceAsync(
        ExperimentInput input, IReadOnlyList<ResolvedExecutable> executables, IReadOnlyList<string> refusals, IReadOnlyList<(string Variable, string Refusal)> homes, GitVersionAnswer version)
    {
        var target = input.Experiment.Target;
        if (!Directory.Exists(target.Source))
        {
            return new SourceAnswer.Missing();
        }

        if (Unasked(executables, refusals, homes) is { } cause)
        {
            return new SourceAnswer.NotAsked($"the source was not checked: {cause}");
        }

        return Admitted(version) is { } why
            ? new SourceAnswer.NotAsked($"the source was not checked: {why}")
            : await SourceFacts.ReadAsync(target, ((ExecutableAnswer.Found)executables.First(executable => executable.Needed.Name == "git").Answer).Real, GitEnvironment.Of(input.Verb.Environment), RootFacts.RealPathOf, input.Verb.Stopping);
    }

    /// <summary>Why the source must not be asked of this git, or nothing when its version was read and passes the floor.</summary>
    private static string? Admitted(GitVersionAnswer version) =>
        version switch
        {
            GitVersionAnswer.Read read when GitVersion.Parse(read.Text) is Outcome<GitVersion>.Ok => GitFloor.Admit(read.Text) is Outcome<GitVersion>.Ok ? null : "git's version is below the floor",
            _ => "git's version could not be read",
        };

    private static IReadOnlyList<ResolvedExecutable> Executables(Experiment experiment, HostKind host, IReadOnlyList<string> entries, IReadOnlyList<string> roots) =>
        [.. HostChecks.NeededExecutables(experiment, host).Select(needed => new ResolvedExecutable(
            needed,
            ExecutableResolution.Resolve(needed.Name, entries, roots, RootFacts.Host, OperatingSystem.IsWindows())))];

    private static IReadOnlyList<string> PathRefusals(IReadOnlyList<string> entries, IReadOnlyList<string> roots) =>
        [.. entries.Select(entry => ExecutableResolution.PathEntryRefusal(entry, roots, RootFacts.Host)).Where(refusal => refusal.Length > 0).Distinct(StringComparer.Ordinal)];

    private static IReadOnlyDictionary<string, Outcome<string>> Templates(ExperimentInput input)
    {
        var files = new BriefTemplateFiles(input.Verb.Module.ShippedBriefs, input.Root.Briefs);
        return BriefRehearsal.StemsUsed(input.Experiment).ToDictionary(stem => stem, files.Read);
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
    internal static async Task<Passage<Outcome<IReadOnlyList<Scenario>>>> ScenariosAsync(ExperimentInput input, GateSeams seams)
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
        input.Verb.Stopping.ThrowIfCancellationRequested();
        var text = ExperimentGate.TextOf(judged.Path, seams.Road.Bounded);
        await seams.AfterRead(judged.Path);
        var same = ExperimentGate.StillTheSame(input.Verb, judged, seams);
        return same.Map(_ => text.Match(ScenariosFile.Read, Outcome<IReadOnlyList<Scenario>>.Failure));
    }
}
