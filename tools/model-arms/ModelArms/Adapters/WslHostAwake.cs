using System.Globalization;
using System.Text;
using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>The WSL keep-awake (D12, E5.S2): a <c>pwsh.exe -NoProfile -ExecutionPolicy Bypass -File keep-awake.ps1</c> child HELD by a <see cref="ProcessSession"/> for the scope — the script holds the Windows execution state only while its process lives, so the adapter holds the child — and looked at every
/// <see cref="CheckEvery"/> (<c>after6.sh:19</c>): a child that has exited is launched again, at most <see cref="RelaunchesInARow"/> times in a row, after which it is said (log and output) and left. The chain is never failed by its keep-awake. Disposing stops the check loop, then the session (stdin closed, five seconds' grace, the tree killed), and logs <c>keep-awake: stopped</c>.
/// <para>
/// <b>The launch.</b> The executable is the REAL path <c>pwsh.exe</c> resolved to (D17 b) and the script is handed in Windows' spelling (<see cref="WslPaths"/>); the environment is <see cref="EnvironmentOf"/> alone (three names — an empty one runs, M-9); the working directory is the experiment root. Each launch's stderr is its own file
/// <c>keep-awake.&lt;stamp&gt;.&lt;n&gt;.err</c> because the session creates it new. Every line the script prints goes to <c>keep-awake.log</c>, as the shell's <c>&gt;&gt; keep-awake.log</c> did.
/// </para>
/// <para>
/// <b>D26 (a)–(f), by the plan's own meanings.</b> (a) kernel facts: no path is judged in this class — <c>HostAwakes</c> resolved <c>pwsh.exe</c> through <c>LaunchAdmission.Resolve</c> to a real path (refused under the target or the root), and the script's path is the module's own beside the binary; (b) no bare name: the launch is by that resolved path (<c>The_module_launches_nothing_by_a_bare_name</c> scans this file);
/// (c) <c>HOME</c> and PATH under a forbidden root: the child is given no <c>HOME</c> at all; its PATH is passed on UNJUDGED, because <c>chain</c> does not run <c>validate</c> — and the Windows <c>pwsh</c> does not receive it without <c>WSLENV</c>, which is not passed, so no WSL PATH entry is ever consulted by it; (d) <c>&lt;fill me&gt;</c>: not applicable here — the <c>awake</c> enum and its mismatch with the host go through <c>HostChecks</c>' own sentence (<c>HostAwakeSelection</c>);
/// (e) exclusive create: each launch's stderr file is created <c>CreateNew</c> by the session, one per launch; the log is appended by NAME, <see cref="FileMode.Append"/>, and not when the name is a link — <b>the residual:</b> between that check and the open a link could be planted, and only the operator can write the root (D17), the same window as <c>lock.log</c>'s; (f) re-read: not applicable (no configuration is re-read).
/// Beyond D26: the check loop is one <see cref="Task.Run(Func{Task})"/> whose whole body is inside a <c>try</c> ending in a catch-all that logs (a fault nobody awaits would end the keep-awake silently), and every wait has a ceiling — the loop's is the dispose, the session's is its own grace and kill, the start's is <see cref="ScriptLineWithin"/>.
/// </para>
/// <para>
/// <b>Not proven here:</b> a real <c>pwsh.exe</c> holding a real <c>SetThreadExecutionState</c> from this adapter (M-9 and M-10 are probes, not the adapter); a Windows host; a relay lost without a signal, which can leave the Windows <c>pwsh</c> running until its own 14400 s end (M-10).
/// </para></summary>
internal sealed class WslHostAwake : IHostAwake
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(2);

    public const int RelaunchesInARow = 3;

    /// <summary>The line <c>keep-awake.ps1</c> prints once it holds the execution state (<c>keep-awake on pid=&lt;pid&gt; prev=&lt;state&gt;</c>).</summary>
    private const string ScriptReported = "keep-awake on pid=";

    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";

    private static readonly Lock LogGate = new();

    private readonly string _pwsh;
    private readonly string _script;
    private readonly string _workingDirectory;
    private readonly IReadOnlyDictionary<string, string> _environment;
    private readonly RootPaths _root;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _wait;
    private readonly TextWriter _output;
    private readonly string _stamp;
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _reported = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile ProcessSession? _session;
    private int _inARow;
    private int _launches = 1;
    private Task _loop = Task.CompletedTask;
    private int _disposed;

    internal WslHostAwake(
        string pwsh, string scriptWindowsPath, string workingDirectory, IReadOnlyDictionary<string, string> environment,
        RootPaths root, TimeProvider clock, Func<TimeSpan, CancellationToken, Task> wait, TextWriter output)
    {
        (_pwsh, _script, _workingDirectory, _environment, _root, _clock, _wait, _output) = (pwsh, scriptWindowsPath, workingDirectory, environment, root, clock, wait, output);
        _stamp = clock.GetUtcNow().ToString(StampFormat, CultureInfo.InvariantCulture);
    }

    public string Describe => $"pwsh.exe {_script}, held, checked every {(int)CheckEvery.TotalMinutes} min";

    /// <summary>How long <see cref="Start"/> waits for the script's own first line before it stops saying the host is held (a pwsh that exits sooner is said at once). The script prints it within a second of a reachable start; fifteen seconds covers a cold <c>pwsh.exe</c>.</summary>
    internal TimeSpan ScriptLineWithin { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>The held child's pid; null before the first launch and after a failed relaunch.</summary>
    internal int? ChildProcessId => _session?.ProcessId;

    /// <summary>Whether the held child has ended (true when there is none): the same fact the check loop reads.</summary>
    internal bool ChildHasExited => _session?.HasExited ?? true;

    /// <summary>The five tokens <c>pwsh.exe</c> is given (<c>after6.sh:17</c>).</summary>
    public static IReadOnlyList<string> Arguments(string scriptWindowsPath) => ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptWindowsPath];

    /// <summary>What the child inherits: <c>PATH</c>, <c>WSL_DISTRO_NAME</c> and <c>WSL_INTEROP</c> when the parent has them, nothing else — no <c>HOME</c>, no database, no credential.</summary>
    public static IReadOnlyDictionary<string, string> EnvironmentOf(IReadOnlyDictionary<string, string> parent) =>
        new[] { "PATH", "WSL_DISTRO_NAME", "WSL_INTEROP" }
            .Where(parent.ContainsKey)
            .ToDictionary(name => name, name => parent[name], StringComparer.Ordinal);

    /// <summary>The first launch; a missing or unstartable <c>pwsh.exe</c> is a failure with its reason (the caller's exit 3). On success the check loop starts. The line <c>keep-awake: &lt;Describe&gt;</c> is printed only once the SCRIPT has reported (<c>keep-awake on pid=</c>), waiting at most <see cref="ScriptLineWithin"/>: a pwsh that exits first — it could not reach the script — is said at once and, like one
    /// that is silent past the bound, does not fail the chain (the relaunch bound applies from the first check).</summary>
    public Outcome<int> Start()
    {
        try
        {
            return Launch(1).Match(
                session =>
                {
                    _session = session;
                    _loop = Task.Run(() => CheckLoopAsync(_stop.Token));
                    Say(Reported(session));
                    return Outcome<int>.Success(session.ProcessId);
                },
                Outcome<int>.Failure);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<int>.Failure($"the keep-awake child could not be started: {ex.Message}");
        }
    }

    private string Reported(ProcessSession session)
    {
        Task.WaitAny([_reported.Task, session.Exited], ScriptLineWithin);
        return (_reported.Task.IsCompleted, session.HasExited) switch
        {
            (true, _) => $"keep-awake: {Describe}",
            (_, true) => $"keep-awake: pwsh.exe exited before the script reported — it could not reach {_script}; the host may sleep (stderr: {session.StderrPath})",
            _ => $"keep-awake: pwsh.exe is running but {_script} has not reported in {ScriptLineWithin.TotalSeconds:0.##} s — held, not confirmed; the host may sleep",
        };
    }

    private void Say(string line)
    {
        _output.WriteLine(line);
        Log(line);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stop.CancelAsync();
        await _loop;
        if (_session is { } session)
        {
            await session.DisposeAsync();
        }

        Log("keep-awake: stopped");
        _stop.Dispose();
    }

    /// <summary>The whole body is inside the <c>try</c> (common.reliability, the detached edge): a fault is a line in the log, never a silent end.</summary>
    private async Task CheckLoopAsync(CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested && await CheckOnceAsync(stop))
            {
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // The scope ended: the dispose is waiting for this to finish.
        }
        catch (Exception ex)
        {
            Log($"keep-awake: the check loop failed — {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>One check after one wait: an exit count of 0 (the child is alive) goes on; more than <see cref="RelaunchesInARow"/> is said and ends the loop; otherwise the child is launched again. False ends the loop.</summary>
    private async Task<bool> CheckOnceAsync(CancellationToken stop)
    {
        await _wait(CheckEvery, stop);
        _inARow = _session!.HasExited ? _inARow + 1 : 0;
        return _inARow switch
        {
            0 => true,
            > RelaunchesInARow => LeaveSaid(_inARow),
            _ => await RelaunchAsync(++_launches),
        };
    }

    private bool LeaveSaid(int inARow)
    {
        var line = $"keep-awake exited at {inARow} checks in a row — not relaunched again; the host may sleep while the chain runs";
        _output.WriteLine(line);
        Log(line);
        return false;
    }

    /// <summary>Launches the <paramref name="n"/>th child in place of the exited one; false (and logged) when it could not be started, which ends the loop.</summary>
    private async Task<bool> RelaunchAsync(int n)
    {
        var old = _session!;
        Log($"keep-awake exited (pid {old.ProcessId}) — relaunched");
        await old.DisposeAsync();
        return Launch(n).Match(
            session =>
            {
                _session = session;
                return true;
            },
            reason =>
            {
                Log($"keep-awake: the relaunch failed — {reason}");
                return false;
            });
    }

    private Outcome<ProcessSession> Launch(int n) =>
        ProcessSession.Start(new SessionLaunch(_pwsh, Arguments(_script), _workingDirectory, _environment, _root.KeepAwakeErr(_stamp, n)), OnLine) switch
        {
            SessionStart.Started started => Outcome<ProcessSession>.Success(started.Session),
            SessionStart.NotFound notFound => Outcome<ProcessSession>.Failure($"{notFound.Executable} could not be started: {notFound.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private void OnLine(string line)
    {
        if (line == ProcessSession.EndOfStream)
        {
            return;
        }

        Log(line);
        if (line.StartsWith(ScriptReported, StringComparison.Ordinal))
        {
            _reported.TrySetResult();
        }
    }

    /// <summary>One line, <c>&lt;utc&gt; &lt;text&gt;</c>, appended to <c>keep-awake.log</c> by name and only when the name is not a link; a failed append is one line on the output and never an exception.</summary>
    private void Log(string text)
    {
        lock (LogGate)
        {
            try
            {
                if (BoundedFile.IsLink(_root.KeepAwakeLog))
                {
                    _output.WriteLine($"model-arms: {_root.KeepAwakeLog} is a symbolic link — not appended to (D17)");
                    return;
                }

                using var stream = new FileStream(_root.KeepAwakeLog, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes($"{_clock.GetUtcNow().ToString(SnapshotFiles.Utc, CultureInfo.InvariantCulture)} {text}\n"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _output.WriteLine($"model-arms: {_root.KeepAwakeLog} could not be appended to: {ex.Message}");
            }
        }
    }
}
