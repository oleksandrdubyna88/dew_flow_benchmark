using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Lanes;
using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>
/// Turning a catalog row into the surface a leg runs against.
///
/// <para>The assertions that matter are the refusals. A presentation quietly downgraded to whatever engine
/// was at hand would produce a number labelled with a surface that never ran — and the SHAPE of a surface is
/// the axis that moved a score nine times: the same four tools scored 4 of 63 over the wire against 36
/// in-process. That is the one substitution which makes the whole comparison meaningless.</para>
/// </summary>
public sealed class LaneResolutionTests
{
    [Fact]
    public void The_no_tools_arm_resolves_without_an_engine_because_that_is_what_makes_it_the_floor()
    {
        var choice = Resolved(Lane(LaneDefinition.NoTools("read carefully")), engine: null);

        choice.Surface.Should().BeOfType<ToolSurface.None>();
        // Its doctrine still travels: "no tools, but read carefully" is a legitimate instruction and a
        // legitimate arm, and dropping it would make the floor untestable as a wording.
        choice.Doctrine.Should().Be("read carefully");
    }

    [Fact]
    public void A_bridge_lane_with_no_engine_is_refused_rather_than_run_without_tools()
    {
        Refusal(Lane(Bridge()), engine: null)
            .Should().Contain("offers tools").And.Contain("no engine");
    }

    [Theory]
    [InlineData(ToolPresentation.CliNative)]
    [InlineData(ToolPresentation.CliNativeWithMcp)]
    public void A_CLI_agent_lane_is_refused_because_this_runner_cannot_drive_its_loop(ToolPresentation presentation)
    {
        // The distinction the refusal has to carry: not "unbuilt" but "cannot be driven this way". A CLI
        // agent runs its own loop and its calls arrive later through the telemetry spool.
        Refusal(Lane(Presented(presentation)), new StubEngine())
            .Should().Contain("its own loop").And.Contain("reconstructed from");
    }

    [Fact]
    public void An_empty_subset_offers_every_tool_the_engine_serves()
    {
        var choice = Resolved(Lane(Bridge()), new StubEngine());

        Looping(choice).Tools.Select(t => t.Name).Should().Equal("read", "search", "list");
    }

    [Fact]
    public void A_named_subset_offers_only_those_tools_in_the_ENGINES_order()
    {
        // The engine's order, not the subset's: a tools array's ordering is something a model can be
        // sensitive to, and it must not vary with how somebody typed a CLI flag.
        var choice = Resolved(Lane(Bridge(["list", "read"])), new StubEngine());

        Looping(choice).Tools.Select(t => t.Name).Should().Equal("read", "list");
    }

    [Fact]
    public void A_subset_naming_a_tool_the_engine_does_not_serve_is_refused_with_both_sides_listed()
    {
        // Never a shorter surface served quietly: a lane whose NAME says four tools and whose request
        // carried three is a row that will be compared against other four-tool rows.
        var refusal = Refusal(Lane(Bridge(["read", "graf_search_types"])), new StubEngine());

        refusal.Should().Contain("graf_search_types");
        refusal.Should().Contain("does not").And.Contain("read");
    }

    [Fact]
    public void The_turn_ceiling_travels_from_the_definition_to_the_surface()
    {
        Looping(Resolved(Lane(Bridge(maxTurns: 1)), new StubEngine())).MaxTurns.Should().Be(1);
    }

    [Fact]
    public void The_resolved_choice_is_keyed_by_the_lane_NAME_a_cell_already_carries()
    {
        // There is no LaneId on a cell; the name is the join. A choice keyed by anything else would need a
        // schema change nobody asked for.
        Resolved(Lane(Bridge(), "bridge-4"), new StubEngine()).Name.Should().Be("bridge-4");
    }

    [Fact]
    public void A_run_that_resolved_no_lane_gets_the_FLOOR_rather_than_a_refusal()
    {
        // What keeps this axis additive: every run planned before the lane catalog existed resolves no
        // lane, every cell gets the floor, and it behaves exactly as it did.
        Roster([], engine: null).Should().BeSameAs(LaneRoster.Floor);
    }

