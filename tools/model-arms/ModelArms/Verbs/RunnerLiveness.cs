using System.Globalization;
using System.Text;
using Bench.Infrastructure.Process;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>Whether an arm's runner is alive (C21): <b>the runner's lock</b> decides, never a pid (D13). A <c>run-arm</c> holds <c>pids/&lt;arm&gt;.lock</c> for its life, and the kernel frees it the instant the process dies, SIGKILL included.</summary>
internal abstract record RunnerState
{
    private RunnerState()
    {
    }

    /// <summary>The lock is held. <see cref="Holder"/> is the words after <i>running</i>: <c>pid N</c> when the pid file's pid is believed, else why not; <see cref="BelievedPid"/> is N when believed.</summary>
    public sealed record Alive(string Holder, int? BelievedPid) : RunnerState;

    /// <summary>The lock is free, or its file absent (nothing is created), and no believed pid says otherwise.</summary>
    public sealed record Gone : RunnerState;

    /// <summary>The lock's path is not a regular file of the root's (D17) → exit 4.</summary>
    public sealed record Refused(string Reason) : RunnerState;

    /// <summary>The host could not say → exit 3.</summary>
    public sealed record Unread(string Reason) : RunnerState;
}

/// <summary>C21. <c>Of</c> is a PROBE: it takes <c>pids/&lt;arm&gt;.lock</c> with <c>create: false, note: false</c> (so it creates nothing and writes no holder note) and releases a lock it gets before it returns. <c>Busy</c> is <see cref="RunnerState.Alive"/>; a lock that is free or absent
/// lets the pid file decide (finding 11) — an unlinked or replaced lock file must not read a live runner as gone, because the runner holds the OLD inode: a pid file naming a pid BELIEVED by its command line (Linux) or live (the other roads) is still <c>Alive</c>, said as such.
/// <para>
/// <b>The belief</b> (Linux, <see cref="HolderRoad.ProcTicks"/>): <c>/proc/&lt;pid&gt;/cmdline</c> read as bytes, split on NUL — believed when some word is <c>run-arm</c>, followed by the arm, and preceded by a word whose file NAME is <c>model-arms</c>, <c>model-arms.dll</c> or <c>model-arms.exe</c> (M-6: argv[0] is the exec'd spelling — a real path, a link's name, or <c>dotnet</c> with the dll at argv[1];
/// the file name is what the three share; <c>/proc/&lt;pid&gt;/exe</c> is not compared, because under <c>dotnet model-arms.dll</c> and in an in-process test it is not this binary). The residual, said: a reused pid whose argv happens to read <c>… model-arms run-arm C1</c> is believed; the lock, not the pid, decides — except when the lock FILE is gone.
/// </para>
/// <para>
/// D26 a: the lock's path is judged by <see cref="GateLockPath"/> through <c>FileGateLock</c>; the pid file is read through <see cref="BoundedFile"/> (64 bytes, no link followed); the command line comes from <c>/proc</c>, named by pid. D26 b–f: nothing is launched; nothing is written; the lock file is never created here.
/// </para></summary>
internal static class RunnerLiveness
{
    /// <summary>How long a runner waits for the probe's momentary hold of its lock (and for an exclusive writer's), polling every 250 ms: a real holder holds for hours, a probe for microseconds.</summary>
    public const int RunnerLockWaitMilliseconds = 2000;

    private const int PidFileLimit = 64;
    private const int CommandLineLimit = 64 * 1024;
    private static readonly string[] ModuleNames = ["model-arms", "model-arms.dll", "model-arms.exe"];

