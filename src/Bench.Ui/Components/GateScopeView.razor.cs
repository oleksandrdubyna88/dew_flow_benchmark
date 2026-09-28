using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Components;

/// <summary>One gate's measurement, one scope and one rubric at a time — the view the three Gate pages share.
///
/// <para><b>Only what was measured is offered.</b> The scope control lists the scopes the runs echoed (a partition per
/// product and settings hash, from <c>/gate/scopes</c>), and the rubric control lists the rubrics the chosen scope's
/// verdicts CARRY. A control holding a scope nobody measured, or a rubric nobody read the findings under, would render a
/// column of dashes that reads as a measurement.</para>
///
/// <para><b>No default where there is a choice.</b> A control with ONE option is chosen for the reader — there is nothing
/// to decide — and a control with two waits: strict and lenient verdicts are two populations, and two products are two
/// scopes, so a pre-selection would make every number below it about a choice nobody made (the arms page's rule).</para>
///
/// <para>Read-only: the view starts nothing and changes no status, so it has no in-flight state to keep across a
/// reload.</para></summary>
public partial class GateScopeView(BenchConsoleApi api) : ComponentBase
{
    /// <summary>The gate word — <c>plan</c>, <c>code</c> or <c>feature</c>.</summary>
    [Parameter, EditorRequired]
    public string Gate { get; set; } = string.Empty;

    /// <summary>A scope id named in the address (<c>?scope=</c>) — chosen only when the runs offer it.</summary>
    [Parameter]
    public string InitialScope { get; set; } = string.Empty;

    /// <summary>A rubric named in the address (<c>?rubric=</c>, its id or its stamp) — chosen only when the scope's
    /// verdicts carry it.</summary>
    [Parameter]
    public string InitialRubric { get; set; } = string.Empty;

    private static readonly GateScopeDto NoScope = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, [], [], false);

    private Read<IReadOnlyList<GateScopeDto>> Scopes { get; set; } = Read<IReadOnlyList<GateScopeDto>>.Unasked;

    private Read<GateModelTableDto> Table { get; set; } = Read<GateModelTableDto>.Unasked;

    private Read<IReadOnlyList<GateRunSummaryDto>> Runs { get; set; } = Read<IReadOnlyList<GateRunSummaryDto>>.Unasked;

    private string ScopeId { get; set; } = string.Empty;

    private string Rubric { get; set; } = string.Empty;

    private bool Loading { get; set; }

    private GateScopeDto Chosen => (Scopes.Value ?? []).FirstOrDefault(s => s.Id == ScopeId) ?? NoScope;

    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        Scopes = await api.GetGateScopesAsync(Gate);
        ScopeId = Pick([.. (Scopes.Value ?? []).Select(s => s.Id)], InitialScope);
        await ReadScopeAsync(InitialRubric);
        Loading = false;
    }

    private async Task ChooseScopeAsync(ChangeEventArgs changed)
    {
        ScopeId = changed.Value?.ToString() ?? string.Empty;
        await ReadScopeAsync(string.Empty);
    }

    private async Task ChooseRubricAsync(ChangeEventArgs changed)
    {
        Rubric = changed.Value?.ToString() ?? string.Empty;
        await ReadTableAsync();
    }

    private async Task ReadScopeAsync(string askedRubric)
    {
        Table = Read<GateModelTableDto>.Unasked;
        Rubric = Pick([.. Chosen.Rubrics.Select(r => r.Stamp)], Chosen.Rubrics.Where(r => r.Id == askedRubric).Select(r => r.Stamp).DefaultIfEmpty(askedRubric).First());
        Runs = ScopeId.Length > 0 ? await api.GetGateRunsAsync(Gate, ScopeId) : Read<IReadOnlyList<GateRunSummaryDto>>.Unasked;
        await ReadTableAsync();
    }

    /// <summary>The table is asked for only when it can be answered: a scope, a rubric, and the suite's tasks recorded — a
    /// scope without them is refused by the server anyway, and the page says why before asking.</summary>
    private async Task ReadTableAsync()
    {
        Table = ScopeId.Length > 0 && Rubric.Length > 0 && Chosen.TasksRecorded
            ? await api.GetGateModelsAsync(Gate, ScopeId, Rubric)
            : Read<GateModelTableDto>.Unasked;
    }

    /// <summary>What the address asked for when it is on offer; else the one option when there is exactly one — nothing to
    /// decide —; else nothing, so the reader decides.</summary>
    private static string Pick(IReadOnlyList<string> options, string asked) =>
        (options.Contains(asked, StringComparer.Ordinal), options.Count) switch
        {
            (true, _) => asked,
            (_, 1) => options[0],
            _ => string.Empty,
        };

    private static string ScopeLabel(GateScopeDto scope) =>
        $"{scope.ProductVersion} · settings {Short(scope.SettingsHash)} · {scope.Runs} run(s) · {scope.Id}";

    private static string RubricLabel(GateRubricDto rubric) =>
        $"{rubric.Id} — {GateModelTable.KindWords(rubric.Kind)} · {rubric.Verdicts} verdict(s) · {rubric.Stamp}";

    private static string Source(string label) => label == "native" ? "measured here (bench gate run)" : $"imported from {label}";

    private static string Short(string hash) => hash.Length > 12 ? hash[..12] : hash;

    /// <summary>The variance refusal, in words, for the reader to meet BEFORE the numbers it qualifies.</summary>
    private static string VarianceSentence(IReadOnlyList<GateVarianceDto> variance)
    {
        var withheld = variance.Count(v => v.SeedsState != "stated");

        return withheld == 0
            ? $"Run-to-run variance is stated for all {variance.Count} task × reviewer pair(s) — three or more assessed repeats each."
            : $"Run-to-run variance withheld for {withheld} of {variance.Count} task × reviewer pair(s) — fewer than three assessed "
              + "repeats is a difference, not a spread.";
    }
}
