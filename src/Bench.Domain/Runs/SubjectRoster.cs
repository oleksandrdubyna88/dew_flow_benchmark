using Bench.Domain.Models;

namespace Bench.Domain.Runs;

/// <summary>One subject of a run, and where its legs are actually sent.</summary>
public sealed record RosterEntry(ModelEndpoint Endpoint, Sampling Sampling)
{
    public string ModelId => Endpoint.Model.Id;
}

/// <summary>Every subject a run measures, resolved once and looked up per leg.
/// <para>
/// A run has always been able to plan several subjects — <see cref="Matrix.Plan"/> takes a list — while
/// the runner held exactly ONE endpoint, so a two-subject matrix would have sent every leg to the first
/// model and labelled the results with the cell's subject. That is not a wrong number anybody could see:
/// the report would name two models and measure one.
/// </para>
/// <para>
/// So the roster is keyed by the model id a CELL carries, and a cell whose subject is not in it is a
/// refusal rather than a fallback — an unresolvable subject must never quietly become the first one.
/// </para></summary>
public sealed record SubjectRoster(IReadOnlyList<RosterEntry> Entries)
{
    public static SubjectRoster Of(ModelEndpoint endpoint, Sampling sampling) => new([new RosterEntry(endpoint, sampling)]);

    public static SubjectRoster Of(IReadOnlyList<RosterEntry> entries) => new(entries);

    public IReadOnlyList<Subject> Subjects =>
        [.. Entries.Select(e => new Subject(e.Endpoint.Model, e.Sampling))];

    public Outcome<RosterEntry> For(string modelId) =>
        Entries.FirstOrDefault(e => string.Equals(e.ModelId, modelId, StringComparison.Ordinal)) is { } entry
            ? Outcome<RosterEntry>.Success(entry)
            : Outcome<RosterEntry>.Failure(
                $"this run has no endpoint for subject '{modelId}' — it measures {Describe}. A leg sent to another "
                + "subject's endpoint would be labelled with this one and be invisible in every number afterwards");

    public string Describe => Entries.Count == 0 ? "no subjects" : string.Join(", ", Entries.Select(e => e.ModelId));
}
