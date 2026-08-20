using Bench.Api;
using Bench.Application;
using Bench.Tests.Application;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bench.Tests.Api;

/// <summary>Where this surface mounts, asserted because it stopped being a private decision.
/// <para>
/// The group used to be <c>/api</c>, which is fine for a host serving nothing else and wrong the moment the
/// module is mounted into the DewFlow daemon beside a console that already owns <c>/api/health</c>. A prefix
/// collision does not fail a build or a startup: one of the two routes wins, and the loser answers a
/// stranger's JSON. So the prefix is a contract now.
/// </para></summary>
public sealed class BenchApiMountingTests
{
    [Fact]
    public void Every_route_lives_under_the_slice_prefix_so_a_host_can_mount_it_beside_its_own()
    {
        Routes().Should().NotBeEmpty("a surface that mounts nothing would pass every assertion below");
        Routes().Should().OnlyContain(route => route.StartsWith("/api/bench", StringComparison.Ordinal));
    }

    [Fact]
    public void The_routes_the_console_reads_are_the_ones_it_expects()
    {
        // Named individually rather than counted: a count passes when a route is renamed, and the pages
        // that read these are in another repository, where a rename is invisible until a page is blank.
        Routes().Should().Contain(["/api/bench/health", "/api/bench/runs", "/api/bench/runs/{id:guid}/report"]);
    }

    /// <summary>Mounts the group in a container that holds the stores, and reads back what it mounted.
    /// <para>
    /// The stores are registered because parameter inference needs them: minimal APIs decide whether
    /// <c>IRunStore</c> is a SERVICE or a request BODY by asking the container, and an empty one infers a
    /// body — which a GET forbids, so enumerating the endpoints throws before any assertion runs. Worth
    /// writing down: the failure names inferred bodies and says nothing about a missing registration.
    /// </para></summary>
    private static IReadOnlyList<string> Routes()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton<IRunStore>(new ScriptedRun([]));
        builder.Services.AddSingleton<IResultStore>(new ScriptedResults([], "m"));

        var app = builder.Build();
        app.MapBenchApi();

        return
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(endpoint => "/" + endpoint.RoutePattern.RawText?.TrimStart('/')),
        ];
    }
}
