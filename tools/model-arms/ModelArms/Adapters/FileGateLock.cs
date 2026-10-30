using Bench.Domain;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The gate lock (plan D10, C7): one gate step at a time across every arm of an experiment, as an OS file lock taken through .NET — never through <c>flock(1)</c> (the shell's way; nothing the module starts is looked
/// up by name, and a lock against a non-.NET holder is out of bounds, parent §9). It is NOT D11's lock (<c>.isolation.lock</c>); the two files are different locks with different holders.
/// <para>
/// <b>The atomic operation it rests on</b> (common.reliability): the kernel's own open-with-no-sharing. <see cref="TryAcquire"/> opens <c>.gate.lock</c> with <see cref="FileShare.None"/>. On Linux the runtime takes
/// <c>flock(LOCK_EX | LOCK_NB)</c> on the descriptor and maps <c>EWOULDBLOCK</c> to an <see cref="IOException"/> whose <c>HResult</c> is the raw errno 11 (read from the runtime's source and observed on .NET 10.0.14); on Windows the share
/// mode is enforced by the file system's own <c>CreateFile</c> and the exception carries <c>ERROR_SHARING_VIOLATION</c> or <c>ERROR_LOCK_VIOLATION</c> (<c>0x80070020</c>, <c>0x80070021</c> — from the Win32 documentation, NOT observed:
/// nothing here ran on Windows). The held lock IS the open stream; <see cref="IDisposable.Dispose"/> closes it.
/// </para>
/// <para>
/// <b>When the holder dies.</b> The lock belongs to the open file description, not to a record anyone wrote, so there is no heartbeat to renew and no id to fence: a holder that is killed (<c>SIGKILL</c> included — observed
/// on this host: a holder killed while a second process had just been refused, and the next try succeeded at once), crashes or exits has its descriptors closed by the kernel, and the lock is free the same instant. On Windows the
/// handle is closed by the system when the process ends, which frees the share mode the same way (documented, not observed). The file is created once and never deleted: a stale file is not a stale lock, and deleting it would let two
/// holders open two different files.
/// </para>
/// <para>
/// <b>An inherited handle.</b> The runtime opens the descriptor close-on-exec, so a child launched while the lock is held (the gate step starts the arm's claude under the lock) does not receive it;
/// <c>FileShare.Inheritable</c> is never passed, because on Linux it makes the runtime take <c>LOCK_SH</c> instead (measured by a break-it: the lock then conflicts with nobody) — D10's warning. What the test
/// <c>A_child_launched_while_the_lock_is_held_does_not_keep_it</c> shows is the release by <see cref="IDisposable.Dispose"/> with a child alive; it cannot see the close-on-exec flag itself: measured on .NET 10.0.14, with the flag
/// CLEARED by <c>fcntl</c> and a child alive, the lock was still free after <c>Dispose</c>, because the runtime unlocks explicitly on release. The case the flag protects — a holder that DIES while its child lives — is argued, not tested.
/// </para>
/// <para>
/// <b>The residual, stated.</b> (1) The lock is advisory on Linux: only a process that asks for it respects it. (2) A file system that does not support <c>flock</c> (some network mounts) makes the runtime IGNORE the lock silently
/// — the runtime's source says "the only error we care about is EWOULDBLOCK" — so two holders could both be told they hold it; the experiment root is a local folder by D17's trust check, and nothing here probed another file system.
/// (3) A runtime started with <c>DOTNET_SYSTEM_IO_DISABLEFILELOCKING</c> or the <c>System.IO.DisableFileLocking</c> switch takes no lock at all (measured: a second open in the same process then succeeds): construction REFUSES that (<see cref="Refusal"/>).
/// (4) Only Linux and Windows have a conflict code that was read from a source; construction fails closed on any other platform instead of guessing one.
/// </para>
/// <para>
/// <b>Every wait has a ceiling — the caller's.</b> <see cref="AcquireAsync"/> waits as long as its token lets it; the lock cannot know how long a gate step may be queued, so the token carries the ceiling (the verb gives one;
/// the gate step gives its own). A poll never spins: <paramref name="poll"/> must be positive.
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

    /// <summary>The lock, or why this runtime or platform cannot give one: the refusal is an expected answer of the environment (exit 3 in the verb), not an exception.</summary>
    public static Outcome<FileGateLock> Create(TimeProvider clock, TimeSpan poll) =>
        Unrefused().Match(_ => Outcome<FileGateLock>.Success(new FileGateLock(clock, poll)), Outcome<FileGateLock>.Failure);

    /// <summary>Whether the runtime takes no file lock, as it reads its own switches (measured on .NET 10.0.14, Linux): the variable decides when it reads as a boolean (<c>1</c>/<c>true</c>, <c>0</c>/<c>false</c>, any case), any
    /// other value leaves the decision to the <c>System.IO.DisableFileLocking</c> switch, and neither set means locking is on.</summary>
    internal static bool LockingIsOff(string? variable, bool? switchValue) => Boolean(variable) ?? switchValue ?? false;

    private static bool? Boolean(string? text) =>
        text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ? true
        : text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ? false
        : null;

    /// <summary>The held lock — the open stream — or <c>null</c> when another holder has it. A sharing conflict is the only IOException answered as null; every other fault (a missing folder, a refused permission) is unexpected and flies.</summary>
    public IDisposable? TryAcquire(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex) when (IsConflict(ex, _conflicts))
        {
            return null;
        }
    }

    /// <summary>Waits until the lock is free and returns it held. Cancellation throws <see cref="OperationCanceledException"/> and holds nothing: the token is looked at before every try, so a waiter told to stop never takes the lock.</summary>
    public async Task<IDisposable> AcquireAsync(string path, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryAcquire(path) is { } held)
            {
                return held;
            }

            await Task.Delay(_poll, _clock, cancellationToken);
        }
    }

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
}
