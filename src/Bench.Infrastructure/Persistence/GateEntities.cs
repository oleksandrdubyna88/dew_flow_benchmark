using Bench.Domain.Gate;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Persistence;

// The seven gate_* tables. The publication guard is STRUCTURAL first: no column below can hold a finding's text, a
// prompt, an answer, a repository name or a path on this machine — only ids, hashes, enum names, numbers, the
// reviewer catalog's references and the ONE free-text column, the redacted failure cause on a cell.
// GateEntitiesGuardTests walks every one of these types and holds each text-bearing property to an allow-list
// keyed by Type.Property; a new column is red until it is named there.

/// <summary>One <c>bench gate run</c> invocation — the campaign its cells belong to.</summary>
public sealed class GateRunRow
{
    public Guid Id { get; set; }

    public GateKind Gate { get; set; }

    public string SuiteStamp { get; set; } = string.Empty;

    public DataDirMode DataDirMode { get; set; }

    public GateRunStatus Status { get; set; }

    /// <summary><c>native</c>, or the harness an imported run came from.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>SHA-256 of the prediction text; the text is in the artefact root.</summary>
    public string PredictionHash { get; set; } = string.Empty;

    public bool AllowProductChange { get; set; }

    public List<GateCellRow> Cells { get; set; } = [];
}

/// <summary>One cell — its claim, the pin it was claimed under, and, once settled, its session's facts.</summary>
public sealed class GateCellRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public GateRunRow? Run { get; set; }

    public string TaskId { get; set; } = string.Empty;

    public string ReviewerId { get; set; } = string.Empty;

    public int Repeat { get; set; }

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

    public GateCellOutcomeKind OutcomeKind { get; set; }

    /// <summary>Whether the columns below hold a settled session's facts. False on every unsettled cell.</summary>
    public bool FactsRecorded { get; set; }

    public bool Valid { get; set; }

    public GateVerdictWord Verdict { get; set; }

    public bool ReplyParsed { get; set; }

    public bool FindingsCaptured { get; set; }

    public long Findings { get; set; }

    public int Turns { get; set; }

    public int HttpCalls { get; set; }

    /// <summary>The vendor's own finish words, one per HTTP call (<c>stop</c>, <c>length</c>).</summary>
    public List<string> FinishReasons { get; set; } = [];

    public List<int> Statuses { get; set; } = [];

    public bool TokensInCaptured { get; set; }

    public long TokensIn { get; set; }

    public bool TokensOutCaptured { get; set; }

    public long TokensOut { get; set; }

    public bool TokensCachedCaptured { get; set; }

    public long TokensCached { get; set; }

    public bool TokensReasoningCaptured { get; set; }

    public long TokensReasoning { get; set; }

    public double SecondsTotal { get; set; }

    public double ReviewSeconds { get; set; }

    public List<double> SecondsPerTurn { get; set; } = [];

    public List<bool> CachedPerCallCaptured { get; set; } = [];

    public List<long> CachedPerCall { get; set; } = [];

    public bool CostCaptured { get; set; }

    public decimal CostUsd { get; set; }

    public int Served { get; set; }

    public int Refused { get; set; }

    public FailureKind FailureKind { get; set; }

    /// <summary>THE free-text column: the failure cause, redacted (<see cref="FailureRedaction"/>) before it is
    /// written, and still refused by the publication guard if anything private survived. For a cell that failed
    /// it is also the cell's outcome detail — one sentence, one column.</summary>
    public string FailureText { get; set; } = string.Empty;

    public string SettingsHash { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;

    /// <summary>The handshake's <c>serverInfo.version</c> (<c>Major.Minor.Build</c>).</summary>
    public string ServerVersion { get; set; } = string.Empty;

    /// <summary>SHA-256 over the resolved reference VALUES — a hash, never the values.</summary>
    public string ReferencesHash { get; set; } = string.Empty;

    public int SettingsChecked { get; set; }

    public int SettingsMismatches { get; set; }
}

/// <summary>One finding — HASHES ONLY, by construction of <see cref="GateFinding"/>.</summary>
public sealed class GateFindingRow
{
    public long Id { get; set; }

    /// <summary>The cell whose settled attempt produced it — the session a verdict joins on.</summary>
    public Guid CellId { get; set; }

    public int Attempt { get; set; }

