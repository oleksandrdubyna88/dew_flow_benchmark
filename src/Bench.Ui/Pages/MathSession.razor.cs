using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>One agent session, whole — the page the replacement map is actually read off.
///
/// <para>Three things sit here that the list deliberately does not carry: the phase economics (where the
/// wall time went), the detectors' findings (the patterns a formula could have done instead), and every
/// call in order with what the working tree said across it. All three read the call SEQUENCE, which is why
/// they belong to a page somebody opened rather than to a list that polls.</para>
/// </summary>
public partial class MathSession(BenchConsoleApi api) : ComponentBase
{
    [Parameter]
    public Guid SessionId { get; set; }

    private Read<SessionDetailDto> Session { get; set; } = Read<SessionDetailDto>.Unasked;

    private SessionDetailDto? Detail => Session.Value;

    private bool Loading { get; set; }

    private double TotalSeconds { get; set; }

    protected override async Task OnParametersSetAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        Loading = true;
        Session = await api.GetSessionAsync(SessionId);
        TotalSeconds = Detail?.Phases.Sum(p => p.Seconds) ?? 0;
        Loading = false;
    }

    private string Title => First(
        Detail?.Summary.Task.Name ?? string.Empty,
        Detail?.Summary.Task.Id ?? string.Empty,
        Detail?.Summary.SessionKey ?? string.Empty);

    private static string First(params string[] candidates) =>
        candidates.FirstOrDefault(c => c.Length > 0) ?? "— unnamed session";

    /// <summary>A phase's share of the session's wall time.
    /// <para>
    /// Refused rather than shown as <c>0 %</c> when nothing was measured. A session of one call has no
    /// elapsed time at all, and a row reading "0 % of nothing" invites the reader to compare it with a real
    /// zero somewhere else.
    /// </para></summary>
    private string Share(double seconds) =>
        TotalSeconds <= 0 ? "—" : $"{seconds / TotalSeconds * 100:F0} %";

    private static string PhaseBadge(string phase) => phase switch
    {
        "Research" => "text-bg-info",
        "Execution" => "text-bg-primary",
        "Verification" => "text-bg-success",
        _ => "text-bg-secondary",
    };

    /// <summary>A disagreement is a defect in OUR taxonomy and is coloured as one. The other three are
    /// observations about the session, and an allowlist candidate is not even that — it is this system
    /// telling us which command to add to its own read-only list.</summary>
    private static string FindingBadge(string kind) => kind switch
    {
        "MutationDisagreement" => "text-bg-danger",
        "ReResearchLoop" => "text-bg-warning",
        "SearchVariantChain" => "text-bg-warning",
        _ => "text-bg-secondary",
    };

    private static string MutationClass(string mutation) => mutation switch
    {
        "Changed" => "text-danger",
        "Unchanged" => "text-body-secondary",
        _ => "text-body-tertiary",
    };

    /// <summary>An open call says so rather than borrowing the outcome column's vocabulary. "Started" and
    /// "NotCaptured" are different facts: one is a call still in flight or denied, the other a call that
    /// finished without telling us how.</summary>
    private static string Outcome(SessionToolCallDto call) =>
        call.State == "Started" ? "open" : call.Outcome;

    private static string OutcomeClass(SessionToolCallDto call) => call switch
    {
        { State: "Started" } => "text-warning",
        { Outcome: "Error" } => "text-danger",
        { Outcome: "Refused" } => "text-warning",
        { Outcome: "NotCaptured" } => "text-body-secondary",
        _ => "text-success",
    };
}
