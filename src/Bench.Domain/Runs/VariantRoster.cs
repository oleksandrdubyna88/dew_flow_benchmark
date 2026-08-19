using Bench.Domain.Variants;

namespace Bench.Domain.Runs;

/// <summary>One variant of a run, resolved from the catalog into the recipe a leg actually runs.</summary>
public sealed record VariantChoice(VariantSelection Selection, VariantDefinition Definition)
{
    /// <summary>What the engine said it computed on when this variant was approved — the ECHO, not the
    /// recipe's claim. It rides on the choice because the approval is where it is read, while the run's
    /// engine identity is built afterwards, once every variant has been approved.</summary>
    public Retrieval.BackendDeclaration Served { get; init; } = Retrieval.BackendDeclaration.None;

    /// <summary>The control arm: no variant named, no retrieval performed. What every run planned before
    /// the catalog existed runs, and what it must keep running unchanged.</summary>
    public static VariantChoice Baseline { get; } = new(VariantSelection.None, VariantDefinition.NoRetrieval);
}

/// <summary>Every retrieval variant a run measures, resolved once and looked up per leg.
/// <para>
/// The same shape as <see cref="SubjectRoster"/>, and for the same reason: the variant is an AXIS of the
/// matrix, so one run holds several and each cell names the one it belongs to. A runner holding a single
/// recipe would send every leg through the first one and label the results with the cell's variant — the
/// defect the subject roster was introduced to end, in the other axis.
/// </para>
/// <para>
/// A cell whose variant is not here is a refusal, never a fallback to the first recipe: a leg measured
/// under a configuration it was not planned for is a number no report can catch.
/// </para></summary>
public sealed record VariantRoster(IReadOnlyList<VariantChoice> Entries)
{
    /// <summary>A run with no variants at all — every cell is the control arm.</summary>
    public static VariantRoster Baseline { get; } = new([]);

    public static VariantRoster Of(IReadOnlyList<VariantChoice> entries) => new(entries);

    public IReadOnlyList<VariantSelection> Selections => [.. Entries.Select(e => e.Selection)];

    /// <summary>The backend this run's engine served on, when every approved variant agrees about it.
    /// <para>
    /// One run points at one engine, so agreement is the ordinary case and disagreement means the engine
    /// answered inconsistently across the variants it was asked about. That is not something to average or
    /// to pick a winner from: the honest reading is <c>not declared</c>, which is exactly the state that
    /// exists for "nothing consistent is known".
    /// </para></summary>
    public Retrieval.BackendDeclaration Served =>
        Entries.Select(e => e.Served).Distinct().ToList() is [var only] ? only : Retrieval.BackendDeclaration.None;

    /// <summary>The recipe for one cell's variant.
    /// <para>
    /// A cell planned without a variant resolves to the baseline, which is what makes this lane additive:
    /// every run that exists today keeps running exactly as it did.
    /// </para></summary>
    public Outcome<VariantChoice> For(VariantSelection selection) =>
        selection switch
        {
            VariantSelection.Selected chosen => Find(chosen),
            _ => Outcome<VariantChoice>.Success(VariantChoice.Baseline),
        };

    public string Describe => Entries.Count == 0 ? "no variants" : string.Join(", ", Entries.Select(e => e.Selection.Canonical));

    private Outcome<VariantChoice> Find(VariantSelection.Selected chosen) =>
        Entries.FirstOrDefault(e => e.Selection is VariantSelection.Selected s && s.Id == chosen.Id) is { } entry
            ? Outcome<VariantChoice>.Success(entry)
            : Outcome<VariantChoice>.Failure(
                $"this run has no recipe for variant '{chosen.Name}' — it measures {Describe}. Running the leg under "
                + "another variant's recipe would label the result with this one, and nothing downstream could tell");
}
