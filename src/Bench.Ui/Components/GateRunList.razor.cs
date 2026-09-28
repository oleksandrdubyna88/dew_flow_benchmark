using Bench.Contracts;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Components;

/// <summary>A scope's run list: valid, verdict, findings, turns, seconds, cost and the failure per attempt — a failure named
/// by its kind and its redacted cause, a turn count no harness recorded as <i>unknown</i>, never zero.</summary>
public partial class GateRunList : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<GateRunSummaryDto> Runs { get; set; } = [];

    private static string Failure(GateRunSummaryDto run) =>
        run.FailureKind == "None" ? GateFigureWords.Dash : $"{run.FailureKind}: {run.FailureText}";
}
