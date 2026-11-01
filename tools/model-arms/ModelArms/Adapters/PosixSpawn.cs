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

/// <summary>What a test hands <see cref="PosixSpawn.Spawn(string, IReadOnlyList{string}, IReadOnlyList{string}, string, string, SpawnHooks)"/>: the two <c>init</c> calls (so one can fail) and <see cref="Event"/>, told <c>alloc</c> for each allocation, <c>destroy actions</c> / <c>destroy attributes</c> for each structure destroyed and <c>free N</c> at the end. <see cref="Real"/> calls libc and tells nobody.</summary>
internal sealed record SpawnHooks(Func<IntPtr, int> ActionsInit, Func<IntPtr, int> AttributesInit, Action<string>? Event)
{
    public static SpawnHooks Real { get; } = new(PosixSpawn.InitActionsCall, PosixSpawn.InitAttributesCall, null);
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
    public const int ENxio = 6;
    public const int EChild = 10;

    private const int ReadOnly = 0x0;
    private const int WriteOnly = 0x1;
    private const int Create = 0x40;
    private const int Append = 0x400;
    private const int NoFollow = 0x20000;
    private const int NonBlock = 0x800;
    private const int FirstClosed = 3;
    private const string CloseFromEntry = "posix_spawn_file_actions_addclosefrom_np";
    private const uint PrivateFile = 0x180;
    private const short SetProcessGroup = 0x02;
    private const short SetSignalsDefault = 0x04;
    private const short SetSignalMask = 0x08;
    private const int NoHang = 1;
    private const int Kill = 9;
    private const int Stop = 19;
    private const int LastCatchable = 31;
    private const string NullDevice = "/dev/null";

    /// <summary>Whether this libc has <c>posix_spawn_file_actions_addclosefrom_np</c> (glibc 2.34 and later; 2.43 here). Without it the runner would inherit every descriptor of this process that is not close-on-exec, so the road is refused (<see cref="DetachedRoads.Current"/>).</summary>
    internal static int InitActionsCall(IntPtr actions) => ActionsInit(actions);

    internal static int InitAttributesCall(IntPtr attributes) => AttributesInit(attributes);

    public static bool CloseFromAvailable { get; } = NativeLibrary.TryLoad("libc", typeof(PosixSpawn).Assembly, null, out var libc) && NativeLibrary.TryGetExport(libc, CloseFromEntry, out _);

    /// <summary>glibc's version text (<c>2.43</c>) from <c>gnu_get_libc_version</c>, which only glibc exports; empty on any other C library (musl) or when it cannot be asked.</summary>
    public static string GlibcVersion { get; } = AskGlibc();

