using System.Reflection;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>One spelling of every path of an experiment root and of an arm. The expected strings below are written
/// out by hand from the shell harness's layout (<c>todo/PLAN_model_arms.md</c> §4.3), so a member renamed in the
/// code cannot quietly agree with itself. The table is also checked against the members the types really have, so
/// a path added without a spelling here fails the build of this class.
/// <para>
/// What this does NOT prove: that a verb writes to these paths, or that the directory exists. The paths are
/// strings; creating folders is the console's.
/// </para></summary>
public sealed class ArmPathsTests
{
    private const string Root = "/e";

    private static readonly RootPaths Paths = new(Root);
    private static readonly ArmPaths Arm = Paths.Arm("abc");

    private static string At(string relative) => Path.Combine([Root, .. relative.Split('/')]);

    private static readonly (string Member, string Actual, string Expected)[] RootTable =
    [
        (nameof(RootPaths.ExperimentFile), Paths.ExperimentFile, At("experiment.json")),
        (nameof(RootPaths.Briefs), Paths.Briefs, At("briefs")),
        (nameof(RootPaths.Base), Paths.Base, At("base")),
        (nameof(RootPaths.ArmMapFile), Paths.ArmMapFile, At("arms.map.json")),
        (nameof(RootPaths.Arms), Paths.Arms, At("r")),
        (nameof(RootPaths.Pids), Paths.Pids, At("pids")),
        (nameof(RootPaths.PidFile), Paths.PidFile("C1"), At("pids/C1.pid")),
        (nameof(RootPaths.PidLog), Paths.PidLog("C1"), At("pids/C1.log")),
        (nameof(RootPaths.Chain), Paths.Chain, At("chain")),
        (nameof(RootPaths.Made), Paths.Made("C1"), At("chain/made-C1")),
        (nameof(RootPaths.Locked), Paths.Locked, At("chain/locked")),
        (nameof(RootPaths.Unlocked), Paths.Unlocked, At("chain/unlocked")),
        (nameof(RootPaths.ObjectiveMarker), Paths.ObjectiveMarker("C1"), At("chain/objective-C1")),
        (nameof(RootPaths.ArmsDone), Paths.ArmsDone, At("chain/arms-done")),
        (nameof(RootPaths.Executables), Paths.Executables, At("chain/executables.txt")),
        (nameof(RootPaths.GateLock), Paths.GateLock, At(".gate.lock")),
        (nameof(RootPaths.LockedModes), Paths.LockedModes, At(".locked-modes")),
        (nameof(RootPaths.LockLog), Paths.LockLog, At("lock.log")),
        (nameof(RootPaths.LockedModesRestored), Paths.LockedModesRestored("20261029T101500Z"), At(".locked-modes.restored-20261029T101500Z")),
        (nameof(RootPaths.KeepAwakeLog), Paths.KeepAwakeLog, At("keep-awake.log")),
    ];

