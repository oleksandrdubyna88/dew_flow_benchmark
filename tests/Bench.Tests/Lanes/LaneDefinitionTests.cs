using Bench.Domain;
using Bench.Domain.Lanes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Lanes;

/// <summary>
/// A lane is an identity, and these tests are about what that costs.
///
/// <para>Every result names the lane it ran under, so two configurations that are genuinely different must
/// hash differently and two spellings of one configuration must hash the same. The refusals matter for the
/// same reason: a field silently clamped or deduplicated would make two inputs one identity, and a report
/// over them would be a comparison of nothing.</para>
/// </summary>
public sealed class LaneDefinitionTests
{
    [Fact]
    public void The_same_configuration_written_two_ways_is_one_identity()
    {
        // Tool ORDER is not a configuration. A lane written with its tools listed differently is the same
        // surface, and a hash that disagreed would split one arm's results across two rows.
        var first = Built(["b_tool", "a_tool"]);
        var second = Built(["a_tool", "b_tool"]);

        second.Hash.Should().Be(first.Hash);
        second.Canonical.Should().Be(first.Canonical);
    }

    /// <summary>The floor arm's hash, PINNED as a literal.
    /// <para>This is the assertion that actually proves "stable across processes": two constructions inside
    /// one test would agree even under <see cref="object.GetHashCode"/>, which is randomised per process and
    /// would relabel every number measured before a restart. A literal written down once fails the day the
    /// canonical form changes — which is exactly when every stored lane identity would move.</para></summary>
    private const string FloorHash = "3d77dc441518560f666f8615dd4647b3848e9ee08cf71e3bbca7d46ce5cf81fb";

    [Fact]
    public void The_floor_arms_hash_is_pinned_so_a_canonical_change_cannot_pass_quietly()
    {
        LaneDefinition.NoTools().Hash.Should().Be(FloorHash);
    }

    [Fact]
    public void A_hash_is_lower_case_hex_and_the_same_for_two_equal_definitions()
    {
        Built().Hash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");
        Built().Hash.Should().Be(Built().Hash);
    }

    [Theory]
    [InlineData("cover all the channels before answering")]
    [InlineData("retrieval first, then confirm, then read")]
    public void A_doctrine_edit_produces_a_different_lane(string doctrine)
    {
        // The axis this whole plan is about: one paragraph moved a score 16.5 points of 63. If a doctrine
        // change did not change the identity, the two arms would be recorded as one.
        var rewritten = Create(doctrine: doctrine);

        rewritten.Hash.Should().NotBe(Built().Hash);
        rewritten.DoctrineHash.Should().NotBe(Built().DoctrineHash);
    }

    [Fact]
    public void The_doctrine_enters_the_identity_as_a_hash_and_the_text_is_kept_beside_it()
    {
        var lane = Create(doctrine: "retrieval first, then confirm, then read");

        // A paragraph inside a canonical string makes it unreadable in a log; the text itself is stored, so
        // a published database still explains its own numbers.
        lane.Canonical.Should().NotContain("retrieval first");
        lane.Canonical.Should().Contain(lane.DoctrineHash[..12]);
        lane.Doctrine.Should().Be("retrieval first, then confirm, then read");
    }

    [Fact]
    public void An_empty_tool_subset_means_every_tool_and_says_so()
    {
        // "Unfiltered" is a real configuration, not an unset field — and it must be distinguishable in the
        // canonical form from a subset that happens to name nothing.
        var everything = Create(tools: []);

        everything.ToolNames.Should().BeEmpty();
        everything.Canonical.Should().Contain("tools=*");
    }

