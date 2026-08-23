using Bench.Application.Sessions;
using Bench.Contracts;
using Bench.Domain.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bench.Api;

/// <summary>The session-trace routes — read for every surface, write for the collector alone.
/// <para>
/// Split into two extension methods rather than one, because the two halves belong to different hosts and
/// that boundary is worth keeping mechanical. <c>bench-api</c> is the READ surface and maps only
/// <see cref="MapSessionReads"/>; a write endpoint there would quietly undo a decision its own
/// <c>Program.cs</c> explains at length. The collector maps both — it is the one process an agent's hook
/// is allowed to reach, and it serves the reads too so a live dashboard can poll it without the whole
/// report stack being up.
/// </para></summary>
public static class SessionApi
{
    /// <summary>Where every vantage point posts. One door, a batch at a time.
    /// <para>
    /// <b>A refusal never fails the batch, and the response says so in numbers.</b> An agent session
    /// cannot be replayed, so one malformed event out of two hundred must cost that event and nothing
    /// else — <c>accepted</c> and <c>refused</c> travel side by side rather than being collapsed into a
    /// status code.
    /// </para></summary>
    public static IEndpointRouteBuilder MapSessionIngest(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bench/sessions");

        group.MapPost("/events", IngestAsync);

        // The hook client's readiness probe. It fires before every tool call, so it must be able to learn
        // "the collector is up" without paying for a database round trip — and must never be the reason a
        // tool call waits.
        group.MapGet("/health", () => Results.Ok(new HealthDto("ok")));

        return app;
    }

    public static IEndpointRouteBuilder MapSessionReads(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bench/sessions");

        group.MapGet("/", async (ISessionStore sessions, CancellationToken cancellationToken, int limit = 50) =>
            Results.Ok((await sessions.RecentAsync(limit, cancellationToken)).Select(SessionContract.From)));

        group.MapGet("/{id:guid}", DetailAsync);

        return app;
    }

    /// <summary>A named method rather than a lambda, so the batch accounting can be asserted without
    /// standing up a host — the route is one line of wiring and this is the part with a rule in it.</summary>
    public static async Task<IResult> IngestAsync(
        IReadOnlyList<SessionEventDto>? events,
        ISessionStore sessions,
        CancellationToken cancellationToken)
    {
        if (events is null || events.Count == 0)
        {
            // A well-formed request carrying nothing is a bad request, not an empty success: a hook client
            // posting an empty body has a defect, and answering 200 would hide it forever.
            return Results.BadRequest(new ProblemDto("no session events in the body"));
        }

        return Results.Ok(await SessionIngest.AcceptAsync(events, sessions, cancellationToken));
    }

    /// <summary>One session, with its calls, its phase economics and what the detectors found.
    /// <para>
    /// <b>404 and never an empty session.</b> "This database has no such session" and "this session did
    /// nothing" are opposite pieces of news, and a caller handed an empty trace for the first would go
    /// looking in the wrong place — the same distinction the run report draws between 400 and 404.
    /// </para></summary>
    public static async Task<IResult> DetailAsync(
        Guid id, ISessionStore sessions, CancellationToken cancellationToken)
    {
        var found = await sessions.ByIdAsync(id, cancellationToken);

        return found.Match(
            run => Results.Ok(SessionContract.From(run, id, ToolTaxonomy.For(run.Runtime))),
            reason => Results.NotFound(new ProblemDto(reason)));
    }
}
