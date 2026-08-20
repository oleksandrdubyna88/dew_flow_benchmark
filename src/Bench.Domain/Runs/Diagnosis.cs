using Bench.Domain.Suites;

namespace Bench.Domain.Runs;

/// <summary>What the Investigate phase delivers (todo/PLAN_investigate_vs_implement.md §3.2): where the
/// defect is caused, why it happens, and — optionally — what should change.
/// <para>
/// A structured artefact rather than prose, because the arm's whole point is that a diagnosis is
/// SCORED: anchors match mechanically against the reference fix's touched members, the mechanism text
/// takes the ordinary answer expectations, and the arbiter reads the whole answer beside both.
/// </para></summary>
public sealed record Diagnosis(
    IReadOnlyList<DiagnosisAnchor> Anchors,
    string Mechanism,
    string FixIntent);

/// <summary>One place a diagnosis names. The path is the only required part; the member and the line
/// span are each optional — but an anchor that carries NEITHER is a bare file claim, and a bare file
/// claim does not answer for a member-level truth (<see cref="DiagnosisScoring"/>): "somewhere in this
/// file" would inflate recall on every truth the file holds.</summary>
public sealed record DiagnosisAnchor(string Path, string Member, LineSpan Lines);

/// <summary>How an Investigate answer read. Three states, never two: a model that cannot follow the
/// output contract is a real, reportable fact — <c>Diagnosis parses</c> is its own metric — but it is a
/// DIFFERENT fact from a wrong diagnosis, and the two must not share a zero.</summary>
public abstract record DiagnosisReading
{
    private DiagnosisReading() { }

    /// <summary>The contract was followed. <paramref name="Said"/> is what the model wrote around the
    /// JSON — kept, never dropped: the arbiter reads it, and it is how contract-shaped answers become
    /// visible at all (the authoring pass's own lesson).</summary>
    public sealed record Parsed(Diagnosis Diagnosis, string Said) : DiagnosisReading;

    /// <summary>The answer carries no JSON object anywhere.</summary>
    public sealed record Absent : DiagnosisReading;

    /// <summary>Something object-shaped was there and could not be read as a diagnosis. Carries the
    /// parser's own words and a sample of what was said, because the next edit to the phase prompt is
    /// made from exactly that text.</summary>
    public sealed record Malformed(string ParseError, string Sample) : DiagnosisReading;
}
