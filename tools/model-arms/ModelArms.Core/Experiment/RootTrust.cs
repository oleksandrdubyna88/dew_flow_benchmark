using System.Globalization;

namespace ModelArms.Core.Experiment;

/// <summary>What kind of file the type bits of a mode name. <see cref="Unknown"/> is "not captured", a different fact from any kind.</summary>
public enum FileKind { Unknown, Regular, Directory, Link, Fifo, Socket, CharacterDevice, BlockDevice }

/// <summary>Which inode a path reached when it was read: the device and inode numbers, the kind of file and the time of its last status change
/// (<paramref name="Ctime"/>, seconds since the epoch with nine decimals). Two reads of one path that disagree reached two different files, or one
/// file that was changed in between; a link made and removed again inside the window moves the ctime of the inode it touched even when the inode
/// number is the same.</summary>
public sealed record FileIdentity(ulong Device, ulong Inode, FileKind Kind, decimal Ctime)
{
    /// <summary>No identity was captured (a fact built by hand, or a platform that reads none).</summary>
    public static FileIdentity Unknown { get; } = new(0, 0, FileKind.Unknown, 0m);
}

/// <summary>Which reader produced a <see cref="FileFacts"/>: the kernel's <c>statx</c> (owner, mode and identity) or the runtime's own facts (mode, kind when a link or a folder, length and write time). Two facts of different sources are
/// never compared field by field — a change of road is itself a change.</summary>
public enum FactsSource { Kernel, Portable }

/// <summary>What the portable road can compare between two reads of one path in place of an inode: the file's length and its last write time in ticks. A swap that keeps both is invisible to it, and the printed line says so.</summary>
public sealed record FileWitness(long Length, long WriteTicks);

/// <summary>What the console learned about one file or folder: where it is, which user owns it (<c>null</c> when the road cannot say — not captured is not zero), its mode and which inode that was.</summary>
public sealed record FileFacts(string Path, int? OwnerUid, UnixFileMode Mode)
{
    public FileIdentity Identity { get; init; } = FileIdentity.Unknown;

    /// <summary>The length and write time, when the portable road read them.</summary>
    public FileWitness? Witness { get; init; }

    public FactsSource Source { get; init; } = FactsSource.Kernel;
}

/// <summary>The answer of the trust check. Three states, never two: a check that was <b>not performed</b> is not a check that passed.</summary>
public abstract record TrustAnswer
{
    private TrustAnswer() { }

    public sealed record Trusted : TrustAnswer;

    /// <summary>Every reason the root cannot be trusted, one sentence each, naming the file.</summary>
    public sealed record Refused(IReadOnlyList<string> Problems) : TrustAnswer;

    /// <summary>The line the console prints instead of a verdict.</summary>
    public sealed record NotPerformed(string Line) : TrustAnswer;
}

/// <summary>D17's first half, pure over facts. <c>experiment.json</c> names executables that run with the operator's rights, so on POSIX the root
/// and the files that name executables must belong to the operator and must not be writable by group or others; otherwise another user could
/// change what the harness runs. The console reads the facts (<c>RootFacts</c>); this only judges them.</summary>
public static class RootTrust
{
    private const UnixFileMode GroupOrOthersWrite = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    private const int PermissionBits = 0b111_111_111;

    /// <summary>The verdict for the files of one root on <paramref name="road"/>. <see cref="TrustRoad.NotPerformed"/> (Windows) declares the check not performed; <see cref="TrustRoad.Kernel"/> judges owner and mode;
    /// <see cref="TrustRoad.Portable"/> judges the mode alone — the owner was not read, and the road's printed line (the gate prints it BEFORE this judgement) has said so.</summary>
    public static TrustAnswer Decide(TrustRoad road, int currentUid, IReadOnlyList<FileFacts> files, string root)
    {
        if (road == TrustRoad.NotPerformed)
        {
            return new TrustAnswer.NotPerformed($"the ownership and mode check of {root} was not performed — POSIX only (D17)");
        }

        IReadOnlyList<string> problems = [.. files.SelectMany(file => Problems(file, road, currentUid))];
        return problems.Count == 0 ? new TrustAnswer.Trusted() : new TrustAnswer.Refused(problems);
    }

    /// <summary>What differs between two reads of one path. Kernel facts: the device, inode, type, owner, mode and ctime. Portable facts: the kind, mode, length and write time — never a silent <c>0 == 0</c> over an identity nobody captured.
    /// Facts of two sources differ in <c>source</c> alone. A path that reaches another file between its trust check and its read is the swap D17 exists to refuse.</summary>
    public static IReadOnlyList<string> Changes(FileFacts before, FileFacts after) =>
        before.Source != after.Source ? ["source"]
        : before.Source == FactsSource.Kernel ? Named(KernelDifferences(before, after))
        : Named(PortableDifferences(before, after));

    private static IReadOnlyList<string> Named(IEnumerable<(string Fact, bool Differs)> differences) =>
        [.. differences.Where(change => change.Differs).Select(change => change.Fact)];

    private static (string Fact, bool Differs)[] KernelDifferences(FileFacts before, FileFacts after) =>
    [
        ("device", before.Identity.Device != after.Identity.Device),
        ("inode", before.Identity.Inode != after.Identity.Inode),
        ("type", before.Identity.Kind != after.Identity.Kind),
        ("owner", before.OwnerUid != after.OwnerUid),
        ("mode", before.Mode != after.Mode),
        ("ctime", before.Identity.Ctime != after.Identity.Ctime),
    ];

    private static (string Fact, bool Differs)[] PortableDifferences(FileFacts before, FileFacts after) =>
    [
        ("kind", before.Identity.Kind != after.Identity.Kind),
        ("mode", before.Mode != after.Mode),
        ("length", before.Witness?.Length != after.Witness?.Length),
        ("mtime", before.Witness?.WriteTicks != after.Witness?.WriteTicks),
    ];

    private static IEnumerable<string> Problems(FileFacts file, TrustRoad road, int currentUid)
    {
        if (road == TrustRoad.Kernel && file.OwnerUid != currentUid)
        {
            yield return $"{file.Path} is owned by uid {file.OwnerUid?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, not by you (uid {currentUid}) — refused: a file that names executables must be the operator's own (D17)";
        }

        if ((file.Mode & GroupOrOthersWrite) != 0)
        {
            yield return $"{file.Path} is mode {Octal(file.Mode)} — writable by group or others; refused (chmod go-w {file.Path}) (D17)";
        }
    }

    /// <summary>The permission bits as three octal digits, <c>775</c>; the set-id and sticky bits are not part of what is judged.</summary>
    private static string Octal(UnixFileMode mode) => Convert.ToString((int)mode & PermissionBits, 8).PadLeft(3, '0');
}
