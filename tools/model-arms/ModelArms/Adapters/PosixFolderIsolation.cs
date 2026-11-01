using System.Runtime.InteropServices;
using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>D11's file-system side on the KERNEL road (Linux x86-64 with <c>statx</c>, <see cref="PlatformRoad"/> <c>Kernel</c>/<c>Kernel</c>): the modes of the plan's folders and files, set through descriptors that PIN them. One instance serves one verb run and holds the descriptors <see cref="Pin"/> opened until it is disposed.
/// <para>
/// <b>The atomic operations it rests on</b> (common.reliability, D26 a): <c>open(O_PATH | O_NOFOLLOW | O_CLOEXEC)</c> pins the LEAF (a link is opened as a link and refused; S1-2), <c>statx</c> on the descriptor (<c>KernelFacts.ReadOpen</c>) reads the kind, the 12 permission bits, the device and the inode of that very object, <see cref="RealPath"/> must equal the path (the ancestors), and every mode is set THROUGH the descriptor by
/// <c>chmod("/proc/self/fd/&lt;fd&gt;")</c> (S1-1: <c>fchmod</c> is <c>EBADF</c> on <c>O_PATH</c>; the road systemd's <c>fchmod_opath</c> takes) — so no path is resolved a second time between the check and the change, and a link planted at the leaf afterwards changes nothing it points at. The snapshot is staged, flushed and renamed by <c>ArtifactCommit</c> (create-new) BEFORE
/// the first mode changes, by the verb.
/// </para>
/// <para>
/// <b>The verification is the observation</b> (<c>common.security</c>): after the apply, EVERY entry is tried by its path as an arm would — a folder is opened for listing (<c>EACCES</c> expected; no name is ever enumerated), a closed folder is probed for traversal, a closed file is opened for reading — and a descriptor that comes back is a sentence. Root, <c>CAP_DAC_OVERRIDE</c> and a file system that ignores modes (a Windows drive under WSL, M-8) all
/// show as such a sentence.
/// </para>
/// <para>
/// <b>Residuals, said</b> (parent §9, the same-user class — an access constraint, not a boundary against a process with this user's rights): an ancestor swapped between <see cref="RealPath.Of"/> and the open is followed by the open (only the operator can write the root or <c>$HOME</c>, D17); <c>/proc</c> absent (a container) makes every chmod <c>ENOENT</c> → exit 3 naming <c>/proc/self/fd</c>; a power loss between the
/// snapshot's rename and the journal commit is the file system's ordering (no directory <c>fsync</c>); a root at 300 still lets this user create, rename and unlink by NAME under it — <c>experiment.json</c> and <c>.locked-modes</c> included — so a forged or replaced snapshot can steer <c>unlock</c> only within <see cref="RestoreBound"/> and only onto entries at their locked or saved mode; an unlinked <c>pids/&lt;arm&gt;.lock</c>; an inode reused
/// by a new object after a deletion reads as the same entry at <c>unlock</c>; a <c>chmod</c> by any same-user process beside the module. <b>What it does not do:</b> Windows (<see cref="NoIsolation"/>); the other POSIX roads (<see cref="RuntimeFolderIsolation"/>).
/// </para></summary>
internal sealed class PosixFolderIsolation(IsolationHands? hands, List<string>? events) : IFolderIsolation
{
    // x86-64 Linux, probed (S1-5; KernelFacts.cs:19 says aarch64 differs — this class is built only on Kernel/Kernel, Linux x86-64).
    private const int OpenPath = 0x200000;
    private const int OpenDirectory = 0x10000;
    private const int OpenNoFollow = 0x20000;
    private const int OpenNonBlock = 0x800;
    private const int OpenCloseOnExec = 0x80000;
    private const int AtFdCwd = -100;
    private const int AtEffectiveAccess = 0x200;
    private const int SearchPermission = 1;
    private const int PermissionBits = 0xFFF;

    private sealed record Held(int Fd, SavedMode Saved);

    private readonly List<Held> _held = [];

    public string Describe => "POSIX file modes (Linux x86-64, kernel road)";

    public bool Applies => true;

    public string NotVerifiedLine => string.Empty;

    public bool IsRoot => hands?.IsRoot is { } hand ? hand() : KernelFacts.EffectiveUid() == 0;

