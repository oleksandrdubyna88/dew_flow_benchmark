using System.Diagnostics;
using System.Text;

namespace Bench.Infrastructure.Process;

/// <summary>How a long-lived child is launched: exe + argv (never a shell string), the working directory, the
/// WHOLE environment it runs under, and the file its stderr is streamed into.</summary>
/// <param name="Environment">The child's environment, exactly — nothing of the parent's is inherited beyond what
/// the caller put here. A log line about the launch names the variables' NAMES only, never a value.</param>
/// <param name="StderrPath">Where stderr goes, line by line and flushed as it arrives — so a child that crashes
/// leaves what it said, which is often the only diagnosis there is.</param>
public sealed record SessionLaunch(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment,
    string StderrPath)
{
    /// <summary>Applied to every stderr line before it reaches the file.</summary>
    public Func<string, string> Scrub { get; init; } = static line => line;
}

/// <summary>How a launch attempt ended before any conversation — the executable was not there, or it started.</summary>
public abstract record SessionStart
{
    private SessionStart()
    {
    }

    public sealed record Started(ProcessSession Session) : SessionStart;

    public sealed record NotFound(string Executable, string Reason) : SessionStart;
}

/// <summary>A long-lived child over stdin/stdout pipes — the one-shot <see cref="ProcessRunner"/>'s sibling for a
/// process that is a CONVERSATION rather than a command (an MCP server on the stdio transport).
/// <para>
/// It widens the launcher rather than copying it: the same exe + argv rule, the same
/// <see cref="ProcessRunner.IsAlreadyGone"/> for a refused kill, the same kill-the-whole-tree on the way out. What
/// is new is only what a pipe needs: stdout is read line by line and handed to a callback, stderr is streamed to a
/// file, and <see cref="DisposeAsync"/> closes stdin, waits a short grace, then kills the tree — so a disposed
/// session never leaves a child behind, whether the child was polite or hung.
/// </para></summary>
public sealed class ProcessSession : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly System.Diagnostics.Process _process;
    private readonly StreamWriter _stderr;
    private readonly Lock _stderrGate = new();
    private readonly Lock _stdinGate = new();
    private readonly Task _stdoutPump;
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    private readonly Func<string, string> _scrub;

    private ProcessSession(System.Diagnostics.Process process, StreamWriter stderr, Action<string> onLine, Func<string, string> scrub)
    {
        _process = process;
        _stderr = stderr;
        _scrub = scrub;
        _process.ErrorDataReceived += (_, e) => Remember(e.Data);
        _process.Exited += (_, _) => _exited.TrySetResult();
        _process.BeginErrorReadLine();
        _stdoutPump = Task.Run(() => PumpAsync(onLine));
        if (_process.HasExited)
        {
            _exited.TrySetResult();
        }
    }

    public int ProcessId { get; private set; }

    /// <summary>Whether the child has ended — by itself, by a crash, or by <see cref="KillAsync"/>.</summary>
    public bool HasExited => _exited.Task.IsCompleted;

    /// <summary>Completes when the child ends, however it ends.</summary>
    public Task Exited => _exited.Task;

    public string StderrPath { get; private init; } = string.Empty;

    /// <summary>Starts the child. A missing executable is a VALUE — the environment's answer, rendered as an exit
    /// code by the caller — never an exception that unwinds a campaign.</summary>
    public static SessionStart Start(SessionLaunch launch, Action<string> onLine)
    {
        var start = new ProcessStartInfo
        {
            FileName = launch.Executable,
            WorkingDirectory = launch.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8NoBom,
            StandardOutputEncoding = Utf8NoBom,
            StandardErrorEncoding = Utf8NoBom,
        };

        foreach (var argument in launch.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // The environment is REPLACED, not merged: whatever the caller did not put in the map is not inherited, so
        // a parent's COAI_* cannot reach the product behind the harness's back.
        start.Environment.Clear();
        foreach (var (name, value) in launch.Environment)
        {
            start.Environment[name] = value;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(launch.StderrPath)) ?? ".");
        var stderr = new StreamWriter(new FileStream(launch.StderrPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), Utf8NoBom);
        var process = new System.Diagnostics.Process { StartInfo = start, EnableRaisingEvents = true };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            stderr.Dispose();
            process.Dispose();
            return new SessionStart.NotFound(launch.Executable, ex.Message);
        }

        return new SessionStart.Started(new ProcessSession(process, stderr, onLine, launch.Scrub) { ProcessId = process.Id, StderrPath = launch.StderrPath });
    }

    /// <summary>Writes one line to the child's stdin. False when the pipe is gone — the child exited — which the
    /// caller reads as "the session is over", never as an exception.</summary>
    public bool WriteLine(string line)
    {
        lock (_stdinGate)
        {
            try
            {
                _process.StandardInput.Write(line);
                _process.StandardInput.Write('\n');
                _process.StandardInput.Flush();
                return true;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                return false;
            }
        }
    }

    /// <summary>Kills the whole tree now. Idempotent; a child already gone is the state being asked for.</summary>
    public async Task KillAsync()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (Exception failure) when (ProcessRunner.IsAlreadyGone(failure))
        {
            // Already gone — nothing to kill.
        }

        await WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>Closes stdin (the polite ending an MCP server honours), waits <paramref name="grace"/>, then kills
    /// the tree. After this the child is gone and stderr is flushed and closed.</summary>
    public async Task StopAsync(TimeSpan grace)
    {
        lock (_stdinGate)
        {
            try
            {
                _process.StandardInput.Close();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The pipe is already gone, which is what closing it asks for.
            }
        }

        if (!await WaitAsync(grace))
        {
            await KillAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await StopAsync(TimeSpan.FromSeconds(5));
        await DrainAsync();
        _process.Dispose();
    }

    private async Task<bool> WaitAsync(TimeSpan budget)
    {
        var finished = await Task.WhenAny(_exited.Task, Task.Delay(budget));
        return finished == _exited.Task;
    }

    /// <summary>Lets the readers reach the end of both pipes, bounded, then closes the stderr file.</summary>
    private async Task DrainAsync()
    {
        await Task.WhenAny(_stdoutPump, Task.Delay(TimeSpan.FromSeconds(5)));

        try
        {
            _process.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
            // Never started or already disposed: nothing left to wait for.
        }

        lock (_stderrGate)
        {
            _stderr.Dispose();
        }
    }

    private async Task PumpAsync(Action<string> onLine)
    {
        try
        {
            while (await _process.StandardOutput.ReadLineAsync() is { } line)
            {
                onLine(line);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The pipe broke with the child; the exit is what the caller observes.
        }
        finally
        {
            onLine(EndOfStream);
        }
    }

    /// <summary>What the stdout callback receives once, after the last line: the pipe closed.</summary>
    public const string EndOfStream = "\u0000<end of stream>";

    private void Remember(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_stderrGate)
        {
            try
            {
                _stderr.WriteLine(_scrub(line));
                _stderr.Flush();
            }
            catch (ObjectDisposedException)
            {
                // A line that raced the dispose; the file is already closed.
            }
        }
    }
}
