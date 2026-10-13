using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Infrastructure.Persistence;

// The five scout_* tables. The publication guard is STRUCTURAL first, as for the gate and the probes: no column below can hold a
// scout's report, a claim's text, a transcript or a path on this machine — only ids, hashes, enum NAMES, numbers, model ids, pins,
// repository-relative paths, and ONE sentence by design: the task statement, which is committed to research/ anyway and is held to the
// leak check before it is stored. ScoutEntitiesGuardTests walks every type.

/// <summary>One scout campaign: its frozen pins, seed, arms and task order, and the SPEND LEDGER — ceiling, open reservations, settled
/// cost and unknown charges — moved only by guarded updates. No status column: open or finished is read off the cells.</summary>
public sealed class ScoutRunRow
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int Seed { get; set; }

    public int Repeats { get; set; }

    public string PinCliVersion { get; set; } = string.Empty;

    public string PinPluginCacheHash { get; set; } = string.Empty;

    public string PinResolvedOpus { get; set; } = string.Empty;

    public string PinResolvedSonnet { get; set; } = string.Empty;

    public string PinSettingsHash { get; set; } = string.Empty;

    /// <summary>The arms asked for, as <see cref="ScoutArmId"/> enum NAMES, in order.</summary>
    public List<string> Arms { get; set; } = [];

    /// <summary>The task ids in the run's order — slugs.</summary>
    public List<string> TaskIds { get; set; } = [];

    /// <summary>Each arm's <c>--max-budget-usd</c> cap, parallel to <see cref="Arms"/> — frozen with the run (S2), so a resume launches under
    /// the caps the run was planned with.</summary>
    public List<decimal> ArmCapsUsd { get; set; } = [];

    /// <summary>Each arm's wall in seconds, parallel to <see cref="Arms"/>.</summary>
    public List<int> ArmWallSeconds { get; set; } = [];

    /// <summary>What the run may conclude (S3, plan D7) — preliminary or decision, stored by NAME.</summary>
    public ScoutRunKind Kind { get; set; }

    public decimal CeilingUsd { get; set; }

    public decimal ReservedUsd { get; set; }

    public decimal SettledUsd { get; set; }

    public decimal UnknownUsd { get; set; }

    public List<ScoutTaskRow> Tasks { get; set; } = [];

    public List<ScoutCellRow> Cells { get; set; } = [];
}

/// <summary>One frozen task of one run (§3.1): a PUBLIC repository's slug, P and S, the kind, the statement, G and the removals as
/// repository-relative paths, and the tree's hash.</summary>
public sealed class ScoutTaskRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public ScoutRunRow? Run { get; set; }

    public string TaskId { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public string ParentSha { get; set; } = string.Empty;

    public string FixSha { get; set; } = string.Empty;

    public ScoutQuestionKind Kind { get; set; }

    public string Statement { get; set; } = string.Empty;

    public List<string> GroundTruth { get; set; } = [];

    public List<string> Removals { get; set; } = [];

    public string TreeHash { get; set; } = string.Empty;
}

