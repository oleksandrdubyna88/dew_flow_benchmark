using System.Globalization;
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

    public sealed record Applied(string Run, string Since, string Digest) : LockState;

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
        : new LockState.Applied(snapshot.Run, applied.VerifiedAt, snapshot.Digest);

    private static LockState Inconsistent(string reason) => new LockState.Inconsistent(reason);

    /// <summary>The snapshot's whole content for a WRITER (<c>lock</c> recovering, <c>unlock</c> restoring): the header <see cref="State"/> reads, and the modes with their kind, mode and — on the kernel road — device and inode. A mode that is not octal, a kind that is neither
    /// <c>closed</c> nor <c>unlisted</c>, a <c>dev</c> without its <c>ino</c> (or the reverse) is a failure naming the entry. <see cref="State"/> itself reads only the header, so the E3 reader's fixtures — which carry <c>modes</c> without a kind — stay <i>applying</i>; the one header parse is shared.</summary>
    public static Outcome<SavedSnapshot> Saved(string json) =>
        JsonRow.Parse(json, ".locked-modes").Match(
            row => Checked(row, ".locked-modes", Header(row)).Match(header => ModesOf(row, header, requireKind: true), Outcome<SavedSnapshot>.Failure),
            Outcome<SavedSnapshot>.Failure);

    private static Outcome<SavedSnapshot> ModesOf(JsonRow row, Snapshot header, bool requireKind)
    {
        var rows = row.Rows("modes");
        if (row.Problem.Length > 0)
        {
            return Outcome<SavedSnapshot>.Failure($".locked-modes: {row.Problem}");
        }

        var modes = new List<SavedMode>();
        foreach (var (each, index) in rows.Select((each, index) => (each, index)))
        {
            var read = One(each, index, requireKind);
            if (read is Outcome<SavedMode>.Fail failed)
            {
                return Outcome<SavedSnapshot>.Failure(failed.Reason);
            }

            modes.Add(((Outcome<SavedMode>.Ok)read).Value);
        }

        return Outcome<SavedSnapshot>.Success(new SavedSnapshot(header.Run, header.Digest, header.Since, modes));
    }

    private static Outcome<SavedMode> One(JsonRow each, int index, bool requireKind)
    {
        var where = $".locked-modes: modes[{index}]";
        var path = each.Text("path", required: true);
        var kind = each.Text("kind", required: requireKind);
        var mode = each.Text("mode", required: true);
        var source = each.Text("source");
        var device = each.ULong("dev");
        var inode = each.ULong("ino");
        var birthText = each.Text("btime");
        var birth = decimal.TryParse(birthText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
        return each.Problem.Length > 0 ? Outcome<SavedMode>.Failure($"{where}: {each.Problem}")
            : path.Contains('\0', StringComparison.Ordinal) || !Path.IsPathFullyQualified(path) ? Outcome<SavedMode>.Failure($"{where}: 'path' must be a fully qualified path with no NUL character")
            : (requireKind || kind.Length > 0) && kind is not ("closed" or "unlisted") ? Outcome<SavedMode>.Failure($"{where}: 'kind' must be closed or unlisted, got '{kind}'")
            : SnapshotFiles.ModeBits(mode) is Outcome<int>.Fail bad ? Outcome<SavedMode>.Failure($"{where}: {bad.Reason}")
            : device.HasValue != inode.HasValue ? Outcome<SavedMode>.Failure($"{where}: 'dev' and 'ino' come together or not at all")
            : birthText.Length > 0 && (!birth.HasValue || !device.HasValue) ? Outcome<SavedMode>.Failure($"{where}: 'btime' must be seconds since the epoch and come with 'dev' and 'ino', got '{birthText}'")
            : Outcome<SavedMode>.Success(new SavedMode(path, kind == "closed" ? IsolationKind.Closed : IsolationKind.Unlisted, mode, device.HasValue ? new NodeIdentity(device.Value, inode!.Value, birth) : null, source));
    }

    private static Snapshot Header(JsonRow row) => new(row.Text("run", required: true), row.Text("digest", required: true), row.Text("since", required: true));

    /// <summary>What <see cref="State"/> reads: the header and the modes, each mode's <c>path</c> (fully qualified, no NUL) and <c>mode</c> (octal) — a good header over corrupt modes is <i>inconsistent</i>, never <i>applied</i>, to <c>status</c> and to the runner's warning. A mode with no <c>kind</c> (the E3 reader's fixtures) is tolerated here and refused by <see cref="Saved"/>, which a writer needs.</summary>
    private static Outcome<Snapshot> Parsed(string json, string file) =>
        JsonRow.Parse(json, file).Match(
            row => Checked(row, file, Header(row)).Match(header => ModesOf(row, header, requireKind: false).Match(_ => Outcome<Snapshot>.Success(header), Outcome<Snapshot>.Failure), Outcome<Snapshot>.Failure),
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