    public Outcome<Pinned> Pin(IsolationPlan plan)
    {
        Release();
        events?.Add("pin");
        foreach (var entry in plan.Entries)
        {
            var pinned = PinOne(entry);
            if (pinned is Outcome<Held>.Fail failed)
            {
                Release();
                return Outcome<Pinned>.Failure(failed.Reason);
            }

            _held.Add(((Outcome<Held>.Ok)pinned).Value);
        }

        return Outcome<Pinned>.Success(new Pinned([.. _held.Select(held => held.Saved)]));
    }

    public Outcome<string> Apply(Pinned pinned, IsolationPlan plan)
    {
        hands?.BeforeApply?.Invoke();
        for (var index = 0; index < plan.Entries.Count; index++)
        {
            var entry = plan.Entries[index];
            var errno = SetMode(_held[index].Fd, entry.Path, IsolationPlans.LockedMode(entry.Kind));
            if (errno != 0)
            {
                return Outcome<string>.Failure(Refused(entry.Path, errno));
            }

            events?.Add("apply " + entry.Path);
            hands?.AfterEachMode?.Invoke(index, entry.Path);
        }

        return Outcome<string>.Success($"{plan.Entries.Count} modes set");
    }

    public IReadOnlyList<string> Verify(IsolationPlan plan)
    {
        events?.Add("verify");
        return [.. plan.Entries.Select(Observed).Where(sentence => sentence.Length > 0)];
    }

    public RestoreReport RollBack(Pinned pinned)
    {
        var restored = new List<string>();
        var failed = new List<string>();
        foreach (var held in Enumerable.Reverse(_held))
        {
            events?.Add("rollback " + held.Saved.Path);
            var errno = SetMode(held.Fd, held.Saved.Path, IsolationSentences.SavedBits(held.Saved));
            (errno == 0 ? restored : failed).Add(errno == 0 ? held.Saved.Path : Refused(held.Saved.Path, errno));
        }

        return new RestoreReport(restored, [], [], failed);
    }

    public RestoreReport Restore(IReadOnlyList<SavedMode> reverse, RestoreBound bound)
    {
        var tally = RestoreTally.Empty;
        foreach (var saved in reverse)
        {
            var judged = Judged(saved, bound);
            tally = tally.Add(judged.Verdict, saved.Path, () => Restored(saved, judged.Fd));
            CloseFd(judged.Fd);
        }

        return tally.Report();
    }

    public Outcome<string> ReApply(IReadOnlyList<SavedMode> open, IsolationPlan plan)
    {
        foreach (var saved in open)
        {
            if (ReApplyOne(saved) is Outcome<string>.Fail failed)
            {
                return failed;
            }
        }

        return Outcome<string>.Success($"{open.Count} entries re-applied");
    }

    private string Restored(SavedMode saved, int fd)
    {
        events?.Add("restore " + saved.Path);
        var errno = SetMode(fd, saved.Path, IsolationSentences.SavedBits(saved));
        return errno == 0 ? string.Empty : Refused(saved.Path, errno);
    }

    public void Dispose() => Release();

    /// <summary>What one entry is now: the descriptor to set a mode through, and the verdict — decided before any call is made.</summary>
    private sealed record Judging(EntryVerdict Verdict, int Fd);

    private Judging Judged(SavedMode saved, RestoreBound bound)
    {
        var inBound = bound.Holds(saved.Path);
        var fd = OpenCall(saved.Path, OpenPath | OpenNoFollow | OpenCloseOnExec);
        if (fd < 0)
        {
            return Marshal.GetLastPInvokeError() is IsolationSentences.Enoent or IsolationSentences.Enotdir
                ? new Judging(RestoreJudgement.Of(saved, null, inBound), -1)
                : new Judging(new EntryVerdict.Fail($"{saved.Path}: {Marshal.GetLastPInvokeErrorMessage()}"), -1);
        }

        return new Judging(Described(fd, saved, inBound), fd);
    }

    private static EntryVerdict Described(int fd, SavedMode saved, bool inBound) =>
        KernelFacts.ReadOpen(fd, saved.Path) switch
        {
            FactsRead.Read read => Judged(read.Facts, saved, inBound),
            FactsRead.Unread unread => new EntryVerdict.Fail(unread.Reason),
            FactsRead.Refused refused => new EntryVerdict.Fail(refused.Reason),
            _ => new EntryVerdict.Fail($"{saved.Path}: unreadable"),
        };

