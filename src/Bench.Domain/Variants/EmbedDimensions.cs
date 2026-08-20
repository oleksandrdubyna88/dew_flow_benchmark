namespace Bench.Domain.Variants;

/// <summary>How wide the vectors in a corpus are — <c>1024</c> for bge-m3 dense, <c>768</c> for a base model.
///
/// <para><b>Why a corpus axis needs it.</b> Without it two corpora built by different models hash as ONE
/// configuration whenever their names happen to match, and a name is exactly what does not identify a model:
/// an operator writes <c>bge-m3</c> into a catalog row while the engine answers
/// <c>BAAI/bge-m3 (dense, FP32)</c>, and the comparison already normalises across that gap. The width does
/// not normalise. It is the one fact about an embedder that is checkable, numeric, and impossible to spell
/// two ways.</para>
///
/// <para><b>Three states, never two.</b> <see cref="NotDeclared"/> is what every corpus written before this
/// existed IS, and it must not read as a mismatch: an engine that has not been taught to report its width has
/// not disagreed with the recipe, it has said nothing. Matched · mismatched · not declared — the shape
/// <c>IndexCommit</c> and <c>BackendDeclaration</c> already carry, for the same reason.</para>
///
/// <para><b>It stays OUT of the canonical form until declared</b>, which is not a detail. A variant is added
/// and retired, never edited, so its hash is its identity and a stored row must keep hashing as it did. A
/// width appended unconditionally would silently re-identify every variant in the catalog — the regression
/// <c>todo/PLAN_corpus_axis_integrity.md</c> names in its own test plan.</para>
/// </summary>
public readonly record struct EmbedDimensions
{
    private EmbedDimensions(int value) => Value = value;

    /// <summary>The width, or <c>0</c> when nothing declared one. Read <see cref="Declared"/> rather than
    /// testing this against zero: a width of zero is not a narrow corpus, it is an absent fact.</summary>
    public int Value { get; }

    public bool Declared => Value > 0;

    /// <summary>What every corpus indexed before anybody recorded a width is.</summary>
    public static EmbedDimensions NotDeclared { get; }

    /// <summary>A width, or nothing when the number cannot be one. Zero and negatives are
    /// <see cref="NotDeclared"/> rather than an error, exactly as <c>BackendDeclaration.Read</c> treats an
    /// unreadable value: a number this side cannot use is a thing it does not know, and that state exists.</summary>
    public static EmbedDimensions Of(int value) => value > 0 ? new EmbedDimensions(value) : NotDeclared;

    /// <summary>Appended to a canonical form only when declared — see the type's note on hash stability.</summary>
    public string Canonical => Declared ? $"/dim={Value}" : string.Empty;

    public string Describe => Declared ? $"{Value} dimensions" : "width not declared";

    /// <summary>Why these two widths cannot be the same corpus, or empty when nothing rules that out.
    /// <para>
    /// Silent when EITHER side is undeclared, which is the three-state rule doing its job: a recipe naming a
    /// width against an engine that reports none has not been contradicted, and blocking there would refuse
    /// every engine that has not yet been taught to answer. The absence is surfaced as a WARNING instead, so
    /// a reader knows the axis went unverified rather than verified-and-matched.
    /// </para></summary>
    public string Refuse(EmbedDimensions other) =>
        Declared && other.Declared && Value != other.Value
            ? $"the corpus that answers holds {Value}-dimensional vectors and the recipe names {other.Value} — "
                + "two embedders, and the row naming one would describe the numbers of the other"
            : string.Empty;
}
