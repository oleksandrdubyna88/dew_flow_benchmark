using Bench.Domain.Probes;
using Bench.Domain.Runs;

namespace Bench.Infrastructure.Persistence;

// The two probe_* tables. The publication guard is STRUCTURAL first, as for the gate: no column below can hold an answer, a
// prompt, a transcript or a path on this machine — only ids, hashes, enum NAMES, numbers, the frozen subjects' REFERENCES
// (environment variable names) and artefact paths relative to the artefact root (D11). There is no free-text column at all:
// the one "why" a row carries is ProbeReason, an allow-listed word. ProbeEntitiesGuardTests walks both types.

/// <summary>One <c>bench probes run</c> invocation — the campaign its cells belong to. No status column (D3): open or finished is
/// read off the cells. The subjects are frozen as four parallel lists (the <c>GateSuiteTaskRow</c> shape).</summary>
public sealed class ProbeRunRow
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The frozen web oracle — a semver, read once at <c>run</c> (D7).</summary>
    public string OracleVersion { get; set; } = string.Empty;

    public OracleSource OracleSource { get; set; }

    public int Repeats { get; set; }

    public bool ArtifactsPruned { get; set; }

    public List<string> SubjectIds { get; set; } = [];

    /// <summary>The runtime WORDS' enum names, parallel to <see cref="SubjectIds"/>.</summary>
    public List<string> SubjectRuntimes { get; set; } = [];

    public List<string> SubjectModels { get; set; } = [];

    /// <summary>Environment variable NAMES, never paths (D4).</summary>
    public List<string> SubjectExecutableRefs { get; set; } = [];

    /// <summary>The api subject's coai vendor id (S2); empty for a CLI subject. Parallel to <see cref="SubjectIds"/>.</summary>
    public List<string> SubjectVendors { get; set; } = [];

    /// <summary>The api subject's PUBLIC vendor base url — the one probe column the guard checks by the endpoint rule
    /// (<see cref="ProbeModel.PublicUrlColumns"/>); empty for a CLI subject.</summary>
    public List<string> SubjectEndpoints { get; set; } = [];

    /// <summary>The api subject's wire dialect word; empty for a CLI subject.</summary>
    public List<string> SubjectDialects { get; set; } = [];

    /// <summary>The confinement mode's enum NAME per subject (S2b): a claude subject's <c>Denylist</c>/<c>Allowlist</c>/<c>Restricted</c>,
    /// <c>Default</c> for every other runtime. Parallel to <see cref="SubjectIds"/>.</summary>
    public List<string> SubjectConfinements { get; set; } = [];

    /// <summary>The probes the run was ASKED for (S4) — enum NAMES, in the order asked. Frozen because the planner's dropped pairs
    /// are recomputed from them and the frozen subjects: a probe every subject dropped plans no cell, so the cells alone cannot
    /// name it.</summary>
    public List<string> Probes { get; set; } = [];

    public List<ProbeCellRow> Cells { get; set; } = [];
}

/// <summary>One cell — its lineage and generation, its claim, the pin it was claimed under, and, once settled, its facts and
/// its artefact refs.</summary>
public sealed class ProbeCellRow
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public ProbeRunRow? Run { get; set; }

    public ProbeKind Probe { get; set; }

    public string SubjectId { get; set; } = string.Empty;

    public int Repeat { get; set; }

    public int Generation { get; set; }

    public int Slot { get; set; }

    public int Position { get; set; }

    public CellState State { get; set; }

    public int Attempts { get; set; }

    /// <summary>How many of <see cref="Attempts"/> were handed back unmeasured (S2c) — a quota stop, an unwritable artefact root. The
    /// sweep abandons on <c>Attempts - UnmeasuredAttempts</c>, so a quota stop is never a step toward Abandoned (D8).</summary>
    public int UnmeasuredAttempts { get; set; }

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

    public ProbeAttemptKind AttemptKind { get; set; }

    public bool ExitCodeCaptured { get; set; }

    public long ExitCode { get; set; }

    public ProbeFact CanaryRead { get; set; }

    public ProbeFact ReadAttempted { get; set; }

    public ProbeFact AnswerCurrent { get; set; }

    public ProbeFact ToolEvidence { get; set; }

    public ProbeFact ShellUsed { get; set; }

    public ProbeFact ReaderOffered { get; set; }

    public ProbeFact Reachable { get; set; }

    public ProbeFact AccountOut { get; set; }

    /// <summary>The attempt's artefacts as four parallel lists: kind names, paths RELATIVE to the artefact root, hashes, lengths.</summary>
    public List<string> ArtifactKinds { get; set; } = [];

    public List<string> ArtifactPaths { get; set; } = [];

    public List<string> ArtifactSha256s { get; set; } = [];

    public List<long> ArtifactLengths { get; set; } = [];

    public ProbeReason Reason { get; set; }
}
