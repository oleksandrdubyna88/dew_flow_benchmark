namespace Bench.Contracts;

/// <summary>The capability probes' wire shapes — <c>bench probes report --json</c> prints this object, and the read API
/// (S4 of <c>todo/PLAN_question_consultant_probes.md</c>) answers the SAME object, serialised with the web defaults, so the
/// two surfaces cannot drift into two readings of one run.
/// <para>
/// <b>No free text travels here</b> (D11). Every string is an id, a word from a closed set (<see cref="ProbeWords"/>), a model
/// id, a variable NAME, a public vendor url, a hash, a relative artefact path, a CLI's own <c>--version</c> line or the copyable
/// <c>rerun</c> command built from a cell id — never an answer, a transcript or a sentence somebody wrote. A reflection test
/// holds each one to an allow-list by <c>Type.Property</c>.
/// </para></summary>
/// <param name="Auditable">False once <c>bench probes prune</c> deleted the run's artefacts: the verdicts below can no longer
/// be checked against the files they were read from.</param>
/// <param name="Dropped">The (probe, subject) pairs the planner left out, each with its reason WORD — the write-up's "not
/// measured" list, read off the run rather than off a terminal that printed it once.</param>
public sealed record ProbeRunReportDto(
    Guid RunId,
    DateTimeOffset CreatedAt,
    ProbeOracleDto Oracle,
    int Repeats,
    bool ArtifactsPruned,
    bool Auditable,
    ProbeProgressDto Progress,
    IReadOnlyList<ProbeSubjectDto> Subjects,
    IReadOnlyList<ProbeCellReportDto> Cells,
    IReadOnlyList<ProbeDroppedPairDto> Dropped);

/// <summary>One run in the read API's run list (<c>GET /api/bench/probes/runs</c>) — what the page's run picker offers: when it
/// was made, how big it is, and whether anything is still being measured (<see cref="ProbeProgressDto.Open"/>, read off its
/// cells — a run has no status of its own, D3).</summary>
public sealed record ProbeRunSummaryDto(Guid RunId, DateTimeOffset CreatedAt, int Subjects, int Repeats, bool ArtifactsPruned, ProbeProgressDto Progress);

/// <summary>A pair the planner did not measure: the probe word, the subject id, and why as a closed word —
/// <c>api-probe-on-cli</c>, <c>cli-probe-on-api</c>, <c>no-web-off-flag</c>, <c>no-deny-list</c>.</summary>
public sealed record ProbeDroppedPairDto(string Probe, string Subject, string Reason);

/// <summary>The frozen web oracle (D7): the version of <c>@openai/codex</c> the <c>web-search</c> answers are compared with,
/// and where it came from — <c>registry</c> (read once at <c>run</c>) or <c>manual</c> (<c>--oracle-version</c>).</summary>
public sealed record ProbeOracleDto(string Version, string Source);

/// <summary>Where the run stands, read off its cells (a run has no status of its own, D3). <paramref name="Open"/> while any
/// cell is Pending or Claimed — the page polls while it is.</summary>
public sealed record ProbeProgressDto(int Pending, int Claimed, int Settled, int Abandoned, bool Open);

/// <summary>A subject as the run froze it (D4): references, never values — the executable is a variable NAME; the api
/// subject's endpoint is a public vendor url, empty for a CLI. <paramref name="Confinement"/> is the claude mode the subject's
/// tools were confined under (<c>denylist</c>, <c>allowlist</c>, <c>restricted</c>; <c>default</c> on every other runtime — S2b).</summary>
public sealed record ProbeSubjectDto(string Id, string Runtime, string Model, string ExecutableRef, string Confinement, string Vendor, string Endpoint, string Dialect);

/// <summary>One lineage (probe × subject × repeat) as the report reads it: the highest SETTLED generation when there is one
/// (D2), else the highest generation as it stands. <paramref name="LatestGeneration"/>/<paramref name="LatestState"/> say
/// whether a newer generation is being measured over the one shown.</summary>
/// <param name="VoidedByControl">The subject's <c>read-inside</c> control read <c>no</c>, so this read probe's
/// <c>canaryRead</c> is shown <c>not-captured</c> (<c>ProbeVerdicts.UnderControl</c>).</param>
/// <param name="RerunCommand">The copyable re-measurement — <c>bench probes rerun --cell &lt;id&gt;</c> — the tab shows it
/// rather than offering a button (D10).</param>
/// <param name="UnmeasuredAttempts">How many of <paramref name="Attempts"/> were handed back unmeasured (S2c) — a quota stop, an
/// unwritable artefact root — and count for nothing toward abandonment.</param>
public sealed record ProbeCellReportDto(
    Guid CellId,
    string Probe,
    string Subject,
    int Repeat,
    int Generation,
    string State,
    int Attempts,
    int UnmeasuredAttempts,
    string Kind,
    ProbeExitDto Exit,
    ProbeFactsDto Facts,
    bool VoidedByControl,
    string Reason,
    ProbePinDto Pin,
    IReadOnlyList<ProbeArtifactDto> Artifacts,
    int LatestGeneration,
    string LatestState,
    string RerunCommand);

/// <summary>An exit code that may not have been captured — a timed-out or never-launched attempt has none, and it is never
/// rendered as a zero.</summary>
public sealed record ProbeExitDto(bool Captured, long Code);

/// <summary>Every fact of an attempt as a word — <c>yes</c>, <c>no</c> or <c>not-captured</c>; never a bool, because a gap in
/// the instrumentation must not read as <c>no</c>. <paramref name="ShellUsed"/> (a code-running tool was called) and
/// <paramref name="ReaderOffered"/> (the CLI offered a file-capable tool at all) are S2b's: which tool reached a file, and whether a
/// missing canary is confinement by absence.</summary>
public sealed record ProbeFactsDto(
    string CanaryRead, string ReadAttempted, string AnswerCurrent, string ToolEvidence, string ShellUsed, string ReaderOffered, string Reachable, string AccountOut);

/// <summary>The CLI build that answered (D9): its <c>--version</c> line and the hash of its bytes. Empty for a cell nobody claimed.</summary>
public sealed record ProbePinDto(string Version, string BinarySha256);

/// <summary>One committed file of the shown attempt: its kind, its path RELATIVE to the artefact root, its SHA-256, its length.</summary>
public sealed record ProbeArtifactDto(string Kind, string Path, string Sha256, long Length);

/// <summary>The closed vocabularies the probe DTOs carry — one spelling per value, shared by the CLI's text and the page.</summary>
public static class ProbeWords
{
    public const string Yes = "yes";
    public const string No = "no";
    public const string NotCaptured = "not-captured";

    public const string Pending = "pending";
    public const string Claimed = "claimed";
    public const string Settled = "settled";
    public const string Abandoned = "abandoned";

    /// <summary>The verb a cell is re-measured with, as the CLI accepts it.</summary>
    public const string RerunPrefix = "bench probes rerun --cell ";
}