    public static RunnerState Of(RootPaths root, string arm) =>
        FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(250)).Match(
            gate => Of(root, arm, PlatformFactsReader.CurrentRoad, gate),
            reason => new RunnerState.Unread(reason));

    public static RunnerState Of(RootPaths root, string arm, PlatformRoad road, FileGateLock gate)
    {
        var path = root.RunnerLock(arm);
        switch (gate.Take(path, create: false, note: false))
        {
            case GateLockAnswer.Busy:
                return Held(root, arm, road, gate, path);
            case GateLockAnswer.Refused refused:
                return new RunnerState.Refused(refused.Reason);
            case GateLockAnswer.Unread unread:
                return new RunnerState.Unread(unread.Reason);
            case GateLockAnswer.Held held:
                held.Handle.Dispose();
                return Unlinked(root, arm, road);
            default:
                return Unlinked(root, arm, road);
        }
    }

    /// <summary>The line the portable roads print once, before their first liveness answer (ledger 2.1): empty on Linux.</summary>
    public static string PortableLine(PlatformRoad road) =>
        road.Holder == HolderRoad.ProcTicks
            ? string.Empty
            : $"runner liveness on {road.Platform}: the lock decides — a pid is NOT checked against its command line here, so a pid reused after a reboot can read as a runner's (D13; ledger 2.1)";

    private static RunnerState Held(RootPaths root, string arm, PlatformRoad road, FileGateLock gate, string path) =>
        PidOf(root, arm, road) switch
        {
            PidRead.None => new RunnerState.Alive($"no pid file yet — the lock is held {gate.Describe(path)}", null),
            PidRead.Unreadable unreadable => new RunnerState.Alive($"pid file unreadable — {unreadable.Why}", null),
            PidRead.Pid pid => Judged(pid.Value, arm, road),
            _ => new RunnerState.Alive("pid file unreadable", null),
        };

    /// <summary>The lock is free or its file gone: a pid file naming a believed pid still says <i>alive</i> (the runner holds the old inode); anything else is gone.</summary>
    private static RunnerState Unlinked(RootPaths root, string arm, PlatformRoad road) =>
        PidOf(root, arm, road) is PidRead.Pid pid && Judged(pid.Value, arm, road) is RunnerState.Alive { BelievedPid: { } believed }
            && (road.Holder == HolderRoad.ProcTicks || WorkerLiveness.ProcessIsAlive(believed))
            ? new RunnerState.Alive($"pid {believed} — its lock file is gone or replaced (unlinked since it started); believed by its command line", believed)
            : new RunnerState.Gone();

    private static RunnerState Judged(int pid, string arm, PlatformRoad road)
    {
        if (road.Holder != HolderRoad.ProcTicks)
        {
            return new RunnerState.Alive($"pid {pid.ToString(CultureInfo.InvariantCulture)} (not checked against its command line on {road.Platform})", pid);
        }

        return CommandLineOf(pid) switch
        {
            Argv.Unreadable unreadable => new RunnerState.Alive($"pid {pid} not believed — /proc/{pid}/cmdline could not be read: {unreadable.Why}", null),
            Argv.Words words when IsRunnerOf(words.Value, arm) => new RunnerState.Alive($"pid {pid}", pid),
            Argv.Words words => new RunnerState.Alive($"pid {pid} is not this arm's runner (its command line is {Shown(words.Value)}) — read as gone", null),
            _ => new RunnerState.Alive($"pid {pid} not believed", null),
        };
    }

    private static bool IsRunnerOf(IReadOnlyList<string> argv, string arm)
    {
        for (var at = 1; at + 1 < argv.Count; at++)
        {
            if (argv[at] == "run-arm" && argv[at + 1] == arm && ModuleNames.Contains(Path.GetFileName(argv[at - 1]), StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The first three words of a command line, a path shown by its file name — the part the belief compares, which a cut of a long absolute path would hide — printable and at most 60 characters.</summary>
    private static string Shown(IReadOnlyList<string> argv) =>
        ExecutableRule.Printable(ExecutableRule.Cut(string.Join(' ', argv.Take(3).Select(word => word.Length == 0 ? "''" : word.StartsWith('/') ? Path.GetFileName(word) : word)), 60));

    private abstract record PidRead
    {
        private PidRead()
        {
        }

        public sealed record None : PidRead;

        public sealed record Unreadable(string Why) : PidRead;

        public sealed record Pid(int Value) : PidRead;
    }

    private static PidRead PidOf(RootPaths root, string arm, PlatformRoad road) =>
        BoundedFile.Read(root.PidFile(arm), PidFileLimit, road) switch
        {
            BoundedRead.Bytes bytes => RunnerPid.Parse(Encoding.ASCII.GetString(bytes.Value).Trim()) is { } pid ? new PidRead.Pid(pid) : new PidRead.Unreadable("it holds no pid"),
            BoundedRead.Refused refused => new PidRead.Unreadable(refused.Reason),
            _ => new PidRead.None(),
        };

    private abstract record Argv
    {
        private Argv()
        {
        }

        public sealed record Words(IReadOnlyList<string> Value) : Argv;

        public sealed record Unreadable(string Why) : Argv;
    }

    private static Argv CommandLineOf(int pid)
    {
        try
        {
            using var stream = new FileStream($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/cmdline", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[CommandLineLimit];
            var total = 0;
            while (total < buffer.Length && stream.Read(buffer, total, buffer.Length - total) is var n and > 0)
            {
                total += n;
            }

            return new Argv.Words(Encoding.UTF8.GetString(buffer, 0, total).TrimEnd('\0').Split('\0', StringSplitOptions.None).ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Argv.Unreadable(ex.Message);
        }
    }
}