    /// <summary>The bound is judged FIRST, whatever the entry is: a path outside it that is reached through a link is refused like any other, never skipped. Inside it, a path reached through a link is skipped; and an entry that would be restored but whose snapshot recorded no device and inode (it was made on the runtime road) is refused —
    /// without them a replaced entry could not be told apart here.</summary>
    private static EntryVerdict Judged(FileFacts facts, SavedMode saved, bool inBound)
    {
        var viaLink = facts.Identity.Kind != FileKind.Link && ThroughALink(saved.Path);
        var verdict = viaLink ? RestoreJudgement.Of(saved, null, inBound) is EntryVerdict.Fail outside ? outside : new EntryVerdict.Skip($"{saved.Path}: reached through a link now — not restored")
            : RestoreJudgement.Of(saved, FactsOf(facts), inBound);
        return verdict is EntryVerdict.Restore && saved.Identity is null
            ? new EntryVerdict.Fail($"{saved.Path}: the snapshot records no device and inode (it was made on a road without them), so a replaced entry cannot be told apart here — not restored (by hand: chmod {saved.Mode} {saved.Path})")
            : verdict;
    }

    private static bool ThroughALink(string path) => RealPath.Of(path) is not Outcome<string>.Ok ok || !string.Equals(ok.Value, path, StringComparison.Ordinal);

    private static PinnedFacts FactsOf(FileFacts facts) =>
        new(facts.Identity.Kind == FileKind.Link, facts.Identity.Kind == FileKind.Directory, facts.Identity.Kind == FileKind.Regular, (int)facts.Mode & PermissionBits, new NodeIdentity(facts.Identity.Device, facts.Identity.Inode, facts.Identity.Birth));

    private Outcome<Held> PinOne(IsolationEntry entry)
    {
        var named = string.IsNullOrEmpty(entry.Source) ? string.Empty : $" — named by {entry.Source}";
        var fd = OpenCall(entry.Path, OpenPath | OpenNoFollow | OpenCloseOnExec);
        if (fd < 0)
        {
            return Outcome<Held>.Failure(Marshal.GetLastPInvokeError() == IsolationSentences.Enoent ? $"{entry.Path} does not exist{named}" : $"{entry.Path} cannot be opened: {Marshal.GetLastPInvokeErrorMessage()}{named}");
        }

        var judged = KernelFacts.ReadOpen(fd, entry.Path) switch
        {
            FactsRead.Read read => Kinded(fd, entry, read.Facts, named),
            FactsRead.Unread unread => Outcome<Held>.Failure(unread.Reason),
            FactsRead.Refused refused => Outcome<Held>.Failure(refused.Reason),
            _ => Outcome<Held>.Failure($"{entry.Path} could not be read"),
        };
        var ancestors = judged is Outcome<Held>.Ok && RealPath.Of(entry.Path) is Outcome<string>.Ok real && !string.Equals(real.Value, entry.Path, StringComparison.Ordinal)
            ? Outcome<Held>.Failure($"{entry.Path} is reached through a link (its real path is {((Outcome<string>.Ok)RealPath.Of(entry.Path)).Value}) — name the real path{named}")
            : judged;
        if (ancestors is Outcome<Held>.Fail)
        {
            CloseFd(fd);
        }

        return ancestors;
    }

    private static Outcome<Held> Kinded(int fd, IsolationEntry entry, FileFacts facts, string named)
    {
        var kind = facts.Identity.Kind;
        var wrong = kind switch
        {
            FileKind.Link => "is a symbolic link",
            FileKind.Directory => string.Empty,
            FileKind.Regular when entry.Kind == IsolationKind.Closed => string.Empty,
            FileKind.Regular => "is a regular file, not a folder",
            _ => $"is a {kind}, not a {(entry.Kind == IsolationKind.Closed ? "file or folder" : "folder")}",
        };
        return wrong.Length > 0
            ? Outcome<Held>.Failure($"{entry.Path} {wrong}{named}")
            : Outcome<Held>.Success(new Held(fd, new SavedMode(entry.Path, entry.Kind, SnapshotFiles.ModeText((int)facts.Mode & PermissionBits), new NodeIdentity(facts.Identity.Device, facts.Identity.Inode, facts.Identity.Birth), entry.Source)));
    }