    public int Ordinal { get; set; }

    public FindingSeverity Severity { get; set; }

    public FindingCategory Category { get; set; }

    public bool IsGating { get; set; }

    public int Line { get; set; }

    public string TextHash { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;
}

/// <summary>One assessor verdict on one finding, under one rubric. Written by the assessment (E4); the table and
/// its guard exist now so nothing the assessment adds can bypass either.</summary>
public sealed class GateVerdictRow
{
    public long Id { get; set; }

    public Guid CellId { get; set; }

    public int FindingOrdinal { get; set; }

    public string RubricId { get; set; } = string.Empty;

    public RubricKind RubricKind { get; set; }

    public string RubricHash { get; set; } = string.Empty;

    /// <summary><c>Strict</c>, <c>Lenient</c> or <c>AssessmentFailure</c> — which case of <see cref="Verdict"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public StrictReading Reading { get; set; }

    public ValueLevel Value { get; set; }

    public SeverityFairness SeverityFair { get; set; }

    public Grounding Grounded { get; set; }

    public string ClusterHash { get; set; } = string.Empty;

    /// <summary>The seed id the finding hit, or empty for none.</summary>
    public string SeedHit { get; set; } = string.Empty;

    public bool WorthHaving { get; set; }

    public AssessmentFailureCause FailureCause { get; set; }

    public string AssessorId { get; set; } = string.Empty;

    public string BatchId { get; set; } = string.Empty;

    public string PromptHash { get; set; } = string.Empty;

    public bool AssessorFamilyMatches { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>A person's hand-check of one assessor's verdicts under one rubric, over a set of campaigns (E4): the counts and
/// the HASH of the answered sample file — the file itself (finding text, notes, the person's comments) stays in the
/// artefact root.</summary>
public sealed class GateHandCheckRow
{
    public long Id { get; set; }

    public List<Guid> Campaigns { get; set; } = [];

    public string RubricId { get; set; } = string.Empty;

    public RubricKind RubricKind { get; set; }

    public string RubricHash { get; set; } = string.Empty;

    public string AssessorId { get; set; } = string.Empty;

    public int Read { get; set; }

    public int Agreed { get; set; }

    public string NoteHash { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }
}

/// <summary>One reviewer catalog row, flattened. References are NAMES; the endpoint is a public vendor url or the
/// NAME of the variable holding a machine-local one (<see cref="ReviewerEndpoint"/>).</summary>
public sealed class GateReviewerRow
{
    public string Id { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public ReviewerRuntime Runtime { get; set; }

    public string Model { get; set; } = string.Empty;

    /// <summary>A public vendor url, as a VALUE — the one column the publication guard checks by the endpoint rule
    /// (public address or refused) instead of the plain <c>://</c> rule. Empty when the endpoint is a reference.</summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>The NAME of the variable holding a machine-local endpoint. Empty when the endpoint is a public url.</summary>
    public string EndpointRef { get; set; } = string.Empty;

    public string KeyName { get; set; } = string.Empty;

    public string CredsKeyRef { get; set; } = string.Empty;

    public string ExecutableRef { get; set; } = string.Empty;

    public string RemoteVendor { get; set; } = string.Empty;

    public string Dialect { get; set; } = string.Empty;

    public string ReasoningEffort { get; set; } = string.Empty;

    public int MaxTokens { get; set; }

    public int TimeoutMinutes { get; set; }

    public int FollowUps { get; set; }

    public int ReviewMinutesCap { get; set; }

    public bool Thinking { get; set; }

    public bool PricesKnown { get; set; }

    public decimal InPerMTok { get; set; }

    public decimal CachedPerMTok { get; set; }

    public decimal OutPerMTok { get; set; }

    public long TierFromTokens { get; set; }

    public decimal TierIn { get; set; }

    public decimal TierCached { get; set; }

    public decimal TierOut { get; set; }

    public bool GatePlan { get; set; }

    public bool GateCode { get; set; }

    public bool GateFeature { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public DateTimeOffset RetiredAt { get; set; }
}

/// <summary>A committed artefact: where it is RELATIVE to the artefact root, and the hash and length of its bytes.</summary>
public sealed class GateArtifactRow
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public Guid CellId { get; set; }

    public int Attempt { get; set; }

    public ArtifactClass Class { get; set; }

    public string RelativePath { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Length { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}
