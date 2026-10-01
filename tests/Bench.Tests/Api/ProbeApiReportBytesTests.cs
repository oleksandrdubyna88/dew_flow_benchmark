using System.Text.Json.Nodes;
using Bench.Api;
using Bench.Application.Probes;
using Bench.Cli;
using Bench.Contracts;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bench.Tests.Api;

/// <summary>S4 acceptance 2: <c>GET /api/bench/probes/runs/{id}</c> answers the object <c>bench probes report --json</c> prints —
/// the same BYTES, with the run measured by the real verbs against a fake CLI and both surfaces reading the same database through
/// the read port the API host registers (<see cref="PostgresProbeReads"/>).</summary>
[Collection("postgres")]
public sealed class ProbeApiReportBytesTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_run_route_answers_the_cli_s_report_json_byte_for_byte_and_the_list_names_the_run_finished()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        setup.AddSubject("codex-fake", "codex", new JsonObject { ["mode"] = "refuse" });
        var run = await setup.PlanAndRunAsync("read-inside,read-outside-bare,api-reachable", 1);
        run.Code.Should().Be(ExitCodes.Pass, run.Output + run.Error);
        var runId = ProbesCliSetup.RunIdOf(run.Output);
        var cli = await setup.RunAsync("report", "--run", runId.ToString(), "--json");
        cli.Code.Should().Be(ExitCodes.Pass, cli.Error);

        await using var db = PostgresFixture.Context(setup.Connection);
        var reads = new PostgresProbeReads(db);
        var http = ProbeApiTests.Http(reads);
        var body = await ProbeApiTests.BodyAsync(await ProbeApi.RunAsync(runId, http, Ct), http);

        http.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        http.Response.ContentType.Should().StartWith("application/json");
        body.Should().Be(cli.Output.TrimEnd(), "one shape, two surfaces: the page reads exactly what the CLI prints");
        body.Should().Contain("\"dropped\":[{\"probe\":\"api-reachable\",\"subject\":\"claude-fake\",\"reason\":\"api-probe-on-cli\"}",
            "the planner's dropped pairs travel in the report, recomputed from the frozen subjects");

        var list = (Ok<IReadOnlyList<ProbeRunSummaryDto>>)await ProbeApi.RunsAsync(ProbeApiTests.Http(reads), Ct);
        list.Value!.Should().ContainSingle(r => r.RunId == runId).Which.Progress.Should().Be(new ProbeProgressDto(0, 0, 4, 0, false));
    }
}
