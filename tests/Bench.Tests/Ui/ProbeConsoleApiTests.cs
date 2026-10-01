using System.Net;
using Bench.Contracts;
using Bench.Ui.Services;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Ui.ProbeUiFixtures;

namespace Bench.Tests.Ui;

/// <summary>The console's two probe reads (S4): each asks the route <c>ProbeApi</c> maps, reads the report back whole, and reads a
/// refusal as the server's own sentence — never as an empty run.</summary>
public sealed class ProbeConsoleApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_run_list_is_read_with_its_window_and_an_empty_store_is_available_and_empty()
    {
        var api = new ScriptedBenchApi().Answers(RunsRoute, Array.Empty<ProbeRunSummaryDto>());

        var read = await new BenchConsoleApi(api.Client()).GetProbeRunsAsync(cancellationToken: Ct);

        read.Available.Should().BeTrue();
        read.Value.Should().BeEmpty();
        api.Calls.Should().Equal($"{RunsRoute}?limit=50");
    }

    [Fact]
    public async Task One_run_reads_back_whole_and_an_unknown_run_is_the_server_s_sentence()
    {
        var runId = Guid.CreateVersion7();
        var report = Report(runId, [Cell("read-inside", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0))],
            dropped: [new ProbeDroppedPairDto("api-reachable", "claude-a", "api-probe-on-cli")]);
        var missing = Guid.CreateVersion7();
        var api = new ScriptedBenchApi().Answers(RunRoute(runId), report).Answers(RunRoute(missing), new ProblemDto($"no probe run {missing}"), HttpStatusCode.NotFound);
        var console = new BenchConsoleApi(api.Client());

        var read = await console.GetProbeRunAsync(runId, Ct);
        var refused = await console.GetProbeRunAsync(missing, Ct);

        read.Value.Should().BeEquivalentTo(report, o => o.WithStrictOrdering());
        refused.Available.Should().BeFalse();
        refused.Detail.Should().Be($"no probe run {missing}");
        api.Calls.Should().Equal(RunRoute(runId), RunRoute(missing));
    }
}
