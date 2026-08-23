using Bench.Contracts;
using Bench.Ui.Services;
using Microsoft.AspNetCore.Components;

namespace Bench.Ui.Pages;

/// <summary>The MATH kind's population — and it is the only tab here whose population is not runs.
///
/// <para>Every other kind compares configurations of this harness. This one looks at the operator's REAL
/// agent sessions, traced call by call, and asks a different question: which of that work did not need a
/// model at all. `todo/ai_math/PLAN_math_over_ai.md` owns the question; this page is where its raw material
/// becomes readable.</para>
///
/// <para>No polling, in keeping with every other page in this console: a Refresh button, and the reader
/// decides when. A live session does change under the reader — which is exactly why the button says
/// "Reading…" while it works rather than pretending the table is current.</para>
/// </summary>
public partial class MathBenchmark(BenchConsoleApi api) : ComponentBase
{
    private Read<IReadOnlyList<SessionSummaryDto>> Sessions { get; set; } =
        Read<IReadOnlyList<SessionSummaryDto>>.Unasked;

    private IReadOnlyList<SessionSummaryDto> Rows { get; set; } = [];

    private bool Loading { get; set; }

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        Loading = true;
        Sessions = await api.GetSessionsAsync();
        Rows = Sessions.Value ?? [];
        Loading = false;
    }

    /// <summary>What to call a session. The human name first, then the id a terminal was opened with, then
    /// the runtime's own key — which is never pretty but is always present, so a session nobody named is
    /// still a row a reader can click rather than a blank cell.</summary>
    private static string Name(SessionSummaryDto row) =>
        First(row.Task.Name, row.Task.Id, Short(row.SessionKey));

    private static string First(params string[] candidates) =>
        candidates.FirstOrDefault(c => c.Length > 0) ?? "— unnamed";

    private static string Short(string key) => key.Length <= 12 ? key : key[..12] + "…";

    /// <summary>A zero rendered as a dash rather than as a number.
    /// <para>
    /// Deliberate, and not decoration: these columns count things that are NOTABLE when present — calls
    /// that never closed, builds that failed. A column of zeroes reads as data and pulls the eye; a column
    /// of dashes lets the one row with a 3 in it be the thing the reader sees.
    /// </para></summary>
    private static string Count(int value) => value == 0 ? "—" : value.ToString();

    /// <summary>The phase colours. Verification is the SUCCESS colour because reaching it at all is the
    /// shape of a session that finished something, and unknown is grey rather than absent — a session whose
    /// tools this build does not recognise must look different from one that has not started.</summary>
    private static string PhaseBadge(string phase) => phase switch
    {
        "Research" => "text-bg-info",
        "Execution" => "text-bg-primary",
        "Verification" => "text-bg-success",
        _ => "text-bg-secondary",
    };
}
