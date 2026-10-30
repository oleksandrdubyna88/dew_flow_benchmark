using System.Runtime.InteropServices;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The seven fields of <c>struct statx</c> the module uses, as the kernel reported them.</summary>
internal readonly record struct StatxAnswer(uint Mask, uint Uid, ushort Mode, ulong Inode, long CtimeSec, uint CtimeNsec, uint DevMajor, uint DevMinor);

/// <summary>The kernel asked directly: D17's trust facts and the question "is there a directory entry by this name", with no process, no text and no locale. A program
/// started by name is looked up by the runtime in the CURRENT DIRECTORY before PATH, so an arm's <c>stat</c> or <c>id</c> in the target would answer the check that exists
/// to keep arms' files from running (measured 2026-10-29 with marker-writing shims); libc is the system's, trusted as <c>realpath(3)</c> already is.
/// <para>
/// <b>Linux</b> reads <c>statx(2)</c> with <c>AT_SYMLINK_NOFOLLOW</c>. <c>struct statx</c> is defined once for every architecture (<c>include/uapi/linux/stat.h</c>: 256 bytes,
/// explicitly padded) — the reason <c>statx</c> exists over <c>struct stat</c>, whose layout differs by architecture and glibc version. The offsets below are that contract;
/// they were probed on this host (x86_64, glibc 2.43, kernel 6.18) against <c>lstat(2)</c> and <c>stat -c</c>, and <c>KernelFactsTests</c> keeps the probe. The answer's mask must
/// carry every fact the module uses, else the file is unread — not captured is not zero. <b>macOS and FreeBSD</b> are refused as unprobed: nothing here has run on a Mac (CI
/// is <c>ubuntu-latest</c>), a Mac needs a probed <c>lstat</c> layout (144 bytes, 64-bit inode: plain <c>lstat</c> on arm64, <c>lstat$INODE64</c> on x86_64), and a text reader of
/// <c>stat -f</c> would be a second reader nobody observed. <b>Windows</b> has no POSIX facts; the gate declares the check not performed.
/// </para></summary>
internal static class KernelFacts
{
    private const int AtFdCwd = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const int AtEmptyPath = 0x1000;
    private const uint StatxBasicStats = 0x7ff;

    // The facts the module uses and the statx mask bit that says the kernel reported each (STATX_TYPE, _MODE, _UID, _INO, _CTIME).
    private static readonly (uint Bit, string Name)[] Needed = [(0x1, "type"), (0x2, "mode"), (0x8, "owner"), (0x100, "inode"), (0x80, "ctime")];

    // The two errno POSIX defines as "no such entry along the path"; the same on every Linux architecture (asm-generic/errno-base.h). The one place the module reads an errno number.
    private const int NoSuchEntry = 2;
    private const int NotADirectory = 20;
    private const int AlreadyExists = 17;
    private const uint OwnerOnlyFolder = 0x1C0; // 0700

    public static int EffectiveUid() => IsUnixLike() ? (int)Geteuid() : -1;

