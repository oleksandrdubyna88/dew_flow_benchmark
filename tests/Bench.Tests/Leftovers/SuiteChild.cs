using System.Diagnostics;

namespace Bench.Tests.Leftovers;

// Source: dew_flow_connect_other_ais, src_mcp/tests_leftovers/SuiteChild.cs at e060d77e (82 lines). Changed here (E6 S2): the namespace; the MatchesNothing member and its remarks are dropped (nothing here uses a
// filter that matches nothing); the comment on the encoding no longer names coai's RunTemp. This is benchmark's own file: it is not a manifest target.

/// <summary>
/// Starts THIS test executable as a child — the way a second sweep process is proved from outside (E6 S2, TwoSweepsAtOnceTests). No shell: every argument is
/// one <see cref="ProcessStartInfo.ArgumentList"/> entry, and stdout and stderr are drained together so a full pipe cannot hold the child.
/// </summary>
public static class SuiteChild
{
    /// <summary>How the child ended: killed for taking too long, or its exit code; its process id; what it wrote to each stream.</summary>
    public sealed record Result(bool TimedOut, int ExitCode, int Pid, string StdOut, string StdErr);

    /// <summary>
    /// Runs this executable with <paramref name="args"/>. <paramref name="env"/> is applied over this process's environment: a value sets a variable, a
    /// <c>null</c> removes it. Past <paramref name="timeout"/> the child and everything it started are killed and reaped.
    /// </summary>
    public static Result Run(IReadOnlyList<string> args, IReadOnlyDictionary<string, string?> env, TimeSpan timeout)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("this process has no executable path");
        var start = StartInfoFor(exe, args, env);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{exe} did not start");
        var pid = process.Id;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (process.WaitForExit(timeout))
        {
            process.WaitForExit();
            Task.WaitAll([stdout, stderr], TimeSpan.FromSeconds(10));

            return new Result(false, process.ExitCode, pid, Text(stdout), Text(stderr));
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit();

        return new Result(true, -1, pid, string.Empty, string.Empty);
    }

    /// <summary>The start of the child: no shell, one argument per entry, the environment applied (a <c>null</c> removes a variable).</summary>
    internal static ProcessStartInfo StartInfoFor(string exe, IReadOnlyList<string> args, IReadOnlyDictionary<string, string?> env)
    {
        // Both streams are read as UTF-8: the child's report lines can hold an em dash, and the child writes UTF-8 bytes, where the default would be the console's code page.
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
            CreateNoWindow = true,
        };
        foreach (var argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in env)
        {
            if (value is null)
            {
                start.Environment.Remove(name);
            }
            else
            {
                start.Environment[name] = value;
            }
        }

        return start;
    }

    private static string Text(Task<string> read) => read.IsCompletedSuccessfully ? read.Result : string.Empty;
}
