using System.Runtime.InteropServices;

namespace ModelArms.Adapters;

/// <summary>What <c>posix_spawn</c> answered: <see cref="Error"/> is its RETURN (0 = spawned; any other value is the error number — <c>posix_spawn</c> sets no <c>errno</c>), <see cref="Pid"/> the child's when it was.</summary>
internal sealed record SpawnResult(int Pid, int Error);

/// <summary>What one non-blocking <c>waitpid</c> of the intermediate said (closed).</summary>
internal abstract record ReapState
{
    private ReapState()
    {
    }

    /// <summary>Still running.</summary>
    public sealed record Running : ReapState;

    /// <summary>Reaped: <paramref name="Status"/> is the raw wait status.</summary>
    public sealed record Ended(int Status) : ReapState
    {
        public bool Exited => (Status & 0x7f) == 0;

        public int Code => (Status >> 8) & 0xff;

        public int Signal => Status & 0x7f;
    }

    /// <summary>Not ours to wait for any more: the runtime already reaped it (<c>ECHILD</c>).</summary>
    public sealed record AlreadyReaped : ReapState;

    /// <summary>The call itself failed (<paramref name="Error"/> is <c>errno</c>).</summary>
    public sealed record Failed(int Error) : ReapState;
}

/// <summary>The libc calls <see cref="SetsidLauncher"/> starts a detached program with, and the constants and sizes they are called with — the ONE place the module names them (D26 a: the kernel's numbers, not the runtime's). <b>Measured on Linux x86-64</b> (glibc 2.43, 2026-10-31; E5 plan M-5, re-measured with a C probe for this story): <c>sizeof(posix_spawn_file_actions_t)</c>
/// 80, <c>posix_spawnattr_t</c> 336, <c>sigset_t</c> 128; <c>O_WRONLY 0x1</c>, <c>O_CREAT 0x40</c>, <c>O_APPEND 0x400</c>, <c>O_NOFOLLOW 0x20000</c> (on Linux arm64 <c>0x20000</c> is <c>O_LARGEFILE</c> and the link would be FOLLOWED — <see cref="DetachedRoads"/> grants this class to Linux x86-64 alone); <c>POSIX_SPAWN_SETPGROUP 0x02</c>, <c>SETSIGDEF 0x04</c>,
/// <c>SETSIGMASK 0x08</c>; <c>WNOHANG 1</c>; <c>ELOOP 40</c>, <c>ECHILD 10</c>; <c>SIGKILL 9</c>, <c>SIGSTOP 19</c>. Every structure lives in a zeroed buffer of <see cref="Capacity"/> bytes — more than the largest measured size — initialised by libc and destroyed in <c>finally</c>.</summary>
internal static class PosixSpawn
{
    /// <summary>The size of every buffer handed to libc; each measured structure fits (<see cref="LargestMeasured"/>).</summary>
    public const int Capacity = 512;

    public const int FileActionsBytes = 80;
    public const int AttributesBytes = 336;
    public const int SigsetBytes = 128;

    public static int LargestMeasured => Math.Max(FileActionsBytes, Math.Max(AttributesBytes, SigsetBytes));

    public const int ELoop = 40;
    public const int EChild = 10;

    private const int ReadOnly = 0x0;
    private const int WriteOnly = 0x1;
    private const int Create = 0x40;
    private const int Append = 0x400;
    private const int NoFollow = 0x20000;
    private const uint PrivateFile = 0x180;
    private const short SetProcessGroup = 0x02;
    private const short SetSignalsDefault = 0x04;
    private const short SetSignalMask = 0x08;
    private const int NoHang = 1;
    private const int Kill = 9;
    private const int Stop = 19;
    private const int LastCatchable = 31;
    private const string NullDevice = "/dev/null";

    /// <summary>Starts <paramref name="program"/> (a path: <c>posix_spawn</c>, never <c>posix_spawnp</c>) with <paramref name="argv"/> and <paramref name="environment"/> (<c>NAME=value</c>) as the leader of a new process group, stdin <c>/dev/null</c>, stdout and stderr the <paramref name="log"/> opened for append, created at 0600 and
    /// <b>refusing a link</b> (<c>O_NOFOLLOW</c> — D26 a: a log that is a link fails the start with <c>ELOOP</c>), every signal 1–31 but 9 and 19 at its default and the mask empty (without it SIGPIPE, ignored by this runtime, stays ignored across <c>exec</c> — M-3), the working directory <paramref name="workingDirectory"/>.</summary>
    public static SpawnResult Spawn(string program, IReadOnlyList<string> argv, IReadOnlyList<string> environment, string log, string workingDirectory)
    {
        var buffers = new List<IntPtr>();
        var strings = new List<IntPtr>();
        IntPtr actions = Zeroed(buffers), attributes = Zeroed(buffers), defaults = Zeroed(buffers), mask = Zeroed(buffers);
        try
        {
            var prepared = FirstError(
                () => ActionsInit(actions),
                () => ActionsAddOpen(actions, 0, NullDevice, ReadOnly, 0),
                () => ActionsAddOpen(actions, 1, log, WriteOnly | Append | Create | NoFollow, PrivateFile),
                () => ActionsAddDup2(actions, 1, 2),
                () => ActionsAddChdir(actions, workingDirectory),
                () => AttributesInit(attributes),
                () => AttributesSetFlags(attributes, (short)(SetProcessGroup | SetSignalsDefault | SetSignalMask)),
                () => AttributesSetGroup(attributes, 0),
                () => DefaultSignals(defaults),
                () => AttributesSetDefaults(attributes, defaults),
                () => SigEmptySet(mask),
                () => AttributesSetMask(attributes, mask));
            if (prepared != 0)
            {
                return new SpawnResult(0, prepared);
            }

            var error = SpawnCall(out var pid, program, actions, attributes, Terminated(argv, strings), Terminated(environment, strings));
            return new SpawnResult(error == 0 ? pid : 0, error);
        }
        finally
        {
            ActionsDestroy(actions);
            AttributesDestroy(attributes);
            Free(buffers, strings);
        }
    }

