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
public sealed record ProcessResult(int ExitCode, string Output, string StandardOutput = "")
{
    public bool Ok => ExitCode == 0;
}

/// <summary>How a launch ended. A timeout is a VALUE, not an exception: a hung git fetch must fail the
/// one step that owns the budget, and not unwind a run of ten thousand legs that was otherwise fine.</summary>
public abstract record ProcessAttempt
{
    private ProcessAttempt() { }

    public sealed record Completed(ProcessResult Result) : ProcessAttempt;

    public sealed record TimedOut(TimeSpan Budget, string Output) : ProcessAttempt;

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
    public static async Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        string input,
        CancellationToken cancellationToken)
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

        using var process = new System.Diagnostics.Process { StartInfo = start };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Expected: the executable is not installed. An environment answer the caller renders as an
            // exit code, not an exception that unwinds a run.
            return new ProcessAttempt.NotFound(executable);
        }

        if (input.Length > 0)
        {
            await WriteAsync(process, input, cancellationToken);
        }

        return await AwaitAsync(process, timeout, cancellationToken);
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

    private static async Task<ProcessAttempt> AwaitAsync(
        System.Diagnostics.Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);

        // None, deliberately, and this is the reason rather than an omission: on a timeout the child is
        // killed and its output is still MERGED into the report below. A cancelled read would throw
        // exactly then — leaving the one diagnosis anybody wants ("what did it print before it hung?")
        // unavailable at the only moment it matters. The reads end on their own when the pipes close.
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

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

            return new ProcessAttempt.TimedOut(timeout, await Merge(stdout, stderr));
        }

        var printed = await stdout;

        return new ProcessAttempt.Completed(
            new ProcessResult(process.ExitCode, await Merge(printed, stderr), printed.Trim()));
    }

    private static async Task<string> Merge(string stdout, Task<string> stderr) =>
        (stdout + await stderr).Trim();

    private static async Task<string> Merge(Task<string> stdout, Task<string> stderr) =>
        await Merge(await stdout, stderr);

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
