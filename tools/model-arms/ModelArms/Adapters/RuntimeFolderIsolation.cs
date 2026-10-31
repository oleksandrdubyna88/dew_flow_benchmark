using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>D11's file-system side on every POSIX road that is not <c>Kernel</c>/<c>Kernel</c> (macOS, FreeBSD, Linux arm64, a Linux without <c>statx</c>; ledger 2.1 — no verb of this epic exits 3 on a POSIX host for want of a kernel reader). The same <see cref="IFolderIsolation"/> over the runtime's facts, no P/Invoke: the kind by the file's link attribute (<c>BoundedFile.IsLink</c>) then
/// <c>Directory.Exists</c> / <c>File.Exists</c> (a link is refused without being followed), the ancestors by <see cref="RealPath"/> as the kernel road, the modes read by <c>File.GetUnixFileMode</c> and set by <c>File.SetUnixFileMode</c> — BY PATH. It refuses only an actual inability to apply, verify or recover, never a platform's name.
/// <para>
/// <b>What it does not verify</b> (D26 a, said in the line <see cref="NotVerifiedLine"/> the verbs print before their first mode call, in the same words): the window between the check and the <c>chmod</c> is open, so a link planted there is followed; the snapshot carries no device and no inode, so a replaced entry is not told apart at <c>unlock</c> (kind and the mode rule are still judged); a FIFO or a device reads as a file.
/// <b>D26 f</b> (the identity compared at <c>unlock</c>) is not performed here, and says so. The verification IS performed, for every entry, by the attempt: a folder must refuse to be enumerated (<c>UnauthorizedAccessException</c> from the runtime's <c>opendir</c>, before a name is read), a closed folder must refuse the status of a child, a closed file must refuse to be opened.
/// The same-user residuals of <see cref="PosixFolderIsolation"/> hold here too. Observed on Linux through the seam (<c>IsolationSeams.Road</c>); a Mac itself is unobserved (parent §9).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class RuntimeFolderIsolation(PlatformRoad road, IsolationHands? hands, List<string>? events) : IFolderIsolation
{
    private const int PermissionBits = 0xFFF;
    private const string Probe = ".model-arms-probe";

    private List<SavedMode> _pinned = [];

    public string Describe => $"POSIX file modes ({road.Platform}, runtime road)";

    public bool Applies => true;

    public string NotVerifiedLine =>
        $"isolation on {road.Platform}: modes are set by path — a link planted between the check and the chmod is followed; a replaced entry is not told apart at unlock; a FIFO or a device reads as a file (ledger 2.1)";

    public bool IsRoot => hands?.IsRoot is { } hand ? hand() : Environment.IsPrivilegedProcess;

    public Outcome<Pinned> Pin(IsolationPlan plan)
    {
        events?.Add("pin");
        var modes = new List<SavedMode>();
        foreach (var entry in plan.Entries)
        {
            var one = PinOne(entry);
            if (one is Outcome<SavedMode>.Fail failed)
            {
                return Outcome<Pinned>.Failure(failed.Reason);
            }

            modes.Add(((Outcome<SavedMode>.Ok)one).Value);
        }

        _pinned = modes;
        return Outcome<Pinned>.Success(new Pinned(modes));
    }

    public Outcome<string> Apply(Pinned pinned, IsolationPlan plan)
    {
        hands?.BeforeApply?.Invoke();
        for (var index = 0; index < plan.Entries.Count; index++)
        {
            var entry = plan.Entries[index];
            if (SetMode(entry.Path, IsolationPlans.LockedMode(entry.Kind)) is { Length: > 0 } refused)
            {
                return Outcome<string>.Failure(refused);
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
        foreach (var saved in Enumerable.Reverse(_pinned))
        {
            events?.Add("rollback " + saved.Path);
            var refused = SetMode(saved.Path, IsolationSentences.SavedBits(saved));
            (refused.Length == 0 ? restored : failed).Add(refused.Length == 0 ? saved.Path : refused);
        }

        return new RestoreReport(restored, [], [], failed);
    }

    public RestoreReport Restore(IReadOnlyList<SavedMode> reverse, RestoreBound bound)
    {
        var restored = new List<string>();
        var skipped = new List<string>();
        var unresolved = new List<string>();
        var failed = new List<string>();
        foreach (var saved in reverse)
        {
            switch (Judged(saved, bound))
            {
                case EntryVerdict.Restore:
                    events?.Add("restore " + saved.Path);
                    var refused = SetMode(saved.Path, IsolationSentences.SavedBits(saved));
                    (refused.Length == 0 ? restored : failed).Add(refused.Length == 0 ? saved.Path : refused);
                    break;
                case EntryVerdict.AlreadyRestored:
                    restored.Add(saved.Path);
                    break;
                case EntryVerdict.Unresolved unresolvedEntry:
                    unresolved.Add(unresolvedEntry.Sentence);
                    break;
                case EntryVerdict.Skip skip:
                    skipped.Add(skip.Sentence);
                    break;
                case EntryVerdict.Fail fail:
                    failed.Add(fail.Sentence);
                    break;
            }
        }

        return new RestoreReport(restored, skipped, unresolved, failed);
    }

    public Outcome<string> ReApply(IReadOnlyList<SavedMode> open, IsolationPlan plan)
    {
        foreach (var saved in open)
        {
            var refusal = Judged(saved, new RestoreBound(string.Empty, string.Empty, [saved.Path])) switch
            {
                EntryVerdict.Skip skip => skip.Sentence,
                EntryVerdict.Fail fail => fail.Sentence,
                _ => SetLocked(saved),
            };
            if (refusal.Length > 0)
            {
                return Outcome<string>.Failure(refusal);
            }
        }

        return Outcome<string>.Success($"{open.Count} entries re-applied");
    }

    public void Dispose()
    {
    }

    private string SetLocked(SavedMode saved)
    {
        events?.Add("reapply " + saved.Path);
        return SetMode(saved.Path, IsolationPlans.LockedMode(saved.Kind));
    }

    private static EntryVerdict Judged(SavedMode saved, RestoreBound bound)
    {
        var inBound = bound.Holds(saved.Path);
        return IsLink(saved.Path) ? RestoreJudgement.Of(saved, new PinnedFacts(IsLink: true, IsFolder: false, IsFile: false, 0, null), inBound)
            : !Directory.Exists(saved.Path) && !File.Exists(saved.Path) ? RestoreJudgement.Of(saved, null, inBound)
            : ThroughALink(saved.Path) ? new EntryVerdict.Skip($"{saved.Path}: reached through a link now — not restored")
            : RestoreJudgement.Of(saved, Facts(saved.Path), inBound);
    }

    private static PinnedFacts Facts(string path) =>
        new(IsLink: false, Directory.Exists(path), File.Exists(path), (int)File.GetUnixFileMode(path) & PermissionBits, null);

    private static bool IsLink(string path) => BoundedFile.IsLink(path);

    private static bool ThroughALink(string path) => RealPath.Of(path) is not Outcome<string>.Ok ok || !string.Equals(ok.Value, path, StringComparison.Ordinal);

    private static Outcome<SavedMode> PinOne(IsolationEntry entry)
    {
        var named = string.IsNullOrEmpty(entry.Source) ? string.Empty : $" — named by {entry.Source}";
        var wrong = IsLink(entry.Path) ? "is a symbolic link"
            : Directory.Exists(entry.Path) ? string.Empty
            : File.Exists(entry.Path) ? (entry.Kind == IsolationKind.Closed ? string.Empty : "is a regular file, not a folder")
            : "does not exist";
        if (wrong.Length > 0)
        {
            return Outcome<SavedMode>.Failure($"{entry.Path} {wrong}{named}");
        }

        if (RealPath.Of(entry.Path) is Outcome<string>.Ok real && !string.Equals(real.Value, entry.Path, StringComparison.Ordinal))
        {
            return Outcome<SavedMode>.Failure($"{entry.Path} is reached through a link (its real path is {real.Value}) — name the real path{named}");
        }

        return ModeOf(entry.Path).Match(
            bits => Outcome<SavedMode>.Success(new SavedMode(entry.Path, entry.Kind, SnapshotFiles.ModeText(bits), null)),
            reason => Outcome<SavedMode>.Failure($"{entry.Path} cannot be read: {reason}{named}"));
    }

    private static Outcome<int> ModeOf(string path)
    {
        try
        {
            return Outcome<int>.Success((int)File.GetUnixFileMode(path) & PermissionBits);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<int>.Failure(ex.Message);
        }
    }

    /// <summary>One entry tried the way an arm would; the empty string when it holds. The one place the module enumerates a folder: the ATTEMPT to list is the observation — it must fail before a name is read.</summary>
    private static string Observed(IsolationEntry entry)
    {
        try
        {
            if (IsLink(entry.Path) || (!Directory.Exists(entry.Path) && !File.Exists(entry.Path)))
            {
                return IsolationSentences.Vanished(entry.Path, "it is gone, or a link now");
            }

            return Directory.Exists(entry.Path) ? FolderHolds(entry) : FileHolds(entry.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return IsolationSentences.Vanished(entry.Path, ex.Message);
        }
    }

    private static string FolderHolds(IsolationEntry entry)
    {
        try
        {
            using var names = Directory.EnumerateFileSystemEntries(entry.Path).GetEnumerator();
            _ = names.MoveNext();
            return IsolationSentences.Listable(entry.Path);
        }
        catch (UnauthorizedAccessException)
        {
            return entry.Kind == IsolationKind.Closed ? Traversal(entry.Path) : string.Empty;
        }
    }

    /// <summary>A closed folder refuses the status of a child (<c>EACCES</c>, S1-4); a missing child of a folder that CAN be searched is <see cref="FileNotFoundException"/> — the "still traversable" answer.</summary>
    private static string Traversal(string folder)
    {
        try
        {
            _ = new FileInfo(Path.Combine(folder, Probe)).Length;
            return IsolationSentences.Traversable(folder);
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return IsolationSentences.Traversable(folder);
        }
    }

    private static string FileHolds(string path)
    {
        try
        {
            File.OpenRead(path).Dispose();
            return IsolationSentences.Readable(path);
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private string SetMode(string path, int mode)
    {
        if (hands?.SetMode is { } hand)
        {
            return hand(-1, path, mode) is var errno and not 0 ? IsolationSentences.Refused(path, errno) : string.Empty;
        }

        try
        {
            File.SetUnixFileMode(path, (UnixFileMode)mode);
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"{path}: {ex.Message}";
        }
    }
}
