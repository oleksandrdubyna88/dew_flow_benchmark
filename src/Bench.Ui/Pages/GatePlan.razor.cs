using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary><i>coai models — plan gate</i> (E6): the same view as the feature gate, over plan-gate runs — one
/// <c>review_plan</c> round is the measurement.</summary>
public partial class GatePlan : ComponentBase
{
    private const string Gate = "plan";

    private const string Purpose =
        "Each reviewer over the seeded plan through the product's own review_plan — one round is the measurement; "
        + "imported coai-bench rounds carry the lenient worth-having rubric, in its own column.";

    /// <summary>A scope id from the address, so a link opens on one scope; chosen only when the runs offer it. Absent from
    /// the address it is null — the one optional UI parameter shape the rules allow.</summary>
    [SupplyParameterFromQuery(Name = "scope")]
    public string? Scope { get; set; }

    /// <summary>A rubric id or stamp from the address; chosen only when the scope's verdicts carry it.</summary>
    [SupplyParameterFromQuery(Name = "rubric")]
    public string? Rubric { get; set; }
}
