namespace Bench.Contracts;

/// <summary>The scout calibration's report — <c>bench scouts report --json</c> prints this object, and the read API of E3 S8 will answer the
/// SAME object, serialised with the web defaults, so the two surfaces cannot read a run two ways (the probes' rule).
/// <para>
/// <b>No free text travels here.</b> Every string is an id, a closed word, a model id, a hash, a repository slug or a repository-relative
/// path's count — never a scout's report, a claim or an assessor's note. A reflection test holds each one to an allow-list by
/// <c>Type.Property</c> (<c>ScoutContractsGuardTests</c>).
/// </para></summary>
/// <param name="Kind"><c>preliminary</c> or <c>decision</c> — frozen on the run (plan D7).</param>
/// <param name="Verdict"><c>preliminary</c> (a preliminary run states no verdict: "preliminary — not a verdict"), <c>rule-change-shown</c>
/// (a decision run whose W1, W2 and W3 all passed) or <c>not-shown</c>.</param>
public sealed record ScoutRunReportDto(
    Guid RunId,
    DateTimeOffset CreatedAt,
    string Kind,
    string Verdict,
    ScoutPinsDto Pins,
    int Seed,
    int Repeats,
    ScoutProgressDto Progress,
    ScoutLedgerDto Ledger,
    ScoutBootstrapDto Bootstrap,
    IReadOnlyList<ScoutWorkloadDto> Workloads,
    IReadOnlyList<ScoutArmReportDto> Arms,
    IReadOnlyList<ScoutTaskReportDto> Tasks);

/// <summary>The pins the run was frozen to (§3.4): the CLI version, the plugin cache's hash, the resolved ids behind the two aliases, the
/// operator's settings hash.</summary>
public sealed record ScoutPinsDto(string CliVersion, string PluginCacheHash, string ResolvedOpus, string ResolvedSonnet, string SettingsHash);

/// <summary>Where the run stands, read off its cells (a run has no status of its own).</summary>
public sealed record ScoutProgressDto(int Pending, int Claimed, int Settled, int Abandoned, bool Open);

/// <summary>The spend ledger (§3.10): the ceiling, what is still reserved, what settled from a <c>result</c>, what was charged as unknown, and
/// the identity <c>spent = settled + unknown</c>; with the attempts behind it, and (S6) how many of them settled ABOVE their arm's cap — an
/// overshoot is settled at its real total, never clipped, and counted.</summary>
public sealed record ScoutLedgerDto(
    decimal CeilingUsd, decimal ReservedUsd, decimal SettledUsd, decimal UnknownUsd, decimal SpentUsd, decimal FreeUsd, int Attempts, int UnknownCharges, int Overshoots);

/// <summary>How the intervals were drawn (§3.8): the fixed seed, the resamples, the one-sided percentiles, and D5's margins.</summary>
public sealed record ScoutBootstrapDto(int Seed, int Resamples, double LowerPercentile, double UpperPercentile, double RecallMargin, double WrongMargin);

/// <summary>One workload's paired comparison: the deciding arms, the outcome word (<c>pass</c>, <c>fail</c>, <c>not-shown</c>,
/// <c>preliminary</c>), the reason words, and the two intervals of B − A.</summary>
public sealed record ScoutWorkloadDto(
    string Workload, string ArmA, string ArmB, string Outcome, IReadOnlyList<string> Reasons, ScoutIntervalDto Recall, ScoutIntervalDto WrongPerTen);

/// <summary>A paired difference B − A over tasks, with its one-sided 95 % bounds (lower = 5th percentile, upper = 95th) and how many tasks
/// and repositories it rests on. <paramref name="Estimable"/> is false with fewer than two repositories.</summary>
public sealed record ScoutIntervalDto(bool Known, double Point, double Lower, double Upper, int Tasks, int Clusters, bool Estimable);

/// <summary>A number that may not be one: <c>known</c>, <c>not-captured</c> (no cell carried it), or <c>not-measured</c> (§3.6: the arm's
/// wrong-claim result cannot be stated — its <c>wrongGap</c> word says why).</summary>
public sealed record ScoutFigureDto(bool Known, double Value, string State)
{
    public const string KnownState = "known";
    public const string NotCapturedState = "not-captured";
    public const string NotMeasuredState = "not-measured";
}

/// <summary>One arm: what it is, every metric of §3.6 (means over its tasks or its scored cells), and its counts.</summary>
/// <param name="WrongGap"><c>none</c>, <c>silent-cells</c> (more than 10 % of its scored cells have no decided claim), <c>claims-unassessed</c>
/// or <c>no-scored-cell</c>.</param>
public sealed record ScoutArmReportDto(
    string Arm,
    string Workload,
    string Model,
    int Scouts,
    string Agent,
    ScoutFigureDto Recall,
    ScoutFigureDto WrongPerTen,
    string WrongGap,
    ScoutFigureDto TeamMs,
    ScoutFigureDto SlowestScoutMs,
    ScoutFigureDto ScoutMs,
    ScoutFigureDto SessionMs,
    ScoutTokensDto Tokens,
    ScoutFigureDto TeamUsd,
    ScoutFigureDto SessionUsd,
    ScoutFigureDto UsdPerRightFile,
    ScoutFigureDto CallerLoad,
    ScoutCountsDto Counts);

/// <summary>Tokens per scored cell: the team's (exact), and one scout's — its output only where it was the one scout on its model.</summary>
public sealed record ScoutTokensDto(
    ScoutFigureDto TeamInput, ScoutFigureDto TeamOutput, ScoutFigureDto TeamCacheRead, ScoutFigureDto TeamCacheWrite, ScoutFigureDto ScoutInput, ScoutFigureDto ScoutOutput)
{
    /// <summary>The team's cache writes by TTL (2026-10-14) — what the report's cost split prices apart.</summary>
    public ScoutFigureDto TeamCacheWrite5m { get; init; } = new(false, 0, ScoutFigureDto.NotCapturedState);

    public ScoutFigureDto TeamCacheWrite1h { get; init; } = new(false, 0, ScoutFigureDto.NotCapturedState);
}

/// <summary>An arm's cells by how they ended, and its scored cells' claims by what became of them.</summary>
public sealed record ScoutCountsDto(
    int Cells, int Scored, int Void, int Stopped, int Aborted,
    int SilentCells, int SilentScouts, int UnparseableClaims, int WrongClaims, int DecidedClaims, int UnresolvedClaims, int UnassessedClaims,
    int TeamTimeBelowSlowestScout);

/// <summary>One task: its repository, its kind, the size of G, and each arm's numbers on it (its runs averaged first).</summary>
/// <param name="Repository">A public task's slug or url; a PRIVATE task's LABEL — its id — and nothing else (plan D10, the one projection).</param>
/// <param name="Privacy"><c>public</c> or <c>private</c>.</param>
/// <param name="Language">The repository's language word (<c>PHP</c>, <c>C#</c>); empty when a public task named none.</param>
public sealed record ScoutTaskReportDto(string Task, string Repository, string Privacy, string Language, string Kind, int GroundTruth, IReadOnlyList<ScoutTaskArmDto> Arms);

public sealed record ScoutTaskArmDto(string Arm, int Cells, int Scored, ScoutFigureDto Recall, ScoutFigureDto WrongPerTen, ScoutFigureDto TeamMs, ScoutFigureDto TeamUsd);
