using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The fields of <c>struct statx</c> the module uses, as the kernel reported them; the birth time is asked for and may not be reported (<see cref="KernelFacts.BirthBit"/>).</summary>
internal readonly record struct StatxAnswer(uint Mask, uint Uid, ushort Mode, ulong Inode, long CtimeSec, uint CtimeNsec, uint DevMajor, uint DevMinor, long BtimeSec = 0, uint BtimeNsec = 0);

/// <summary>The kernel asked directly: D17's trust facts and the question "is there a directory entry by this name", with no process, no text and no locale. A program
/// started by name is looked up by the runtime in the CURRENT DIRECTORY before PATH, so an arm's <c>stat</c> or <c>id</c> in the target would answer the check that exists
/// to keep arms' files from running (measured 2026-10-29 with marker-writing shims); libc is the system's, trusted as <c>realpath(3)</c> already is.
/// <para>
/// <b>Linux</b> reads <c>statx(2)</c> with <c>AT_SYMLINK_NOFOLLOW</c>. <c>struct statx</c> is defined once for every architecture (<c>include/uapi/linux/stat.h</c>: 256 bytes,
/// explicitly padded) — the reason <c>statx</c> exists over <c>struct stat</c>, whose layout differs by architecture and glibc version. The offsets below are that contract;
/// they were probed on this host (x86_64, glibc 2.43, kernel 6.18) against <c>lstat(2)</c> and <c>stat -c</c>, and <c>KernelFactsTests</c> keeps the probe. The answer's mask must
/// carry every fact the module uses, else the file is unread — not captured is not zero. <b>The reader no longer refuses a platform it is not asked on</b> (E3.S0): the ROAD (<c>PlatformRoads</c>) decides which reader a host is asked, so a Mac or FreeBSD is
/// read by <c>PortableFacts</c> and never reaches the kernel's layout — a <c>lstat</c> offset nobody probed would read a WRONG owner or mode without an error. <b>Windows</b> has no POSIX facts; the gate declares the check not performed.
/// </para></summary>
internal static class KernelFacts
{
    private const int AtFdCwd = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const int AtEmptyPath = 0x1000;
    // x86-64 Linux, probed (octal in asm-generic: O_NOCTTY 0400, O_NONBLOCK 04000, O_NOFOLLOW 0400000, O_CLOEXEC 02000000). These are NOT the values of another architecture — on aarch64 O_NOFOLLOW is 0100000 and 0x20000 is O_LARGEFILE, so a caller there would FOLLOW a link. ReadSmall no longer refuses to run elsewhere: its only caller is BoundedFile, and only on BoundedRoad.Kernel (Linux x86-64 with a working statx), a structural test keeps it so.
    private const int OpenNoCtty = 0x100;
    private const int OpenNonBlock = 0x800;
    private const int OpenNoFollow = 0x20000;
    private const int OpenCloseOnExec = 0x80000;
    private const int OpenSmallFile = OpenNoCtty | OpenNonBlock | OpenNoFollow | OpenCloseOnExec;
    private const uint StatxBasicStats = 0x7ff;

    /// <summary><c>STATX_BTIME</c>: the file system reports when the inode was CREATED. Asked for beside the basic stats and never required — a file system without it leaves the bit clear and <see cref="FileIdentity.Birth"/> is <c>null</c>. It is what tells a folder made again from the one that stood there: ext4 hands a freed inode number to the very next file made, so the device and inode alone call a replaced folder the same one.</summary>
    internal const uint BirthBit = 0x800;

    private const uint StatxAsked = StatxBasicStats | BirthBit;

    // The facts the module uses and the statx mask bit that says the kernel reported each (STATX_TYPE, _MODE, _UID, _INO, _CTIME).
    private static readonly (uint Bit, string Name)[] Needed = [(0x1, "type"), (0x2, "mode"), (0x8, "owner"), (0x100, "inode"), (0x80, "ctime")];

    // The two errno POSIX defines as "no such entry along the path"; the same on every Linux architecture (asm-generic/errno-base.h). Read by Present alone — the one place the module reads an errno number.
    private const int NoSuchEntry = 2;
    private const int NotADirectory = 20;
    private const uint OwnerOnlyFolder = 0x1C0; // 0700

    public static int EffectiveUid() => IsUnixLike() ? (int)Geteuid() : -1;

