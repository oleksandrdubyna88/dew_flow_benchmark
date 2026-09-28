using Bench.Api;
using Bench.Application;
using Bench.Application.Gate;
using Bench.Contracts;
using Bench.Domain.Gate;
using Bench.Tests.Application;
using Bench.Tests.Gate;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Api;

/// <summary>The gate routes (E6, S6.2): where they mount, that a host which never registered the gate's read port still
/// starts — the qln console registers its bench ports by hand — and every status the handlers decide: 400 asked wrongly, 404
/// not here, 409 its suite's tasks were never recorded, 503 the port is not registered.</summary>
public sealed class GateApiTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void The_gate_routes_mount_under_the_slice_prefix_in_a_host_that_registered_only_the_run_stores()
    {
        // The qln daemon's container, as it is today: IRunStore and IResultStore, nothing of the gate. A handler taking the
        // gate port as a plain parameter would be inferred as a BODY there, and a GET with an inferred body fails the
        // endpoint table as a whole — every console route down, not one.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IRunStore>(new ScriptedRun([]));
        builder.Services.AddSingleton<IResultStore>(new ScriptedResults([], "m"));
        var app = builder.Build();
        app.MapBenchApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Select(e => "/" + e.RoutePattern.RawText?.TrimStart('/')).ToList();

        routes.Should().Contain(["/api/bench/gate/scopes", "/api/bench/gate/{gate}/models", "/api/bench/gate/{gate}/runs", "/api/bench/gate/runs/{id:guid}"]);
    }

    [Fact]
    public async Task A_host_without_the_gate_read_port_answers_503_naming_the_registration()
    {
        var result = await GateApi.ScopesAsync(Http(null), Ct);

        Status(result).Should().Be(StatusCodes.Status503ServiceUnavailable);
        Problem(result).Reason.Should().Be(GateApi.NotRegistered);
        GateApi.NotRegistered.Should().Contain(nameof(IGateReads)).And.Contain("PostgresGateReads");
    }

    [Fact]
    public async Task The_scope_list_answers_200_with_every_partition_and_a_word_that_is_no_gate_is_400()
    {
        var reads = Reads(out var a);

        var all = await GateApi.ScopesAsync(Http(reads), Ct);
        var bad = await GateApi.ScopesAsync(Http(reads), Ct, gate: "7");

        Status(all).Should().Be(StatusCodes.Status200OK);
        ((Ok<IReadOnlyList<GateScopeDto>>)all).Value.Should().ContainSingle(s => s.Id == a.Scope.Id);
        Status(bad).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task A_table_answers_400_without_a_scope_or_a_rubric_404_for_an_unknown_scope_and_409_when_the_tasks_are_not_recorded()
    {
        var reads = Reads(out var a);

        Status(await GateApi.ModelsAsync("feature", Http(reads), Ct, scope: null, rubric: "strict-v1")).Should().Be(StatusCodes.Status400BadRequest);
        Status(await GateApi.ModelsAsync("feature", Http(reads), Ct, scope: a.Scope.Id, rubric: null)).Should().Be(StatusCodes.Status400BadRequest);
        Status(await GateApi.ModelsAsync("feature", Http(reads), Ct, scope: "000000000000", rubric: "strict-v1")).Should().Be(StatusCodes.Status404NotFound);
        Status(await GateApi.ModelsAsync("feature", Http(reads with { Tasks = new Dictionary<string, IReadOnlyList<TaskSummary>>() }), Ct, scope: a.Scope.Id, rubric: "strict-v1"))
            .Should().Be(StatusCodes.Status409Conflict);
        Status(await GateApi.ModelsAsync("feature", Http(reads), Ct, scope: a.Scope.Id, rubric: "strict-v1")).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task The_run_list_is_400_without_a_scope_and_one_run_is_404_when_this_database_does_not_hold_it()
    {
        var reads = Reads(out var a);

        Status(await GateApi.RunsAsync("feature", Http(reads), Ct, scope: null)).Should().Be(StatusCodes.Status400BadRequest);
        Status(await GateApi.RunsAsync("feature", Http(reads), Ct, scope: a.Scope.Id)).Should().Be(StatusCodes.Status200OK);
        Status(await GateApi.RunAsync(a.RunId, Http(reads), Ct)).Should().Be(StatusCodes.Status200OK);
        Status(await GateApi.RunAsync(Guid.CreateVersion7(), Http(reads), Ct)).Should().Be(StatusCodes.Status404NotFound);
    }

    private static ScriptedGateReads Reads(out GateRunRecord a)
    {
        a = Run("cs2", "grok", 1);
        var verdict = Verdict(a, 0, Strict(StrictReading.Supported, "cs2-S1"), StrictHash);
        return new ScriptedGateReads([a], new Dictionary<string, IReadOnlyList<TaskSummary>> { [a.Scope.SuiteStamp] = Input([]).Tasks },
            [StrictRubric], [verdict], []);
    }

    private static HttpContext Http(IGateReads? reads)
    {
        var services = new ServiceCollection();
        if (reads is not null)
        {
            services.AddSingleton(reads);
        }

        return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
    }

    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    private static ProblemDto Problem(IResult result) => (ProblemDto)((IValueHttpResult)result).Value!;
}