    private static readonly (string Member, string Actual, string Expected)[] ArmTable =
    [
        (nameof(ArmPaths.Folder), Arm.Folder, At("r/abc")),
        (nameof(ArmPaths.CloneFolder), Arm.CloneFolder, At("r/abc/arm")),
        (nameof(ArmPaths.Out), Arm.Out, At("r/abc/out")),
        (nameof(ArmPaths.Notes), Arm.Notes, At("r/abc/notes")),
        (nameof(ArmPaths.Work), Arm.Work, At("r/abc/work")),
        (nameof(ArmPaths.Tmp), Arm.Tmp, At("r/abc/tmp")),
        (nameof(ArmPaths.Worktrees), Arm.Worktrees, At("r/abc/wt")),
        (nameof(ArmPaths.Worktree), Arm.Worktree("t1"), At("r/abc/wt/t1")),
        (nameof(ArmPaths.CoaiSettings), Arm.CoaiSettings("side"), At("r/abc/coai-data/side/settings.json")),
        (nameof(ArmPaths.McpNone), Arm.McpNone, At("r/abc/mcp-none.json")),
        (nameof(ArmPaths.McpCoai), Arm.McpCoai, At("r/abc/mcp-coai.json")),
        (nameof(ArmPaths.Session), Arm.Session("2-review"), At("r/abc/out/2-review.session")),
        (nameof(ArmPaths.Stream), Arm.Stream("2-review", 3), At("r/abc/out/2-review.a3.jsonl")),
        (nameof(ArmPaths.Stderr), Arm.Stderr("2-review", 3), At("r/abc/out/2-review.a3.err")),
        (nameof(ArmPaths.Prompt), Arm.Prompt("2-review", 3), At("r/abc/out/2-review.a3.prompt.md")),
        (nameof(ArmPaths.Done), Arm.Done("2-review"), At("r/abc/out/2-review.done")),
        (nameof(ArmPaths.Before), Arm.Before("2-review"), At("r/abc/out/2-review.before")),
        (nameof(ArmPaths.Diff), Arm.Diff("2-review"), At("r/abc/out/2-review.diff")),
        (nameof(ArmPaths.Timing), Arm.Timing, At("r/abc/out/timing.jsonl")),
        (nameof(ArmPaths.ObjectiveFolder), Arm.ObjectiveFolder, At("r/abc/out/objective")),
        (nameof(ArmPaths.Objective), Arm.Objective, At("r/abc/out/objective.json")),
        (nameof(ArmPaths.TasksPlanLog), Arm.TasksPlanLog, At("r/abc/out/tasks-plan.log")),
        (nameof(ArmPaths.BatchDone), Arm.BatchDone(12), At("r/abc/out/batch-12.done")),
        (nameof(ArmPaths.TaskBefore), Arm.TaskBefore("t2"), At("r/abc/out/task-t2.before")),
        (nameof(ArmPaths.CoderHead), Arm.CoderHead("t2"), At("r/abc/out/task-t2-coder.head")),
        (nameof(ArmPaths.CoderDiff), Arm.CoderDiff("t2"), At("r/abc/out/task-t2-coder.diff")),
        (nameof(ArmPaths.Picked), Arm.Picked("t2"), At("r/abc/out/picked-t2")),
        (nameof(ArmPaths.PickRecord), Arm.PickRecord("t2"), At("r/abc/out/pick-t2.json")),
        (nameof(ArmPaths.PickLog), Arm.PickLog("t2"), At("r/abc/out/pick-t2.log")),
        (nameof(ArmPaths.PrepBuildLog), Arm.PrepBuildLog, At("r/abc/out/prep-build.log")),
        (nameof(ArmPaths.ObjectiveLog), Arm.ObjectiveLog, At("r/abc/out/objective.out")),
        (nameof(ArmPaths.WorktreeBuildLog), Arm.WorktreeBuildLog("t2"), At("r/abc/out/wt-build-t2.log")),
        (nameof(ArmPaths.NotesFile), Arm.NotesFile(new OutputBinding("HA_JSON", "notes/hA.json")), At("r/abc/notes/hA.json")),
        (nameof(ArmPaths.NotesFile), Arm.NotesFile(new OutputBinding("X", "work/a/b.md")), At("r/abc/work/a/b.md")),
        (nameof(ArmPaths.TaskNotes), Arm.TaskNotes("t2"), At("r/abc/notes/t2-code.md")),
        (nameof(ArmPaths.TasksJson), Arm.TasksJson, At("r/abc/work/tasks.json")),
        (nameof(ArmPaths.TaskBrief), Arm.TaskBrief("t2"), At("r/abc/work/t2.md")),
        (nameof(ArmPaths.BatchesJson), Arm.BatchesJson, At("r/abc/work/batches.json")),
    ];

    [Fact]
    public void Every_path_of_an_arm_is_spelled_as_the_shell_spelled_it()
    {
        foreach (var (member, actual, expected) in RootTable.Concat(ArmTable))
        {
            actual.Should().Be(expected, $"{member} is one fixed spelling");
        }

        RootTable.Select(row => row.Member).Should().BeEquivalentTo(PathMembers(typeof(RootPaths)),
            "a path added to RootPaths needs its spelling in this table");
        ArmTable.Select(row => row.Member).Distinct().Should().BeEquivalentTo(PathMembers(typeof(ArmPaths)),
            "a path added to ArmPaths needs its spelling in this table, and the seven fixed notes members are retired: a step's notes files are its bindings now");
    }

