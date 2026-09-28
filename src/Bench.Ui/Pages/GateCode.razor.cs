using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary><i>coai models — code gate</i> (E6): the same view over code-gate runs — the code stage only; the plan loop that
/// unlocks it is measured by the plan gate, never here.</summary>
public partial class GateCode : ComponentBase
{
    private const string Gate = "code";

    private const string Purpose =
        "Each reviewer over the planted-defect diff through the product's own review_code — the code stage only; "
        + "the plan loop before it is the plan gate's measurement.";

    /// <summary>A scope id from the address, so a link opens on one scope; chosen only when the runs offer it. Absent from
    /// the address it is null — the one optional UI parameter shape the rules allow.</summary>
    [SupplyParameterFromQuery(Name = "scope")]
    public string? Scope { get; set; }

    /// <summary>A rubric id or stamp from the address; chosen only when the scope's verdicts carry it.</summary>
    [SupplyParameterFromQuery(Name = "rubric")]
    public string? Rubric { get; set; }
}
