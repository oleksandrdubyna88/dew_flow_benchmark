using Bench.Domain.Targets;

namespace Bench.Domain.Runs;

/// <summary>The two things that must be identical before two results may be put beside each other.
/// <para>
/// Everything else — engine, model, lane, repeat — is a dimension you compare ALONG. The target and
/// the suite are what you compare WITHIN, and mixing them is the most expensive mistake this whole
/// project has on record: a series of configurations measured over weeks turned out to have been
/// scored against a corpus that had been rebuilt underneath them, which retroactively demoted every
/// one of those conclusions back to a hypothesis.
/// </para></summary>
public readonly record struct ComparisonScope(string TargetCanonical, string SuiteStamp)
{
    public string Describe => $"{TargetCanonical} / {SuiteStamp}";
}

/// <summary>The full identity of one result row.</summary>
public sealed record MeasurementTuple(
    MeasurementTarget Target,
    EngineRef Engine,
    string SuiteStamp,
    Subject Subject,
    Lane Lane,
    int Repeat)
{
    public ComparisonScope Scope => new(Target.Canonical, SuiteStamp);

    public string Canonical =>
        string.Join('|', Target.Canonical, Engine.Canonical, SuiteStamp, Subject.Canonical, Lane.Canonical, Repeat);

    /// <summary>Whether these two rows may be compared at all. Not "should" — <em>may</em>.</summary>
    public static bool Comparable(MeasurementTuple a, MeasurementTuple b) => a.Scope == b.Scope;

    /// <summary>Explains a refusal, so a report can say why instead of silently dropping a row.</summary>
    public static Outcome<ComparisonScope> ScopeOf(IReadOnlyList<MeasurementTuple> rows)
    {
        if (rows.Count == 0)
        {
            return Outcome<ComparisonScope>.Failure("no rows to compare");
        }

        var scopes = rows.Select(r => r.Scope).Distinct().ToList();

        return scopes.Count == 1
            ? Outcome<ComparisonScope>.Success(scopes[0])
            : Outcome<ComparisonScope>.Failure(
                $"rows span {scopes.Count} measurement scopes and are not comparable: {string.Join(" vs ", scopes.Select(s => s.Describe))}");
    }
}
