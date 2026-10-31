using System.Globalization;
using System.Text;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>Every path of an experiment root, spelled once. Nothing else in the module concatenates one:
/// a verb asks this type, so the layout of <c>todo/PLAN_model_arms.md</c> §4.3 cannot drift between the
/// verb that writes a file and the verb that reads it. A path segment taken from a caller (an arm name) must
/// be one name — empty, <c>.</c>, <c>..</c> or a separator is a programming error and throws
/// <see cref="ArgumentException"/>; an operator's configured file name goes through <see cref="Of"/>, which
/// refuses by value.</summary>
public sealed record RootPaths(string Root)
{
    public string ExperimentFile => Path.Combine(Root, "experiment.json");

    public string Briefs => Path.Combine(Root, "briefs");

    public string Base => Path.Combine(Root, "base");

    public string ArmMapFile => Path.Combine(Root, "arms.map.json");

    /// <summary>The folder holding one subfolder per arm, named by token.</summary>
    public string Arms => Path.Combine(Root, "r");

    public string Pids => Path.Combine(Root, "pids");

    public string PidFile(string arm) => Path.Combine(Root, "pids", ArmPaths.Segment(arm, "arm") + ".pid");

    /// <summary>The runner's own lock (D13, E5.S1): held by the <c>run-arm</c> process for its life, free the moment it dies — the one answer to whether an arm runs. Never deleted (§4.11): a stale file is not a stale lock.</summary>
    public string RunnerLock(string arm) => Path.Combine(Root, "pids", ArmPaths.Segment(arm, "arm") + ".lock");

    public string PidLog(string arm) => Path.Combine(Root, "pids", ArmPaths.Segment(arm, "arm") + ".log");

    public string Chain => Path.Combine(Root, "chain");

    public string Made(string arm) => Path.Combine(Root, "chain", "made-" + ArmPaths.Segment(arm, "arm"));

    public string Locked => Path.Combine(Root, "chain", "locked");

    public string Unlocked => Path.Combine(Root, "chain", "unlocked");

    public string ObjectiveMarker(string arm) => Path.Combine(Root, "chain", "objective-" + ArmPaths.Segment(arm, "arm"));

    public string ArmsDone => Path.Combine(Root, "chain", "arms-done");

    public string Executables => Path.Combine(Root, "chain", "executables.txt");

    public string GateLock => Path.Combine(Root, ".gate.lock");

    /// <summary>D11's lock over every change of a mode (E5.S1): taken EXCLUSIVE by <c>lock</c> and <c>unlock</c>, held SHARED by every runner for its life, so no runner starts during a restore and no restore starts beside a runner.</summary>
    public string IsolationLock => Path.Combine(Root, ".isolation.lock");

    public string LockedModes => Path.Combine(Root, ".locked-modes");

    /// <summary>The second immutable file of D11's two-file state (<c>{"run","digest","verifiedAt"}</c>): present beside <see cref="LockedModes"/> with the same run and digest, the snapshot is <i>applied</i>. Spelled here for the readers (E3.S4, §8 item 2); E5.S1 writes it.</summary>
    public string LockedModesApplied => Path.Combine(Root, ".locked-modes.applied");

    /// <summary>One run's stage marker, <c>chain/&lt;run&gt;/locked|unlocked|arms-done</c> (D27). The root-level <see cref="Locked"/>, <see cref="Unlocked"/> and <see cref="ArmsDone"/> are E5.S4's to remove.</summary>
    public string RunMarker(string run, RunStage stage) =>
        Path.Combine(Root, "chain", ArmPaths.Segment(run, "run"), stage switch { RunStage.Locked => "locked", RunStage.Unlocked => "unlocked", _ => "arms-done" });

    public string LockLog => Path.Combine(Root, "lock.log");

