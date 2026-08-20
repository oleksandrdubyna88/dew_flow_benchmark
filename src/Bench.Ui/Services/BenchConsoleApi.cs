using System.Net.Http.Json;
using System.Text.Json;
using Bench.Contracts;

namespace Bench.Ui.Services;

/// <summary>The console's read side of the benchmark API.
/// <para>
/// <b>Every method distinguishes "the server said nothing" from "the server said empty."</b> A run list that
/// could not be read and a database holding no runs are opposite facts, and a page rendering both as an empty
/// table sends its reader hunting in the wrong place. So each answer is a <see cref="Read{T}"/> carrying the
/// value, whether it arrived, and why it did not — the sibling console's shape, followed rather than
/// re-invented.
/// </para>
/// <para>
/// <b>A refusal is read, not swallowed.</b> The API explains itself: a report with no metric is a 400 whose
/// body names the flag, and a run this database does not hold is a 404. Reporting either as "unreachable"
/// would throw away the only sentence that tells the reader what to do next.
/// </para></summary>
public sealed class BenchConsoleApi(HttpClient http)
{
    /// <summary>The runs, newest first as the store orders them.</summary>
    public Task<Read<IReadOnlyList<RunSummaryDto>>> GetRunsAsync(
        int limit = 50, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<RunSummaryDto>>($"api/bench/runs?limit={limit}", cancellationToken);

    /// <summary>One run's comparison. The metric is required and has no default — the same refusal the CLI
    /// gives, for the same reason: a console that picked one would report on an axis nobody chose.</summary>
    public Task<Read<RunReportDto>> GetReportAsync(
        Guid runId, string metric, CancellationToken cancellationToken = default) =>
        GetAsync<RunReportDto>(
            $"api/bench/runs/{runId}/report?metric={Uri.EscapeDataString(metric)}", cancellationToken);

    /// <summary>The compute-backend comparison, across runs. The baseline travels only when one was chosen,
    /// because an empty one is a legitimate answer — no arm is a winner — rather than a missing argument.</summary>
    public Task<Read<ArmComparisonDto>> GetArmsAsync(
        string metric, string baseline = "", CancellationToken cancellationToken = default) =>
        GetAsync<ArmComparisonDto>(
            $"api/bench/arms?metric={Uri.EscapeDataString(metric)}&baseline={Uri.EscapeDataString(baseline)}",
            cancellationToken);

    private async Task<Read<T>> GetAsync<T>(string route, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var response = await http.GetAsync(route, cancellationToken);

            return response.IsSuccessStatusCode
                ? Received(await response.Content.ReadFromJsonAsync<T>(cancellationToken), route)
                : Read<T>.Missing(await RefusedAsync(response, route, cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return Read<T>.Missing(Unreachable(ex, route));
        }
    }

    private static Read<T> Received<T>(T? value, string route)
        where T : class =>
        value is null ? Read<T>.Missing($"{route} answered with an empty body") : Read<T>.Arrived(value);

    /// <summary>The server's own sentence, when it sent one.</summary>
    private static async Task<string> RefusedAsync(
        HttpResponseMessage response, string route, CancellationToken cancellationToken)
    {
        var problem = await ReadProblemAsync(response, cancellationToken);

        return problem.Length > 0 ? problem : $"{route} answered {(int)response.StatusCode}";
    }

    private static async Task<string> ReadProblemAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ProblemDto>(cancellationToken))?.Reason ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A refusal that is not JSON is still a refusal; the status code below carries it.
            return string.Empty;
        }
    }

    /// <summary>Why nothing arrived. A timeout reaches us as a cancellation, and a reader must be able to
    /// tell "slow" from "broken" — neither of which is "there is nothing there".</summary>
    private static string Unreachable(Exception ex, string route) =>
        ex switch
        {
            TaskCanceledException => $"{route} did not answer in time",
            JsonException => $"{route} answered with something this console could not read: {ex.Message}",
            _ => $"{route} could not be reached: {ex.Message}",
        };
}

/// <summary>A value that may not have arrived, and the reason when it did not. The family's
/// flag-and-reason shape, on the console's side of the wire.
/// <para>
/// Declared here rather than shared with the sibling console: the two live in repositories with no common
/// ancestor to hold it, and vendoring one of them into the other to borrow a three-line record would be the
/// worse trade. Named as a deliberate copy so the next reader knows which it is.
/// </para></summary>
public sealed record Read<T>(bool Available, T? Value, string Detail)
    where T : class
{
    public static Read<T> Arrived(T value) => new(true, value, string.Empty);

    public static Read<T> Missing(string detail) => new(false, null, detail);

    /// <summary>Nothing has been asked yet — deliberately not the same state as a failed read, and
    /// deliberately not "available with nothing in it".</summary>
    public static Read<T> Unasked { get; } = new(false, null, string.Empty);
}
