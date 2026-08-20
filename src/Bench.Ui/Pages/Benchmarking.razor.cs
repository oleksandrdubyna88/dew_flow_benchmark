using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>The benchmark's landing page: every run, folded onto the compute arm it was measured on.
///
/// <para><b>It answers the operator's question by refusing it.</b> "Is the WSL sidecar faster than the
/// Windows one" is what this repository was pointed at, and the arms are here side by side — but no winner
/// is declared, because the arm is recorded on the RUN and cross-run comparison needs a comparison scope
/// nothing establishes yet. The refusal sits above the table rather than in a footnote: a reader who scrolls
/// past it would take two averages as a result.</para>
///
/// <para>The decisions are all in <see cref="ArmGrouping"/>, which is pure and tested. This class does the
/// two things a component must: fetch, and hold what came back.</para>
/// </summary>
public partial class Benchmarking(BenchConsoleApi api) : ComponentBase
{
    private Read<IReadOnlyList<RunSummaryDto>> Runs { get; set; } = Read<IReadOnlyList<RunSummaryDto>>.Unasked;

    /// <summary>The grouping, recomputed from whatever arrived. Never null and never a half state: an empty
    /// comparison carries its own refusal, so the page has something true to render before the first read
    /// finishes.</summary>
    private ArmComparison Arms { get; set; } = ArmGrouping.Compare([]);

    private bool Loading { get; set; }

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        Loading = true;
        Runs = await api.GetRunsAsync();
        Arms = ArmGrouping.Compare(Runs.Value ?? []);
        Loading = false;
    }
}
