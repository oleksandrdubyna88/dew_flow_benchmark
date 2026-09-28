using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary><i>coai models — feature gate</i> (E6): which reviewer model, through <c>review_feature</c>, finds the planted
/// defects of a seeded task — at what cost, how repeatably. The Gate tab lands here; the plan and code gates are the same
/// view over their own runs. The page is the view's frame: the scope, the rubric and every figure are
/// <see cref="Components.GateScopeView"/>'s.</summary>
public partial class GateFeature : ComponentBase
{
    private const string Gate = "feature";

    private const string Purpose =
        "Each reviewer model plus its calibrated transport, over the seeded tasks, through the product's own review_feature: "
        + "valid runs, seeds hit, strict verdicts, time, tokens and cost per run.";

    /// <summary>A scope id from the address, so a link opens on one scope; chosen only when the runs offer it. Absent from
    /// the address it is null — the one optional UI parameter shape the rules allow.</summary>
    [SupplyParameterFromQuery(Name = "scope")]
    public string? Scope { get; set; }

    /// <summary>A rubric id or stamp from the address; chosen only when the scope's verdicts carry it.</summary>
    [SupplyParameterFromQuery(Name = "rubric")]
    public string? Rubric { get; set; }
}
