using System.Net;
using Bench.Contracts;
using Bench.Ui.Services;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Ui.GateUiFixtures;

namespace Bench.Tests.Ui;

/// <summary>The console's four gate reads (E6): each asks the route the API maps, with its arguments escaped, and reads a
/// refusal as the server's own sentence — a 409 that names the verb, a 404 that names what the gate holds.</summary>
public sealed class GateConsoleApiTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_scopes_are_read_for_one_gate_and_an_empty_gate_is_available_and_empty()
    {
        var api = new ScriptedBenchApi().Answers("/api/bench/gate/scopes", Array.Empty<GateScopeDto>());

        var read = await new BenchConsoleApi(api.Client()).GetGateScopesAsync("feature", Ct);

        read.Available.Should().BeTrue();
        read.Value.Should().BeEmpty();
        api.Calls.Should().Equal("/api/bench/gate/scopes?gate=feature");
    }

    [Fact]
    public async Task The_table_travels_its_scope_and_rubric_escaped_and_reads_back_whole()
    {
        var scope = Scope("aaaaaaaaaaa1", rubrics: [Strict]);
        var api = new ScriptedBenchApi().Answers("/api/bench/gate/feature/models", Table(scope, Strict, [Row("grok")]));

        var read = await new BenchConsoleApi(api.Client()).GetGateModelsAsync("feature", scope.Id, Strict.Stamp, Ct);

        read.Value!.Rows.Should().ContainSingle().Which.SupportedPct.State.Should().Be(GateFigureDto.NotHandCheckedState);
        api.Calls.Should().Equal($"/api/bench/gate/feature/models?scope=aaaaaaaaaaa1&rubric={Uri.EscapeDataString(Strict.Stamp)}");
        api.Calls[0].Should().Contain("strict-v1%23", "a stamp's # would otherwise end the query and ask under no rubric at all");
    }

    [Fact]
    public async Task A_scope_whose_tasks_are_not_recorded_is_the_servers_sentence_naming_the_verb()
    {
        var api = new ScriptedBenchApi().Answers("/api/bench/gate/feature/models",
            new ProblemDto("the tasks of suite s#1 are not recorded — `bench gate suite record` …"), HttpStatusCode.Conflict);

        var read = await new BenchConsoleApi(api.Client()).GetGateModelsAsync("feature", "x", "strict-v1", Ct);

        read.Available.Should().BeFalse();
        read.Detail.Should().Contain("bench gate suite record");
    }

    [Fact]
    public async Task The_run_list_and_one_run_are_read_from_their_own_routes()
    {
        var runId = Guid.CreateVersion7();
        var api = new ScriptedBenchApi().Answers("/api/bench/gate/code/runs", new[] { RunSummary("cs2") });
        var console = new BenchConsoleApi(api.Client());

        (await console.GetGateRunsAsync("code", "aaaaaaaaaaa1", Ct)).Value.Should().ContainSingle();
        var missing = await console.GetGateRunAsync(runId, Ct);

        missing.Available.Should().BeFalse();
        api.Calls.Should().Equal("/api/bench/gate/code/runs?scope=aaaaaaaaaaa1", $"/api/bench/gate/runs/{runId}");
    }
}
