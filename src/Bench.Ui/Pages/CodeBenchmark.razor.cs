using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>The CODE kind's population: fix runs, and nothing else.
///
/// <para>Fix runs used to render on the Sidecar tab — the operator caught an investigate run showing
/// under a hardware comparison it had nothing to do with (2026-08-21). The kinds are the tabs
/// (`BenchmarkTabs`), so the code kind reads the same run list the store serves and keeps ONLY its own:
/// filtering here rather than in the API, because the population is small and a second endpoint would be
/// a second definition of "a fix run".</para>
///
/// <para>This is `PLAN_code_lane.md` §7 step 8's landing — task detail (statement, phases, diff,
/// build/test output, per-signal columns) grows on this page as the lane lands.</para>
/// </summary>
public partial class CodeBenchmark(BenchConsoleApi api) : ComponentBase
{
    private Read<IReadOnlyList<RunSummaryDto>> Runs { get; set; } = Read<IReadOnlyList<RunSummaryDto>>.Unasked;

    private IReadOnlyList<RunSummaryDto> FixRuns { get; set; } = [];

    private bool Loading { get; set; }

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        Loading = true;
        Runs = await api.GetRunsAsync();
        FixRuns = Keep(Runs.Value ?? []);
        Loading = false;
    }

    /// <summary>The kind is matched by the stored name, case-insensitively — the DTO carries the enum's
    /// spelling, and a renamed casing must not silently empty this tab.</summary>
    private static IReadOnlyList<RunSummaryDto> Keep(IReadOnlyList<RunSummaryDto> runs) =>
        [.. runs.Where(r => string.Equals(r.Kind, "Fix", StringComparison.OrdinalIgnoreCase))];
}
