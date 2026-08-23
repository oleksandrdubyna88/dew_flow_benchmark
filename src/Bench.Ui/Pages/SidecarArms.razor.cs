using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>The compute backends, side by side — the question this benchmark was pointed at.
///
/// <para><b>Scoped, and the scope is on screen.</b> Two runs are only comparable inside one target-and-suite
/// pair, so this renders one table PER scope rather than one table of everything. A single table would be the
/// project's most expensive recorded mistake with a tidier presentation: a series measured over weeks turned
/// out to have been scored against a corpus rebuilt underneath it, and every conclusion went back to being a
/// hypothesis.</para>
///
/// <para><b>No baseline is chosen for the reader.</b> Without one every arm is <c>NotAWinner</c>, which is
/// correct rather than unhelpful: an arm that becomes the baseline BECAUSE it scored highest cannot then be
/// beaten, and the verdict would be circular. Picking one is a decision, so it is a control.</para>
/// </summary>
public partial class SidecarArms(BenchConsoleApi api) : ComponentBase
{
    private Read<ArmComparisonDto> Comparison { get; set; } = Read<ArmComparisonDto>.Unasked;

    /// <summary>Empty until the reader picks — the same no-default rule the run report holds to.</summary>
    private string Metric { get; set; } = string.Empty;

    private string Baseline { get; set; } = string.Empty;

    private bool Loading { get; set; }

    /// <summary>What the arms measured. Empty until the first read answers, and empty ALSO when nothing was
    /// measured — the page says which.</summary>
    private Read<IReadOnlyList<string>> Metrics { get; set; } = Read<IReadOnlyList<string>>.Unasked;

    protected override async Task OnInitializedAsync() => Metrics = await api.GetArmMetricsAsync();

    private async Task ChooseMetricAsync(ChangeEventArgs changed)
    {
        Metric = changed.Value?.ToString() ?? string.Empty;
        await ReadAsync();
    }

    private async Task ChooseBaselineAsync(ChangeEventArgs changed)
    {
        Baseline = changed.Value?.ToString() ?? string.Empty;
        await ReadAsync();
    }

    private async Task ReadAsync()
    {
        if (Metric.Length == 0)
        {
            return;
        }

        Loading = true;
        Comparison = await api.GetArmsAsync(Metric, Baseline);
        Loading = false;
    }

    /// <summary>Every arm the comparison found, for the baseline control. Drawn from the ANSWER rather than
    /// from a fixed list: the arms that exist are whatever the runs echoed, and offering one nothing was
    /// measured on would leave the reader wondering why it changed nothing.</summary>
    private IReadOnlyList<string> Arms =>
        Comparison.Value is null
            ? []
            : [.. Comparison.Value.Scopes.SelectMany(scope => scope.Arms).Select(arm => arm.Arm).Distinct().Order()];

    /// <summary>The verdict's colour. <c>Unproven</c> is a WARNING and never a paler success: an arm that won
    /// only on the half that chose it is the shape of every false winner.</summary>
    private static string Badge(string proof) =>
        proof switch
        {
            "Confirmed" => "text-bg-success",
            "Unproven" => "text-bg-warning",
            "Suspicious" => "text-bg-danger",
            _ => "text-bg-secondary",
        };

    /// <summary>The accelerator peak, with the evidence behind it — or a statement that nobody looked.
    /// <para>
    /// <b>Zero samples is not zero bytes.</b> A card nobody read and an idle card are opposite facts, and a
    /// bare <c>0 GiB</c> would read as the second. The sample count travels with the number because an
    /// accelerator read costs about a second on Windows and is unavailable off it, so a short leg may catch
    /// none and a peak from one sample is not the claim a peak from thirty is.
    /// </para></summary>
    private static string Vram(ArmCostDto cost) =>
        cost.VramSamples == 0
            ? "not sampled"
            : $"{cost.PeakVramBytes / 1073741824.0:0.#} GiB ({cost.VramSamples} sample(s))";

    private static string Half(HalfReadingDto half) =>
        half.Measured ? $"{half.Average:0.###} ({half.Legs} leg(s))" : "not measured";

    /// <summary>How loudly the scope's hardware reading is stated.
    /// <para>
    /// <c>SeveralMachines</c> is a WARNING and not a neutral note: a gap between arms measured on two machines
    /// is a gap about the machines, and it is indistinguishable from a real backend result once it has been
    /// averaged. The two unknown states are neutral rather than green — nobody looked is not agreement — and
    /// the clean case is stated too, because silence here would read as agreement either way.
    /// </para></summary>
    private static string MachineBanner(string state) =>
        state switch
        {
            "SeveralMachines" => "bg-warning-subtle text-warning-emphasis",
            "OneMachine" => "bg-body-tertiary text-body-secondary",
            _ => "bg-body-tertiary text-body-secondary",
        };

    private static string MachineText(string state) =>
        state == "SeveralMachines" ? "text-warning-emphasis fw-semibold" : "text-body-secondary";

    /// <summary>One arm's machines, compactly. The unrecorded count travels with the number for the reason the
    /// VRAM sample count does: a run nobody probed is not evidence of a machine, and a bare "1" would claim
    /// one where only part of the population was read.</summary>
    private static string Machines(MachineAgreementDto machines) =>
        machines.State switch
        {
            "OneMachine" => "one",
            "SeveralMachines" => $"{machines.Machines} machines",
            "PartlyRecorded" => $"1 (+{machines.Unrecorded} unrecorded)",
            _ => "not recorded",
        };
}
