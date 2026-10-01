using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>The <b>Probes</b> tab (S4 of <c>todo/PLAN_question_consultant_probes.md</c>): what each CLI and api vendor can actually
/// do for the question consultant, as facts per CLI build — one probe run at a time, read through <c>/api/bench/probes/*</c>, the
/// object <c>bench probes report --json</c> prints.
///
/// <para><b>Read-only (D10).</b> The page starts nothing and changes no status: a cell is re-measured with the
/// <c>bench probes rerun --cell</c> command it shows, so there is no in-flight state of the page's own to keep across a reload —
/// the run's state is the cells', persisted by the CLI, and re-read here.</para>
///
/// <para><b>Live while something is measured.</b> While any cell is Pending or Claimed the page re-reads the run every
/// <see cref="PollInterval"/> through a <see cref="LivePoller"/>, and stops the moment a read says nothing is in flight
/// (the durable-status rule's "self-terminating when idle"). Every read — the first, a run switch, a poll — takes a ticket
/// (<c>_read</c>); an answer whose ticket is no longer the latest is dropped, so a late poll for a run the reader left never
/// replaces the run they chose (the Gate view's rule). Disposal cancels the poll's token, and a tick checks it before it
/// renders.</para></summary>
public partial class ProbesBenchmark(BenchConsoleApi api, TimeProvider clock) : ComponentBase, IAsyncDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly LivePoller _poller = new(clock, PollInterval);

    /// <summary>Which read is the latest; an answer to an earlier one is dropped.</summary>
    private int _read;

    /// <summary>A run id from the address (<c>?run=</c>), so a link opens one run. Absent, it is null — the one optional UI parameter
    /// shape the rules allow — and the newest run is opened.</summary>
    [SupplyParameterFromQuery(Name = "run")]
    public string? Run { get; set; }

    private Read<IReadOnlyList<ProbeRunSummaryDto>> Runs { get; set; } = Read<IReadOnlyList<ProbeRunSummaryDto>>.Unasked;

    private Read<ProbeRunReportDto> Report { get; set; } = Read<ProbeRunReportDto>.Unasked;

    private Guid RunId { get; set; }

    private bool Loading { get; set; }

    /// <summary>Why the latest POLL failed, shown above the last report that did arrive — a dropped connection is not a finished run.</summary>
    private string PollProblem { get; set; } = string.Empty;

    private bool InFlight => Report.Value?.Progress.Open == true;

    private bool Listed => (Runs.Value ?? []).Any(r => r.RunId == RunId);

    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        var runs = await api.GetProbeRunsAsync();
        Runs = runs.Value is { } listed ? Read<IReadOnlyList<ProbeRunSummaryDto>>.Arrived(Newest(listed)) : runs;
        RunId = Pick(Runs.Value ?? [], Run ?? string.Empty);
        await OpenAsync();
        Loading = false;
    }

    public async ValueTask DisposeAsync()
    {
        await _poller.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task ChooseRunAsync(ChangeEventArgs changed)
    {
        RunId = Guid.TryParse(changed.Value?.ToString(), out var chosen) ? chosen : Guid.Empty;
        await OpenAsync();
    }

    private async Task RefreshAsync()
    {
        Loading = true;
        await OpenAsync();
        Loading = false;
    }

    /// <summary>Reads the chosen run afresh: the poll is stopped first, so no tick for the run being left can start, and the answer
    /// is kept only when no later read began meanwhile.</summary>
    private async Task OpenAsync()
    {
        _poller.Stop();
        var read = ++_read;
        Report = Read<ProbeRunReportDto>.Unasked;
        PollProblem = string.Empty;

        var report = RunId != Guid.Empty ? await api.GetProbeRunAsync(RunId) : Read<ProbeRunReportDto>.Unasked;
        if (read != _read)
        {
            return;
        }

        Report = report;
        FollowProgress();
    }

    /// <summary>One poll: the same read, dropped when the page is gone (the token) or when a newer read was started (the ticket). A
    /// failed poll keeps the last report on screen with the reason above it, and keeps polling.</summary>
    private async Task PollAsync(CancellationToken token)
    {
        var read = ++_read;
        var report = await api.GetProbeRunAsync(RunId, token);
        if (token.IsCancellationRequested || read != _read)
        {
            return;
        }

        (Report, PollProblem) = report.Available ? (report, string.Empty) : (Report, report.Detail);
        FollowProgress();
        await InvokeAsync(StateHasChanged);
    }

    private void FollowProgress()
    {
        if (InFlight)
        {
            _poller.Start(PollAsync);
            return;
        }

        _poller.Stop();
    }

    /// <summary>Newest first, whatever order the server sent — the picker's promise, kept here rather than trusted.</summary>
    private static IReadOnlyList<ProbeRunSummaryDto> Newest(IReadOnlyList<ProbeRunSummaryDto> runs) => [.. runs.OrderByDescending(r => r.CreatedAt)];

    /// <summary>The run the address named when it is a run id (a run older than the list's window still opens — the server answers
    /// 404 for one it does not hold); else the newest; else none.</summary>
    private static Guid Pick(IReadOnlyList<ProbeRunSummaryDto> runs, string asked) =>
        (Guid.TryParse(asked, out var named), runs.Count) switch
        {
            (true, _) => named,
            (_, > 0) => runs[0].RunId,
            _ => Guid.Empty,
        };

    private static string RunLabel(ProbeRunSummaryDto run) =>
        $"{run.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC · {run.Subjects} subject(s) × {run.Repeats} repeat(s) · "
        + (run.Progress.Open ? $"open ({run.Progress.Pending + run.Progress.Claimed} to measure)" : "finished")
        + (run.ArtifactsPruned ? " · pruned" : string.Empty);

    private static string ProgressSentence(ProbeProgressDto progress) =>
        $"{progress.Pending} pending · {progress.Claimed} claimed · {progress.Settled} settled · {progress.Abandoned} abandoned — "
        + (progress.Open ? $"open, re-read every {PollInterval.TotalSeconds:0} s while a cell is pending or claimed" : "finished");

    /// <summary>Why a pair was not measured, in words; an unknown word is shown as it came.</summary>
    private static string DroppedSentence(string reason) => reason switch
    {
        "api-probe-on-cli" => "the api probe runs through the product's api path, not a CLI",
        "cli-probe-on-api" => "an api subject runs no CLI probe — there is no process to confine",
        "no-web-off-flag" => "the CLI has no flag that turns the web off and no tool deny-list",
        _ => reason,
    };
}
