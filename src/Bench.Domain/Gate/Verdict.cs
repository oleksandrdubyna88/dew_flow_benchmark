namespace Bench.Domain.Gate;

/// <summary>The strict rubric's reading: <c>supported</c> only when trigger, mechanism AND consequence are all
/// correct at the code; <c>partial</c> when the core is right and one is wrong or exaggerated.</summary>
public enum StrictReading
{
    Supported,
    Partial,
    Refuted,
    Unresolved,
}

public enum ValueLevel
{
    High,
    Medium,
    Low,
    None,
}

public enum SeverityFairness
{
    Yes,
    Overstated,
    Understated,
}

public enum Grounding
{
    Yes,
    Near,
    No,
}

/// <summary>Why an assessor batch produced no reading for a finding — a VERDICT case, never a gap. A batch
/// that does not parse, is truncated, or names ids it was not given is retried once; if the retry fails the
/// same way every finding of it gets one of these, excluded from every rate and shown as its own count.</summary>
public enum AssessmentFailureCause
{
    Unparseable,
    Truncated,
    UnknownIds,
    NoAnswer,
}

/// <summary>Which seed a finding identified — the same TRIGGER and MECHANISM, not merely the same file — or none.</summary>
public abstract record SeedHit
{
    private SeedHit()
    {
    }

    public sealed record None : SeedHit;

    public sealed record Of(SeedId Seed) : SeedHit;

    public bool IsHit => this is Of;

    public static SeedHit Parse(string? word) =>
        (word ?? string.Empty).Trim() is { Length: > 0 } text && !string.Equals(text, "none", StringComparison.OrdinalIgnoreCase)
            ? SeedId.Parse(text).Match(id => new Of(id), _ => (SeedHit)new None())
            : new None();
}

/// <summary>What an assessor said about one finding — a closed hierarchy, one case per rubric kind plus the
/// failure case. Every case carries the kind of question it answers; the envelope pins the rubric.</summary>
public abstract record Verdict
{
    private Verdict()
    {
    }

    /// <summary>A strict reading. The cluster key and the note are TEXT and live in the artefact store; what is
    /// here is the cluster's hash — equality is all a report needs from it — and the seed id.</summary>
    public sealed record Strict(
        StrictReading Reading,
        ValueLevel Value,
        SeverityFairness SeverityFair,
        Grounding Grounded,
        string ClusterHash,
        SeedHit SeedHit) : Verdict;

    /// <summary>The coai-bench judge's one-turn <i>worth having</i> — imported under its own rubric, shown in
    /// its own column, never in a strict figure.</summary>
    public sealed record Lenient(bool WorthHaving) : Verdict;

    public sealed record AssessmentFailure(AssessmentFailureCause Cause) : Verdict;

    public RubricKind Kind => this switch
    {
        Strict => RubricKind.Strict,
        Lenient => RubricKind.LenientWorth,
        AssessmentFailure => throw new InvalidOperationException("an assessment failure answers no rubric kind — read the envelope's rubric"),
        _ => throw new InvalidOperationException("unreachable"),
    };

    /// <summary>Whether this verdict enters a rate. A failure never does: it is shown as its own count.</summary>
    public bool CountsInRates => this is not AssessmentFailure;

    public bool IsSupported => this is Strict { Reading: StrictReading.Supported };

    public bool IsSupportedOrPartial => this is Strict { Reading: StrictReading.Supported or StrictReading.Partial };

    public bool IsHighValue => this is Strict { Value: ValueLevel.High };

    public bool IsOverstated => this is Strict { SeverityFair: SeverityFairness.Overstated };
}

/// <summary>One verdict row: which finding of which run, under which rubric, by which assessor, in which
/// batch. The join from the blinded id to (run, ordinal) happened through the key in the process that holds
/// the artefact root; the domain sees the joined row.</summary>
public sealed record GateVerdict
{
    private GateVerdict(Guid runId, int findingOrdinal, Rubric rubric, Verdict reading, GateReviewerId assessor, string batchId, string promptHash, bool assessorFamilyMatches)
    {
        RunId = runId;
        FindingOrdinal = findingOrdinal;
        Rubric = rubric;
        Reading = reading;
        Assessor = assessor;
        BatchId = batchId;
        PromptHash = promptHash;
        AssessorFamilyMatches = assessorFamilyMatches;
    }

    public Guid RunId { get; }

    public int FindingOrdinal { get; }

    public Rubric Rubric { get; }

    public Verdict Reading { get; }

    /// <summary>The assessor is a reviewer-catalog row too — a model plus its transport.</summary>
    public GateReviewerId Assessor { get; }

    public string BatchId { get; }

    /// <summary>The hash of the assessor prompt as sent — the rubric text plus the batch's framing.</summary>
    public string PromptHash { get; }

    /// <summary>An assessor of the same vendor family as the reviewer it judges is COUNTED APART, never
    /// refused — the <c>SelfJudged</c> discipline.</summary>
    public bool AssessorFamilyMatches { get; }

    /// <summary>A verdict is issued UNDER a rubric the catalog holds, and its reading must be of that rubric's
    /// kind: a strict reading under the lenient rubric is two populations in one row.</summary>
    public static Outcome<GateVerdict> Under(
        RubricCatalog catalog, string? rubricHash, Guid runId, int findingOrdinal, Verdict reading,
        GateReviewerId assessor, string? batchId, string? promptHash, bool assessorFamilyMatches) =>
        catalog.Resolve(rubricHash).Match(
            rubric => KindMatches(rubric, reading)
                ? Outcome<GateVerdict>.Success(new GateVerdict(
                    runId, findingOrdinal, rubric, reading, assessor, (batchId ?? string.Empty).Trim(), (promptHash ?? string.Empty).Trim(), assessorFamilyMatches))
                : Outcome<GateVerdict>.Failure(
                    $"a {reading.Kind} reading cannot be issued under rubric {rubric.Stamp}, which is {rubric.Kind} — two rubrics never share a row"),
            Outcome<GateVerdict>.Failure);

    private static bool KindMatches(Rubric rubric, Verdict reading) =>
        reading is Verdict.AssessmentFailure || reading.Kind == rubric.Kind;
}