/// <summary>One cell — its lineage and generation, its claim, the pin it was claimed under, and once settled its outcome, reason and
/// facts. Every fact is a captured flag beside its number, so an absent number never reads as zero.</summary>
public sealed class ScoutCellRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public ScoutRunRow? Run { get; set; }

    public string TaskId { get; set; } = string.Empty;

    public ScoutArmId Arm { get; set; }

    public int Repeat { get; set; }

    public int Generation { get; set; }

    public int Slot { get; set; }

    public int Position { get; set; }

    public CellState State { get; set; }

    public int Attempts { get; set; }

    public string Owner { get; set; } = string.Empty;

    public string OwnerHost { get; set; } = string.Empty;

    public int OwnerPid { get; set; }

    public DateTimeOffset ClaimedAt { get; set; }

    public string PinBinarySha256 { get; set; } = string.Empty;

    public string PinVersionText { get; set; } = string.Empty;

    public string PinGitSha { get; set; } = string.Empty;

    public bool PinDirtyCaptured { get; set; }

    public long PinDirtyFiles { get; set; }

    public string PinCheckedTree { get; set; } = string.Empty;

    public ScoutCellOutcome Outcome { get; set; }

    public ScoutReason Reason { get; set; }

    public bool RecallFoundCaptured { get; set; }

    public long RecallFound { get; set; }

    public bool RecallTotalCaptured { get; set; }

    public long RecallTotal { get; set; }

    public bool TeamMsCaptured { get; set; }

    public long TeamMs { get; set; }

    public bool SessionMsCaptured { get; set; }

    public long SessionMs { get; set; }

    public bool ComputedUsdCaptured { get; set; }

    public decimal ComputedUsd { get; set; }

    public bool ReportedUsdCaptured { get; set; }

    public decimal ReportedUsd { get; set; }

    /// <summary>The scout TEAM's tokens and cost (S3) — exact per team, the main session and the internal calls excluded.</summary>
    public bool TeamTokensCaptured { get; set; }

    public long TeamInputTokens { get; set; }

    public long TeamOutputTokens { get; set; }

    public long TeamCacheReadTokens { get; set; }

    public long TeamCacheWrite5mTokens { get; set; }

    public long TeamCacheWrite1hTokens { get; set; }

    public bool TeamUsdCaptured { get; set; }

    public decimal TeamUsd { get; set; }

    public List<ScoutRecordRow> Scouts { get; set; } = [];
}

/// <summary>One assessed claim (S3, §3.7) — the verdict joined to its claim by (cell, scout ordinal, claim index); the claim's statement only as
/// its SHA-256, its path only when repository-relative, the assessor's model and the rubric's hash. Never the claim's text, never the note.</summary>
public sealed class ScoutClaimRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public Guid CellId { get; set; }

    public int Ordinal { get; set; }

    public int Index { get; set; }

    public string Path { get; set; } = string.Empty;

    public bool LineCaptured { get; set; }

    public long Line { get; set; }

    public string StatementSha256 { get; set; } = string.Empty;

    public ScoutClaimVerdict Verdict { get; set; }

    public string AssessorModel { get; set; } = string.Empty;

    public string RubricSha256 { get; set; } = string.Empty;

    public DateTimeOffset AssessedAt { get; set; }
}

/// <summary>One scout of a settled cell (<see cref="ScoutRecord"/>): numbers, model ids, a prompt hash, and its KEY FILES as
/// repository-relative paths — never its text.</summary>
public sealed class ScoutRecordRow
{
    public Guid Id { get; set; }

    public Guid CellId { get; set; }

    public ScoutCellRow? Cell { get; set; }

    public int Ordinal { get; set; }

    public ScoutFocus Focus { get; set; }

    public string InputModel { get; set; } = string.Empty;

    public string PromptSha256 { get; set; } = string.Empty;

    public string ResolvedModel { get; set; } = string.Empty;

    public bool DurationCaptured { get; set; }

    public long DurationMs { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CacheReadTokens { get; set; }

    public long CacheWrite5mTokens { get; set; }

    public long CacheWrite1hTokens { get; set; }

    public bool OutputSeparable { get; set; }

    public bool CostCaptured { get; set; }

    public decimal CostUsd { get; set; }

    public List<string> KeyFiles { get; set; } = [];

    public int KeyFilesUnparseable { get; set; }

    public int KeyFilesExtra { get; set; }

    public bool Silent { get; set; }

    public int ClaimsParsed { get; set; }

    public int ClaimsUnparseable { get; set; }

    public int ClaimsExtra { get; set; }
}

/// <summary>One paid attempt and its reservation (§3.10): which cell and which claim attempt, the amount reserved, where it stands, and
/// — once settled — what it cost.</summary>
public sealed class ScoutAttemptRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public Guid CellId { get; set; }

    public int Number { get; set; }

    public decimal ReservedUsd { get; set; }

    public ScoutAttemptState State { get; set; }

    public decimal SettledUsd { get; set; }

    public DateTimeOffset ReservedAt { get; set; }
}
