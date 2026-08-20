using System.Net;
using Bench.Contracts;
using Bench.Ui.Services;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>The console's read side, and the distinction the whole shape exists for.
/// <para>
/// <b>"The server said nothing" and "the server said empty" are opposite facts.</b> A page rendering both as
/// an empty table sends its reader to the wrong place — to the CLI when the daemon is down, or to the daemon
/// when the database is simply empty. Every assertion here is about keeping those two apart.
/// </para></summary>
public sealed class BenchConsoleApiTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Runs_that_arrive_are_available_with_no_detail_to_explain()
    {
        var api = new ScriptedBenchApi().Answers("/api/bench/runs", new[] { Summary("wsl-to-wsl/migraphx/R9700") });

        var read = await new BenchConsoleApi(api.Client()).GetRunsAsync(cancellationToken: Ct);

        read.Available.Should().BeTrue();
        read.Value.Should().ContainSingle().Which.ComputeArm.Should().Be("wsl-to-wsl/migraphx/R9700");
        read.Detail.Should().BeEmpty("a successful read has nothing to explain, and a detail beside a value "
            + "would leave a page deciding which of the two to believe");
    }

    [Fact]
    public async Task An_empty_database_is_available_and_empty_rather_than_unavailable()
    {
        var api = new ScriptedBenchApi().Answers("/api/bench/runs", Array.Empty<RunSummaryDto>());

        var read = await new BenchConsoleApi(api.Client()).GetRunsAsync(cancellationToken: Ct);

        // The whole point of the flag. Nobody has measured anything yet — the API is fine.
        read.Available.Should().BeTrue();
        read.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task A_refusal_carries_the_servers_own_sentence_rather_than_a_status_code()
    {
        var api = new ScriptedBenchApi().Answers(
            "/api/bench/runs/" + Guid.Empty + "/report",
            new ProblemDto("name the metric to report on — there is no default"),
            HttpStatusCode.BadRequest);

        var read = await new BenchConsoleApi(api.Client()).GetReportAsync(Guid.Empty, "whatever", Ct);

        // The API explains itself; reporting "unreachable" over an explanation throws away the only sentence
        // that says what to do next.
        read.Available.Should().BeFalse();
        read.Detail.Should().Contain("name the metric");
    }

    [Fact]
    public async Task A_route_that_is_not_there_says_so_by_status_when_it_offers_no_sentence()
    {
        var read = await new BenchConsoleApi(new ScriptedBenchApi().Client()).GetRunsAsync(cancellationToken: Ct);

        read.Available.Should().BeFalse();
        read.Detail.Should().Contain("404");
    }

    [Fact]
    public async Task A_server_that_cannot_be_reached_is_a_different_sentence_from_one_that_refused()
    {
        var read = await new BenchConsoleApi(new HttpClient(new RefusingHandler())
        {
            BaseAddress = ScriptedBenchApi.BaseAddress,
        }).GetRunsAsync(cancellationToken: Ct);

        read.Available.Should().BeFalse();
        read.Detail.Should().Contain("could not be reached");
    }

    [Fact]
    public async Task The_metric_travels_escaped_so_a_name_with_a_space_is_not_a_truncated_request()
    {
        var api = new ScriptedBenchApi();

        await new BenchConsoleApi(api.Client()).GetReportAsync(Guid.Empty, WellKnownMetrics.AnchorRecall, Ct);

        // "Anchor recall" has a space in it, and every metric this console offers but one does. An unescaped
        // query would arrive truncated at the space and report on a metric nobody named.
        api.Calls.Should().ContainSingle().Which.Should().Contain("metric=Anchor%20recall");
    }

    private static RunSummaryDto Summary(string arm) =>
        new(Guid.CreateVersion7(), "run", "repo@commit", "s@v3#abcdef012345", $"Qln|e|1.0|fp|{arm}", arm,
            "Planned", DateTimeOffset.UnixEpoch);

    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No connection could be made because the target machine actively refused it");
    }
}
