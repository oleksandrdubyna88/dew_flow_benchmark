using Bench.Application.Lanes;
using Bench.Domain;
using Bench.Domain.Lanes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Lanes;

/// <summary>
/// The stored shape of a lane.
///
/// <para>This JSON is the published artefact: it is what the catalog row holds, what a CLI accepts, and
/// what travels with the results. So the tests here are about what it REFUSES — a field silently dropped
/// would run a cell under a surface nobody asked for and label it with the name that asked for the
/// other one.</para>
/// </summary>
public sealed class LaneJsonTests
{
    [Fact]
    public void A_definition_round_trips_through_its_stored_form()
    {
        var original = Create(["a_tool", "b_tool"], "concise-v1", "retrieval first, then confirm", 25);

        var read = LaneJson.Read(LaneJson.Write(original))
            .Should().BeOfType<Outcome<LaneDefinition>.Ok>().Subject.Value;

        // Hash equality is the assertion that matters: two definitions that are equal as values but hash
        // differently would split one arm across two rows.
        read.Hash.Should().Be(original.Hash);
        read.Doctrine.Should().Be("retrieval first, then confirm");
        read.ToolNames.Should().Equal("a_tool", "b_tool");
    }

    [Fact]
    public void The_doctrine_travels_as_TEXT_inside_the_row()
    {
        // A published database must explain its own numbers without a second artefact. A doctrine stored as
        // a key into some other table is a number nobody can interpret once that table is gone.
        LaneJson.Write(Create(doctrine: "cover all the channels before answering"))
            .Should().Contain("cover all the channels before answering");
    }

    [Fact]
    public void A_field_this_build_does_not_know_is_REFUSED_rather_than_dropped()
    {
        // The whole reason for JsonUnmappedMemberHandling.Disallow. A dropped field is a configuration
        // somebody wrote down, nothing honoured, and the result labelled as if it had been.
        var refusal = LaneJson.Read(
            """{"presentation":"Bridge","tools":[],"descriptionSet":"","doctrine":"","maxTurns":1,"temperature":0.7}""");

        refusal.Should().BeOfType<Outcome<LaneDefinition>.Fail>()
            .Which.Reason.Should().Contain("could not be read");
    }

    [Fact]
    public void An_unknown_presentation_is_refused_with_the_legal_values_named()
    {
        // Never resolved to a default. A lane silently demoted to None would be the no-tools FLOOR wearing a
        // tool lane's name — the one substitution that makes every comparison against the floor meaningless.
        var refusal = LaneJson.Read("""{"presentation":"grpc","tools":[],"descriptionSet":"","doctrine":"","maxTurns":1}""");

        refusal.Should().BeOfType<Outcome<LaneDefinition>.Fail>()
            .Which.Reason.Should().Contain("grpc").And.Contain("Bridge").And.Contain("McpStdio");
    }

    [Fact]
    public void A_wire_that_omits_the_turn_ceiling_is_refused_rather_than_defaulted()
    {
        // It lands on 0 and the domain refuses it by name. Better than a silent 1, which would record a
        // single-turn micro-task under the name of an agentic lane.
        LaneJson.Read("""{"presentation":"Bridge","tools":[],"descriptionSet":"","doctrine":""}""")
            .Should().BeOfType<Outcome<LaneDefinition>.Fail>()
            .Which.Reason.Should().Contain("at least one turn");
    }

    [Fact]
    public void A_row_written_under_a_different_casing_still_resolves()
    {
        // A definition that stops parsing is a lane every historical result names and nothing can explain,
        // so reading is case-insensitive even though writing is camelCase.
        LaneJson.Read("""{"Presentation":"bridge","Tools":["a_tool"],"DescriptionSet":"default","Doctrine":"x","MaxTurns":3}""")
            .Should().BeOfType<Outcome<LaneDefinition>.Ok>()
            .Which.Value.MaxTurns.Should().Be(3);
    }

    [Fact]
    public void Malformed_json_is_a_value_not_a_throw()
    {
        LaneJson.Read("{not json")
            .Should().BeOfType<Outcome<LaneDefinition>.Fail>()
            .Which.Reason.Should().Contain("could not be read");
    }

    private static LaneDefinition Create(
        IReadOnlyList<string>? tools = null,
        string descriptionSet = "default",
        string doctrine = "",
        int maxTurns = 25) =>
        LaneDefinition.Create(tools ?? ["a_tool"], descriptionSet, doctrine, ToolPresentation.Bridge, maxTurns)
            .Should().BeOfType<Outcome<LaneDefinition>.Ok>().Subject.Value;
}
