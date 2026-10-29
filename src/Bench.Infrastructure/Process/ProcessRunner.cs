using System.Diagnostics;

namespace Bench.Infrastructure.Process;

/// <param name="Output">Stdout and stderr MERGED, which is what a diagnostic report wants: "what did it print
/// before it failed" does not care which pipe carried it.</param>
/// <param name="StandardOutput">Stdout ALONE, for a caller whose stdout is a PAYLOAD rather than a log.
/// <para>
/// Added 2026-08-17, from a live run: the Claude CLI prints a workspace-trust warning on stdout/stderr beside
/// its answer, and the merged text therefore started with prose. The JSON parser refused it — correctly, since
/// it was handed something that was not the agent's answer. Merging is right for git and wrong for an agent,
/// so both are available and the caller says which it means.
/// </para></param>
/// <param name="StandardError">Stderr ALONE — for a caller that stores the two pipes as two artefacts (the probes, S2): a CLI's
/// quota marker and its usage error arrive on stderr, and a transcript on stdout is read by a grammar that must not meet them.</param>
public sealed record ProcessResult(int ExitCode, string Output, string StandardOutput = "", string StandardError = "")
{
    public bool Ok => ExitCode == 0;
}

/// <summary>How a launch ended. A timeout is a VALUE, not an exception: a hung git fetch must fail the
/// one step that owns the budget, and not unwind a run of ten thousand legs that was otherwise fine.</summary>
public abstract record ProcessAttempt
{
    private ProcessAttempt() { }

    public sealed record Completed(ProcessResult Result) : ProcessAttempt;

    /// <param name="StandardOutput">What had reached stdout before the kill, alone — a probe keeps it as the attempt's artefact.</param>
    public sealed record TimedOut(TimeSpan Budget, string Output, string StandardOutput = "", string StandardError = "") : ProcessAttempt;

    public sealed record NotFound(string Executable) : ProcessAttempt;

    public string Describe =>
        this switch
        {
            Completed c => $"exit {c.Result.ExitCode}",
            TimedOut t => $"timed out after {t.Budget.TotalSeconds:0.#}s",
            NotFound n => $"'{n.Executable}' is not on PATH",
            _ => "unknown",
        };
}

/// <summary>The one place this repository launches an external process.
/// <para>
/// Always <b>exe + argv</b>, never a shell string. That matters more here than in most projects: the
/// benchmark clones repositories at operator-supplied urls, and a url concatenated into a shell command
/// is arbitrary code execution wearing a `--repo` flag. Arguments go through
/// <see cref="ProcessStartInfo.ArgumentList"/>, which quotes them for the platform and never re-parses
/// them.
/// </para></summary>
public static class ProcessRunner
{
    public static Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        RunAsync(executable, arguments, workingDirectory, timeout, string.Empty, cancellationToken);

