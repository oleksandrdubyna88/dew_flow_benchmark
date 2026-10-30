using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary><c>RunArms.Of</c> (E3.S3, C15): the one derivation of which arms the runs name — <c>make</c>, <c>status</c> and the brief rehearsal all ask it. Pure.</summary>
public sealed class RunArmsTests
{
    private static RunSpec Run(string name, params string[][] rounds) => new(name, rounds, [], []);

    [Fact]
    public void Every_arm_of_every_run_is_listed_once_in_file_order()
    {
        var runs = new[] { Run("part6", ["C1", "CR1"], ["C2"]), Run("part7", ["CHH1", "C1"], ["CR1", "CHH2"]) };

        RunArms.Of(runs).Should().Equal("C1", "CR1", "C2", "CHH1", "CHH2");
    }

    [Fact]
    public void No_run_means_no_arm()
    {
        RunArms.Of([]).Should().BeEmpty();
    }
}
