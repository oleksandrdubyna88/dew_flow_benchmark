using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>One run's comparison — the same object <c>bench report --json</c> prints, rendered.
///
/// <para><b>No metric is chosen for the reader.</b> The page reads nothing until one is picked, which
/// mirrors <c>RunReport.NoMetricNamed</c> rather than working around it: a default would report on an axis
/// nobody chose, and every number on screen would be about it. The names are offered from
/// <see cref="WellKnownMetrics"/> so choosing does not require knowing an exact string.</para>
///
/// <para>Nothing here judges. A low average renders as a low average, exactly as the CLI exits <c>0</c> for
/// one: no bar has been agreed, and a console that coloured a score red would be inventing the bar.</para>
/// </summary>
public partial class BenchmarkRun(BenchConsoleApi api) : ComponentBase
{
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>Empty until the reader picks one — the page's whole no-default rule, held in one field.</summary>
    private string Metric { get; set; } = string.Empty;

    private Read<RunReportDto> Report { get; set; } = Read<RunReportDto>.Unasked;

    /// <summary>The metric names this run actually carries.
    /// <para>
    /// Asked of the run rather than taken from <see cref="WellKnownMetrics"/>, which could never hold them:
    /// a tool metric is named per tool — <c>Tool use: search_literal</c> — so the published list offered a
    /// bare <c>Tool use</c> that matched nothing, and the one axis a tool run produces rendered empty. An
    /// empty table reads as a broken run rather than as a bad question, which is the failure this replaces.
    /// </para></summary>
    private Read<IReadOnlyList<string>> Metrics { get; set; } = Read<IReadOnlyList<string>>.Unasked;

    protected override async Task OnInitializedAsync() => Metrics = await api.GetRunMetricsAsync(Id);

    private bool Loading { get; set; }

    private async Task ChooseAsync(ChangeEventArgs changed)
    {
        Metric = changed.Value?.ToString() ?? string.Empty;

        if (Metric.Length > 0)
        {
            await ReadAsync();
        }
    }

    private async Task ReadAsync()
    {
        Loading = true;
        Report = await api.GetReportAsync(Id, Metric);
        Loading = false;
    }

    /// <summary>The verdict's colour. <c>Unproven</c> is a WARNING rather than a paler success: an arm that
    /// won only on the half that chose it is the shape of every false winner, and rendering it as a modest
    /// win is precisely how one gets believed.</summary>
    private static string Badge(string proof) =>
        proof switch
        {
            "Confirmed" => "text-bg-success",
            "Unproven" => "text-bg-warning",
            "Suspicious" => "text-bg-danger",
            _ => "text-bg-secondary",
        };

    /// <summary>A half nobody measured, said out loud. Rendering an unmeasured half as <c>0</c> would make
    /// an absence look like a failure — opposite readings of the same blank cell.</summary>
    private static string Half(HalfReadingDto half) =>
        half.Measured ? $"{half.Average:0.###} ({half.Legs} leg(s))" : "not measured";
}
