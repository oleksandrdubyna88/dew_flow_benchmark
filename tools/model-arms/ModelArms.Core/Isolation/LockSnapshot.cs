using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Isolation;

/// <summary>The state of the isolation snapshot (D11), read from its two immutable files.</summary>
public abstract record LockState
{
    private LockState()
    {
    }

    public sealed record None : LockState;

    public sealed record Applying(string Run, string Since) : LockState;

    public sealed record Applied(string Run, string Since) : LockState;

    public sealed record Inconsistent(string Reason) : LockState;
}

/// <summary>The snapshot's state from its two immutable files (D11, E3 §8 item 2), as values — nothing here reads a file and nothing restores one. <c>.locked-modes</c> is <c>{"run","digest","since","modes":[…]}</c>, <c>.locked-modes.applied</c> is <c>{"run","digest","verifiedAt"}</c>:
/// <list type="bullet">
/// <item>neither — <see cref="LockState.None"/>;</item>
/// <item>the snapshot alone — <see cref="LockState.Applying"/> since the snapshot's <c>since</c>: a <c>lock</c> killed half-way;</item>
/// <item>both, of one run and one digest — <see cref="LockState.Applied"/> since the applied file's <c>verifiedAt</c>, the moment the isolation was observed to hold;</item>
/// <item>anything else — <see cref="LockState.Inconsistent"/> with the reason: a file that was refused or does not parse, an applied file with no snapshot, two files of different runs or digests. A state nobody can read is never read as none.</item>
/// </list></summary>
public static class LockSnapshots
{
    private sealed record Snapshot(string Run, string Digest, string Since);

    private sealed record Verified(string Run, string Digest, string VerifiedAt);

    public static LockState State(Read<string> snapshot, Read<string> applied) =>
        (snapshot, applied) switch
        {
            (Read<string>.Absent, Read<string>.Absent) => new LockState.None(),
            (Read<string>.Refused refused, _) => new LockState.Inconsistent(refused.Reason),
            (_, Read<string>.Refused refused) => new LockState.Inconsistent(refused.Reason),
            (Read<string>.Absent, _) => new LockState.Inconsistent(".locked-modes.applied is there without .locked-modes"),
            (Read<string>.Present first, Read<string>.Absent) => Parsed(first.Value, ".locked-modes").Match<LockState>(one => new LockState.Applying(one.Run, one.Since), Inconsistent),
            (Read<string>.Present first, Read<string>.Present second) => Both(first.Value, second.Value),
            _ => new LockState.Inconsistent("the snapshot files were read in a state this build does not know"),
        };

    private static LockState Both(string snapshot, string applied) =>
        Parsed(snapshot, ".locked-modes").Match<LockState>(
            one => Verification(applied).Match<LockState>(two => Joined(one, two), Inconsistent),
            Inconsistent);

    private static LockState Joined(Snapshot snapshot, Verified applied) =>
        snapshot.Run != applied.Run ? new LockState.Inconsistent($".locked-modes is for run {snapshot.Run} and .locked-modes.applied for run {applied.Run}")
        : snapshot.Digest != applied.Digest ? new LockState.Inconsistent($".locked-modes holds the closed set {snapshot.Digest} and .locked-modes.applied {applied.Digest}")
        : new LockState.Applied(snapshot.Run, applied.VerifiedAt);

    private static LockState Inconsistent(string reason) => new LockState.Inconsistent(reason);

    private static Outcome<Snapshot> Parsed(string json, string file) =>
        JsonRow.Parse(json, file).Match(
            row => Checked(row, file, new Snapshot(row.Text("run", required: true), row.Text("digest", required: true), row.Text("since", required: true))),
            Outcome<Snapshot>.Failure);

    private static Outcome<Verified> Verification(string json) =>
        JsonRow.Parse(json, ".locked-modes.applied").Match(
            row => Checked(row, ".locked-modes.applied", new Verified(row.Text("run", required: true), row.Text("digest", required: true), row.Text("verifiedAt", required: true))),
            Outcome<Verified>.Failure);

    /// <summary>The row's values, or the first thing wrong with a field — the row's problem reads for the field asked for last, so it is read AFTER every field.</summary>
    private static Outcome<T> Checked<T>(JsonRow row, string file, T value) =>
        row.Problem.Length == 0 ? Outcome<T>.Success(value) : Outcome<T>.Failure($"{file}: {row.Problem}");
}

/// <summary>The one line every verb but <c>lock</c> and <c>unlock</c> prints about the isolation (D11): <c>isolation applied for &lt;run&gt; since &lt;utc&gt;</c> · <c>a lock was interrupted — run `lock &lt;run&gt;` or `unlock`</c> · <c>no isolation snapshot</c> · <c>isolation snapshot
/// inconsistent — &lt;reason&gt;</c>. A name or a reason that came from a file is printed escaped.</summary>
public static class IsolationLine
{
    public static string Of(LockState state) =>
        state switch
        {
            LockState.Applied applied => $"isolation applied for {ExecutableRule.Printable(applied.Run)} since {ExecutableRule.Printable(applied.Since)}",
            LockState.Applying applying => $"a lock was interrupted — run `lock {ExecutableRule.Printable(applying.Run)}` or `unlock`",
            LockState.Inconsistent inconsistent => "isolation snapshot inconsistent — " + ExecutableRule.Printable(inconsistent.Reason),
            _ => "no isolation snapshot",
        };
}