    [Fact]
    public void Every_lane_of_a_run_resolves_into_the_roster_a_plan_carries()
    {
        var roster = Roster(
            [Lane(LaneDefinition.NoTools("first"), "floor"), Lane(Bridge(), "bridge-3")],
            new StubEngine());

        roster.Entries.Select(e => e.Name).Should().Equal("floor", "bridge-3");
    }

    [Fact]
    public void One_lane_that_cannot_be_honoured_refuses_the_WHOLE_roster_rather_than_dropping_an_arm()
    {
        // A run planned as two doctrines and started as one produces a comparison whose missing arm is
        // invisible — its cells would refuse one by one, hours in, as a pile of abandoned cells rather
        // than as "this run cannot be planned".
        RosterRefusal(
            [Lane(LaneDefinition.NoTools("first"), "floor"), Lane(Bridge(), "bridge-3")],
            engine: null)
            .Should().Contain("bridge-3").And.Contain("no engine");
    }

    [Fact]
    public void A_lane_NAME_used_twice_is_refused_because_a_cell_resolves_its_lane_by_name()
    {
        // LaneRoster.For takes the first match, so a repeat would measure one lane twice and label the
        // other half of the cells with a surface that never ran.
        RosterRefusal(
            [Lane(Bridge(), "same"), Lane(Bridge(["read"]), "same")],
            new StubEngine())
            .Should().Contain("'same'").And.Contain("more than once");
    }

    private static LaneRoster Roster(IReadOnlyList<ToolLane> lanes, IEngine? engine) =>
        LaneResolution.Resolve(lanes, engine).Should().BeOfType<Outcome<LaneRoster>.Ok>().Subject.Value;

    private static string RosterRefusal(IReadOnlyList<ToolLane> lanes, IEngine? engine) =>
        LaneResolution.Resolve(lanes, engine).Should().BeOfType<Outcome<LaneRoster>.Fail>().Subject.Reason;

    private static ToolSurface.Looping Looping(LaneChoice choice) =>
        choice.Surface.Should().BeOfType<ToolSurface.Looping>().Subject;

    private static LaneChoice Resolved(ToolLane lane, IEngine? engine) =>
        LaneResolution.Resolve(lane, engine).Should().BeOfType<Outcome<LaneChoice>.Ok>().Subject.Value;

    private static string Refusal(ToolLane lane, IEngine? engine) =>
        LaneResolution.Resolve(lane, engine).Should().BeOfType<Outcome<LaneChoice>.Fail>().Subject.Reason;

    private static LaneDefinition Bridge(IReadOnlyList<string>? tools = null, int maxTurns = 25) =>
        LaneDefinition.Create(tools ?? [], "default", "a doctrine", ToolPresentation.Bridge, maxTurns)
            .Should().BeOfType<Outcome<LaneDefinition>.Ok>().Subject.Value;

    private static LaneDefinition Presented(ToolPresentation presentation) =>
        LaneDefinition.Create([], "default", "", presentation, 5)
            .Should().BeOfType<Outcome<LaneDefinition>.Ok>().Subject.Value;

    private static ToolLane Lane(LaneDefinition definition, string name = "lane-under-test") =>
        ToolLane.Create(name, "", definition, DateTimeOffset.UnixEpoch)
            .Should().BeOfType<Outcome<ToolLane>.Ok>().Subject.Value;

    /// <summary>Three tools in a deliberate order, so the ordering assertions mean something.</summary>
    private sealed class StubEngine : IEngine
    {
        public EngineRef Describe => EngineRef.Filesystem();

        public string TraceContractVersion => string.Empty;

        public IReadOnlyList<EngineTool> Tools { get; } =
        [
            new EngineTool("read", "reads", """{"type":"object"}"""),
            new EngineTool("search", "searches", """{"type":"object"}"""),
            new EngineTool("list", "lists", """{"type":"object"}"""),
        ];

        public Task<Outcome<string>> WarmAsync(string checkoutPath, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("warm"));

        public Task<ToolAnswer> InvokeAsync(string tool, string argumentsJson, CancellationToken cancellationToken) =>
            Task.FromResult(ToolAnswer.Success("ok"));
    }
}
