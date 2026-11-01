using Bench.Domain;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Isolation;

/// <summary>What <c>lock</c> does over the state it found (D11's table as values).</summary>
public abstract record LockStep
{
    private LockStep()
    {
    }

    /// <summary>Nothing stands: pin, snapshot, apply, verify, record.</summary>
    public sealed record Fresh : LockStep;

    /// <summary>A lock was interrupted (any run's): restore <see cref="Old"/>'s modes pinned ANEW in this process; only when every entry was restored, move it aside and go on as <see cref="Fresh"/>; else keep it and stop (exit 3).</summary>
    public sealed record Recover(SavedSnapshot Old) : LockStep;

    /// <summary>The same run and set is applied since <see cref="Since"/>: verify again, re-apply what was opened, claim nothing unobserved.</summary>
    public sealed record AlreadyLocked(string Since) : LockStep;

    /// <summary>Another run's — or the same run's with another closed set — isolation is applied: exit 4, <c>unlock</c> first.</summary>
    public sealed record RefuseOtherSet(string LockedRun) : LockStep;

    /// <summary>The files disagree or do not parse: exit 4, no mode touched.</summary>
    public sealed record RefuseInconsistent(string Reason) : LockStep;
}

/// <summary><c>lock</c>'s decision (D11): pure over the state, the plan and the snapshot's text. An <c>inconsistent</c> state is never read as none by a writer, and an <c>applying</c> one whose modes do not parse cannot be recovered, so it is refused too.</summary>
public static class LockDecision
{
    public static LockStep Of(LockState state, IsolationPlan plan, Read<string> snapshotText) =>
        state switch
        {
            LockState.None => new LockStep.Fresh(),
            LockState.Applying => Recovery(snapshotText),
            LockState.Applied applied when string.Equals(applied.Run, plan.Run, StringComparison.Ordinal) && string.Equals(applied.Digest, plan.Digest, StringComparison.Ordinal) => new LockStep.AlreadyLocked(applied.Since),
            LockState.Applied applied => new LockStep.RefuseOtherSet(applied.Run),
            LockState.Inconsistent inconsistent => new LockStep.RefuseInconsistent(inconsistent.Reason),
            _ => new LockStep.RefuseInconsistent("the isolation state is one this build does not know"),
        };

    private static LockStep Recovery(Read<string> snapshotText) =>
        snapshotText is Read<string>.Present present
            ? LockSnapshots.Saved(present.Value).Match<LockStep>(saved => new LockStep.Recover(saved), reason => new LockStep.RefuseInconsistent(reason))
            : new LockStep.RefuseInconsistent(".locked-modes could not be read for the recovery");
}

/// <summary>What <c>unlock</c> does over the state it found.</summary>
public abstract record UnlockStep
{
    private UnlockStep()
    {
    }

    /// <summary>No snapshot, no applied file: exit 0, <i>nothing locked</i>.</summary>
    public sealed record Nothing : UnlockStep;

    /// <summary>Restore <see cref="Saved"/> in reverse, then move both files aside (<see cref="AppliedPresent"/>: the applied file, a refused or crashed-between one included, is renamed too).</summary>
    public sealed record Restore(SavedSnapshot Saved, bool AppliedPresent) : UnlockStep;

    /// <summary>An applied file with no snapshot (a crash between <c>unlock</c>'s two renames): renamed aside, no mode call.</summary>
    public sealed record RenameAppliedAlone : UnlockStep;

    /// <summary>A <c>.locked-modes</c> that is there and cannot be used (a link, too big, not UTF-8, not parsing): exit 4 naming it — the operator restores by hand. <c>--force</c> does not change it.</summary>
    public sealed record Refuse(string Reason) : UnlockStep;
}

/// <summary><c>unlock</c>'s decision (D11): the snapshot is the one authority for modes. It restores from a parsing <c>.locked-modes</c> whatever the pair says (two runs, two digests included), renames an applied file that stands alone, and refuses a snapshot it cannot read.</summary>
public static class UnlockDecision
{
    public static UnlockStep Of(LockState state, Read<string> snapshotText, Read<string> appliedText) =>
        (snapshotText, state) switch
        {
            (_, LockState.None) => new UnlockStep.Nothing(),
            (Read<string>.Present present, _) => LockSnapshots.Saved(present.Value).Match<UnlockStep>(saved => new UnlockStep.Restore(saved, appliedText is not Read<string>.Absent), reason => new UnlockStep.Refuse(reason)),
            (Read<string>.Refused refused, _) => new UnlockStep.Refuse(refused.Reason),
            _ => appliedText is Read<string>.Absent ? new UnlockStep.Nothing() : new UnlockStep.RenameAppliedAlone(),
        };
}
