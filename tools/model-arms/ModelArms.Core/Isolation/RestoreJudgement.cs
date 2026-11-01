using System.Globalization;

namespace ModelArms.Core.Isolation;

/// <summary>What a re-pinned entry is NOW, as the adapter read it from the descriptor: a link, a folder or a regular file, the 12 permission bits and, on the kernel road, the inode. <c>null</c> for the whole value means the name is gone.</summary>
public sealed record PinnedFacts(bool IsLink, bool IsFolder, bool IsFile, int Mode, NodeIdentity? Identity);

/// <summary>The verdict on one entry at <c>unlock</c> (and at the recovery of an interrupted lock).</summary>
public abstract record EntryVerdict
{
    private EntryVerdict()
    {
    }

    /// <summary>Set the saved mode.</summary>
    public sealed record Restore : EntryVerdict;

    /// <summary>Already at the saved mode: counted, no call (a harmless retry).</summary>
    public sealed record AlreadyRestored : EntryVerdict;

    /// <summary>The operator's own mode: left as it is, reported.</summary>
    public sealed record Unresolved(string Sentence) : EntryVerdict;

    /// <summary>Gone, a link now, another kind or replaced: left, said.</summary>
    public sealed record Skip(string Sentence) : EntryVerdict;

    /// <summary>Outside the bound of what this experiment names: not restored, with the hand command.</summary>
    public sealed record Fail(string Sentence) : EntryVerdict;
}

/// <summary>Finding 3 and 11 as a decision: whether to put an entry's saved mode back, judged from what the entry is now against what the lock recorded. In this order — outside the bound (a forged <c>.locked-modes</c> must not steer this user's <c>chmod</c>); gone; a link or another kind;
/// another inode; the mode (already the saved one — a retry; the LOCKED one — restore; anything else is the operator's own change and survives). The runtime road records no inode, so there a replaced entry is not told apart and the rest still holds.</summary>
public static class RestoreJudgement
{
    public static EntryVerdict Of(SavedMode saved, PinnedFacts? now, bool inBound) =>
        !inBound ? Outside(saved, now)
        : now is null ? new EntryVerdict.Skip($"{saved.Path}: not found by this name — if it was renamed, it may still stand at {Locked(saved)} elsewhere")
        : Inside(saved, now);

    /// <summary>An entry outside the bound is never given a mode: <see cref="EntryVerdict.Fail"/>. Except that one already standing at its saved mode (the operator restored it by hand, as the failure told them to) needs no call and is counted — a harmless retry.</summary>
    private static EntryVerdict Outside(SavedMode saved, PinnedFacts? now) =>
        now is { IsLink: false } && Replaced(saved, now) is false && SnapshotFiles.ModeText(now.Mode) == saved.Mode
            ? new EntryVerdict.AlreadyRestored()
            : new EntryVerdict.Fail($"{saved.Path}: not under any folder this experiment names — not restored (by hand: chmod {saved.Mode} {saved.Path})");

    private static EntryVerdict Inside(SavedMode saved, PinnedFacts now) =>
        now.IsLink ? new EntryVerdict.Skip($"{saved.Path}: a link now — not restored")
        : WrongKind(saved, now) ? new EntryVerdict.Skip($"{saved.Path}: not the {(saved.Kind == IsolationKind.Unlisted ? "folder" : "kind of file")} it was when locked — not restored")
        : Replaced(saved, now) ? new EntryVerdict.Skip($"{saved.Path}: replaced since the lock ({Difference(saved.Identity!, now.Identity!)}) — its mode is left as it is")
        : ByMode(saved, now);

    private static string Locked(SavedMode saved) => SnapshotFiles.ModeText(IsolationPlans.LockedMode(saved.Kind));

    private static bool WrongKind(SavedMode saved, PinnedFacts now) => !now.IsFolder && (saved.Kind == IsolationKind.Unlisted || !now.IsFile);

    private static bool Replaced(SavedMode saved, PinnedFacts now) =>
        saved.Identity is { } was && now.Identity is { } current && was.IsReplacedBy(current);

    private static string Difference(NodeIdentity was, NodeIdentity current) =>
        was.Inode != current.Inode || was.Device != current.Device
            ? $"inode {was.Inode} → {current.Inode}"
            : $"inode {was.Inode} made again: created {Born(was)} → {Born(current)}";

    private static string Born(NodeIdentity node) => node.Birth?.ToString("0.#########", CultureInfo.InvariantCulture) ?? "unknown";

    private static EntryVerdict ByMode(SavedMode saved, PinnedFacts now)
    {
        var current = SnapshotFiles.ModeText(now.Mode);
        return current == saved.Mode ? new EntryVerdict.AlreadyRestored()
            : current == Locked(saved) ? new EntryVerdict.Restore()
            : new EntryVerdict.Unresolved($"{saved.Path}: mode {current} is neither the saved {saved.Mode} nor the locked {Locked(saved)} — changed by hand since the lock; left as it is");
    }
}
