using System.Diagnostics;

namespace Bench.Infrastructure.Process;

public sealed record ProcessResult(int ExitCode, string Output)
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
    public static async Task<ProcessAttempt> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        return await AwaitAsync(process, timeout, cancellationToken);
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            return new ProcessAttempt.TimedOut(timeout, await Merge(stdout, stderr));
        }

        return new ProcessAttempt.Completed(new ProcessResult(process.ExitCode, await Merge(stdout, stderr)));
    }

    private static async Task<string> Merge(Task<string> stdout, Task<string> stderr) =>
        (await stdout + await stderr).Trim();

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