    /// <summary>Where <c>unlock</c> moves <see cref="LockedModes"/> to once it has restored the modes; the stamp is UTC, like <c>20261029T101500Z</c>.</summary>
    public string LockedModesRestored(string stamp) => Path.Combine(Root, ".locked-modes.restored-" + ArmPaths.Segment(stamp, "stamp"));

    /// <summary>Where <c>unlock</c> moves <see cref="LockedModesApplied"/> to, beside <see cref="LockedModesRestored"/> (the same stamp rules, a <c>-2</c>… suffix for a second cycle in one second).</summary>
    public string LockedModesAppliedRestored(string stamp) => Path.Combine(Root, ".locked-modes.applied.restored-" + ArmPaths.Segment(stamp, "stamp"));

    public string KeepAwakeLog => Path.Combine(Root, "keep-awake.log");

    /// <summary>What <c>prep-base</c> writes at the root (<c>prep-base.sh:5</c>, <c>:14-17</c>): a line per stage, and each tool's log. An earlier attempt's copies are moved aside into <see cref="PrepBasePrevious"/> before the next attempt creates its own.</summary>
    public string PrepBaseLog => Path.Combine(Root, "prep-base.log");

    public string BaseBuildLog => Path.Combine(Root, "base-build.log");

    /// <summary>The log of the <paramref name="n"/>th <c>npm ci</c> of the attempt (1-based, the rules submodule's first).</summary>
    public string BaseNpmLog(int n) => Path.Combine(Root, "base-npm-" + n.ToString(CultureInfo.InvariantCulture) + ".log");

    public string BaseSuiteLog => Path.Combine(Root, "base-test.log");

    public string BaseExtensionLog => Path.Combine(Root, "base-ts-test.log");

    /// <summary>The folder the main suite writes its results into (<c>--results-directory</c>).</summary>
    public string BaseResults => Path.Combine(Root, "base-results");

    /// <summary>Where an earlier attempt's <c>prep-base.log</c>, <c>base-*.log</c> and <c>base-results</c> are moved to (kept, never followed); the stamp is UTC, like <c>20261030T101500Z</c>.</summary>
    public string PrepBasePrevious(string stamp) => Path.Combine(Root, "prep-base.prev-" + ArmPaths.Segment(stamp, "stamp"));

    /// <summary>A file the operator named in the experiment file, resolved inside the root; a rooted name or any
    /// <c>..</c> segment would leave it and is refused.</summary>
    public Outcome<string> Of(string relative) =>
        IsInsideTheRoot(relative)
            ? Outcome<string>.Success(Path.Combine(Root, relative))
            : Outcome<string>.Failure($"'{relative}' must be a file name inside the experiment root");

    public ArmPaths Arm(string token) => new(Root, ArmPaths.Segment(token, "token"));

    private static bool IsInsideTheRoot(string relative) =>
        relative.Length > 0 && !IsRooted(relative) && !relative.Split('/', '\\').Contains("..");

    private static bool IsRooted(string relative) =>
        Path.IsPathRooted(relative) || relative[0] is '/' or '\\' || (relative.Length > 1 && relative[1] == ':');

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Root = ").Append(Root);
        return true;
    }
}

/// <summary>The three stages of a run the chain marks (D27).</summary>
public enum RunStage { Locked, Unlocked, ArmsDone }

/// <summary>Every path under one arm's folder, <c>r/&lt;token&gt;</c>, spelled once — the shell harness's
/// spelling, plus the files its helper scripts wrote that the first sketch of the layout missed.</summary>
public sealed record ArmPaths(string Root, string Token)
{
    public string Folder => Path.Combine(Root, "r", Segment(Token, "token"));

    /// <summary>The arm's clone of the target repository. Not called <c>Clone</c>: a record reserves that name for its copy method (CS8859).</summary>
    public string CloneFolder => Under("arm");

    public string Out => Under("out");

    public string Notes => Under("notes");

    public string Work => Under("work");

    /// <summary>The CLI's <c>TMPDIR</c>: its default would show every arm's name.</summary>
    public string Tmp => Under("tmp");