    [Fact]
    public void The_coder_diff_is_the_one_renamed_file()
    {
        Arm.CoderHead("t1").Should().EndWith("task-t1-coder.head");
        Arm.CoderDiff("t1").Should().EndWith("task-t1-coder.diff");

        var spelled = new[] { typeof(RootPaths), typeof(ArmPaths) }
            .SelectMany(type => PathMembers(type).Select(name => (type, name)))
            .Select(member => Spell(member.type, member.name))
            .ToList();

        spelled.Should().HaveCountGreaterThan(50, "the scan must still reach every member, or it proves nothing");
        spelled.Should().NotContain(path => path.Contains("haiku", StringComparison.OrdinalIgnoreCase),
            "the shell named a coder's files after its model; no path of the module may name a model");
    }

    [Fact]
    public void A_configured_file_name_that_escapes_the_root_is_refused()
    {
        Paths.Of("../x.json").Failed().Should().BeTrue();
        Paths.Of("/etc/x.json").Failed().Should().BeTrue();
        Paths.Of("notes/../../x.json").Failed().Should().BeTrue("a .. segment anywhere leaves the root");
        Paths.Of("..\\x.json").Failed().Should().BeTrue("a backslash separator is a separator too");
        Paths.Of("C:\\x.json").Failed().Should().BeTrue();
        Paths.Of("").Failed().Should().BeTrue();

        Paths.Of("../x.json").Reason().Should().Be("'../x.json' must be a file name inside the experiment root");
        Paths.Of("scenarios.json").Ok().Should().Be(At("scenarios.json"));
    }

    [Fact]
    public void A_binding_with_a_climbing_segment_is_a_programming_error()
    {
        var thrown = new Action[]
        {
            () => Arm.NotesFile(new OutputBinding("X", "notes/../x.md")),
            () => Arm.NotesFile(new OutputBinding("X", "notes//x.md")),
        };

        foreach (var action in thrown)
        {
            action.Should().Throw<ArgumentException>().Where(ex => ex.Message.Contains("output segment", StringComparison.Ordinal), "the reader refused such a path, so a throw here is a caller's bug");
        }
    }

    [Fact]
    public void A_path_segment_with_a_separator_is_a_programming_error()
    {
        var thrown = new Action[]
        {
            () => Arm.Stream("a/b", 1),
            () => Arm.Worktree(".."),
            () => Arm.Done(""),
            () => Arm.TaskNotes("a\\b"),
            () => Arm.CoaiSettings("."),
            () => Paths.PidFile("../C1"),
            () => Paths.LockedModesRestored("a/b"),
            () => Paths.Arm("a/b"),
            () => _ = new ArmPaths(Root, "..").Folder,
        };

        foreach (var action in thrown)
        {
            action.Should().Throw<ArgumentException>();
        }

        Arm.Stream("a.b-c", 1).Should().EndWith("a.b-c.a1.jsonl", "a dot inside a name is not a segment");
    }

    private static IEnumerable<string> PathMembers(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.Name is not (nameof(RootPaths.Root) or nameof(ArmPaths.Token)))
            .Select(p => p.Name)
            .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.ReturnType == typeof(string) && !m.IsSpecialName && m.Name != nameof(ToString))
                .Select(m => m.Name))
            .Distinct();

    /// <summary>The member's value, with a sample name for every string argument and a sample number for every int.</summary>
    private static string Spell(Type type, string member)
    {
        var target = type == typeof(RootPaths) ? (object)Paths : Arm;
        if (type.GetProperty(member) is { } property)
        {
            return (string)property.GetValue(target)!;
        }

        var method = type.GetMethod(member)!;
        var arguments = method.GetParameters().Select(p => p.ParameterType == typeof(int) ? (object)3 : p.ParameterType == typeof(OutputBinding) ? new OutputBinding("X", "notes/t1.md") : "t1").ToArray();
        return (string)method.Invoke(target, arguments)!;
    }
}
