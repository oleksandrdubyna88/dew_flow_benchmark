using Bench.Contracts;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Components;

/// <summary>The per-model table for one scope under ONE rubric — the operator's columns.
///
/// <para><b>The verdict columns are headed in the rubric's own words and with its id</b>: a strict reading asks whether
/// trigger, mechanism and consequence are right at the code, a lenient one whether a finding was worth having. A heading
/// that said only "supported" would let a reader carry one into the other, so the kinds never share a column — and the
/// strict-only columns (partial, high value, overstated) are not drawn under a lenient rubric at all.</para>
///
/// <para>Every figure goes through <see cref="GateFigureWords"/>: <c>—</c> nobody looked, <i>unknown</i> nothing captured,
/// <i>not hand-checked</i> a strict percentage no person has checked — never a zero.</para></summary>
public partial class GateModelTable : ComponentBase
{
    private static readonly GateModelTableDto NoTable = new(
        new GateScopeDto(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, [], [], false),
        string.Empty, string.Empty, [], [], [], [], []);

    [Parameter, EditorRequired]
    public GateModelTableDto Table { get; set; } = NoTable;

    [Parameter, EditorRequired]
    public IReadOnlyList<GateModelRowDto> Rows { get; set; } = [];

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string Note { get; set; } = string.Empty;

    /// <summary>The table's <c>data-test</c> name — three tables of one shape sit on one page.</summary>
    [Parameter]
    public string Test { get; set; } = "gate-model-table";

    private bool IsStrict => Table.RubricKind != "LenientWorth";

    /// <summary>A rubric kind as a reader says it.</summary>
    public static string KindWords(string kind) => kind == "LenientWorth" ? "lenient: worth having?" : "strict: trigger, mechanism and consequence";

    private string SupportedHeading => IsStrict ? $"supported % ({Table.RubricId})" : $"worth having % ({Table.RubricId})";

    private string PartialHeading => $"supported+partial % ({Table.RubricId})";

    private string CountsHeading => IsStrict ? "supported · partial · refuted · unresolved" : "worth having · not worth";

    private string Counts(GateModelRowDto r) =>
        IsStrict ? $"{r.Supported} · {r.Partial} · {r.Refuted} · {r.Unresolved}" : $"{r.Supported} · {r.Refuted}";

    private static string W(GateFigureDto figure, string format = "0.##") => GateFigureWords.Of(figure, format);

    private static string Seeds(GateModelRowDto r) =>
        r.SeedsHitMean.Known ? $"{W(r.SeedsHitMean)} ({r.SeedsHitMin}–{r.SeedsHitMax})" : W(r.SeedsHitMean);

    private static string Failures(GateModelRowDto r) =>
        r.Failures.Count == 0 ? GateFigureWords.Dash : string.Join(", ", r.Failures.Select(f => $"{f.FailureKind} × {f.Runs}"));
}
