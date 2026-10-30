using Bench.Domain;
using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The gate lock (plan D10, C7): one gate step at a time across every arm of an experiment, as an OS file lock taken through .NET — never through <c>flock(1)</c> (the shell's way; nothing the module starts is looked
/// up by name, and a lock against a non-.NET holder is out of bounds, parent §9). It is NOT D11's lock (<c>.isolation.lock</c>); the two files are different locks with different holders.
/// <para>
/// <b>The atomic operation it rests on</b> (common.reliability): the kernel's own open-with-no-sharing. The path is opened with <see cref="FileShare.None"/>. On Linux the runtime takes <c>flock(LOCK_EX | LOCK_NB)</c> on the descriptor
/// and maps <c>EWOULDBLOCK</c> to an <see cref="IOException"/> whose <c>HResult</c> is the raw errno 11 (read from the runtime's source and observed on .NET 10.0.14); on Windows the share mode is enforced by the file system's own
/// <c>CreateFile</c> and the exception carries <c>ERROR_SHARING_VIOLATION</c> or <c>ERROR_LOCK_VIOLATION</c> (<c>0x80070020</c>, <c>0x80070021</c> — from the Win32 documentation, NOT observed: nothing here ran on Windows). The held lock IS the
/// open stream; <see cref="IDisposable.Dispose"/> closes it.
/// </para>
/// <para>
/// <b>What the path may be (D17).</b> <c>.gate.lock</c> is a regular file of the root's own, or absent; <see cref="Take"/> — the one road every caller goes through — reads the path WITHOUT following a link (the module's <c>statx</c> reader) and
/// refuses a link, a folder, a FIFO, a device or any other kind with a sentence naming the path, what it is and D17. A missing file is made with <see cref="FileMode.CreateNew"/> (<c>O_CREAT | O_EXCL</c>, which never follows a link and fails
/// on one planted in the window), an existing one is opened with <see cref="FileMode.Open"/> (never created through a link), and after the open the inode the handle really holds (<c>statx</c> on the descriptor) is compared, device and inode,
/// with the file the path now names; a mismatch is refused. <c>status</c> creates nothing. <b>The residual swap window:</b> between the check and the open, and between the comparison and whatever the caller does next, a process that
/// can write the root can still swap the file; the comparison closes the swap that is visible at the instant it is made (a link, another inode) and not one made after it. Only the operator can write the root (D17's trust check), so this is a
/// residual and not a hole. A link in a PARENT folder of the root is D17's root check's, not this one's. Off Linux the facts do not exist: nothing is judged (the gate printed that the trust check was not performed) and the platform mapping below
/// already refuses everything but Linux and Windows.
/// </para>
/// <para>
/// <b>When the holder dies.</b> The lock belongs to the open file description, not to a record anyone wrote, so there is no heartbeat to renew and no id to fence: a holder that is killed (<c>SIGKILL</c> included — observed on this host: a
/// holder killed while a second process had just been refused, and the next try succeeded at once), crashes or exits has its descriptors closed by the kernel, and the lock is free the same instant. On Windows the handle is closed by
/// the system when the process ends, which frees the share mode the same way (documented, not observed). The file is created once and never deleted: a stale file is not a stale lock, and deleting it would let two holders open two different files.
/// </para>
/// <para>
/// <b>When the holder does NOT die: the wedged holder.</b> A holder that is stopped or hung (measured with <c>SIGSTOP</c>) keeps the lock until something kills it; the kernel has no reason to free it. Every waiter is bounded only by its own
/// ceiling or token — this class has none to give (below) — so the caller that waits MUST carry a ceiling and say so to the operator (E2 §8 item 57, owner E2.S7). To make the wedge findable, a holder writes <c>.gate.lock.holder</c> (see
/// <see cref="GateHolderNote"/>: pid, process start, acquisition time) once it holds the lock, and <c>gate-lock status</c> prints <c>held by pid N since &lt;utc&gt;</c> when that names a live holder. The note is a hint; the lock is the truth.
/// </para>
/// <para>
/// <b>An inherited handle.</b> The runtime opens the descriptor close-on-exec, so a child launched while the lock is held (the gate step starts the arm's claude under the lock) does not receive it; <c>FileShare.Inheritable</c> is never passed, because
/// on Linux it makes the runtime take <c>LOCK_SH</c> instead (measured by a break-it: the lock then conflicts with nobody) — D10's warning. Two tests watch it: <c>A_child_launched_while_the_lock_is_held_does_not_keep_it</c> (release by
/// <see cref="IDisposable.Dispose"/> with a child alive — the runtime unlocks explicitly on release, so that one cannot see the flag), and <c>A_holder_killed_while_its_child_lives_frees_the_lock_and_the_child_holds_no_descriptor_of_it</c>, which
/// kills a holder that started a child and then reads the child's descriptors: with the flag cleared the child keeps the lock.
/// </para>
/// <para>
/// <b>Which file systems honour it — measured, not assumed.</b> On .NET 10.0.14 inside the WSL VM the lock was honoured on ext4, on tmpfs and on the 9p mount of the Windows drive (<c>/mnt/c</c>): an in-process double open, a
/// <c>flock(1)</c> pair and two <c>hold</c> processes through a link to <c>/mnt/c</c> all serialised. A Windows process does NOT see a lock taken inside the VM on the same file. <b>NFS and CIFS were not probed.</b> A file system that does
/// not support <c>flock</c> makes the runtime IGNORE the lock silently (its source: "the only error we care about is EWOULDBLOCK"), so two holders could both be told they hold it; nothing here detects that.
/// </para>
/// <para>
/// <b>The other residuals.</b> The lock is advisory on Linux: only a process that asks for it respects it. A runtime started with <c>DOTNET_SYSTEM_IO_DISABLEFILELOCKING</c> or the <c>System.IO.DisableFileLocking</c> switch takes no lock at all
/// (measured: a second open in the same process then succeeds): construction REFUSES that (<see cref="Refusal"/>). Only Linux and Windows have a conflict code that was read from a source; construction fails closed on any other platform.
/// </para>
/// <para>
/// <b>Every wait has a ceiling — the caller's.</b> <see cref="AcquireAsync"/> waits as long as its token lets it; the lock cannot know how long a gate step may be queued, so the token carries the ceiling (the verb gives one; the gate step gives
/// its own). A poll never spins: <paramref name="poll"/> must be positive.
/// </para></summary>
public sealed class FileGateLock : IGateLock
{
    public const string Refusal = "file locking is disabled in this runtime — the gate lock would be inert";

    private const string DisableVariable = "DOTNET_SYSTEM_IO_DISABLEFILELOCKING";

    private const string DisableSwitch = "System.IO.DisableFileLocking";

    /// <summary>Linux: <c>EWOULDBLOCK</c> (= <c>EAGAIN</c>), the raw errno the runtime puts in the exception's <c>HResult</c> (observed).</summary>
    private const int LinuxWouldBlock = 11;

    /// <summary>Windows: <c>ERROR_SHARING_VIOLATION</c> and <c>ERROR_LOCK_VIOLATION</c> as HRESULTs (the Win32 documentation, not observed).</summary>
    private const int WindowsSharingViolation = unchecked((int)0x80070020);

    private const int WindowsLockViolation = unchecked((int)0x80070021);

    private readonly TimeProvider _clock;
    private readonly TimeSpan _poll;
    private readonly int[] _conflicts;

    /// <summary>Throws <see cref="InvalidOperationException"/> with <see cref="Refusal"/> (or the platform's) when this runtime or host cannot lock; <see cref="Create"/> says the same as a value.</summary>
    public FileGateLock(TimeProvider clock, TimeSpan poll)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(poll, TimeSpan.Zero);
        _clock = clock;
        _poll = poll;
        _conflicts = Unrefused().Match(codes => codes, reason => throw new InvalidOperationException(reason));
    }

    /// <summary>A test's hand on the window between the path check and the open. Internal: production code never sets it.</summary>
    internal Action<string>? AfterJudged { get; init; }

    /// <summary>A test's hand on the window between the open and the comparison of the handle with the path. Internal: production code never sets it.</summary>
    internal Action<string>? AfterOpened { get; init; }

    /// <summary>The lock, or why this runtime or platform cannot give one: the refusal is an expected answer of the environment (exit 3 in the verb), not an exception.</summary>
    public static Outcome<FileGateLock> Create(TimeProvider clock, TimeSpan poll) =>
        Unrefused().Match(_ => Outcome<FileGateLock>.Success(new FileGateLock(clock, poll)), Outcome<FileGateLock>.Failure);

    /// <summary>Whether the runtime takes no file lock, as it reads its own switches (measured on .NET 10.0.14, Linux): the variable decides when it reads as a boolean (<c>1</c>/<c>true</c>, <c>0</c>/<c>false</c>, any case, no trimming — the
    /// runtime does not trim), any other value leaves the decision to the <c>System.IO.DisableFileLocking</c> switch, and neither set means locking is on.</summary>
    internal static bool LockingIsOff(string? variable, bool? switchValue) => Boolean(variable) ?? switchValue ?? false;

    private static bool? Boolean(string? text) =>
        text?.ToUpperInvariant() switch
        {
            "1" or "TRUE" => true,
            "0" or "FALSE" => false,
            _ => null,
        };

    /// <summary>One attempt, the road every other member goes through. <paramref name="create"/>: make the file when it is absent (<c>status</c> passes false and creates nothing); <paramref name="note"/>: this caller is a holder, so write
    /// <c>.gate.lock.holder</c> (<c>status</c> passes false: it only looks). The answer says which of five facts it was (<see cref="GateLockAnswer"/>).</summary>
    public GateLockAnswer Take(string path, bool create, bool note) =>
        GateLockPath.Of(path) switch
        {
            GateLockPath.Refused refused => new GateLockAnswer.Refused(refused.Reason),
            GateLockPath.Unread unread => new GateLockAnswer.Unread(unread.Reason),
            GateLockPath.Missing when !create => new GateLockAnswer.Absent(),
            GateLockPath.Missing => Opened(path, FileMode.CreateNew, note),
            _ => Opened(path, FileMode.Open, note),
        };

    /// <summary>The held lock — the open stream, with the holder's note written — or <c>null</c> when another holder has it. A path the lock may not use throws <see cref="GateLockRefusedException"/>: the port has no other way to say it, and a caller
    /// that wants the answer as a value uses <see cref="Take"/>.</summary>
    public IDisposable? TryAcquire(string path) =>
        Take(path, create: true, note: true) switch
        {
            GateLockAnswer.Held held => held.Handle,
            GateLockAnswer.Refused refused => throw new GateLockRefusedException(refused.Reason),
            GateLockAnswer.Unread unread => throw new GateLockRefusedException(unread.Reason),
            _ => null,
        };

    /// <summary>Waits until the lock is free and returns it held. Cancellation throws <see cref="OperationCanceledException"/> and holds nothing: the token is looked at before every try, so a waiter told to stop never takes the lock. A path the lock
    /// may not use throws <see cref="GateLockRefusedException"/>.</summary>
    public async Task<IDisposable> AcquireAsync(string path, CancellationToken cancellationToken) =>
        await AcquireAnswerAsync(path, cancellationToken) switch
        {
            GateLockAnswer.Held held => held.Handle,
            GateLockAnswer.Refused refused => throw new GateLockRefusedException(refused.Reason),
            GateLockAnswer.Unread unread => throw new GateLockRefusedException(unread.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary><see cref="AcquireAsync"/> with the answer as a value: <c>Held</c>, or the <c>Refused</c> or <c>Unread</c> that ended the wait (never <c>Busy</c>: that is what it waits out).</summary>
    public async Task<GateLockAnswer> AcquireAnswerAsync(string path, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var answer = Take(path, create: true, note: true);
            if (answer is not (GateLockAnswer.Busy or GateLockAnswer.Absent))
            {
                return answer;
            }

            await Task.Delay(_poll, _clock, cancellationToken);
        }
    }

    /// <summary>The words <c>status</c> prints for a lock somebody has, after <c>held</c>.</summary>
    public static string WhoHolds(string path) => GateHolderNote.Describe(path);

    private GateLockAnswer Opened(string path, FileMode mode, bool note)
    {
        AfterJudged?.Invoke(path);
        return Open(path, mode) is { } stream ? VerifiedOrHeld(stream, path, note) : NotTaken(path, mode);
    }

    /// <summary>No stream: an existing file that is gone is <c>Absent</c>; anything else — another holder, a create that lost the race — is <c>Busy</c> and is judged again on the next try.</summary>
    private static GateLockAnswer NotTaken(string path, FileMode mode) =>
        mode == FileMode.Open && !Path.Exists(path) ? new GateLockAnswer.Absent() : new GateLockAnswer.Busy();

    /// <summary>The opened file is compared with the path; a mismatch drops it and answers the refusal, else it is the held lock (and, for a holder, its note).</summary>
    private GateLockAnswer VerifiedOrHeld(FileStream stream, string path, bool note)
    {
        AfterOpened?.Invoke(path);
        return Verified(stream, path) is { } refused ? Dropped(stream, refused) : new GateLockAnswer.Held(new HeldLock(stream, path, note ? _clock.GetUtcNow() : null));
    }

    private static GateLockAnswer Dropped(FileStream stream, GateLockAnswer refused)
    {
        stream.Dispose();
        return refused;
    }

    /// <summary><c>null</c> when the lock is held by someone else (a sharing conflict is the lock's answer), and for a create that lost the race to a file that appeared first: that file is judged again on the next try, a planted link refused then.</summary>
    private FileStream? Open(string path, FileMode mode)
    {
        try
        {
            return new FileStream(path, mode, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex) when (IsConflict(ex, _conflicts) || (mode == FileMode.CreateNew && Path.Exists(path)))
        {
            return null;
        }
        catch (FileNotFoundException) when (mode == FileMode.Open)
        {
            return null;
        }
    }

    /// <summary>After the open: the file the handle holds is the file the path names — a regular file, the same device and inode. <c>null</c> when it is; else the refusal. Off Windows only: there the POSIX facts do not exist.</summary>
    private static GateLockAnswer? Verified(FileStream stream, string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var held = KernelFacts.ReadOpen((int)stream.SafeFileHandle.DangerousGetHandle(), path);
        return GateLockPath.Of(path) switch
        {
            GateLockPath.Regular named when held is FactsRead.Read open => Same(open.Facts.Identity, named.Facts.Identity, path),
            GateLockPath.Refused refused => new GateLockAnswer.Refused(refused.Reason),
            GateLockPath.Unread unread => new GateLockAnswer.Unread(unread.Reason),
            _ => new GateLockAnswer.Refused($"{path} changed between its check and its open — refused (D17)"),
        };
    }

    private static GateLockAnswer? Same(FileIdentity open, FileIdentity named, string path) =>
        open.Device == named.Device && open.Inode == named.Inode
            ? null
            : new GateLockAnswer.Refused($"{path} is not the file that was opened (device {open.Device} inode {open.Inode} held, device {named.Device} inode {named.Inode} named) — it was swapped while the lock was taken, refused (D17)");

    /// <summary>A sharing conflict is an <see cref="IOException"/> of exactly that type (not a <see cref="DirectoryNotFoundException"/> or another subclass) carrying the platform's code.</summary>
    private static bool IsConflict(IOException ex, int[] conflicts) => ex.GetType() == typeof(IOException) && conflicts.Contains(ex.HResult);

    private static Outcome<int[]> Unrefused() =>
        LockingIsOff(Environment.GetEnvironmentVariable(DisableVariable), AppContext.TryGetSwitch(DisableSwitch, out var off) ? off : null)
            ? Outcome<int[]>.Failure(Refusal)
            : ConflictCodes();

    /// <summary>The platform → conflict code mapping, explicit and with no catch-all: a platform nobody probed fails closed.</summary>
    private static Outcome<int[]> ConflictCodes() =>
        OperatingSystem.IsLinux() ? Outcome<int[]>.Success([LinuxWouldBlock])
        : OperatingSystem.IsWindows() ? Outcome<int[]>.Success([WindowsSharingViolation, WindowsLockViolation])
        : Outcome<int[]>.Failure("the gate lock's conflict code was read for Linux and Windows only — this platform was not measured, so no lock is attempted");

    /// <summary>The held lock: the stream, and — for a holder — the note that goes with it. Released once; the note is removed BEFORE the lock goes, so a next holder's note is never the one deleted.</summary>
    private sealed class HeldLock : IDisposable
    {
        private readonly FileStream _stream;
        private readonly string _path;
        private readonly bool _noted;
        private int _released;

        public HeldLock(FileStream stream, string path, DateTimeOffset? since)
        {
            _stream = stream;
            _path = path;
            _noted = since is not null;
            if (since is { } at)
            {
                GateHolderNote.Write(path, at);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1)
            {
                return;
            }

            if (_noted)
            {
                GateHolderNote.Remove(_path);
            }

            _stream.Dispose();
        }
    }
}

/// <summary>The path of the gate lock is one the lock may not use (a link, a folder, a FIFO, a file swapped under the check) or could not be read. Only the port-shaped members (<c>TryAcquire</c>, <c>AcquireAsync</c>) throw it, because the
/// port has no other way to say it; <see cref="FileGateLock.Take"/> returns the same fact as a value. The message names the path, what it is and D17.</summary>
public sealed class GateLockRefusedException(string reason) : InvalidOperationException(reason);