    /// <summary>Whether <c>statx</c> works on this host: one <c>statx</c> of <c>/</c> that throws neither <see cref="EntryPointNotFoundException"/> nor <see cref="DllNotFoundException"/> AND returns 0. A <c>-1</c> (<c>ENOSYS</c> under a seccomp filter, an
    /// old kernel) is unavailable too: the entry point being THERE says nothing about the kernel answering. Failure carries the words for the road's printed line. Linux only; elsewhere the call is never made.</summary>
    public static Outcome<bool> StatxProbe()
    {
        if (!OperatingSystem.IsLinux())
        {
            return Outcome<bool>.Failure("statx is Linux's");
        }

        try
        {
            return Statx("/", out _) == 0 ? Outcome<bool>.Success(true) : Outcome<bool>.Failure($"statx of / failed: {Marshal.GetLastPInvokeErrorMessage()}");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Outcome<bool>.Failure($"statx is not in this libc: {ex.Message}");
        }
    }

    /// <summary>Whether <c>statx</c> works here, asked once per process.</summary>
    public static bool StatxIsAvailable() => StatxHere.Value;

    private static readonly Lazy<bool> StatxHere = new(() => StatxProbe().Match(available => available, _ => false));

    /// <summary>The facts of the file at the NORMALISED <paramref name="full"/> path itself (a link is reported as a link).</summary>
    public static FactsRead Read(string full)
    {
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
        try
        {
            return StatxCall(fd, string.Empty, AtEmptyPath, StatxAsked, out var buffer) == 0 ? Decode(label, Answer(buffer)) : new FactsRead.Unread($"{label}: {Marshal.GetLastPInvokeErrorMessage()}");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return new FactsRead.Unread($"statx is not available in this libc ({ex.Message}) — refused rather than assumed (D17)");
        }
    }

    /// <summary>The bytes of a SMALL regular file, read without ever waiting and without ever reading more than <paramref name="limit"/> bytes: the file is opened with <c>O_NOFOLLOW</c> (a link fails at the open with <c>ELOOP</c>: nothing outside the folder is opened, so a link to a FIFO with a blocked writer does not release it), <c>O_NONBLOCK</c> (a FIFO opens at once instead of waiting for a
    /// writer), <c>O_NOCTTY</c> and <c>O_CLOEXEC</c>; its kind is judged from the DESCRIPTOR's facts (<see cref="ReadOpen"/>: a FIFO, a device, a folder is refused before a byte is read), the path must still name the same inode (the same device-and-inode comparison the gate lock makes after its open, for a swap made AFTER the open), and the read stops at <c>limit + 1</c> bytes: a file bigger than the limit is refused, never read whole. A failure is a value; nothing here throws what the library below it throws.
    /// <para>
    /// The open flags are the values probed on this host (Linux x86-64, kernel 6.18); they are NOT valid for another architecture or platform, and this method no longer refuses to run there — <c>BoundedFile</c> asks it only on the <see cref="BoundedRoad.Kernel"/> road, which <c>PlatformRoads</c> grants to Linux x86-64 with a working <c>statx</c> alone.
    /// </para></summary>
    public static Outcome<byte[]> ReadSmall(string path, int limit)
    {
        try
        {
            var fd = OpenCall(path, OpenSmallFile);
            return fd < 0 ? Outcome<byte[]>.Failure($"{path}: {Marshal.GetLastPInvokeErrorMessage()}") : ReadDescriptor(fd, path, limit);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return Outcome<byte[]>.Failure($"open is not available in this libc ({ex.Message})");
        }
    }

    private static Outcome<byte[]> ReadDescriptor(int fd, string path, int limit)
    {
        using var handle = new SafeFileHandle(fd, ownsHandle: true);
        return SameRegularInode(ReadOpen(fd, path), Read(path))
            ? Bounded(handle, path, limit)
            : Outcome<byte[]>.Failure($"{path} is not a regular file of its own (a FIFO, a device, a folder, or swapped while it was opened)");
    }

    /// <summary>Both reads are of a regular file and of the same file: equal device and inode.</summary>
    private static bool SameRegularInode(FactsRead held, FactsRead named) =>
        held is FactsRead.Read { Facts.Identity: { Kind: FileKind.Regular } open }
        && named is FactsRead.Read { Facts.Identity: { Kind: FileKind.Regular } there }
        && open.Device == there.Device
        && open.Inode == there.Inode;

