using Bench.Application.Gate;
using Bench.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Api;

/// <summary>The coai gate benchmark's read routes (E6) — <c>/api/bench/gate/*</c>, over the same <see cref="GateReportQuery"/>
/// the CLI's <c>bench gate report</c> drives, answering the same <see cref="GateReportContract"/> shapes.
/// <para>
/// <b>The read port is resolved from the REQUEST's services, never taken as a parameter.</b> Minimal APIs decide whether an
/// interface parameter is a service or a body by asking the container; in a host that never registered
/// <see cref="IGateReads"/> — the qln console registers its bench ports by hand — the parameter is inferred as a BODY, and a
/// GET with an inferred body fails the whole endpoint table at startup, every console route with it. Resolved here, a host
/// without the registration answers 503 naming it, and every other route keeps working.
/// </para>
/// <para>
/// 400 against 404 against 409 is the CLI's 4 against 4 against 3: asked wrongly, not here, and here but its suite's tasks
/// were never recorded — the one a caller fixes with a verb rather than a different URL.
/// </para></summary>
public static class GateApi
{
    public const string NotRegistered =
        "the gate's read port is not registered on this host — register Bench.Application.Gate." + nameof(IGateReads)
        + " (PostgresGateReads over the BenchDbContext) beside IRunStore, then restart";

    public static IEndpointRouteBuilder MapGateReads(this IEndpointRouteBuilder app)
    {
        var gate = app.MapGroup("/api/bench/gate");

        gate.MapGet("/scopes", ScopesAsync);
        gate.MapGet("/runs/{id:guid}", RunAsync);
        gate.MapGet("/{gate}/models", ModelsAsync);
        gate.MapGet("/{gate}/runs", RunsAsync);

        return app;
    }

    /// <summary>Every scope the runs span — a partition per product and settings hash, never a fixed list — optionally of
    /// one gate. What the page's scope control offers, and nothing else.</summary>
    public static Task<IResult> ScopesAsync(HttpContext http, CancellationToken cancellationToken, string? gate = null) =>
        WithReads(http, reads => GateReportQuery.ScopesAsync(reads, gate ?? string.Empty, cancellationToken));

    /// <summary>One scope's per-model table under ONE rubric. <c>rubric</c> is required as <c>metric</c> is on the run
    /// report: there is no default, and no table across rubric kinds.</summary>
    public static Task<IResult> ModelsAsync(
        string gate, HttpContext http, CancellationToken cancellationToken, string? scope = null, string? rubric = null) =>
        WithReads(http, reads => GateReportQuery.ModelsAsync(reads, gate, scope ?? string.Empty, rubric ?? string.Empty, cancellationToken));

    /// <summary>One scope's run list — every attempt, the superseded ones marked.</summary>
    public static Task<IResult> RunsAsync(string gate, HttpContext http, CancellationToken cancellationToken, string? scope = null) =>
        WithReads(http, reads => GateReportQuery.RunsAsync(reads, gate, scope ?? string.Empty, cancellationToken));

    public static Task<IResult> RunAsync(Guid id, HttpContext http, CancellationToken cancellationToken) =>
        WithReads(http, reads => GateReportQuery.RunAsync(reads, id, cancellationToken));

    private static async Task<IResult> WithReads<T>(HttpContext http, Func<IGateReads, Task<GateAnswer<T>>> read) =>
        http.RequestServices.GetService<IGateReads>() is { } reads
            ? Answer(await read(reads))
            : Results.Json(new ProblemDto(NotRegistered), statusCode: StatusCodes.Status503ServiceUnavailable);

    private static IResult Answer<T>(GateAnswer<T> answer) =>
        answer switch
        {
            GateAnswer<T>.Answered ok => Results.Ok(ok.Value),
            GateAnswer<T>.Refused { Kind: GateRefusalKind.BadRequest } refused => Results.BadRequest(new ProblemDto(refused.Reason)),
            GateAnswer<T>.Refused { Kind: GateRefusalKind.Conflict } refused => Results.Conflict(new ProblemDto(refused.Reason)),
            GateAnswer<T>.Refused refused => Results.NotFound(new ProblemDto(refused.Reason)),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
