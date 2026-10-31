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

/// <summary>An executable the experiment needs and the field that asked for it (<c>target.suite.command[0]</c>, <c>coai.binary</c>); the role is empty for the three
/// the harness always needs, which name themselves.</summary>
public sealed record NeededExecutable(string Role, string Name);

/// <summary>A needed executable and what <see cref="ExecutableResolution.Resolve"/> answered for it.</summary>
public sealed record ResolvedExecutable(NeededExecutable Needed, ExecutableAnswer Answer);

/// <summary>What git said about <c>target.source</c>, or why it was not asked. Four states, closed: each has its own exit.</summary>
public abstract record SourceAnswer
{
    private SourceAnswer() { }

    /// <summary>The source is not a folder — exit 4.</summary>
    public sealed record Missing : SourceAnswer;

    /// <summary>Git answered all it was asked; every "no" is an answer (<paramref name="IsGit"/> false stops the questions) — exit 4 each.</summary>
    public sealed record Asked(bool IsGit, bool PlanBranch, bool Base, bool PlanAtBase) : SourceAnswer;

    /// <summary>Git was not asked, and the sentence says why (git absent or refused, PATH refused) — printed as a finding the exit of which is already 3 or 4 from the cause.</summary>
    public sealed record NotAsked(string Reason) : SourceAnswer;

    /// <summary>Git ran and could not say (dubious ownership, a timeout, a start that failed) — exit 3.</summary>
    public sealed record Unanswered(string Reason) : SourceAnswer;
}

/// <summary>What the resolved git said to <c>--version</c> (D23, E4.S0), or why it was not asked. Closed: <see cref="Read"/> is the first line of stdout, trimmed — whatever it is, <see cref="GitFloor.Admit"/> judges it; <see cref="Unanswered"/> git ran or was started and did not answer (does not start, exits non-zero, timed out) — exit 3; <see cref="NotAsked"/> git is absent or refused,
/// or PATH or a config folder is refused: a finding whose cause is already printed, mirrored on <see cref="SourceAnswer.NotAsked"/>, exit 3.</summary>
public abstract record GitVersionAnswer
{
    private GitVersionAnswer() { }

    public sealed record Read(string Text) : GitVersionAnswer;

    public sealed record Unanswered(string Reason) : GitVersionAnswer;

    public sealed record NotAsked(string Reason) : GitVersionAnswer;
}

/// <summary>Everything the console read about the host that <see cref="HostChecks.Decide"/> judges. A fact it could not establish is never a guess: git that did not
/// answer is <see cref="SourceAnswer.Unanswered"/>, not "no".
/// <para>
/// <paramref name="PathRefusals"/> are the sentences <see cref="ExecutableResolution.PathEntryRefusal"/> gave for the entries of PATH (the children inherit it),
/// <paramref name="EnvironmentRefusals"/> the sentences <see cref="ExecutableResolution.ConfigFolderRefusal"/> gave for <c>HOME</c> and <c>XDG_CONFIG_HOME</c> (git reads its
/// global config from there), and <paramref name="Templates"/> the brief template each stem the schemes use was read as. All four are <c>required</c>: a caller that did not read them
/// must say so, not default to a host with nothing to say.
/// </para></summary>
public sealed record HostFacts(
    HostKind Host,
    IReadOnlyList<ResolvedExecutable> Executables,
    Outcome<IReadOnlyList<Scenario>> Scenarios,
    bool CoaiSettingsExists)
{
    public required SourceAnswer Source { get; init; }

    public required GitVersionAnswer Version { get; init; }

    public required IReadOnlyList<string> PathRefusals { get; init; }

    public required IReadOnlyList<string> EnvironmentRefusals { get; init; }

    public required IReadOnlyDictionary<string, Outcome<string>> Templates { get; init; }
}

/// <summary>The environment git is launched under: the verb's map minus every <c>GIT_*</c> name (ordinal, ignoring case). A parent's <c>GIT_DIR</c>,
/// <c>GIT_WORK_TREE</c>, <c>GIT_CEILING_DIRECTORIES</c>, <c>GIT_CONFIG_*</c>, <c>GIT_EXEC_PATH</c> or <c>GIT_SSH*</c> would redirect the question or name a program.
/// <c>HOME</c> and <c>PATH</c> pass: the operator's own global config is theirs, and PATH was judged.</summary>
public static class GitEnvironment
{
    public static IReadOnlyDictionary<string, string> Of(IReadOnlyDictionary<string, string> environment) =>
        environment.Where(variable => !variable.Key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(variable => variable.Key, variable => variable.Value, StringComparer.Ordinal);
}

/// <summary>One finding. <paramref name="Environment"/> is true when the host is not ready (a CLI it lacks: exit 3, D16) and false when the configuration is
/// wrong (exit 4).</summary>
public sealed record HostProblem(string Text, bool Environment);

/// <summary>D23: every check an experiment must pass on the machine it will run on, decided from facts the console read and returned all at once.</summary>
public static class HostChecks
{
    private const string PowerShell = "pwsh.exe";

