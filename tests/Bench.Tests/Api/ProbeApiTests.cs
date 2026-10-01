using System.Text;
using Bench.Api;
using Bench.Application;
using Bench.Application.Probes;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Tests.Application;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Bench.Tests.Infrastructure.ProbeStoreFixtures;

namespace Bench.Tests.Api;

/// <summary>The probe read routes (S4 of the question-consultant probes plan): where they mount, that they only READ, that a host
/// which never registered <see cref="IProbeReads"/> still starts and answers 503 naming it, and every status the handlers decide —
/// 400 asked wrongly, 404 not here. The byte-for-byte equality with <c>bench probes report --json</c> is
/// <see cref="ProbeApiReportBytesTests"/> (it needs a database).</summary>
public sealed class ProbeApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_probe_routes_mount_under_the_slice_prefix_and_every_one_of_them_only_reads()
    {
        var endpoints = Endpoints();

        endpoints.Select(e => e.Route).Should().Contain(["/api/bench/probes/runs", "/api/bench/probes/runs/{id:guid}"]);
        endpoints.Where(e => e.Route.StartsWith("/api/bench/probes", StringComparison.Ordinal)).Should().OnlyContain(
            e => e.Methods.SequenceEqual(new[] { "GET" }), "D10: the tab is read-only and re-measuring is a CLI verb — no probe route may write");
    }

    [Fact]
    public async Task A_host_without_the_probe_read_port_answers_503_naming_the_registration_on_both_routes()
    {
        var list = await ProbeApi.RunsAsync(Http(null), Ct);
        var one = await ProbeApi.RunAsync(Guid.CreateVersion7(), Http(null), Ct);

        Status(list).Should().Be(StatusCodes.Status503ServiceUnavailable);
        Status(one).Should().Be(StatusCodes.Status503ServiceUnavailable);
        Problem(one).Reason.Should().Be(ProbeApi.NotRegistered);
        ProbeApi.NotRegistered.Should().Contain(nameof(IProbeReads)).And.Contain("PostgresProbeReads");
    }

    [Fact]
    public async Task The_run_list_answers_each_run_newest_first_with_where_it_stands()
    {
        var (older, olderCells) = Planned(2);
        var (newer, newerCells) = Planned(1);
        var reads = new ScriptedProbeReads([newer with { CreatedAt = Noon.AddHours(1) }, older], [.. olderCells, .. newerCells]);

        var result = await ProbeApi.RunsAsync(Http(reads), Ct);

        Status(result).Should().Be(StatusCodes.Status200OK);
        ((Ok<IReadOnlyList<ProbeRunSummaryDto>>)result).Value.Should().Equal(
            new ProbeRunSummaryDto(newer.Id, Noon.AddHours(1), 1, 3, false, new ProbeProgressDto(1, 0, 0, 0, true)),
            new ProbeRunSummaryDto(older.Id, Noon, 1, 3, false, new ProbeProgressDto(2, 0, 0, 0, true)));
        reads.AskedLimit.Should().Be(50, "the list's default window");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ProbeApi.MaxLimit + 1)]
    public async Task A_run_list_window_outside_one_to_the_maximum_is_400_naming_the_bounds(int limit)
    {
        var result = await ProbeApi.RunsAsync(Http(new ScriptedProbeReads([], [])), Ct, limit);

        Status(result).Should().Be(StatusCodes.Status400BadRequest);
        Problem(result).Reason.Should().Contain("limit").And.Contain(ProbeApi.MaxLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task One_run_is_the_report_query_s_object_and_an_unknown_run_is_404_saying_so()
    {
        var (run, cells) = Planned(1);
        var reads = new ScriptedProbeReads([run], cells);

        var found = await ProbeApi.RunAsync(run.Id, Http(reads), Ct);
        var missing = await ProbeApi.RunAsync(Guid.CreateVersion7(), Http(reads), Ct);

        Status(found).Should().Be(StatusCodes.Status200OK);
        ((Ok<ProbeRunReportDto>)found).Value.Should().BeEquivalentTo(ProbeReport.Of(run, cells), o => o.WithStrictOrdering());
        Status(missing).Should().Be(StatusCodes.Status404NotFound);
        Problem(missing).Reason.Should().Contain("no probe run");
    }

    private static IReadOnlyList<(string Route, IReadOnlyList<string> Methods)> Endpoints()
    {
        // The qln daemon's container shape: the run stores and nothing of the probes — a handler taking the port as a parameter
        // would be inferred as a BODY there and fail the whole endpoint table.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IRunStore>(new ScriptedRun([]));
        builder.Services.AddSingleton<IResultStore>(new ScriptedResults([], "m"));
        var app = builder.Build();
        app.MapBenchApi();

        return [.. ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Select(e => ("/" + e.RoutePattern.RawText?.TrimStart('/'),
                (IReadOnlyList<string>)[.. e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []]))];
    }

    internal static HttpContext Http(IProbeReads? reads)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (reads is not null)
        {
            services.AddSingleton(reads);
        }

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider(), Response = { Body = new MemoryStream() } };
    }

    internal static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    private static ProblemDto Problem(IResult result) => (ProblemDto)((IValueHttpResult)result).Value!;

    /// <summary>The bytes the minimal-API JSON writer puts on the wire for <paramref name="result"/>.</summary>
    internal static async Task<string> BodyAsync(IResult result, HttpContext http)
    {
        await result.ExecuteAsync(http);
        http.Response.Body.Position = 0;
        return Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
    }
}

/// <summary>A probe read port over fixed values — the run list in the order given (the store orders it newest first).</summary>
internal sealed class ScriptedProbeReads(IReadOnlyList<ProbeRun> runs, IReadOnlyList<ProbeCell> cells) : IProbeReads
{
    public int AskedLimit { get; private set; }

    public Task<IReadOnlyList<ProbeRun>> RecentRunsAsync(int limit, CancellationToken cancellationToken)
    {
        AskedLimit = limit;
        return Task.FromResult<IReadOnlyList<ProbeRun>>([.. runs.Take(limit)]);
    }

    public Task<Outcome<ProbeRun>> RunAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(runs.FirstOrDefault(r => r.Id == runId) is { } run ? Outcome<ProbeRun>.Success(run) : Outcome<ProbeRun>.Failure($"no probe run {runId}"));

    public Task<IReadOnlyList<ProbeCell>> CellsAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProbeCell>>([.. cells.Where(c => c.RunId == runId)]);

    public Task<IReadOnlyList<ProbeCell>> LatestSettledAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(ProbeGenerations.LatestSettled([.. cells.Where(c => c.RunId == runId)]));
}