    /// <summary>The facts of the file at the NORMALISED <paramref name="full"/> path itself (a link is reported as a link).</summary>
    public static FactsRead Read(string full)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new FactsRead.Unread(
                $"the owner and mode of {full} cannot be read on this platform: no probed kernel reader (a Mac needs a probed lstat layout: 144 bytes, 64-bit inode — plain lstat on arm64, lstat$INODE64 on x86_64) — refused rather than assumed (D17)");
        }

        try
        {
            return Statx(full, out var buffer) == 0 ? Decode(full, Answer(buffer)) : new FactsRead.Unread($"{full}: {Marshal.GetLastPInvokeErrorMessage()}");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return new FactsRead.Unread($"statx is not available in this libc ({ex.Message}) — refused rather than assumed (D17)");
        }
    }

    /// <summary>The facts of the file an OPEN descriptor holds (<c>statx</c> with <c>AT_EMPTY_PATH</c>, <c>fstat</c>'s question): which inode the handle really is, whatever the path now names. <paramref name="label"/> is the path the descriptor was
    /// opened by, for the words of a failure.</summary>
    public static FactsRead ReadOpen(int fd, string label)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new FactsRead.Unread($"the inode behind the open handle of {label} cannot be read on this platform: no probed kernel reader — refused rather than assumed (D17)");
        }

        try
        {
            return StatxCall(fd, string.Empty, AtEmptyPath, StatxBasicStats, out var buffer) == 0 ? Decode(label, Answer(buffer)) : new FactsRead.Unread($"{label}: {Marshal.GetLastPInvokeErrorMessage()}");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return new FactsRead.Unread($"statx is not available in this libc ({ex.Message}) — refused rather than assumed (D17)");
        }
    }

    /// <summary>Whether a directory entry is there under <paramref name="path"/>: a dangling link is (true), nothing by that name is not (false), and a kernel that cannot say
    /// (a folder the user may not search, an I/O error) is a failure carrying its words. Off Linux: <c>Path.Exists</c>, true for a dangling link, never a failure — a stated gap.</summary>
    public static Outcome<bool> Present(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return Outcome<bool>.Success(Path.Exists(path));
        }

        try
        {
            return Statx(path, out _) == 0 ? Outcome<bool>.Success(true) : Missing(path, Marshal.GetLastPInvokeError());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Outcome<bool>.Failure($"'{path}': statx is not available in this libc ({ex.Message})");
        }
    }

    /// <summary>Makes ONE new folder, exclusively: <c>mkdir(2)</c> answers <c>EEXIST</c> for anything at the name — a folder, a file, a link live or dangling — and the answer is
    /// <see cref="FolderMade.AlreadyThere"/>, so a folder someone else made between the caller's existence check and this call is never adopted (<c>Directory.CreateDirectory</c> adopts it
    /// silently). The mode asked for is <c>0700</c>, which the umask may narrow: the caller sets the mode explicitly afterwards. Linux only; any other platform is refused as unprobed.</summary>
    public static FolderMade MakeFolder(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new FolderMade.Failed("no probed kernel mkdir on this platform — refused rather than assumed (D17)");
        }

        try
        {
            return MkdirCall(path, OwnerOnlyFolder) == 0 ? new FolderMade.Made() : Refused(path, Marshal.GetLastPInvokeError());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return new FolderMade.Failed($"mkdir is not available in this libc ({ex.Message}) — refused rather than assumed (D17)");
        }
    }

    private static FolderMade Refused(string path, int errno) =>
        errno == AlreadyExists ? new FolderMade.AlreadyThere() : new FolderMade.Failed($"'{path}': {Marshal.GetPInvokeErrorMessage(errno)}");

    /// <summary>Pure over the seven fields: the mask check, the permission and type bits of the mode, the device as one comparison key (<c>major &lt;&lt; 32 | minor</c> — not
    /// <c>makedev</c>'s encoding), the change time to the nanosecond.</summary>
    internal static FactsRead Decode(string full, StatxAnswer raw)
    {
        var absent = Needed.Where(fact => (raw.Mask & fact.Bit) == 0).Select(fact => fact.Name).ToList();
        if (absent.Count > 0)
        {
            return new FactsRead.Unread($"the file system did not report the {string.Join(", ", absent)} of {full} — refused rather than assumed (D17)");
        }

        var mode = new RootFacts.RawMode(raw.Mode);
        var identity = new FileIdentity(((ulong)raw.DevMajor << 32) | raw.DevMinor, raw.Inode, mode.Kind, raw.CtimeSec + (raw.CtimeNsec / 1_000_000_000m));
        return new FactsRead.Read(new FileFacts(full, (int)raw.Uid, mode.Permissions) { Identity = identity });
    }

    private static Outcome<bool> Missing(string path, int errno) =>
        errno is NoSuchEntry or NotADirectory
            ? Outcome<bool>.Success(false)
            : Outcome<bool>.Failure($"'{path}': {Marshal.GetPInvokeErrorMessage(errno)}");

    private static int Statx(string path, out StatxBuffer buffer) => StatxCall(AtFdCwd, path, AtSymlinkNoFollow, StatxBasicStats, out buffer);

    private static StatxAnswer Answer(StatxBuffer b) => new(b.Mask, b.Uid, b.Mode, b.Ino, b.CtimeSec, b.CtimeNsec, b.DevMajor, b.DevMinor);

    private static bool IsUnixLike() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD();

    /// <summary><c>struct statx</c> by the kernel's contract: 256 bytes, the offsets of the fields the module reads.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(20)]
        public uint Uid;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(32)]
        public ulong Ino;

        [FieldOffset(96)]
        public long CtimeSec;

        [FieldOffset(104)]
        public uint CtimeNsec;

        [FieldOffset(136)]
        public uint DevMajor;

        [FieldOffset(140)]
        public uint DevMinor;
    }

    [DllImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static extern int StatxCall(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, out StatxBuffer buffer);

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int MkdirCall([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint Geteuid();
}