    /// <summary>The executables the experiment will start, each name once (the first role that asked for it kept), in the order the harness reaches for them:
    /// the three it always needs, the first word of each configured command, <c>npm</c> when <c>nodeModules</c> names a folder to restore (<c>prep-base</c> runs <c>npm ci</c> whatever the extension's command is — E3 §8 item 7), PowerShell when the awake keeper uses it, and the coai binary
    /// when a step is a gate step.</summary>
    public static IReadOnlyList<NeededExecutable> NeededExecutables(Experiment experiment, HostKind host) =>
    [
        .. new NeededExecutable[]
            {
                new(string.Empty, "git"),
                new(string.Empty, "claude"),
                new(string.Empty, "dotnet"),
                new("target.lowPriority[0]", First(experiment.Target.LowPriority)),
                new("target.build[0]", First(experiment.Target.Build)),
                new("target.suite.command[0]", First(experiment.Target.Suite.Command)),
                new("target.extension.command[0]", First(experiment.Target.Extension.Command)),
                new("target.nodeModules", experiment.Target.NodeModules.Count > 0 ? "npm" : string.Empty),
                new("awake", KeepsAwakeWithPowerShell(experiment.Awake, host) ? PowerShell : string.Empty),
                new("coai.binary", HasGateStep(experiment) ? experiment.Coai.Binary : string.Empty),
            }
            .Where(needed => needed.Name.Length > 0)
            .DistinctBy(needed => needed.Name, StringComparer.Ordinal),
    ];

    /// <summary>One <see cref="HostProblem"/> per finding, all at once, in the order of the checks.</summary>
    public static IReadOnlyList<HostProblem> Decide(Experiment experiment, HostFacts facts) =>
    [
        .. ExecutableProblems(facts),
        .. facts.PathRefusals.Select(Wrong),
        .. facts.EnvironmentRefusals.Select(Wrong),
        .. GitFloorProblems(facts),
        .. SourceProblems(experiment.Target, facts),
        .. ScenarioProblems(facts),
        .. CoaiProblems(experiment, facts),
        .. HostProblems(experiment, facts),
        .. TemplateProblems(experiment, facts),
    ];

    /// <summary>One WARNING line, never a refusal (validate's contract is unchanged): the isolation leaves <c>arms.map.json</c> readable by its fixed name under a root at 300 — every token, so a sibling's <c>r/&lt;token&gt;</c> (a residual the shell shared) — and <c>tools.deny</c> lacks the entry that denies the Read tool on THIS root's map. <c>tools.deny</c> reaches the CLI as written, with no
    /// <c>&lt;root&gt;</c> placeholder, so the entry is an absolute path the operator writes (<c>init</c> blanks every value and cannot). It would not stop <c>Bash(cat *)</c>, which the tool sets allow, so even present it narrows the residual and does not close it. POSIX roots only.</summary>
    public static IReadOnlyList<string> Warnings(Experiment experiment)
    {
        var wanted = $"Read(/{experiment.Root}/arms.map.json)";
        return experiment.Root.StartsWith('/') && !experiment.Tools.Deny.Contains(wanted, StringComparer.Ordinal)
            ? [$"warning: tools.deny has no {wanted} — a root at 300 still lets an arm read arms.map.json by its name (every token, so a sibling's folder); add the entry for THIS root (an absolute path: tools.deny is passed to the CLI as written) — it narrows the residual, `Bash(cat *)` still reads it"]
            : [];
    }

    private static string First(IReadOnlyList<string> command) => command.Count > 0 ? command[0] : string.Empty;

    private static bool KeepsAwakeWithPowerShell(AwakeChoice awake, HostKind host) =>
        awake == AwakeChoice.Wsl || (awake == AwakeChoice.Auto && host == HostKind.Wsl);

    private static bool HasGateStep(Experiment experiment) =>
        experiment.Schemes.SelectMany(scheme => scheme.Entries).OfType<SchemeEntry.Step>()
            .Any(step => experiment.Steps.TryGetValue(step.Id, out var spec) && spec.Kind == StepKind.Gate);

