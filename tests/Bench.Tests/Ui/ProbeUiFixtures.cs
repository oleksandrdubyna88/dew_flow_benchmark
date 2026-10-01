using Bench.Contracts;

namespace Bench.Tests.Ui;

/// <summary>Wire shapes for the Probes page's tests — built as <c>GET /api/bench/probes/*</c> answers them, every state and
/// every fact word in its real spelling (<see cref="ProbeWords"/>), so a page test pins what the page does with a real answer.</summary>
internal static class ProbeUiFixtures
{
    public const string RunsRoute = "/api/bench/probes/runs";

    public static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly ProbeFactsDto NothingCaptured = new(
        ProbeWords.NotCaptured, ProbeWords.NotCaptured, ProbeWords.NotCaptured, ProbeWords.NotCaptured,
        ProbeWords.NotCaptured, ProbeWords.NotCaptured, ProbeWords.NotCaptured, ProbeWords.NotCaptured);

    public static string RunRoute(Guid runId) => $"{RunsRoute}/{runId}";

    public static ProbeRunSummaryDto Summary(Guid runId, DateTimeOffset created, bool open) =>
        new(runId, created, 2, 3, false, open ? new ProbeProgressDto(1, 1, 2, 0, true) : new ProbeProgressDto(0, 0, 4, 0, false));

    public static ProbeFactsDto Canary(string word) => NothingCaptured with { CanaryRead = word };

    /// <summary>One lineage's row. <paramref name="latestGeneration"/> defaults to the shown generation (nothing newer in flight).</summary>
    public static ProbeCellReportDto Cell(
        string probe, string subject, int repeat, string state, string kind = "none", ProbeFactsDto? facts = null, ProbeExitDto? exit = null,
        int generation = 1, int latestGeneration = 0, string latestState = "", bool voided = false, string reason = "none", string pin = "",
        Guid? id = null)
    {
        var cellId = id ?? Guid.CreateVersion7();

        return new ProbeCellReportDto(
            cellId, probe, subject, repeat, generation, state, state == ProbeWords.Pending ? 0 : 1, 0, kind,
            exit ?? new ProbeExitDto(false, 0), facts ?? NothingCaptured, voided, reason,
            new ProbePinDto(pin, pin.Length > 0 ? new string('a', 64) : string.Empty), [],
            latestGeneration == 0 ? generation : latestGeneration, latestState.Length == 0 ? state : latestState,
            ProbeWords.RerunPrefix + cellId.ToString("D"));
    }

    /// <summary>A run whole, its progress counted off the cells exactly as the server counts it (open while any is Pending/Claimed).</summary>
    public static ProbeRunReportDto Report(
        Guid runId, IReadOnlyList<ProbeCellReportDto> cells, IReadOnlyList<ProbeDroppedPairDto>? dropped = null, bool pruned = false)
    {
        var pending = cells.Count(c => c.LatestState == ProbeWords.Pending);
        var claimed = cells.Count(c => c.LatestState == ProbeWords.Claimed);
        var settled = cells.Count(c => c.State == ProbeWords.Settled);
        var abandoned = cells.Count(c => c.State == ProbeWords.Abandoned);

        return new ProbeRunReportDto(
            runId, Noon, new ProbeOracleDto("0.52.0", "registry"), 3, pruned, !pruned,
            new ProbeProgressDto(pending, claimed, settled, abandoned, pending + claimed > 0),
            [
                new ProbeSubjectDto("claude-a", "claude", "sonnet", "BENCH_CLAUDE", "denylist", string.Empty, string.Empty, string.Empty),
                new ProbeSubjectDto("codex-b", "codex", "gpt-6-astra", "BENCH_CODEX", "default", string.Empty, string.Empty, string.Empty),
            ],
            cells,
            dropped ?? []);
    }

    /// <summary>A run with one cell in flight (claimed) beside one settled.</summary>
    public static ProbeRunReportDto InFlight(Guid runId) =>
        Report(runId, [
            Cell("read-inside", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0)),
            Cell("read-inside", "codex-b", 1, ProbeWords.Claimed),
        ]);

    /// <summary>The same run, every cell settled.</summary>
    public static ProbeRunReportDto Done(Guid runId) =>
        Report(runId, [
            Cell("read-inside", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0)),
            Cell("read-inside", "codex-b", 1, ProbeWords.Settled, "answered", Canary("no"), new ProbeExitDto(true, 0)),
        ]);
}
