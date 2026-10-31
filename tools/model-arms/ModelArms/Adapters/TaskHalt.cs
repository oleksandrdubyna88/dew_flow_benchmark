using Bench.Cli;
using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Tasks;

namespace ModelArms.Adapters;

/// <summary>Why the task pipeline stops: the exit code (D16), the words, and the task it names (<c>null</c>: the batch).</summary>
internal sealed record Halt(int Code, string Why, string? Id);

/// <summary>A step of the pipeline's reading: the value, or the stop. Two cases because a stop carries an exit code and the task it names, which <c>Outcome&lt;T&gt;</c>'s one reason cannot say (the <c>Passage&lt;T&gt;</c> of the gate, with the task).</summary>
internal abstract record Taken<T>
{
    private Taken()
    {
    }

    public sealed record Got(T Value) : Taken<T>;

    public sealed record Halted(Halt Halt) : Taken<T>;

    /// <summary>True with the value; false with the stop.</summary>
    public bool TryGet(out T value, out Halt? halt)
    {
        value = this is Got got ? got.Value : default!;
        halt = this is Halted halted ? halted.Halt : null;
        return this is Got;
    }
}

/// <summary>What every unit of the task pipeline shares: the arm's paths, the target, the tools, the log, the probe, the clock and the files — and the two ways it asks git (the clone, a folder).</summary>
internal sealed class TaskContext(ArmPaths paths, TargetFacts target, TaskTools tools, StepLog log, TaskProbe probe, TimeProvider clock)
{
    public const string TmpPrefix = "tmp/";

    public ArmPaths Paths => paths;

    public TargetFacts Target => target;

    public TaskTools Tools => tools;

    public StepLog Log => log;

    public TaskProbe Probe => probe;

    public TimeProvider Clock => clock;

    public ArmFiles Files { get; } = new(paths);

    public ArmGit Clone => new(tools.Git, paths.CloneFolder, tools.Environment);

    public string GitFolder => Path.Combine(paths.CloneFolder, ".git");

    public string LockFile => Path.Combine(GitFolder, "index.lock");

    public ArmGit In(string folder) => new(tools.Git, folder, tools.Environment);

    /// <summary>Whether a directory entry is there, asked by NAME of the kernel; a kernel that cannot say is the host's failure (exit 3).</summary>
    public static Taken<bool> Look(string path) =>
        KernelFacts.Present(path) switch
        {
            Outcome<bool>.Ok ok => new Taken<bool>.Got(ok.Value),
            Outcome<bool>.Fail failed => new Taken<bool>.Halted(new Halt(ExitCodes.Environment, failed.Reason, null)),
            _ => throw new InvalidOperationException("a closed set"),
        };

    /// <summary>The stale <c>index.lock</c> (§8 item 4): git's own text says only a person may remove it once no git runs; reading it as a conflict names the wrong cause. Checked by name before the sequencer abort and before every cherry-pick — M-r: the only two writers of the clone's index. Never removed.</summary>
    public Halt? LockHalt(string? id)
    {
        if (!Look(LockFile).TryGet(out var there, out var halt))
        {
            return halt;
        }

        return there
            ? new Halt(ExitCodes.Environment, $"{(id is null ? string.Empty : id + ": ")}{LockFile} exists — a git was killed while writing, or one still runs in {paths.CloneFolder}; remove it once none does, then run me again", id)
            : null;
    }

    /// <summary>A git that could not answer: an enumeration of the clone's config that cannot run is the BATCH's stop and no question is asked with fewer pairs (§8 item 25); anything else is the task's (or the batch's) exit 3.</summary>
    public static Halt Failed(string? id, string reason) =>
        reason.StartsWith(ArmGit.EnumerationFailure, StringComparison.Ordinal) && id is null
            ? new Halt(ExitCodes.Environment, $"the clone's config cannot be enumerated: {reason}", null)
            : new Halt(ExitCodes.Environment, id is null ? reason : $"{id}: {reason}", id);

    /// <summary>A failure while the batch is being READ: an enumeration that cannot run names no task (it is the clone's), any other git failure names the one being read.</summary>
    public static Halt FailedRead(string id, string reason) => Failed(reason.StartsWith(ArmGit.EnumerationFailure, StringComparison.Ordinal) ? null : id, reason);
}
