using Bench.Application;
using Bench.Domain.Authoring;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>The code-task payload's codec: a stored CONFIGURATION, so unknown members refuse the read
/// by name — the `VariantJson` discipline, one row over.</summary>
public sealed class CodeTaskCodecTests
{
    private static readonly CommitSha Base = CommitSha.Parse(new string('b', 40)).Ok();
    private static readonly CommitSha Fix = CommitSha.Parse(new string('f', 40)).Ok();

    [Fact]
    public void A_task_round_trips_whole()
    {
        var task = CodeTask.Harvested(Base, Fix, "diff --git a/x b/x\n", gatesRan: true, "red at base, green with fix").Ok();

        var read = CodeTaskCodec.Read(CodeTaskCodec.Write(task)).Ok();

        read.Should().Be(task);
        read.Kind.Should().Be(CodeTask.FixKind);
        read.Mechanism.Should().BeEmpty("the mechanism is authored later, and harvest must not invent one");
    }

    [Fact]
    public void An_unknown_member_refuses_the_read_by_name()
    {
        var withExtra = CodeTaskCodec.Write(CodeTask.Harvested(Base, Fix, "diff\n", false, "skipped").Ok())
            .TrimEnd('}') + ",\"surprise\":1}";

        CodeTaskCodec.Read(withExtra).Reason().Should().Contain("surprise");
    }

    [Fact]
    public void A_payload_with_a_broken_sha_is_refused()
    {
        var json =
            "{\"kind\":\"fix\",\"baseCommit\":\"not-a-sha\",\"fixCommit\":\"" + new string('f', 40)
            + "\",\"referenceDiff\":\"d\",\"mechanism\":\"\",\"gatesRan\":false,\"gateDetail\":\"\"}";

        CodeTaskCodec.Read(json).Failed().Should().BeTrue();
    }

    [Fact]
    public void An_empty_payload_is_a_routing_defect_not_a_task()
    {
        CodeTaskCodec.Read(string.Empty).Reason().Should().Contain("empty");
    }

    [Fact]
    public void A_diffless_task_is_refused_at_construction()
    {
        CodeTask.Harvested(Base, Fix, "   ", false, "").Reason().Should().Contain("diff");
    }
}