    /// <summary>One non-blocking <c>waitpid</c> of <paramref name="pid"/>.</summary>
    public static ReapState Reap(int pid)
    {
        var reaped = WaitPid(pid, out var status, NoHang);
        return reaped switch
        {
            0 => new ReapState.Running(),
            var same when same == pid => new ReapState.Ended(status),
            _ => Marshal.GetLastPInvokeError() == EChild ? new ReapState.AlreadyReaped() : new ReapState.Failed(Marshal.GetLastPInvokeError()),
        };
    }

    private static int FirstError(params Func<int>[] steps)
    {
        foreach (var step in steps)
        {
            if (step() is var error and not 0)
            {
                return error;
            }
        }

        return 0;
    }

    private static int DefaultSignals(IntPtr set)
    {
        var error = SigEmptySet(set);
        for (var signal = 1; signal <= LastCatchable && error == 0; signal++)
        {
            error = signal is Kill or Stop ? 0 : SigAddSet(set, signal);
        }

        return error;
    }

    private static IntPtr Zeroed(List<IntPtr> buffers)
    {
        var buffer = Marshal.AllocHGlobal(Capacity);
        Marshal.Copy(new byte[Capacity], 0, buffer, Capacity);
        buffers.Add(buffer);
        return buffer;
    }

    /// <summary>The words as a NULL-terminated array of UTF-8 C strings; each string is kept in <paramref name="strings"/> to be freed.</summary>
    private static IntPtr[] Terminated(IReadOnlyList<string> words, List<IntPtr> strings)
    {
        var array = new IntPtr[words.Count + 1];
        for (var at = 0; at < words.Count; at++)
        {
            array[at] = Marshal.StringToCoTaskMemUTF8(words[at]);
            strings.Add(array[at]);
        }

        return array;
    }

    private static void Free(List<IntPtr> buffers, List<IntPtr> strings)
    {
        buffers.ForEach(Marshal.FreeHGlobal);
        strings.ForEach(Marshal.FreeCoTaskMem);
    }

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
    private static extern int ActionsInit(IntPtr actions);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
    private static extern int ActionsDestroy(IntPtr actions);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addopen")]
    private static extern int ActionsAddOpen(IntPtr actions, int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
    private static extern int ActionsAddDup2(IntPtr actions, int fd, int newFd);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addchdir_np")]
    private static extern int ActionsAddChdir(IntPtr actions, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport("libc", EntryPoint = "posix_spawnattr_init")]
    private static extern int AttributesInit(IntPtr attributes);

    [DllImport("libc", EntryPoint = "posix_spawnattr_destroy")]
    private static extern int AttributesDestroy(IntPtr attributes);

    [DllImport("libc", EntryPoint = "posix_spawnattr_setflags")]
    private static extern int AttributesSetFlags(IntPtr attributes, short flags);

    [DllImport("libc", EntryPoint = "posix_spawnattr_setpgroup")]
    private static extern int AttributesSetGroup(IntPtr attributes, int group);

    [DllImport("libc", EntryPoint = "posix_spawnattr_setsigdefault")]
    private static extern int AttributesSetDefaults(IntPtr attributes, IntPtr signals);

    [DllImport("libc", EntryPoint = "posix_spawnattr_setsigmask")]
    private static extern int AttributesSetMask(IntPtr attributes, IntPtr signals);

    [DllImport("libc", EntryPoint = "sigemptyset")]
    private static extern int SigEmptySet(IntPtr signals);

    [DllImport("libc", EntryPoint = "sigaddset")]
    private static extern int SigAddSet(IntPtr signals, int signal);

    [DllImport("libc", EntryPoint = "posix_spawn")]
    private static extern int SpawnCall(out int pid, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr actions, IntPtr attributes, IntPtr[] argv, IntPtr[] environment);

    [DllImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    private static extern int WaitPid(int pid, out int status, int options);
}