    /// <summary>At most <paramref name="limit"/> bytes: the read asks for one byte more, and a file that gives it is refused as too big — the file is never read past <c>limit + 1</c> bytes whatever its size.</summary>
    private static Outcome<byte[]> Bounded(SafeFileHandle handle, string path, int limit)
    {
        var buffer = new byte[limit + 1];
        var total = 0;
        while (total < buffer.Length && RandomAccess.Read(handle, buffer.AsSpan(total), total) is var n and > 0)
        {
            total += n;
        }

        return total > limit ? Outcome<byte[]>.Failure($"{path} is bigger than {limit} bytes") : Outcome<byte[]>.Success(buffer.AsSpan(0, total).ToArray());
    }

    /// <summary>Whether a directory entry is there under <paramref name="path"/>: a dangling link is (true), nothing by that name is not (false), and a kernel that cannot say
    /// (a folder the user may not search, an I/O error) is a failure carrying its words. Asked by <c>statx</c> where it works; elsewhere (macOS, FreeBSD, a Linux without <c>statx</c>) by <c>Path.Exists</c>: true for a dangling link, never a failure, and an
    /// <c>EACCES</c> reads as absent — a stated gap (the module doc), not a check.</summary>
    public static Outcome<bool> Present(string path) => Present(path, StatxIsAvailable());

    /// <summary><see cref="Present(string)"/> with the question of which road handed in, so a test can take the runtime's road on a host where <c>statx</c> works.</summary>
    internal static Outcome<bool> Present(string path, bool statx)
    {
        if (!statx)
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

    /// <summary>Makes ONE new folder, exclusively: <c>mkdir(2)</c> through libc — a path and a mode, no struct (<c>mode_t</c> is 16 bits on Darwin and FreeBSD; <c>0700</c> fits) — fails for anything already at the name, and a failure is classified by a STATE,
    /// not by an errno number: <see cref="Present"/> says an entry is there (a folder, a file, a link live or dangling) → <see cref="FolderMade.AlreadyThere"/>, so a folder someone else made between the caller's existence check and this call is never adopted
    /// (<c>Directory.CreateDirectory</c> adopts it silently); no entry → <see cref="FolderMade.Failed"/> with the operating system's words for the <c>mkdir</c> failure (read BEFORE the presence check can overwrite them). The mode asked for is <c>0700</c>, which the umask may
    /// narrow: the caller sets the mode explicitly afterwards. Windows has no libc <c>mkdir</c>: its caller makes the folder by the runtime.</summary>
    public static FolderMade MakeFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FolderMade.Failed("libc mkdir is POSIX; the folder is made by the runtime on Windows");
        }

        try
        {
            return MkdirCall(path, OwnerOnlyFolder) == 0 ? new FolderMade.Made() : Classified(path, Marshal.GetLastPInvokeError());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return new FolderMade.Failed($"mkdir is not available in this libc ({ex.Message})");
        }
    }

    private static FolderMade Classified(string path, int errno) =>
        Present(path).Match<FolderMade>(
            there => there ? new FolderMade.AlreadyThere() : Failed(path, errno),
            _ => Failed(path, errno));

    private static FolderMade Failed(string path, int errno) => new FolderMade.Failed($"'{path}': {Marshal.GetPInvokeErrorMessage(errno)}");

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
        var identity = new FileIdentity(((ulong)raw.DevMajor << 32) | raw.DevMinor, raw.Inode, mode.Kind, raw.CtimeSec + (raw.CtimeNsec / 1_000_000_000m))
        {
            Birth = (raw.Mask & BirthBit) != 0 ? raw.BtimeSec + (raw.BtimeNsec / 1_000_000_000m) : null,
        };
        return new FactsRead.Read(new FileFacts(full, (int)raw.Uid, mode.Permissions) { Identity = identity });
    }

    private static Outcome<bool> Missing(string path, int errno) =>
        errno is NoSuchEntry or NotADirectory
            ? Outcome<bool>.Success(false)
            : Outcome<bool>.Failure($"'{path}': {Marshal.GetPInvokeErrorMessage(errno)}");

    private static int Statx(string path, out StatxBuffer buffer) => StatxCall(AtFdCwd, path, AtSymlinkNoFollow, StatxAsked, out buffer);

    private static StatxAnswer Answer(StatxBuffer b) => new(b.Mask, b.Uid, b.Mode, b.Ino, b.CtimeSec, b.CtimeNsec, b.DevMajor, b.DevMinor, b.BtimeSec, b.BtimeNsec);

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

        [FieldOffset(80)]
        public long BtimeSec;

        [FieldOffset(88)]
        public uint BtimeNsec;

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

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenCall([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int MkdirCall([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint Geteuid();
}
