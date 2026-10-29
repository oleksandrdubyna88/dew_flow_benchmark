using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The arm map: random tokens minted once, and the two checks that keep an arm from learning another arm's
/// name — <c>Leaks</c> for a text about to be handed to it, <c>Scrub</c> for a line about to be logged.
/// <para>
/// What this does NOT prove: that the randomness is unpredictable. The source is a parameter here and a
/// counter; the console passes <c>RandomNumberGenerator</c>, and nothing in this class can say that it does.
/// </para></summary>
public sealed class ArmMapTests
{
    private static readonly string[] Schemes = ["C", "CR"];

    /// <summary>A byte source counting up from <paramref name="start"/>: eight big-endian bytes per call, and a record of the calls.</summary>
    private sealed class CountingSource(long start)
    {
        private long _next = start;

        public int Calls { get; private set; }

        public byte[] Next()
        {
            Calls++;
            var bytes = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(bytes, _next++);
            return bytes;
        }
    }

    private static string Token(long counter) => counter.ToString("x16");

    private static ArmMap Mapped(params string[] arms) => ArmMap.Extend(ArmMap.Empty, arms, new CountingSource(1).Next);

    [Fact]
    public void Tokens_are_minted_once_and_never_reminted_for_an_existing_map()
    {
        var first = ArmMap.Extend(ArmMap.Empty, ["C1", "CR1"], new CountingSource(1).Next);
        first.Entries.Select(e => e.Key).Should().Equal("C1", "CR1");
        first.Entries.Select(e => e.Value).Should().OnlyContain(token => Regex.IsMatch(token, "^[0-9a-f]{16}$"))
            .And.OnlyHaveUniqueItems().And.HaveCount(2);

        var second = ArmMap.Extend(first, ["C1", "CR1", "SRS1"], new CountingSource(100).Next);
        second.Entries.Take(2).Should().Equal(first.Entries, "an arm that has a token keeps it");
        second.Entries[2].Should().Be(new KeyValuePair<string, string>("SRS1", Token(100)));

        var untouched = ArmMap.Extend(second, ["C1"], () => throw new InvalidOperationException("no arm is new, nothing may be drawn"));
        untouched.Entries.Should().Equal(second.Entries, "an arm missing from the list is kept, not dropped");
    }

    [Fact]
    public void A_token_that_collides_with_one_already_in_the_map_is_drawn_again()
    {
        var map = ArmMap.Extend(ArmMap.Empty, ["C1"], new CountingSource(1).Next);
        var source = new CountingSource(1);

        var extended = ArmMap.Extend(map, ["CR1"], source.Next);

        extended.Entries[1].Value.Should().Be(Token(2), "the first draw repeated C1's bytes and was thrown away");
        source.Calls.Should().Be(2);
    }