    public string Worktrees => Under("wt");

    public string Worktree(string id) => Under("wt", Segment(id, "id"));

    public string CoaiSettings(string side) => Under("coai-data", Segment(side, "side"), "settings.json");

    /// <summary>The folder <c>COAI_DATA_DIR</c> names for the arm's coai server (<c>after6.sh:34</c>); <see cref="CoaiSettings"/> lives under it.</summary>
    public string CoaiData => Under("coai-data");

    public string McpNone => Under("mcp-none.json");

    public string McpCoai => Under("mcp-coai.json");

    public string Session(string step) => OutFile(step, ".session");

    public string Stream(string step, int n) => OutFile(step, Attempt(n, ".jsonl"));

    public string Stderr(string step, int n) => OutFile(step, Attempt(n, ".err"));

    public string Prompt(string step, int n) => OutFile(step, Attempt(n, ".prompt.md"));

    public string Done(string step) => OutFile(step, ".done");

    /// <summary>The gate's marker "attempt N finished, continue its session" (D7, D10) — spelled as <see cref="Done"/> is.</summary>
    public string Continue(string step) => OutFile(step, ".continue");

    public string Before(string step) => OutFile(step, ".before");

    public string Diff(string step) => OutFile(step, ".diff");

    public string Timing => Under("out", "timing.jsonl");

    public string ObjectiveFolder => Under("out", "objective");

    public string Objective => Under("out", "objective.json");

    /// <summary>The four logs <c>checks</c> keeps in <see cref="ObjectiveFolder"/> (the shell's <c>build.log</c>, <c>mcp-tests.log</c>, <c>format.log</c>, <c>ts-tests.log</c>): each is created exclusively before the first child starts (E3.S5).</summary>
    public string ObjectiveBuildLog => Under("out", "objective", "build.log");

    public string ObjectiveSuiteLog => Under("out", "objective", "mcp-tests.log");

    public string ObjectiveFormatLog => Under("out", "objective", "format.log");

    public string ObjectiveExtensionLog => Under("out", "objective", "ts-tests.log");

    /// <summary>How each stage ended (<c>{"build":{"exit":0,"seconds":65,"killed":false},…}</c>): what <c>checks --parse-only</c> reads the exits from.</summary>
    public string ObjectiveStages => Under("out", "objective", "stages.json");

    /// <summary>Where an earlier <see cref="ObjectiveFolder"/> is renamed to before a new run makes its own (kept, never followed); the stamp is UTC, like <c>20261030T101500Z</c>.</summary>
    public string ObjectivePrevious(string stamp) => Under("out", "objective.prev-" + Segment(stamp, "stamp"));

    public string TasksPlanLog => Under("out", "tasks-plan.log");

    public string BatchDone(int i) => Under("out", "batch-" + Number(i) + ".done");

    public string TaskBefore(string id) => TaskFile(id, ".before");

    /// <summary>The coder's snapshot of HEAD before a task; the shell's name for it carried a model's name, and no path may.</summary>
    public string CoderHead(string id) => TaskFile(id, "-coder.head");

    public string CoderDiff(string id) => TaskFile(id, "-coder.diff");

    public string Picked(string id) => Under("out", "picked-" + Segment(id, "id"));

    public string PickRecord(string id) => Under("out", "pick-" + Segment(id, "id") + ".json");

    public string PickLog(string id) => Under("out", "pick-" + Segment(id, "id") + ".log");

    /// <summary>The warm build <c>make</c> runs in the clone.</summary>
    public string PrepBuildLog => Under("out", "prep-build.log");

    /// <summary>What the objective checks printed.</summary>
    public string ObjectiveLog => Under("out", "objective.out");

    public string WorktreeBuildLog(string id) => Under("out", "wt-build-" + Segment(id, "id") + ".log");

