using Bench.Application;
using Bench.Contracts;
using Bench.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bench.Api;

/// <summary>The HTTP door onto the same use cases the CLI drives. Thin by construction: if an endpoint
/// ever needs logic of its own, that logic belongs in the Application layer where both surfaces can
/// reach it.
/// <para>
/// <b>Read-only.</b> Nothing here starts a run, and that is a boundary rather than an omission:
/// <c>bench run</c> is the one verb that reaches a model, and it stays a command an agent runs rather
/// than a service an orchestrator supervises. A start button belongs to the console's own worker, with
/// the accelerator lease that makes two drains against one card safe.
/// </para></summary>
public static class BenchApi
{
    public static IEndpointRouteBuilder MapBenchApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/health", () => Results.Ok(new HealthDto("ok")));

        group.MapPost("/plan", (PlanRequestDto request) =>
            PlanRequestHandler.Handle(request).Match(
                Results.Ok,
                reason => Results.BadRequest(new ProblemDto(reason))));

        group.MapGet("/runs", async (IRunStore runs, CancellationToken cancellationToken, int limit = 50) =>
            Results.Ok((await runs.RecentAsync(limit, cancellationToken)).Select(RunReportContract.From)));

        // The two integers, on their own route, because a page that polls for progress must not pay for a
        // whole comparison to learn that nothing has changed.
        group.MapGet("/runs/{id:guid}/scoreboard", async (
            Guid id, IResultStore results, CancellationToken cancellationToken) =>
            Results.Ok(await results.ScoreboardAsync(id, cancellationToken)));

        group.MapGet("/runs/{id:guid}/report", ReportAsync);

        return app;
    }

    /// <summary>The comparison. Every decision is <see cref="RunReport"/>, which the CLI calls too, and the
    /// body is <see cref="RunReportContract"/>'s flattening of it — the same object <c>bench report --json</c>
    /// prints, so the two surfaces cannot drift into two truths.
    /// <para>
    /// <b>400 against 404 is the HTTP spelling of the CLI's 4 against 3.</b> A request that named no metric
    /// asked wrongly; a run id that is not in this database is a different piece of news, and collapsing
    /// them sends a caller looking in the wrong place.
    /// </para>
    /// <para>
    /// A named method rather than a lambda, so the decision above can be asserted without standing up a
    /// host — the route is one line of wiring and this is the part with a rule in it.
    /// </para></summary>
    public static async Task<IResult> ReportAsync(
        Guid id,
        IRunStore runs,
        IResultStore results,
        CancellationToken cancellationToken,
        string? metric = null,
        int minLegs = 2,
        double minSpread = Domain.Runs.Discrimination.DefaultMinSpread,
        string? baseline = null)
    {
        if (string.IsNullOrEmpty(metric))
        {
            return Results.BadRequest(new ProblemDto(RunReport.NoMetricNamed));
        }

        var request = new RunReportRequest(id, metric, minLegs, minSpread, baseline ?? string.Empty);
        var built = await RunReport.BuildAsync(runs, results, request, cancellationToken);

        return built.Match(
            view => Results.Ok(RunReportContract.From(view)),
            reason => Results.NotFound(new ProblemDto(reason)));
    }
}

public sealed record HealthDto(string Status);

public sealed record ProblemDto(string Reason);