    [Fact]
    public void A_source_that_cannot_give_a_fresh_token_is_a_programming_error_and_not_an_endless_loop()
    {
        var map = Mapped("C1");
        var stuck = () => ArmMap.Extend(map, ["CR1"], () => new CountingSource(1).Next());
        var short5 = () => ArmMap.Extend(map, ["CR1"], () => new byte[5]);
        var notAnArm = () => ArmMap.Extend(map, ["c-1"], new CountingSource(9).Next);

        stuck.Should().Throw<InvalidOperationException>();
        short5.Should().Throw<InvalidOperationException>().WithMessage("*5 bytes*");
        notAnArm.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_token_never_appears_in_a_log_line_or_a_brief()
    {
        var map = Mapped("C1", "CR1");
        var own = map.TokenOf("C1").Ok();
        var other = map.TokenOf("CR1").Ok();

        map.Scrub($"C1 started in /e/r/{own}/arm").Should().Be("C1 started in /e/r/<token>/arm");
        map.Scrub($"{other} and {own}").Should().Be("<token> and <token>");
        map.Scrub("nothing to hide").Should().Be("nothing to hide");

        map.Leaks($"/e/r/{own}/notes/code.md", "C1", Schemes).Should().BeEmpty("an arm's own token is its working directory");
        map.Leaks($"/e/r/{other}/notes", "C1", Schemes).Should().Equal(other);
        map.Leaks("see C1", "C1", Schemes).Should().Equal("C1");
        map.Leaks("scheme CR", "C1", Schemes).Should().Equal("CR");
        map.Leaks("MCP suite", "C1", ["C"]).Should().BeEmpty("a scheme name is a whole word only");
        map.Leaks("C10 and XC1", "C1", Schemes).Should().BeEmpty("neither is C1 as a whole word");
        map.Leaks("(CR1).", "C1", Schemes).Should().Equal(["CR1"], "punctuation is not part of a word");
    }

    [Fact]
    public void An_unknown_arm_is_refused_naming_the_known_ones()
    {
        var map = Mapped("C1", "CR1");

        map.TokenOf("C1").Ok().Should().Be(Token(1));
        map.TokenOf("SRS1").Reason().Should().Contain("SRS1").And.Contain("C1, CR1");
        ArmMap.Empty.TokenOf("C1").Reason().Should().Contain("(none)");
    }

    [Fact]
    public void A_map_round_trips_through_its_json_in_the_shells_shape()
    {
        var map = Mapped("CR1", "C1");

        var json = map.ToJson();

        ArmMap.Read(json).Ok().Entries.Should().Equal(map.Entries, "insertion order survives");
        json.Should().Be($"{{\n  \"CR1\": \"{Token(1)}\",\n  \"C1\": \"{Token(2)}\"\n}}");
        using var document = JsonDocument.Parse(json);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        document.RootElement.EnumerateObject().Should().OnlyContain(p => p.Value.ValueKind == JsonValueKind.String);
        ArmMap.Read(ArmMap.Empty.ToJson()).Ok().Entries.Should().BeEmpty();
    }

    [Fact]
    public void A_malformed_or_duplicate_token_is_refused()
    {
        var token = Token(7);

        ArmMap.Read($"{{\"C1\":\"{token}\"}}").Ok().Entries.Should().ContainSingle("a well-formed map is read");

        ArmMap.Read("{\"C1\":\"XYZ\"}").Reason().Should().Contain("C1").And.Contain("16 lower-case hex");
        ArmMap.Read($"{{\"C1\":\"{token}\",\"C2\":\"{token}\"}}").Reason().Should().Contain("C1").And.Contain("C2").And.Contain("share one token");
        ArmMap.Read($"{{\"c-1\":\"{token}\"}}").Reason().Should().Contain("c-1");
        ArmMap.Read($"{{\"C1\":\"{token}\",\"C1\":\"{Token(8)}\"}}").Reason().Should().Contain("C1").And.Contain("more than once");
        ArmMap.Read("{\"C1\":\"904c78af0123\"}").Failed().Should().BeTrue("the shell's 12-hex tokens are not 16 (a run mints its own)");
        ArmMap.Read($"{{\"C1\":\"{token.ToUpperInvariant().Replace('0', 'A')}\"}}").Failed().Should().BeTrue("upper-case hex is not the spelling");
        ArmMap.Read("[1]").Failed().Should().BeTrue();
        ArmMap.Read("{\"C1\":5}").Reason().Should().Contain("C1");
        ArmMap.Read("{").Failed().Should().BeTrue();
    }

    [Fact]
    public void Every_wrong_arm_of_one_map_is_named_whatever_kind_of_fault_it_has()
    {
        var token = Token(7);

        var reason = ArmMap.Read($"{{\"C1\":5,\"c-1\":\"{token}\",\"C2\":\"XYZ\"}}").Reason();

        reason.Should().Contain("C1", "its value is not a string").And.Contain("c-1", "its name is not an arm name").And.Contain("C2", "its token is not hex");
        reason.Split('\n').Should().HaveCount(3, "one sentence per wrong arm");
        ArmMap.Read("{\"C1\":1234567890123456}").Failed().Should().BeTrue("a number that happens to have 16 digits is still not a token");
    }

    [Fact]
    public void Two_maps_with_the_same_entries_are_equal_and_a_different_token_is_not()
    {
        var json = Mapped("C1", "CR1").ToJson();
        var first = ArmMap.Read(json).Ok();
        var second = ArmMap.Read(json).Ok();
        var other = ArmMap.Read(json.Replace(Token(2), Token(9))).Ok();

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.Should().NotBe(other, "the second arm's token differs");
        first.Should().NotBe(Mapped("CR1", "C1"), "order is part of the map");
    }

    [Fact]
    public void The_entries_cannot_be_changed_through_the_list_they_are_handed_out_as()
    {
        var map = Mapped("C1");

        map.Entries.Should().NotBeOfType<List<KeyValuePair<string, string>>>().And.NotBeOfType<KeyValuePair<string, string>[]>();
        (map.Entries is IList<KeyValuePair<string, string>> { IsReadOnly: false }).Should().BeFalse("a cast back to a writable list would let a caller rewrite a token");
    }
}