    /// <summary>The file a binding stands for (<c>notes/hA.json</c>), under the arm folder. The reader refused a climbing, empty or rooted segment, so a throw here is a
    /// caller's bug, caught by <see cref="Segment"/> as it is for every other member.</summary>
    public string NotesFile(OutputBinding binding) => Path.Combine([Folder, .. binding.RelativePath.Split('/').Select(segment => Segment(segment, "output segment"))]);

    public string TaskNotes(string id) => Under("notes", Segment(id, "id") + "-code.md");

    public string TasksJson => Under("work", "tasks.json");

    public string TaskBrief(string id) => Under("work", Segment(id, "id") + ".md");

    public string BatchesJson => Under("work", "batches.json");

    /// <summary>Whether <paramref name="relativePath"/> (under the arm folder, forward slashes) is a file the task pipeline writes: <c>work/tasks.json</c>, <c>work/batches.json</c>, a task's
    /// brief or its notes. The shapes are read from the members that spell them (<see cref="TasksJson"/>, <see cref="BatchesJson"/>, <see cref="TaskBrief"/>, <see cref="TaskNotes"/>) with a
    /// stand-in task id, so a path spelled there once is spelled nowhere else; the id in the stand-in's place is one <see cref="TaskIds"/> accepts (<c>t1</c>), so <c>work/summary.md</c> is not a task's. Compared without regard to case, so it fails closed on a host whose file system ignores it.</summary>
    public static bool IsPipelineFile(string relativePath) =>
        PipelineNames.Contains(relativePath, StringComparer.OrdinalIgnoreCase) || PipelineShapes.Any(shape => shape.Matches(relativePath));

    private const string StandIn = "{id}";

    private static readonly ArmPaths Sample = new("root", "token");

    private static readonly string[] PipelineNames = [Relative(Sample.TasksJson), Relative(Sample.BatchesJson)];

    private static readonly PipelineShape[] PipelineShapes = [PipelineShape.Of(Relative(Sample.TaskBrief(StandIn))), PipelineShape.Of(Relative(Sample.TaskNotes(StandIn)))];

    private static string Relative(string path) => Path.GetRelativePath(Sample.Folder, path).Replace('\\', '/');

    /// <summary>A path with one task id in it: what comes before the id and what comes after.</summary>
    private sealed record PipelineShape(string Prefix, string Suffix)
    {
        public static PipelineShape Of(string spelled) => new(spelled[..spelled.IndexOf(StandIn, StringComparison.Ordinal)], spelled[(spelled.IndexOf(StandIn, StringComparison.Ordinal) + StandIn.Length)..]);

        /// <summary>The path is the prefix, a task id (<see cref="TaskIds.Pattern"/>) and the suffix. Compared as the destination rule compares, without regard to case, so <c>notes/T1-code.md</c> is a pipeline file too.</summary>
        public bool Matches(string path) =>
            path.Length > Prefix.Length + Suffix.Length && path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)
            && TaskIds.IsTaskId(path[Prefix.Length..^Suffix.Length].ToLowerInvariant());
    }

    /// <summary>A name that is exactly one path segment; anything else is a caller's bug, not an operator's mistake.</summary>
    internal static string Segment(string value, string what) =>
        value.Length == 0 || value is "." or ".." || value.AsSpan().IndexOfAny('/', '\\') >= 0
            ? throw new ArgumentException($"a {what} must be one path segment (not empty, '.', '..' or containing a separator), got '{value}'", what)
            : value;

    private string Under(params string[] segments) => Path.Combine([Folder, .. segments]);

    private string OutFile(string step, string suffix) => Under("out", Segment(step, "step") + suffix);

    private string TaskFile(string id, string suffix) => Under("out", "task-" + Segment(id, "id") + suffix);

    private static string Attempt(int n, string suffix) => ".a" + Number(n) + suffix;

    private static string Number(int n) => n.ToString(CultureInfo.InvariantCulture);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Root = ").Append(Root).Append(", Token = ").Append(Token);
        return true;
    }
}