    private Outcome<string> ReApplyOne(SavedMode saved)
    {
        var judged = Judged(saved, new RestoreBound(string.Empty, string.Empty, [saved.Path]));
        try
        {
            return judged.Verdict switch
            {
                EntryVerdict.Skip skip => Outcome<string>.Failure(skip.Sentence),
                EntryVerdict.Fail fail => Outcome<string>.Failure(fail.Sentence),
                _ when judged.Fd < 0 => Outcome<string>.Failure($"{saved.Path} could not be pinned again"),
                _ => Locked(saved, judged.Fd),
            };
        }
        finally
        {
            CloseFd(judged.Fd);
        }
    }

    private Outcome<string> Locked(SavedMode saved, int fd)
    {
        events?.Add("reapply " + saved.Path);
        var errno = SetMode(fd, saved.Path, IsolationPlans.LockedMode(saved.Kind));
        return errno == 0 ? Outcome<string>.Success(string.Empty) : Outcome<string>.Failure(Refused(saved.Path, errno));
    }

    /// <summary>One entry tried the way an arm would; the empty string when it holds.</summary>
    private static string Observed(IsolationEntry entry) =>
        KernelFacts.Read(entry.Path) switch
        {
            FactsRead.Read { Facts.Identity.Kind: FileKind.Directory } => FolderHolds(entry),
            FactsRead.Read { Facts.Identity.Kind: FileKind.Regular } => FileHolds(entry.Path),
            FactsRead.Read read => IsolationSentences.Vanished(entry.Path, $"it is a {read.Facts.Identity.Kind} now"),
            FactsRead.Unread unread => IsolationSentences.Vanished(entry.Path, unread.Reason[(unread.Reason.IndexOf(": ", StringComparison.Ordinal) + 2)..]),
            _ => IsolationSentences.Vanished(entry.Path, "it could not be read"),
        };

    private static string FolderHolds(IsolationEntry entry)
    {
        var listed = Tried(entry.Path, OpenDirectory | OpenNoFollow | OpenCloseOnExec);
        if (listed.Length > 0 || entry.Kind != IsolationKind.Closed)
        {
            return listed;
        }

        return FaccessatCall(AtFdCwd, entry.Path, SearchPermission, AtEffectiveAccess) == 0
            ? IsolationSentences.Traversable(entry.Path)
            : RefusedAs(entry.Path, Marshal.GetLastPInvokeError(), string.Empty);
    }

    private static string FileHolds(string path) => Tried(path, OpenNoFollow | OpenCloseOnExec | OpenNonBlock, readable: true);

    /// <summary>An open for reading (a listing, for a folder): <c>EACCES</c> is the isolation holding; a descriptor is a sentence and is closed at once; any other answer is a vanished entry.</summary>
    private static string Tried(string path, int flags, bool readable = false)
    {
        var fd = OpenCall(path, flags);
        if (fd >= 0)
        {
            CloseFd(fd);
            return readable ? IsolationSentences.Readable(path) : IsolationSentences.Listable(path);
        }

        return RefusedAs(path, Marshal.GetLastPInvokeError(), string.Empty);
    }

    private static string RefusedAs(string path, int errno, string held) =>
        errno == IsolationSentences.Eacces ? held : IsolationSentences.Vanished(path, Marshal.GetPInvokeErrorMessage(errno));

    /// <summary>A refused mode call in the operating system's words; an <c>ENOENT</c> names the folder the descriptor road needs (a container without <c>/proc</c>).</summary>
    private static string Refused(string path, int errno) =>
        errno == IsolationSentences.Enoent ? $"{IsolationSentences.Refused(path, errno)} (the mode is set through /proc/self/fd — is /proc mounted?)" : IsolationSentences.Refused(path, errno);

    private int SetMode(int fd, string path, int mode)
    {
        if (hands?.SetMode is { } hand)
        {
            return hand(fd, path, mode);
        }

        return ChmodCall($"/proc/self/fd/{fd}", (uint)mode) == 0 ? 0 : Marshal.GetLastPInvokeError();
    }

    private void Release()
    {
        foreach (var held in _held)
        {
            CloseFd(held.Fd);
        }

        _held.Clear();
    }

    private static void CloseFd(int fd)
    {
        if (fd >= 0)
        {
            _ = CloseCall(fd);
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenCall([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int CloseCall(int fd);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int ChmodCall([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    [DllImport("libc", EntryPoint = "faccessat", SetLastError = true)]
    private static extern int FaccessatCall(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode, int flags);
}
