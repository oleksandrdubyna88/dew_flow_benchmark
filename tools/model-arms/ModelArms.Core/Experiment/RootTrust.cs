namespace ModelArms.Core.Experiment;

/// <summary>What kind of file the type bits of a mode name. <see cref="Unknown"/> is "not captured", a different fact from any kind.</summary>
public enum FileKind { Unknown, Regular, Directory, Link, Fifo, Socket, CharacterDevice, BlockDevice }

/// <summary>Which inode a path reached when it was read: the device and inode numbers and the kind of file. Two reads of one path that disagree
/// reached two different files.</summary>
public sealed record FileIdentity(ulong Device, ulong Inode, FileKind Kind)
{
    /// <summary>No identity was captured (a fact built by hand, or a platform that reads none).</summary>
    public static FileIdentity Unknown { get; } = new(0, 0, FileKind.Unknown);
}

/// <summary>What the console learned about one file or folder: where it is, which user owns it, its mode and which inode that was.</summary>
public sealed record FileFacts(string Path, int OwnerUid, UnixFileMode Mode)
{
    public FileIdentity Identity { get; init; } = FileIdentity.Unknown;
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

    public static TrustAnswer Decide(bool posix, int currentUid, IReadOnlyList<FileFacts> files, string root)
    {
        if (!posix)
        {
            return new TrustAnswer.NotPerformed($"the ownership and mode check of {root} was not performed — POSIX only (D17)");
        }

        IReadOnlyList<string> problems = [.. files.SelectMany(file => Problems(file, currentUid))];
        return problems.Count == 0 ? new TrustAnswer.Trusted() : new TrustAnswer.Refused(problems);
    }

    /// <summary>What differs between two reads of one path: the names of the facts (device, inode, type, owner, mode) that are not the same, empty when
    /// nothing is. A path that reaches another file between its trust check and its read is the swap D17 exists to refuse.</summary>
    public static IReadOnlyList<string> Changes(FileFacts before, FileFacts after) =>
    [
        .. new (string Fact, bool Differs)[]
        {
            ("device", before.Identity.Device != after.Identity.Device),
            ("inode", before.Identity.Inode != after.Identity.Inode),
            ("type", before.Identity.Kind != after.Identity.Kind),
            ("owner", before.OwnerUid != after.OwnerUid),
            ("mode", before.Mode != after.Mode),
        }.Where(change => change.Differs).Select(change => change.Fact),
    ];

    private static IEnumerable<string> Problems(FileFacts file, int currentUid)
    {
        if (file.OwnerUid != currentUid)
        {
            yield return $"{file.Path} is owned by uid {file.OwnerUid}, not by you (uid {currentUid}) — refused: a file that names executables must be the operator's own (D17)";
        }

        if ((file.Mode & GroupOrOthersWrite) != 0)
        {
            yield return $"{file.Path} is mode {Octal(file.Mode)} — writable by group or others; refused (chmod go-w {file.Path}) (D17)";
        }
    }

    /// <summary>The permission bits as three octal digits, <c>775</c>; the set-id and sticky bits are not part of what is judged.</summary>
    private static string Octal(UnixFileMode mode) => Convert.ToString((int)mode & PermissionBits, 8).PadLeft(3, '0');
}
