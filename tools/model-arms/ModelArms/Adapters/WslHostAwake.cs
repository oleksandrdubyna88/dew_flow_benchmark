using System.Globalization;
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
    private readonly TaskCompletionSource<ScriptReport> _reported = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile ProcessSession? _session;
    private volatile string _described = string.Empty;
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

    /// <summary>What the first child came to once <see cref="StartAsync"/> has run — held and checked, exited before the script held the host, failed, or running but not confirmed — and the held phrase before. The one place the chain's line <c>keep-awake: &lt;Describe&gt;</c> is written is the factory.</summary>
    public string Describe => _described.Length > 0 ? _described : Held;

    private string Held => $"pwsh.exe {_script}, held, checked every {(int)CheckEvery.TotalMinutes} min";

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

    /// <summary>The first launch; a missing or unstartable <c>pwsh.exe</c> is a failure with its reason (the caller's exit 3). On success the check loop starts, one line says that the script's confirmation is awaited (<c>keep-awake: waiting up to N s …</c>), and the wait ends at the first of: the script's own line (<c>keep-awake on pid=… prev=…</c>), the child's exit,
    /// <see cref="ScriptLineWithin"/>, or <paramref name="ct"/> (the child is then stopped and the cancellation propagates). What came of it is <see cref="Describe"/>; a pwsh that exits first or a script that reports a previous state of 0 does not fail the chain (the relaunch bound applies from the first check).</summary>
    public async Task<Outcome<int>> StartAsync(CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await Launch(1).Match(session => BeginAsync(session, ct), reason => Task.FromResult(Outcome<int>.Failure(reason)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<int>.Failure($"the keep-awake child could not be started: {ex.Message}");
        }
    }

    private async Task<Outcome<int>> BeginAsync(ProcessSession session, CancellationToken ct)
    {
        _session = session;
        _loop = Task.Run(() => CheckLoopAsync(_stop.Token));
        _output.WriteLine($"keep-awake: waiting up to {ScriptLineWithin.TotalSeconds:0.##} s for the script's confirmation");
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await Task.WhenAny(_reported.Task, session.Exited, Task.Delay(ScriptLineWithin, bound.Token));
        await bound.CancelAsync();
        if (ct.IsCancellationRequested)
        {
            await DisposeAsync();
            ct.ThrowIfCancellationRequested();
        }

        _described = StatusOf(_reported.Task.IsCompleted ? _reported.Task.Result : null, session.HasExited, session.StderrPath);
        Log($"keep-awake: {_described}");
        return Outcome<int>.Success(session.ProcessId);
    }

    /// <summary>What to say about the first child, pure over what was seen: an EXITED child is never held, whatever it printed first; a script that reported a previous state of 0 says <c>SetThreadExecutionState</c> failed (as <c>WindowsAwake</c> does); a confirmed one is described as held; a silent one is held, not confirmed.</summary>
    internal string StatusOf(ScriptReport? report, bool hasExited, string stderrPath) =>
        (report, hasExited) switch
        {
            (_, true) => $"pwsh.exe exited before the script held the host — it could not reach {_script}; the host may sleep (stderr: {stderrPath})",
            (ScriptReport.Failed, _) => "SetThreadExecutionState failed — the host may sleep",
            (ScriptReport.Confirmed, _) => Held,
            _ => $"pwsh.exe is running but {_script} has not reported in {ScriptLineWithin.TotalSeconds:0.##} s — held, not confirmed; the host may sleep",
        };

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
        Say($"keep-awake exited at {inARow} checks in a row — not relaunched again; the host may sleep while the chain runs");
        return false;
    }

    /// <summary>A line for the operator (the output first, so whoever reads the log has already been told) and for the log.</summary>
    private void Say(string line)
    {
        _output.WriteLine(line);
        Log(line);
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
                _session = null;
                Say($"keep-awake: the relaunch failed — {reason}; the host may sleep while the chain runs");
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

    /// <summary>The script's own line, <c>keep-awake on pid=&lt;pid&gt; prev=&lt;state&gt;</c>: confirmed only when <c>prev=</c> parses as an unsigned integer and is not 0 (a previous state of 0 is the call failing); any other line, or a <c>prev=</c> that is not a number, reports nothing.</summary>
    internal static ScriptReport? ReportOf(string line)
    {
        var at = line.StartsWith(ScriptReported, StringComparison.Ordinal) ? line.IndexOf(" prev=", StringComparison.Ordinal) : -1;
        return at >= 0 && uint.TryParse(line.AsSpan(at + " prev=".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var previous)
            ? previous == 0 ? ScriptReport.Failed : ScriptReport.Confirmed
            : null;
    }

    private void OnLine(string line)
    {
        if (line == ProcessSession.EndOfStream)
        {
            return;
        }

        Log(line);
        if (ReportOf(line) is { } report)
        {
            _reported.TrySetResult(report);
        }
    }

    /// <summary>One line appended to <c>keep-awake.log</c> by <see cref="RootLogFile"/> (by name, not through a link, never throwing), serialised so the stdout pump and the check loop never interleave a line.</summary>
    private void Log(string text)
    {
        lock (LogGate)
        {
            RootLogFile.Append(_root.KeepAwakeLog, _clock, text, _output);
        }
    }
}

/// <summary>What the keep-awake script said about the execution state it took: held (a previous state it replaced) or failed (a previous state of 0).</summary>
internal enum ScriptReport { Confirmed, Failed }
