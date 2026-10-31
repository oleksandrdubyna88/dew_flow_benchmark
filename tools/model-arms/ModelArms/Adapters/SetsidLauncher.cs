using System.Runtime.InteropServices;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>D13, C24 — the POSIX start of a detached arm: <c>posix_spawn</c> (<see cref="PosixSpawn"/>) of the RESOLVED <c>setsid</c> with argv <c>[setsid, executable, ..arguments]</c> — a path both, never a name — as the leader of its own process group, so <c>setsid</c> FORKS (measured: started by <c>Process.Start</c> it does not and is the runner; started as a group leader it does, M-1, M-2) and the runner
/// is reparented away from this process instead of staying its child and, once finished, a zombie (M-4). The redirection is the kernel's, through file actions: stdin <c>/dev/null</c>, stdout and stderr the log, opened for append, created 0600, <c>O_NOFOLLOW</c>. No pid is read back (D13): the intermediate <c>setsid</c> is only reaped, within 10 s; the runner names itself and holds its own lock.
/// <para>
/// <b>D26 (a)–(f), by D26's own meanings.</b> (a) the log is opened by the KERNEL with <c>O_NOFOLLOW</c> (a link there is <c>ELOOP</c>: exit 4, nothing started) — no textual path join decides it; (b) nothing launches by a bare name: <c>setsid</c> and the program are the real paths <c>ExecutableResolution</c> found, the environment is the map the verb was handed, the working directory is named, and <c>posix_spawnp</c> (a PATH look-up) is not used —
/// <c>ModelArmsArchitectureTests</c> scans for it; (c) the PATH and the configuration folders the runner will inherit are judged by <c>run-all</c> before this is reached (<see cref="LaunchAdmission"/>); (d) a <c>&lt;fill me&gt;</c> placeholder cannot arrive: the gate refused the experiment that held one; (e) the root and <c>pids/</c> were made exclusively before (<see cref="RootFolders"/>), not here;
/// (f) the experiment was re-read by the gate before the verb that calls this ran. Constants: measured on Linux x86-64 only, which <see cref="DetachedRoads"/> enforces.
/// </para>
/// <para>
/// <b>Residuals, said:</b> a start that <c>posix_spawn</c> accepted is not a runner that runs — the program's own failure to exec (a wrong path) goes to the log, after this returned <see cref="DetachedStart.Started"/>; liveness is the runner's lock. A program that exits non-zero at once through a <c>setsid</c> that did NOT fork is reported only if it ends within the 10 s.
/// </para></summary>
internal sealed class SetsidLauncher(string setsid, TimeProvider clock) : IDetachedLauncher
{
    private static readonly TimeSpan DetachCeiling = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DetachPoll = TimeSpan.FromMilliseconds(10);

    public SetsidLauncher(string setsid)
        : this(setsid, TimeProvider.System)
    {
    }

    public DetachedStart Start(string executable, IReadOnlyList<string> arguments, string log, IReadOnlyDictionary<string, string> environment, string workingDirectory)
    {
        var spawned = PosixSpawn.Spawn(setsid, [setsid, executable, .. arguments], [.. environment.Select(entry => $"{entry.Key}={entry.Value}")], log, workingDirectory);
        return spawned.Error switch
        {
            0 => Reaped(spawned.Pid, log),
            PosixSpawn.ELoop => new DetachedStart.Failed(4, $"{log} is a link — refused (D17), nothing started"),
            PosixSpawn.ENxio => new DetachedStart.Failed(4, $"{log} is a FIFO with no reader — refused, nothing started"),
            var error => new DetachedStart.Failed(3, $"posix_spawn of {setsid}: {Marshal.GetPInvokeErrorMessage(error)}"),
        };
    }

    /// <summary>The intermediate <c>setsid</c> reaped every 10 ms for at most 10 s; its exit status is the only thing read from it.</summary>
    private DetachedStart Reaped(int pid, string log)
    {
        var started = clock.GetTimestamp();
        while (clock.GetElapsedTime(started) < DetachCeiling)
        {
            if (PosixSpawn.Reap(pid) is var state and not ReapState.Running)
            {
                return Answer(state, log);
            }

            Thread.Sleep(DetachPoll);
        }

        return new DetachedStart.Started("setsid did not detach within 10 s — the runner stays a child of this process");
    }

    /// <summary>What a reap that is not <i>still running</i> says.</summary>
    private DetachedStart Answer(ReapState state, string log) =>
        state switch
        {
            ReapState.Ended { Exited: true, Code: 0 } => new DetachedStart.Started(),
            ReapState.Ended { Exited: true } ended => new DetachedStart.Failed(3, $"setsid exited {ended.Code} — see {log}"),
            ReapState.Ended ended => new DetachedStart.Failed(3, $"setsid was ended by signal {ended.Signal} — see {log}"),
            ReapState.AlreadyReaped => new DetachedStart.Started(),
            ReapState.Failed failed => new DetachedStart.Failed(3, $"waitpid of {setsid}'s start: {Marshal.GetPInvokeErrorMessage(failed.Error)}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