    /// <summary>The same launch, with something written to the child's STDIN.
    /// <para>
    /// Added for the authoring pipeline, and the reason is a limit rather than a preference: a CLI agent's
    /// prompt runs to kilobytes, and an argument list has a platform maximum — around 32 KB on Windows, and
    /// the failure arrives on the machine with the biggest target repository, at the moment the prompt grows.
    /// Passing it on stdin has no such ceiling.
    /// </para>
    /// <para>
    /// An extension of the one launcher rather than a second one beside it. The alternative — an adapter that
    /// starts its own process because this one could not take input — is how this repository ended up with a
    /// duplicated process launcher the first time.
    /// </para></summary>
    public static Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string input,
        CancellationToken cancellationToken) =>
        RunAsync(executable, arguments, workingDirectory, timeout, input, environment: null, cancellationToken);

    /// <summary>The same launch under a REPLACED environment: when <paramref name="environment"/> is given, the child gets
    /// exactly those variables and inherits nothing else — the shape <see cref="ProcessSession"/> launches the product with,
    /// now available to the one-shot launcher so <c>coai-mcp --probe-api</c> (the probes, S2) gets the vault key once, under
    /// the product's name, and none of the harness's <c>COAI_*</c>. Null inherits the parent's, as every other caller does.</summary>
    public static Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string input,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken) =>
        RunAsync(executable, arguments, workingDirectory, timeout, input, environment, stdoutLine: null, cancellationToken);

    /// <summary>The same launch with stdout handed to <paramref name="stdoutLine"/> LINE BY LINE AS IT ARRIVES (the scouts, S2): a child
    /// that is paid for while it runs must leave its stream on disk before it exits, so a crash of the harness loses at most the line in
    /// flight — the buffered launch hands the output back only at exit (scouts plan §3.10). The whole stdout is still returned.
    /// <para>
    /// <b>The wall and the tree kill are unchanged</b>: the child's whole process tree dies at the wall AND on any external cancellation,
    /// exactly as for the buffered launch. A sink that THROWS (an artefact root that refused a line, or a harness stopping at a
    /// checkpoint) ends the launch the same way — the child is killed, never left spending with nobody reading it — and the sink's
    /// exception is what the caller sees.
    /// </para></summary>
    public static Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string input,
        IReadOnlyDictionary<string, string>? environment,
        Action<string>? stdoutLine,
        CancellationToken cancellationToken) =>
        RunAsync(executable, arguments, workingDirectory, timeout, input, environment, stdoutLine, stderrLine: null, cancellationToken);

    /// <summary>The same launch with stderr ALSO handed to <paramref name="stderrLine"/> LINE BY LINE AS IT ARRIVES, beside stdout's sink (model
    /// arms E1.S5): a step that is paid for while it runs keeps the CLI's warnings on disk the moment they are printed. The whole stderr is
    /// still returned in <see cref="ProcessResult.StandardError"/> and <see cref="ProcessResult.Output"/>. Either sink may be null.
    /// <para>
    /// <b>The wall and the tree kill are unchanged</b>, and a stderr sink that THROWS ends the launch exactly as a stdout sink does: the
    /// child's tree is killed and the sink's exception is what the caller sees.
    /// </para></summary>
    public static async Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string input,
        IReadOnlyDictionary<string, string>? environment,
        Action<string>? stdoutLine,
        Action<string>? stderrLine,
        CancellationToken cancellationToken)
    {
        var start = StartInfo(executable, arguments, workingDirectory, input, environment);

        if (stdoutLine is not null || stderrLine is not null)
        {
            Utf8(start);
        }

        using var process = new System.Diagnostics.Process { StartInfo = start };

        if (!TryStart(process))
        {
            return new ProcessAttempt.NotFound(executable);
        }

        if (input.Length > 0)
        {
            await WriteAsync(process, input, cancellationToken);
        }

        return await AwaitAsync(process, timeout, stdoutLine, stderrLine, cancellationToken);
    }

    /// <summary>The same launch with stdout handed to <paramref name="readStdout"/> as a raw byte STREAM, and stdin written WHILE it is read
    /// (<c>git cat-file --batch</c>, the hygiene scan — E2 code round). Two things the other launches cannot do: a blob is a binary payload a
    /// text decode would change; and a child that answers each input line with a payload fills its stdout pipe before a long input is fully
    /// written, so write-then-read would leave both sides waiting on a full pipe. The reader owns the stream to its end — the token it is handed
    /// is never cancelled, so a child killed at the wall still reads as end-of-stream, as the line pump does; a reader that throws kills the
    /// child as a line sink does, and its exception is what the caller sees. The attempt's <c>StandardOutput</c> is empty — the bytes went to
    /// the reader — and <c>Output</c> is stderr alone.</summary>
    public static async Task<ProcessAttempt> StreamAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string input,
        Func<Stream, CancellationToken, Task> readStdout,
        CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process { StartInfo = StartInfo(executable, arguments, workingDirectory, input, environment: null) };

        if (!TryStart(process))
        {
            return new ProcessAttempt.NotFound(executable);
        }

        var writing = input.Length > 0 ? WriteAsync(process, input, cancellationToken) : Task.CompletedTask;
        var attempt = await AwaitAsync(
            process, timeout, budget => ReadAsync(process.StandardOutput.BaseStream, readStdout, budget), Reader(process.StandardError, line: null), cancellationToken);
        await writing;

        return attempt;
    }

    private static ProcessStartInfo StartInfo(string executable, IReadOnlyList<string> arguments, string workingDirectory, string input, IReadOnlyDictionary<string, string>? environment)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Redirected only when there is something to write. A child that reads stdin and finds an OPEN
            // empty pipe waits forever, and the timeout would then report a hang the caller caused.
            RedirectStandardInput = input.Length > 0,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        Replace(start, environment);
        return start;
    }

    /// <summary>Whether the child started. False when the executable is not installed — an environment answer the caller renders as an exit
    /// code, not an exception that unwinds a run.</summary>
    private static bool TryStart(System.Diagnostics.Process process)
    {
        try
        {
            process.Start();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Writes the input and CLOSES the pipe.
    /// <para>
    /// The close is the load-bearing half: a CLI reading until end-of-input keeps waiting while the pipe is
    /// open, so a writer that forgets it turns every call into a timeout. Disposing the writer is what sends
    /// the EOF.
    /// </para></summary>
    private static async Task WriteAsync(
        System.Diagnostics.Process process, string input, CancellationToken cancellationToken)
    {
        try
        {
            await using var stdin = process.StandardInput;
            await stdin.WriteAsync(input.AsMemory(), cancellationToken);
        }
        catch (IOException)
        {
            // The child exited before reading its prompt — a refusal it is about to explain on stdout, and
            // one this method must not turn into an exception that hides that explanation.
        }
    }

    private static Task<ProcessAttempt> AwaitAsync(
        System.Diagnostics.Process process, TimeSpan timeout, Action<string>? stdoutLine, Action<string>? stderrLine, CancellationToken cancellationToken) =>
        AwaitAsync(process, timeout, Reader(process.StandardOutput, stdoutLine), Reader(process.StandardError, stderrLine), cancellationToken);

    /// <summary>Buffered to the end when no sink is given — None as the token, deliberately, and this is the reason rather than an omission: on a
    /// timeout the child is killed and its output is still MERGED into the report below. A cancelled read would throw exactly then — leaving
    /// the one diagnosis anybody wants ("what did it print before it hung?") unavailable at the only moment it matters. The reads end on their
    /// own when the pipes close — or pumped line by line into the sink, under the wait's budget.</summary>
    private static Func<CancellationTokenSource, Task<string>> Reader(StreamReader pipe, Action<string>? line) =>
        budget => line is null ? pipe.ReadToEndAsync(CancellationToken.None) : PumpAsync(pipe, line, budget);

    /// <summary>The wait, the wall and the tree kill, over whichever readers the launch chose for the two pipes: buffered text, a line pump, or
    /// (stdout only) a byte reader (<see cref="StreamAsync"/>) — one copy of the part that must never differ between them.</summary>
    private static async Task<ProcessAttempt> AwaitAsync(
        System.Diagnostics.Process process,
        TimeSpan timeout,
        Func<CancellationTokenSource, Task<string>> readStdout,
        Func<CancellationTokenSource, Task<string>> readStderr,
        CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);

        var stdout = readStdout(budget);
        var stderr = readStderr(budget);

        try
        {
            await process.WaitForExitAsync(budget.Token);
        }
        catch (OperationCanceledException)
        {
            // The child dies with the wait, WHOEVER cancelled it. This guard used to fire only for the
            // internal timeout, so every external cancellation — the drain's post-grace stop, a Ctrl+C —
            // orphaned the exact child it was shutting down; on Windows the orphan keeps file handles
            // open inside a scratch worktree, the cleanup then cannot remove the directory, and a stale
            // worktree entry survives that nothing sweeps. Found by review, proven by a red test.
            Kill(process);

            if (cancellationToken.IsCancellationRequested)
            {
                // The caller's own signal still surfaces as one — only the orphan is new.
                throw;
            }

            var (hungOut, hungErr) = (await stdout, await stderr);
            return new ProcessAttempt.TimedOut(timeout, Merge(hungOut, hungErr), hungOut.Trim(), hungErr.Trim());
        }

        var (printed, errors) = (await stdout, await stderr);

        return new ProcessAttempt.Completed(
            new ProcessResult(process.ExitCode, Merge(printed, errors), printed.Trim(), errors.Trim()));
    }

    private static string Merge(string stdout, string stderr) => (stdout + stderr).Trim();

    /// <summary>A streamed launch reads and writes its pipes as UTF-8 (no BOM), whatever the console's code page: the CLIs it serves (claude)
    /// speak UTF-8, and the scouts' stream carries the frozen prompts BACK — an em dash decoded through the console's code page would turn
    /// every prompt hash into a mismatch (found by the fake CLI's replay test, where the OEM code page decoded <c>—</c> as <c>ΓÇö</c>). The
    /// buffered launch keeps the platform default every earlier caller was measured under.</summary>
    private static void Utf8(ProcessStartInfo start)
    {
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        start.StandardOutputEncoding = utf8;
        start.StandardErrorEncoding = utf8;

        if (start.RedirectStandardInput)
        {
            start.StandardInputEncoding = utf8;
        }
    }

    /// <summary>A pipe read line by line, each line handed to the sink the moment it is read, and the whole text kept. A sink that throws
    /// cancels the wait's budget — which kills the child's tree in <see cref="AwaitAsync"/> — and its exception travels out of the read.</summary>
    private static async Task<string> PumpAsync(StreamReader reader, Action<string> line, CancellationTokenSource budget)
    {
        var text = new System.Text.StringBuilder();

        try
        {
            while (await reader.ReadLineAsync(CancellationToken.None) is { } read)
            {
                text.Append(read).Append('\n');
                line(read);
            }
        }
        catch (Exception)
        {
            // Whatever the sink refused, the child must not keep spending with nobody reading it.
            await budget.CancelAsync();
            throw;
        }

        return text.ToString();
    }

    /// <summary>Stdout handed to a byte reader whole, under a token that is never cancelled (the pipe's close is its end, as for the line pump);
    /// a reader that throws cancels the wait's budget — which kills the child's tree in <see cref="AwaitAsync"/> — and its exception travels
    /// out of the read. Nothing of stdout is kept here: the reader had it.</summary>
    private static async Task<string> ReadAsync(Stream stdout, Func<Stream, CancellationToken, Task> reader, CancellationTokenSource budget)
    {
        try
        {
            await reader(stdout, CancellationToken.None);
            return string.Empty;
        }
        catch (Exception)
        {
            await budget.CancelAsync();
            throw;
        }
    }

    /// <summary>The environment is REPLACED, not merged, when one is given: whatever the caller did not put in the map is not
    /// inherited, so a parent's <c>COAI_*</c> cannot reach the product behind the harness's back.</summary>
    private static void Replace(ProcessStartInfo start, IReadOnlyDictionary<string, string>? environment)
    {
        if (environment is null)
        {
            return;
        }

        start.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }
    }

    private static void Kill(System.Diagnostics.Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception failure) when (IsAlreadyGone(failure))
        {
            // Best effort: it exited between the timeout firing and this call. Nothing to kill, and
            // nothing the caller can do about it either — the timeout is already the answer.
        }
    }

    /// <summary>Whether a refused kill means there was nothing left to kill.
    /// <para>
    /// A named rule rather than a catch list, because the two exceptions are not obviously the same fact
    /// and the guard was wrong for exactly that reason: it caught <see cref="InvalidOperationException"/>
    /// alone, so the OTHER way a dying process refuses a kill — a
    /// <see cref="System.ComponentModel.Win32Exception"/> for access denied, or for a pid that stopped
    /// existing between the check and the call — escaped as an unhandled exception out of a best-effort
    /// cleanup path. This launcher is the family's reference implementation and is being copied into a
    /// sibling repository, so the gap would have been copied with it.
    /// </para>
    /// <para>
    /// Everything else still propagates. A guard that swallowed every exception would turn a real fault
    /// in this method into silence, which is the failure mode a best-effort path invites.
    /// </para></summary>
    public static bool IsAlreadyGone(Exception failure) =>
        failure is InvalidOperationException or System.ComponentModel.Win32Exception;
}
