using System.Text.RegularExpressions;
using Bench.Domain.Gate;

namespace Bench.Domain.Probes;

/// <summary>How an attempt's EXIT reads (S2): zero is an answer to read; a usage error is a launch REFUSED by this build —
/// the argv was wrong for it, every fact <i>not captured</i>, the exit code kept —; every other non-zero exit is FAILED,
/// nothing measured, the exit code and the stderr artefact saying why.
/// <para>
/// Per CLI, because each has its own usage exit: codex (clap) exits <b>2</b>; claude (commander) exits 1 and says
/// <c>error: unknown option</c> on stderr; agy (yargs lineage) says <c>Unknown argument</c>; <c>coai-mcp --probe-api</c> exits
/// <b>65</b> for bad arguments and <b>78</b> when the vault or the key is missing (measured 2026-10-01). What a live build
/// prints beyond the exit code is S5's first-cell hand-check; until then a claude or agy exit that names no usage error is
/// FAILED, never a refusal.
/// </para></summary>
public static class ProbeExits
{
    /// <summary>EX_USAGE — <c>coai-mcp --probe-api</c> was given bad arguments.</summary>
    public const int CoaiUsageExit = 65;

    /// <summary>EX_CONFIG — <c>coai-mcp --probe-api</c> found no vault or no key: nothing about the vendor can be measured.</summary>
    public const int CoaiNoVaultExit = 78;

    private static readonly string[] ClaudeUsage = ["error: unknown option", "error: missing required argument", "error: too many arguments", "error: option '"];

    private static readonly string[] AgyUsage = ["Unknown argument", "unknown option", "Not enough arguments", "Missing required argument"];

    public static ProbeAttemptKind Classify(ProbeRuntime runtime, int exitCode, string stderr) =>
        (exitCode, IsUsageError(runtime, exitCode, stderr)) switch
        {
            (0, _) => ProbeAttemptKind.Answered,
            (_, true) => ProbeAttemptKind.LaunchRefused,
            _ => ProbeAttemptKind.Failed,
        };

    private static bool IsUsageError(ProbeRuntime runtime, int exitCode, string stderr) => runtime switch
    {
        ProbeRuntime.Codex => exitCode == 2,
        ProbeRuntime.Claude => exitCode != 0 && ClaudeUsage.Any(m => stderr.Contains(m, StringComparison.OrdinalIgnoreCase)),
        ProbeRuntime.Antigravity => exitCode != 0 && AgyUsage.Any(m => stderr.Contains(m, StringComparison.OrdinalIgnoreCase)),
        ProbeRuntime.Api => exitCode == CoaiUsageExit,
        _ => false,
    };
}

/// <summary>What <c>coai-mcp --probe-api</c> printed (D6): status codes, model ids, refused fields and token counts, the vendor's
/// text redacted. Read for the HTTP statuses its rows answered with, and whether the key was refused — a 401/402/403, or an
/// account marker in the text. Statuses nobody printed leave <c>reachable</c> <i>not captured</i>.</summary>
public static partial class ProbeApiOutput
{
    [GeneratedRegex(@"(?:\bHTTP\b|\bstatus\b)[ :=]*(\d{3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex Status { get; }

    public static IReadOnlyList<int> Statuses(string stdout) =>
        [.. Status.Matches(stdout).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))];

    /// <summary>Whether the vendor refused the ACCOUNT: a 401, 402 or 403 among the statuses, or a marker in the text (the same
    /// reading a CLI's stdout gets); <c>no</c> when statuses were captured and none refuses; <i>not captured</i> otherwise.</summary>
    public static ProbeFact KeyRefused(IReadOnlyList<int> statuses, string text) =>
        (statuses.Any(s => s is 401 or 402 or 403) || ReviewerAccountOut.CliReason(text).Length > 0, statuses.Count > 0) switch
        {
            (true, _) => ProbeFact.Yes,
            (_, true) => ProbeFact.No,
            _ => ProbeFact.NotCaptured,
        };
}
