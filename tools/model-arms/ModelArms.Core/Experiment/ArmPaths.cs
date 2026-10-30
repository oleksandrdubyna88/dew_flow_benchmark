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

    public string PidLog(string arm) => Path.Combine(Root, "pids", ArmPaths.Segment(arm, "arm") + ".log");

    public string Chain => Path.Combine(Root, "chain");

    public string Made(string arm) => Path.Combine(Root, "chain", "made-" + ArmPaths.Segment(arm, "arm"));

    public string Locked => Path.Combine(Root, "chain", "locked");

    public string Unlocked => Path.Combine(Root, "chain", "unlocked");

    public string ObjectiveMarker(string arm) => Path.Combine(Root, "chain", "objective-" + ArmPaths.Segment(arm, "arm"));

    public string ArmsDone => Path.Combine(Root, "chain", "arms-done");

    public string Executables => Path.Combine(Root, "chain", "executables.txt");

    public string GateLock => Path.Combine(Root, ".gate.lock");

    public string LockedModes => Path.Combine(Root, ".locked-modes");

    public string LockLog => Path.Combine(Root, "lock.log");

    /// <summary>Where <c>unlock</c> moves <see cref="LockedModes"/> to once it has restored the modes; the stamp is UTC, like <c>20261029T101500Z</c>.</summary>
    public string LockedModesRestored(string stamp) => Path.Combine(Root, ".locked-modes.restored-" + ArmPaths.Segment(stamp, "stamp"));

    public string KeepAwakeLog => Path.Combine(Root, "keep-awake.log");

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

    public string McpNone => Under("mcp-none.json");

    public string McpCoai => Under("mcp-coai.json");

    public string Session(string step) => OutFile(step, ".session");

    public string Stream(string step, int n) => OutFile(step, Attempt(n, ".jsonl"));

    public string Stderr(string step, int n) => OutFile(step, Attempt(n, ".err"));

    public string Prompt(string step, int n) => OutFile(step, Attempt(n, ".prompt.md"));

    public string Done(string step) => OutFile(step, ".done");

    public string Before(string step) => OutFile(step, ".before");

    public string Diff(string step) => OutFile(step, ".diff");

    public string Timing => Under("out", "timing.jsonl");

    public string ObjectiveFolder => Under("out", "objective");

    public string Objective => Under("out", "objective.json");

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
    /// stand-in task id, so a path spelled there once is spelled nowhere else. Compared without regard to case, so it fails closed on a host whose file system ignores it.</summary>
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

        public bool Matches(string path) =>
            path.Length > Prefix.Length + Suffix.Length && path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && path.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)
            && !path[Prefix.Length..^Suffix.Length].Contains('/', StringComparison.Ordinal);
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
