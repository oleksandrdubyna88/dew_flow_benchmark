using Bench.Domain;
using ModelArms.Core.Briefs;

namespace ModelArms.Core.Experiment;

/// <summary>Which kind of machine the harness runs on. The three differ in what they can do: only Windows and WSL have the PowerShell that keeps the
/// machine awake, only POSIX has the file modes <c>isolation</c> uses.</summary>
public enum HostKind { Windows, Wsl, OtherPosix }

/// <summary>Names the machine from three facts the console read.</summary>
public static class HostKinds
{
    /// <summary>Windows; else WSL when the distribution name is set or <c>/proc/version</c> mentions Microsoft (ignoring case); else any other POSIX host.</summary>
    public static HostKind From(bool isWindows, string wslDistroName, string procVersion) =>
        isWindows ? HostKind.Windows
        : wslDistroName.Length > 0 || procVersion.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ? HostKind.Wsl
        : HostKind.OtherPosix;
}

/// <summary>One executable the experiment needs and where it really is on this host (the REAL path, which the harness launches and prints), or why it is refused.</summary>
public sealed record ResolvedExecutable(string Name, Outcome<string> Path);

/// <summary>Everything the console read about the host that <see cref="HostChecks.Decide"/> judges. A fact it could not establish is <c>false</c>, and the
/// sentence that names the fact is the answer — not a guess about why.
/// <para>
/// <paramref name="PathRefusals"/> are the sentences <see cref="ExecutableResolution.PathEntryRefusal"/> gave for the entries of PATH (the children inherit it),
/// and <paramref name="Templates"/> the brief template each step kind the schemes use was read as — both default to nothing read, so a host that was not
/// asked is a host with nothing to say.
/// </para></summary>
public sealed record HostFacts(
    HostKind Host,
    IReadOnlyList<ResolvedExecutable> Executables,
    bool SourceExists,
    bool SourceIsGit,
    bool PlanBranchExists,
    bool BaseExists,
    bool PlanAtBase,
    Outcome<IReadOnlyList<Scenario>> Scenarios,
    bool CoaiBinaryExists,
    bool CoaiSettingsExists)
{
    public IReadOnlyList<string> PathRefusals { get; init; } = [];

    public IReadOnlyDictionary<StepKind, Outcome<string>> Templates { get; init; } = new Dictionary<StepKind, Outcome<string>>();
}

/// <summary>One finding. <paramref name="Environment"/> is true when the host is not ready (a CLI it lacks: exit 3, D16) and false when the configuration is
/// wrong (exit 4).</summary>
public sealed record HostProblem(string Text, bool Environment);

/// <summary>D23: every check an experiment must pass on the machine it will run on, decided from facts the console read and returned all at once.</summary>
public static class HostChecks
{
    private const string PowerShell = "pwsh.exe";
    private const string Refusal = "(D17)";