    private static string AskGlibc()
    {
        try
        {
            return Marshal.PtrToStringUTF8(GnuGetLibcVersion()) ?? string.Empty;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <summary>Starts <paramref name="program"/> (a path: <c>posix_spawn</c>, never <c>posix_spawnp</c>) with <paramref name="argv"/> and <paramref name="environment"/> (<c>NAME=value</c>) as the leader of a new process group, stdin <c>/dev/null</c>, stdout and stderr the <paramref name="log"/> opened for append, created at 0600 and
    /// <b>refusing a link</b> (<c>O_NOFOLLOW</c> — D26 a: a log that is a link fails the start with <c>ELOOP</c>) and <b>not waiting on a FIFO</b> (<c>O_NONBLOCK</c>: a FIFO with no reader fails with <c>ENXIO</c> instead of blocking this process inside <c>posix_spawn</c>; on a regular file the flag changes nothing), every descriptor from 3 up closed in the child (no lock or handle of this process crosses into the runner), every signal 1–31 but 9 and 19 at its default and the mask empty (without it SIGPIPE, ignored by this runtime, stays ignored across <c>exec</c> — M-3), the working directory <paramref name="workingDirectory"/>.</summary>
    public static SpawnResult Spawn(string program, IReadOnlyList<string> argv, IReadOnlyList<string> environment, string log, string workingDirectory) =>
        Spawn(program, argv, environment, log, workingDirectory, SpawnHooks.Real);

    /// <summary>The same with the two <c>init</c> calls and the record of what was allocated, initialised and destroyed handed in — the only way a test makes an <c>init</c> fail (<see cref="SpawnHooks"/>). Cleanup is entered with the first allocation: every buffer is freed, and a structure is destroyed only when its own <c>init</c> succeeded.</summary>
    internal static SpawnResult Spawn(string program, IReadOnlyList<string> argv, IReadOnlyList<string> environment, string log, string workingDirectory, SpawnHooks hooks)
    {
        using var native = new Native(hooks);
        var (actions, attributes, defaults, mask) = (native.Buffer(), native.Buffer(), native.Buffer(), native.Buffer());
        var prepared = FirstError(
            () => native.InitActions(actions),
            () => ActionsAddOpen(actions, 0, NullDevice, ReadOnly, 0),
            () => ActionsAddOpen(actions, 1, log, WriteOnly | Append | Create | NoFollow | NonBlock, PrivateFile),
            () => ActionsAddDup2(actions, 1, 2),
            () => ActionsAddCloseFrom(actions, FirstClosed),
            () => ActionsAddChdir(actions, workingDirectory),
            () => native.InitAttributes(attributes),
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

        var error = SpawnCall(out var pid, program, actions, attributes, native.Terminated(argv), native.Terminated(environment));
        return new SpawnResult(error == 0 ? pid : 0, error);
    }

    /// <summary>What a test needs to know about the buffers: how many bytes of a buffer pre-filled with a sentinel each structure's calls changed — the structure's real extent, observed, not a constant compared with a constant. <c>posix_spawnattr_init</c> and <c>sigemptyset</c> clear their whole structure and
    /// <c>posix_spawn_file_actions_init</c> its fixed part, so each count is the structure's size (336, 128) or its fixed part (≤ 80).</summary>
    internal static (int Actions, int Attributes, int Sigset) BytesTouched()
    {
        const byte sentinel = 0xA5;
        using var native = new Native(SpawnHooks.Real);
        var (actions, attributes, signals) = (native.Buffer(sentinel), native.Buffer(sentinel), native.Buffer(sentinel));
        FirstError(
            () => native.InitActions(actions),
            () => ActionsAddOpen(actions, 0, NullDevice, ReadOnly, 0),
            () => ActionsAddDup2(actions, 1, 2),
            () => ActionsAddCloseFrom(actions, FirstClosed),
            () => ActionsAddChdir(actions, "/"),
            () => native.InitAttributes(attributes),
            () => AttributesSetFlags(attributes, SetProcessGroup),
            () => AttributesSetGroup(attributes, 0),
            () => DefaultSignals(signals),
            () => AttributesSetDefaults(attributes, signals),
            () => AttributesSetMask(attributes, signals));
        return (Touched(actions, sentinel), Touched(attributes, sentinel), Touched(signals, sentinel));
    }

    /// <summary>One past the last byte that is no longer the sentinel.</summary>
    private static int Touched(IntPtr buffer, byte sentinel)
    {
        var bytes = new byte[Capacity];
        Marshal.Copy(buffer, bytes, 0, Capacity);
        return Array.FindLastIndex(bytes, value => value != sentinel) + 1;
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

    /// <summary>What is allocated and initialised for one start, tracked from the moment it exists: <see cref="Buffer"/> and <see cref="Terminated"/> record each allocation as they make it, <see cref="InitActions"/> and <see cref="InitAttributes"/> record an <c>init</c> only when it succeeded, and <see cref="Dispose"/> destroys exactly those structures and frees exactly those allocations.</summary>
    private sealed class Native(SpawnHooks hooks) : IDisposable
    {
        private readonly List<IntPtr> _memory = [];
        private readonly List<IntPtr> _strings = [];
        private IntPtr _actions;
        private IntPtr _attributes;

        public IntPtr Buffer(byte fill = 0)
        {
            var buffer = Marshal.AllocHGlobal(Capacity);
            _memory.Add(buffer);
            hooks.Event?.Invoke("alloc");
            Marshal.Copy(Enumerable.Repeat(fill, Capacity).ToArray(), 0, buffer, Capacity);
            return buffer;
        }

        public int InitActions(IntPtr actions)
        {
            var error = hooks.ActionsInit(actions);
            _actions = error == 0 ? actions : IntPtr.Zero;
            return error;
        }

        public int InitAttributes(IntPtr attributes)
        {
            var error = hooks.AttributesInit(attributes);
            _attributes = error == 0 ? attributes : IntPtr.Zero;
            return error;
        }

        /// <summary>The words as a NULL-terminated array of UTF-8 C strings, each recorded as it is allocated.</summary>
        public IntPtr[] Terminated(IReadOnlyList<string> words)
        {
            var array = new IntPtr[words.Count + 1];
            for (var at = 0; at < words.Count; at++)
            {
                array[at] = Marshal.StringToCoTaskMemUTF8(words[at]);
                _strings.Add(array[at]);
                hooks.Event?.Invoke("alloc");
            }

            return array;
        }

        public void Dispose()
        {
            if (_actions != IntPtr.Zero)
            {
                ActionsDestroy(_actions);
                hooks.Event?.Invoke("destroy actions");
            }

            if (_attributes != IntPtr.Zero)
            {
                AttributesDestroy(_attributes);
                hooks.Event?.Invoke("destroy attributes");
            }

            _memory.ForEach(Marshal.FreeHGlobal);
            _strings.ForEach(Marshal.FreeCoTaskMem);
            hooks.Event?.Invoke($"free {_memory.Count + _strings.Count}");
        }
    }

    [DllImport("libc", EntryPoint = "gnu_get_libc_version")]
    private static extern IntPtr GnuGetLibcVersion();

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
    private static extern int ActionsInit(IntPtr actions);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
    private static extern int ActionsDestroy(IntPtr actions);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addopen")]
    private static extern int ActionsAddOpen(IntPtr actions, int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
    private static extern int ActionsAddDup2(IntPtr actions, int fd, int newFd);

    [DllImport("libc", EntryPoint = "posix_spawn_file_actions_addclosefrom_np")]
    private static extern int ActionsAddCloseFrom(IntPtr actions, int from);

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