    [Fact]
    public void ToolsHash_is_what_makes_holding_the_tool_set_fixed_a_group_by()
    {
        // "Which wording wins, with the same tools" is the question the leaderboard exists for. It is a
        // GROUP BY only if the tool set has its own column.
        var doctrineA = Create(doctrine: "one");
        var doctrineB = Create(doctrine: "two");

        doctrineA.ToolsHash.Should().Be(doctrineB.ToolsHash);
        doctrineA.Hash.Should().NotBe(doctrineB.Hash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_lane_that_allows_no_turn_is_refused(int turns)
    {
        Refusal(maxTurns: turns).Should().Contain("at least one turn");
    }

    [Fact]
    public void An_absurd_turn_ceiling_is_REFUSED_rather_than_clamped()
    {
        // Deliberately against this repository's usual clamp-numbers rule: a definition is hashed, so
        // clamping 1000 to 100 would make two different configurations one identity, and the report would
        // silently be about a lane nobody configured.
        var refusal = Refusal(maxTurns: 1000);

        refusal.Should().Contain("1000").And.Contain(LaneDefinition.MaxTurnCeiling.ToString());
    }

    [Fact]
    public void A_tool_named_twice_is_refused_rather_than_deduplicated()
    {
        Refusal(tools: ["a_tool", "a_tool"]).Should().Contain("more than once").And.Contain("a_tool");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("has a space")]
    public void A_tool_name_that_is_not_a_token_is_refused(string name)
    {
        Refusal(tools: [name]).Should().Contain("non-blank token");
    }

    [Fact]
    public void The_no_tools_arm_may_not_name_a_tool()
    {
        // The floor every tool claim is compared against. It is the one row whose meaning must not be in
        // doubt, so a contradiction in it is refused rather than ignored.
        var refusal = Refusal(tools: ["a_tool"], presentation: ToolPresentation.None);

        refusal.Should().Contain("no tool presentation").And.Contain("offers nothing");
    }

    [Fact]
    public void The_no_tools_arm_may_not_name_a_description_set()
    {
        // Tools empty on purpose: with a tool in the list the FIRST contradiction fires and this assertion
        // would pass on the wrong refusal.
        Refusal(tools: [], descriptionSet: "concise-v1", presentation: ToolPresentation.None)
            .Should().Contain("nothing for it to describe");
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("sets/concise")]
    [InlineData(@"sets\concise")]
    public void A_description_set_that_names_a_PATH_is_refused(string set)
    {
        // A set name travels to a server and becomes a directory under a configured root. Refusing it here
        // is refusing to record an identity that cannot be served — before it reaches a hash and a report.
        Refusal(descriptionSet: set).Should().Contain("names a path, not a set");
    }

    [Fact]
    public void The_no_tools_floor_is_a_named_arm_rather_than_an_unset_definition()
    {
        var floor = LaneDefinition.NoTools();

        floor.OffersTools.Should().BeFalse();
        floor.MaxTurns.Should().Be(1);
        floor.Canonical.Should().Contain("presentation=None");
        // It still hashes, and still carries a doctrine hash — "no instruction" is an arm that has to be
        // groupable beside the others rather than a special case.
        floor.Hash.Should().HaveLength(64);
    }

    [Fact]
    public void The_presentation_is_part_of_the_identity()
    {
        // Measured: the same four tools scored 4/63 over the wire and 36/63 in-process. A leaderboard that
        // could not tell those apart would attribute to wording what belongs to the shape.
        Create(presentation: ToolPresentation.Bridge).Hash
            .Should().NotBe(Create(presentation: ToolPresentation.McpStdio).Hash);
    }

    private static LaneDefinition Built(IReadOnlyList<string>? tools = null) => Create(tools: tools);

    private static LaneDefinition Create(
        IReadOnlyList<string>? tools = null,
        string descriptionSet = "default",
        string doctrine = "",
        ToolPresentation presentation = ToolPresentation.Bridge,
        int maxTurns = 25) =>
        LaneDefinition.Create(tools ?? ["a_tool", "b_tool"], descriptionSet, doctrine, presentation, maxTurns)
            .Should().BeOfType<Outcome<LaneDefinition>.Ok>().Subject.Value;

    private static string Refusal(
        IReadOnlyList<string>? tools = null,
        string descriptionSet = "default",
        ToolPresentation presentation = ToolPresentation.Bridge,
        int maxTurns = 25) =>
        LaneDefinition.Create(tools ?? ["a_tool"], descriptionSet, "", presentation, maxTurns)
            .Should().BeOfType<Outcome<LaneDefinition>.Fail>().Subject.Reason;
}