    /// <summary>The executables the experiment will start, each once, in the order the harness reaches for them: the three it always needs, the first word of
    /// each configured command, PowerShell when the awake keeper uses it, and the coai binary when a step is a gate step.</summary>
    public static IReadOnlyList<string> NeededExecutables(Experiment experiment, HostKind host) =>
    [
        .. new[]
            {
                "git",
                "claude",
                "dotnet",
                First(experiment.Target.LowPriority),
                First(experiment.Target.Build),
                First(experiment.Target.Suite.Command),
                First(experiment.Target.Extension.Command),
                KeepsAwakeWithPowerShell(experiment.Awake, host) ? PowerShell : string.Empty,
                HasGateStep(experiment) ? experiment.Coai.Binary : string.Empty,
            }
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>One <see cref="HostProblem"/> per finding, all at once, in the order of the checks.</summary>
    public static IReadOnlyList<HostProblem> Decide(Experiment experiment, HostFacts facts) =>
    [
        .. ExecutableProblems(facts),
        .. facts.PathRefusals.Select(Wrong),
        .. SourceProblems(experiment.Target, facts),
        .. ScenarioProblems(facts),
        .. CoaiProblems(experiment, facts),
        .. HostProblems(experiment, facts),
        .. FlowProblems(experiment),
        .. TemplateProblems(experiment, facts),
    ];

    private static string First(IReadOnlyList<string> command) => command.Count > 0 ? command[0] : string.Empty;

    private static bool KeepsAwakeWithPowerShell(AwakeChoice awake, HostKind host) =>
        awake == AwakeChoice.Wsl || (awake == AwakeChoice.Auto && host == HostKind.Wsl);

    private static bool HasGateStep(Experiment experiment) =>
        experiment.Schemes.SelectMany(scheme => scheme.Entries).OfType<SchemeEntry.Step>()
            .Any(step => experiment.Steps.TryGetValue(step.Kind, out var spec) && spec.Mode == StepMode.Gate);

    private static HostProblem Wrong(string text) => new(text, Environment: false);

    private static IEnumerable<HostProblem> Only(bool found, string text) => found ? [Wrong(text)] : [];

    /// <summary>An executable the host lacks is the environment (exit 3). One it HAS but that D17 refuses (a link into the target, a file under the root) is a
    /// wrong configuration (exit 4): every D17 refusal says so, and "not on PATH" and "does not exist" do not.</summary>
    private static IEnumerable<HostProblem> ExecutableProblems(HostFacts facts) =>
        facts.Executables.Select(executable => executable.Path).OfType<Outcome<string>.Fail>()
            .Select(fail => new HostProblem(fail.Reason, Environment: !fail.Reason.Contains(Refusal, StringComparison.Ordinal)));

    private static IEnumerable<HostProblem> SourceProblems(TargetFacts target, HostFacts facts) =>
        (facts.SourceExists, facts.SourceIsGit) switch
        {
            (false, _) => [Wrong($"target.source {target.Source} does not exist")],
            (true, false) => [Wrong($"target.source {target.Source} is not a git repository")],
            _ => GitProblems(target, facts),
        };

    /// <summary>The plan is asked of a commit that exists, so a missing base is one finding, not two.</summary>
    private static IEnumerable<HostProblem> GitProblems(TargetFacts target, HostFacts facts) =>
    [
        .. Only(!facts.PlanBranchExists, $"the plan branch {target.PlanBranch} does not exist in {target.Source}"),
        .. Only(!facts.BaseExists, $"the base commit {target.Base.Value} is not in {target.Source}"),
        .. Only(facts.BaseExists && !facts.PlanAtBase, $"the plan {target.Plan} does not exist at {target.Base.Value}"),
    ];

    private static IEnumerable<HostProblem> ScenarioProblems(HostFacts facts) =>
        facts.Scenarios is Outcome<IReadOnlyList<Scenario>>.Fail fail ? fail.Reason.Split('\n').Select(Wrong) : [];

    private static IEnumerable<HostProblem> CoaiProblems(Experiment experiment, HostFacts facts) =>
        experiment.Coai.Configured
            ? [.. Only(!facts.CoaiBinaryExists && !Refused(facts, experiment.Coai.Binary), $"coai.binary {experiment.Coai.Binary} does not exist"),
                .. Only(!facts.CoaiSettingsExists, $"coai.settings {experiment.Coai.Settings} does not exist")]
            : Only(HasGateStep(experiment), "a scheme has a gate step and coai is not configured");

    /// <summary>Whether the executable of that name was already refused above: a coai binary that is missing is said once.</summary>
    private static bool Refused(HostFacts facts, string name) =>
        facts.Executables.Any(executable => executable.Name == name && executable.Path is Outcome<string>.Fail);

    private static IEnumerable<HostProblem> HostProblems(Experiment experiment, HostFacts facts) =>
    [
        .. Only(facts.Host == HostKind.Windows && experiment.Isolation.Configured, "isolation by file modes is POSIX only — remove \"isolation\" on Windows"),
        .. AwakeProblems(experiment.Awake, facts.Host),
    ];

    private static IEnumerable<HostProblem> AwakeProblems(AwakeChoice awake, HostKind host) =>
        (awake, host) switch
        {
            (AwakeChoice.Wsl, not HostKind.Wsl) => [Wrong(NeedsHost("wsl", HostKind.Wsl, host))],
            (AwakeChoice.Windows, not HostKind.Windows) => [Wrong(NeedsHost("windows", HostKind.Windows, host))],
            _ => [],
        };

    private static string NeedsHost(string awake, HostKind needed, HostKind actual) =>
        $"awake \"{awake}\" needs a {needed} host; this host is {actual}";

    private static IEnumerable<HostProblem> FlowProblems(Experiment experiment) =>
    [
        .. experiment.Schemes.Where(scheme => scheme.Entries.OfType<SchemeEntry.Tasks>().Any()).SelectMany(scheme => AbsentTaskSteps(experiment, scheme)),
        .. experiment.Rounds.SelectMany(round => round).GroupBy(arm => arm, StringComparer.Ordinal).Where(arm => arm.Count() > 1)
            .Select(arm => Wrong($"arm {arm.Key} is named twice in rounds")),
    ];

    private static IEnumerable<HostProblem> AbsentTaskSteps(Experiment experiment, Scheme scheme)
    {
        var absent = SchemeEntry.Tasks.Kinds.Where(kind => !experiment.Steps.ContainsKey(kind)).Select(StepKinds.Key).ToList();
        return Only(absent.Count > 0, $"scheme {scheme.Name} has a tasks entry, and steps does not define {string.Join(", ", absent)}");
    }

    /// <summary>R4 of the brief filler's review: every template the schemes use is filled once now, so a name the leak guard would refuse is found before the
    /// chain starts and not at a step's launch hours into it.</summary>
    private static IEnumerable<HostProblem> TemplateProblems(Experiment experiment, HostFacts facts) =>
        BriefRehearsal.Problems(experiment, new RootPaths(experiment.Root), ScenarioCount(facts), facts.Templates).Select(Wrong);

    private static int ScenarioCount(HostFacts facts) =>
        facts.Scenarios is Outcome<IReadOnlyList<Scenario>>.Ok ok ? ok.Value.Count : 1;
}
