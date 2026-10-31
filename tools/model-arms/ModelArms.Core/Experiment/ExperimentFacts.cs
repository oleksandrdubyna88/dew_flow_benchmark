using Bench.Domain.Targets;

namespace ModelArms.Core.Experiment;

/// <summary>Where this run stands, handed in by the console: the experiment root and the home directory it was given, the git
/// checkout that already encloses the root (empty when there is none), and the repository this binary was built from. Core
/// asks the world nothing; the console answers these four questions once and passes the answers here.</summary>
public sealed record ExperimentLocation(string Root, string Home, string EnclosingGitTop, string ThisRepository);

/// <summary>How the machine is kept awake while the arms run. <c>Auto</c> lets the console pick by host; the others force one way.</summary>
public enum AwakeChoice { Auto, Windows, Wsl, None }

/// <summary>The suite the objective checks run: its command (an executable and its arguments), the CTRF report it writes
/// and the prefix its test names carry.</summary>
public sealed record SuiteFacts(IReadOnlyList<string> Command, string CtrfFile, string NamePrefix);

/// <summary>The formatter check: the solution it runs over and the verbs it runs.</summary>
public sealed record FormatFacts(string Solution, IReadOnlyList<string> Verbs);

/// <summary>The editor extension's own tests: the folder they run in and the command.</summary>
public sealed record ExtensionFacts(string Directory, IReadOnlyList<string> Command);

/// <summary>The repository the arms work on and every command the objective checks run. Every command is an executable and its
/// arguments: no shell string anywhere, and the executable is a name from PATH or an absolute path outside the target (D17).</summary>
public sealed record TargetFacts(
    string Source,
    string PlanBranch,
    CommitSha Base,
    string Plan,
    string Branch,
    string RulesSubmodule,
    IReadOnlyList<string> LowPriority,
    IReadOnlyList<string> Build,
    SuiteFacts Suite,
    FormatFacts Format,
    ExtensionFacts Extension,
    IReadOnlyList<string> NodeModules,
    IReadOnlyList<string> TestGlobs);

/// <summary>The waiting limits, in seconds and tries, that every step and the gate run under.</summary>
public sealed record Limits(int WaitSeconds, int Tries, int GateWaitSeconds, int GateTries, int MinimumRemainingSeconds, int KillGraceSeconds);

/// <summary>How many task batches the SRS pipeline's split may plan, how many run at once, the ids of the three steps the block runs (the split, its one fix round and the
/// task coder) and what the block contributes to the steps after it besides its split steps' outputs — the example binds <c>CODE_NOTES</c>, the path the shell handed SRS's
/// reviewers though no SRS step wrote it. <see cref="Shell"/> is what the shell harness hard-coded, kept as the default when the file says nothing.
/// <para>
/// Equality is by value, <see cref="Outputs"/> included.
/// </para></summary>
public sealed record TaskLimits(int Min, int Max, int MaxParallel, StepId Split, StepId SplitFix, StepId TaskCode, IReadOnlyList<OutputBinding> Outputs)
{
    public static TaskLimits Shell { get; } = new(4, 7, 4, new StepId("split"), new StepId("splitFix"), new StepId("taskCode"), []);

    public bool Equals(TaskLimits? other) =>
        other is not null && (Min, Max, MaxParallel, Split, SplitFix, TaskCode) == (other.Min, other.Max, other.MaxParallel, other.Split, other.SplitFix, other.TaskCode)
        && Outputs.SequenceEqual(other.Outputs);

    public override int GetHashCode() => HashCode.Combine(Min, Max, MaxParallel, Split, SplitFix, TaskCode, Outputs.Count);
}

/// <summary>One named run (D27): its rounds of arms, the arms of earlier runs it closes while it works, and the paths it closes besides, already expanded.</summary>
public sealed record RunSpec(string Name, IReadOnlyList<IReadOnlyList<string>> Rounds, IReadOnlyList<string> ClosedArms, IReadOnlyList<string> Closed);

/// <summary>The multi-vendor code gate's binary, its settings file and the folder name its side lives under. <see cref="None"/>
/// means the experiment runs no gate; absent is a different fact from empty text, so it has its own value.</summary>
public sealed record CoaiFacts(string Binary, string Settings, string Side)
{
    public static CoaiFacts None { get; } = new(string.Empty, string.Empty, string.Empty);

    public bool Configured => Binary.Length > 0;
}

/// <summary>The paths closed (mode 000) and unlisted (mode 300) while the arms run, already expanded: <c>&lt;root&gt;</c> is the
/// experiment root and a leading <c>~</c> is the home directory.</summary>
public sealed record IsolationFacts(IReadOnlyList<string> Closed, IReadOnlyList<string> Unlisted)
{
    public static IsolationFacts None { get; } = new([], []);

    public bool Configured => Closed.Count + Unlisted.Count > 0;
}

/// <summary>Everything the experiment file states, read strictly. <c>ScenariosFile</c> and <c>BaselineFile</c> are RELATIVE names
/// inside the root, as configured; <c>Root</c> is absolute, normalised, without a trailing separator.</summary>
public sealed record Experiment(
    string Root,
    TargetFacts Target,
    string ScenariosFile,
    string BaselineFile,
    string Known,
    IReadOnlyDictionary<StepId, StepSpec> Steps,
    IReadOnlyList<Scheme> Schemes,
    IReadOnlyList<RunSpec> Runs,
    Limits Limits,
    TaskLimits Tasks,
    ToolSets Tools,
    CoaiFacts Coai,
    IsolationFacts Isolation,
    AwakeChoice Awake);
