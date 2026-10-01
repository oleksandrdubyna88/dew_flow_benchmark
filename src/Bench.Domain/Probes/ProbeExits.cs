using System.Text.Json;

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

/// <summary>What <c>coai-mcp --probe-api</c> printed, read: the completion cases' HTTP statuses and whether any of them said the
/// account is out. <paramref name="Captured"/> is false when stdout was not the report at all.</summary>
public sealed record ProbeApiReport(bool Captured, IReadOnlyList<int> Statuses, bool AccountMarker)
{
    public static ProbeApiReport NotCaptured { get; } = new(false, [], false);
}

/// <summary>What <c>coai-mcp --probe-api</c> prints (D6) — as coai-mcp 0.40.3 printed it on 2026-10-01 (S2b, finding 4;
/// <c>tests/Bench.Tests/Fixtures/probes/coai-mcp-0.40.3-probe-api-grok-403.json</c>): ONE JSON object on stdout —
/// <c>{vendor, endpoint, dialect, model, models{status, ids, error}, requests[{model, case, status, refusedField, error, …}]}</c> —
/// with the progress lines on stderr. The S2 guess of <c>HTTP ddd</c> text lines is withdrawn.
/// <para>
/// The <c>requests</c> are the completion cases; the deliberate <c>wrong_key</c> case (a 400 the product provokes on purpose) is
/// excluded from both facts. The account is OUT on a 401/402/403, or on the vendor's own wording — the live grok run answered 403
/// on every case with "…has either used all available credits or reached its monthly spending limit…".
/// </para></summary>
public static class ProbeApiOutput
{
    private const string WrongKeyCase = "wrong_key";

    private static readonly string[] AccountMarkers = ["credits", "spending limit", "spend limit", "quota", "usage limit", "credit balance", "insufficient"];

    public static ProbeApiReport Read(string stdout)
    {
        using var document = ProbeJson.Parse(stdout);

        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("requests", out var requests) || requests.ValueKind != JsonValueKind.Array)
        {
            return ProbeApiReport.NotCaptured;
        }

        var cases = requests.EnumerateArray().Where(r => ProbeJson.Text(r, "case") != WrongKeyCase).ToList();

        return new ProbeApiReport(
            true,
            [.. cases.Where(r => ProbeJson.Number(r, "status", out _)).Select(r => ProbeJson.Number(r, "status", out var status) ? (int)status : 0)],
            cases.Any(r => AccountMarkers.Any(m => ProbeJson.Text(r, "error").Contains(m, StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>Whether the vendor refused the ACCOUNT: a 401, 402 or 403 among the completion cases, or the credits / spending-limit /
    /// quota wording in one; <c>no</c> when the report was read and none says so; <i>not captured</i> when there was no report.</summary>
    public static ProbeFact AccountOut(ProbeApiReport report) =>
        (report.Captured, report.Statuses.Any(s => s is 401 or 402 or 403) || report.AccountMarker) switch
        {
            (false, _) => ProbeFact.NotCaptured,
            (_, true) => ProbeFact.Yes,
            _ => ProbeFact.No,
        };
}
