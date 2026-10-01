using Bench.Application.Probes;
using Bench.Cli;
using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.ProbeStoreFixtures;

namespace Bench.Tests.Cli;

/// <summary>The text half of <c>bench probes report</c>: what the planner did not measure is named in the report itself (S4), not
/// only in the one terminal <c>run</c> printed it to — the write-up's "not measured" list is read off the run.</summary>
public sealed class ProbeReportTextTests
{
    [Fact]
    public void The_text_report_names_every_pair_the_planner_dropped_with_its_reason_word()
    {
        var (run, _, cells) = PlannedMatrix([ProbeKind.ReadInside, ProbeKind.ApiReachable], Subject("claude-a", "claude"), Subject("grok-d", "api"));

        var text = ProbeReportText.Of(ProbeReport.Of(run, cells));

        text.Should().Contain("not measured   read-inside × grok-d (cli-probe-on-api)")
            .And.Contain("not measured   api-reachable × claude-a (api-probe-on-cli)");
    }
}