    private static HostProblem Wrong(string text) => new(text, Environment: false);

    private static IEnumerable<HostProblem> Only(bool found, string text) => found ? [Wrong(text)] : [];

    /// <summary>The exit is decided on the TYPE of the answer, never on its words: an executable the host lacks is the environment (exit 3, named by the field that
    /// asked for it), one the configuration names and D17 refuses is a wrong configuration (exit 4).</summary>
    private static IEnumerable<HostProblem> ExecutableProblems(HostFacts facts) => facts.Executables.SelectMany(ProblemsOf);

    private static IEnumerable<HostProblem> ProblemsOf(ResolvedExecutable executable) =>
        executable.Answer switch
        {
            ExecutableAnswer.Absent absent => [new HostProblem($"{executable.Needed.Role} {absent.Reason}".TrimStart(), Environment: true)],
            ExecutableAnswer.Refused refused => [Wrong(refused.Reason)],
            _ => [],
        };

    /// <summary>D23: the git's version against the floor. Every finding is the environment (exit 3): the host's git is too old, unreadable or unasked. A refusal of a version names the git it is about, by the real path the resolution found.</summary>
    private static IEnumerable<HostProblem> GitFloorProblems(HostFacts facts) =>
        facts.Version switch
        {
            GitVersionAnswer.Read read => GitFloor.Admit(read.Text).Match(_ => [], reason => (IEnumerable<HostProblem>)[new HostProblem($"git at {ExecutableRule.Shown(RealGit(facts))}: {reason}", Environment: true)]),
            GitVersionAnswer.Unanswered unanswered => [new(unanswered.Reason, Environment: true)],
            GitVersionAnswer.NotAsked notAsked => [new(notAsked.Reason, Environment: true)],
            _ => [],
        };

    private static string RealGit(HostFacts facts) =>
        facts.Executables.FirstOrDefault(executable => executable.Needed.Name == "git")?.Answer is ExecutableAnswer.Found found ? found.Real : "(not resolved)";

    private static IEnumerable<HostProblem> SourceProblems(TargetFacts target, HostFacts facts) =>
        facts.Source switch
        {
            SourceAnswer.Missing => [Wrong($"target.source {target.Source} does not exist")],
            SourceAnswer.Asked { IsGit: false } => [Wrong($"target.source {target.Source} is not a git repository")],
            SourceAnswer.Asked asked => GitProblems(target, asked),
            SourceAnswer.NotAsked notAsked => [new(notAsked.Reason, Environment: true)],
            SourceAnswer.Unanswered unanswered => [new(unanswered.Reason, Environment: true)],
            _ => [],
        };

    /// <summary>The plan is asked of a commit that exists, so a missing base is one finding, not two.</summary>
    private static IEnumerable<HostProblem> GitProblems(TargetFacts target, SourceAnswer.Asked asked) =>
    [
        .. Only(!asked.PlanBranch, $"the plan branch {target.PlanBranch} does not exist in {target.Source}"),
        .. Only(!asked.Base, $"the base commit {target.Base.Value} is not in {target.Source}"),
        .. Only(asked.Base && !asked.PlanAtBase, $"the plan {target.Plan} does not exist at {target.Base.Value}"),
    ];

    private static IEnumerable<HostProblem> ScenarioProblems(HostFacts facts) =>
        facts.Scenarios is Outcome<IReadOnlyList<Scenario>>.Fail fail ? fail.Reason.Split('\n').Select(Wrong) : [];

    private static IEnumerable<HostProblem> CoaiProblems(Experiment experiment, HostFacts facts) =>
        experiment.Coai.Configured
            ? Only(!facts.CoaiSettingsExists, $"coai.settings {experiment.Coai.Settings} does not exist")
            : Only(HasGateStep(experiment), "a scheme has a gate step and coai is not configured");

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

    /// <summary>R4 of the brief filler's review: every template the schemes use is filled once now, so a name the leak guard would refuse is found before the
    /// chain starts and not at a step's launch hours into it.</summary>
    private static IEnumerable<HostProblem> TemplateProblems(Experiment experiment, HostFacts facts) =>
        BriefRehearsal.Problems(experiment, new RootPaths(experiment.Root), ScenarioCount(facts), facts.Templates).Select(Wrong);

    private static int ScenarioCount(HostFacts facts) =>
        facts.Scenarios is Outcome<IReadOnlyList<Scenario>>.Ok ok ? ok.Value.Count : 1;
}
