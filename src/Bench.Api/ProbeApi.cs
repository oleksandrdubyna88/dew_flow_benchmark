using System.Globalization;
using Bench.Application.Probes;
using Bench.Contracts;
using Bench.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bench.Api;

/// <summary>The question consultant's capability probes, READ (S4 of <c>todo/PLAN_question_consultant_probes.md</c>) —
/// <c>/api/bench/probes/*</c>, over the same <see cref="ProbeReport"/> <c>bench probes report --json</c> prints, so the page and
/// the terminal read one run one way, byte for byte.
/// <para>
/// <b>Read-only by decision (D10).</b> Re-measuring a cell is the CLI verb <c>bench probes rerun</c>, which the page shows as a
/// copyable command; nothing here claims, settles or plans, and the port behind it — <see cref="IProbeReads"/> — has no write.
/// </para>
/// <para>
/// <b>The read port is resolved from the REQUEST's services</b>, never taken as a parameter — <see cref="GateApi"/>'s reason: a host
/// that never registered it (the qln console registers its bench ports by hand) would otherwise infer the parameter as a BODY and
/// fail its whole endpoint table at startup. Resolved here, such a host answers 503 naming the registration and every other route
/// keeps working. 400 is asked wrongly; 404 is a run this database does not hold.
/// </para></summary>
public static class ProbeApi
{
    /// <summary>The widest run list one request may ask for — the picker shows the newest; a window past this is a scan.</summary>
    public const int MaxLimit = 500;

    public const string NotRegistered =
        "the probes' read port is not registered on this host — register Bench.Application.Probes." + nameof(IProbeReads)
        + " (PostgresProbeReads over the BenchDbContext) beside IRunStore, then restart";

    public static IEndpointRouteBuilder MapProbeReads(this IEndpointRouteBuilder app)
    {
        var probes = app.MapGroup("/api/bench/probes");

        probes.MapGet("/runs", RunsAsync);
        probes.MapGet("/runs/{id:guid}", RunAsync);

        return app;
    }

    /// <summary>The newest runs first, each with where it stands (open while any cell is Pending or Claimed) — the run picker.</summary>
    public static Task<IResult> RunsAsync(HttpContext http, CancellationToken cancellationToken, int limit = 50) =>
        WithReads(http, async reads => limit is >= 1 and <= MaxLimit
            ? Results.Ok(await ProbeReport.RecentAsync(reads, limit, cancellationToken))
            : Results.BadRequest(new ProblemDto(
                $"limit must be between 1 and {MaxLimit.ToString(CultureInfo.InvariantCulture)}, got {limit.ToString(CultureInfo.InvariantCulture)}")));

    /// <summary>One run whole — exactly <see cref="ProbeReport.ReadAsync"/>'s object.</summary>
    public static Task<IResult> RunAsync(Guid id, HttpContext http, CancellationToken cancellationToken) =>
        WithReads(http, async reads => await ProbeReport.ReadAsync(reads, id, cancellationToken) switch
        {
            Outcome<ProbeRunReportDto>.Ok ok => Results.Ok(ok.Value),
            Outcome<ProbeRunReportDto>.Fail fail => Results.NotFound(new ProblemDto(fail.Reason)),
            _ => throw new InvalidOperationException("unreachable"),
        });

    private static async Task<IResult> WithReads(HttpContext http, Func<IProbeReads, Task<IResult>> read) =>
        http.RequestServices.GetService<IProbeReads>() is { } reads
            ? await read(reads)
            : Results.Json(new ProblemDto(NotRegistered), statusCode: StatusCodes.Status503ServiceUnavailable);
}
