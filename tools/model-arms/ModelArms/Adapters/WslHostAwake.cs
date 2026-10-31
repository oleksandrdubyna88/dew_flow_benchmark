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
/// <b>D26 (a)–(f).</b> (a) no path is judged here — the caller resolved <c>pwsh.exe</c> and the script's path is the module's own; (b) the launch is by the resolved path, never a bare name; (c) the child's environment is three names, none a secret (and the parent's PATH is not judged beyond the resolution of <c>pwsh.exe</c> itself, which refuses a file under the target or the root); (d) the log is opened by NAME, <see cref="FileMode.Append"/>, and not when the name is a link
/// (one line on the output instead) — <b>the residual:</b> between that check and the open a link could be planted, and only the operator can write the root (D17), the same window as <c>lock.log</c>'s; (e) the check loop is one <see cref="Task.Run(Func{Task})"/> whose whole body is inside a <c>try</c> ending in a catch-all that logs (a fault nobody awaits would end the keep-awake silently);
/// (f) every wait has a ceiling: the loop's is the dispose, the session's is its own grace and kill.
/// </para>
/// <para>
/// <b>Not proven here:</b> a real <c>pwsh.exe</c> holding a real <c>SetThreadExecutionState</c> from this adapter (M-9 and M-10 are probes, not the adapter); a Windows host; a relay lost without a signal, which can leave the Windows <c>pwsh</c> running until its own 14400 s end (M-10).
/// </para></summary>
internal sealed class WslHostAwake : IHostAwake
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(2);

    public const int RelaunchesInARow = 3;

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
    private volatile ProcessSession? _session;
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

    /// <summary>The first launch; a missing or unstartable <c>pwsh.exe</c> is a failure with its reason (the caller's exit 3). On success the check loop starts and the line <c>keep-awake: &lt;Describe&gt;</c> is printed.</summary>
    public Outcome<int> Start()
    {
        try
        {
            return Launch(1).Match(
                session =>
                {
                    _session = session;
                    _output.WriteLine($"keep-awake: {Describe}");
                    _loop = Task.Run(() => CheckLoopAsync(_stop.Token));
                    return Outcome<int>.Success(session.ProcessId);
                },
                Outcome<int>.Failure);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<int>.Failure($"the keep-awake child could not be started: {ex.Message}");
        }
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
            var (inARow, launches) = (0, 1);
            while (!stop.IsCancellationRequested)
            {
                await _wait(CheckEvery, stop);
                inARow = _session!.HasExited ? inARow + 1 : 0;
                if (inARow > RelaunchesInARow)
                {
                    SayLeft(inARow);
                    return;
                }

                if (inARow > 0 && !await RelaunchAsync(++launches))
                {
                    return;
                }
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

    private void SayLeft(int inARow)
    {
        var line = $"keep-awake exited at {inARow} checks in a row — not relaunched again; the host may sleep while the chain runs";
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
        if (line != ProcessSession.EndOfStream)
        {
            Log(line);
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
