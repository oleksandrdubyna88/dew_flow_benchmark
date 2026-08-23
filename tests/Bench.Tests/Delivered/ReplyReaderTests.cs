using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>Reading a model's reply, and refusing the ones that cannot be read.
/// <para>
/// The tolerance and the strictness pull in opposite directions on purpose. Models wrap JSON in prose and
/// fences however firmly they are told not to, so the PACKAGING is forgiven — and once the object is
/// found, every field rule refuses rather than repairs, because a repaired reply puts a number nobody
/// produced into a published score.
/// </para></summary>
public sealed class ReplyReaderTests
{
    private static readonly string[] TwoKeys = ["s1", "s2"];

    [Fact]
    public void JSON_wrapped_in_PROSE_is_still_found()
    {
        var reply = ReplyReader.ReadObject("Sure! Here is the analysis you asked for:\n{\"steps\":[]}\nHope that helps.");

        reply.Should().BeOfType<Reply<System.Text.Json.JsonDocument>.Ok>();
    }

    [Fact]
    public void JSON_wrapped_in_a_FENCE_is_still_found()
    {
        DeliveredWorkReplies.ReadDecomposition("```json\n{\"steps\":[]}\n```")
            .Should().BeOfType<Reply<Decomposition>.Ok>();
    }

    [Fact]
    public void A_reply_with_NO_json_is_refused_rather_than_read_as_empty()
    {
        // "The model said nothing parseable" and "the model said there are no steps" are opposite facts,
        // and an empty decomposition would score zero as though that were a finding.
        DeliveredWorkReplies.ReadDecomposition("I could not complete this task.")
            .Reason.Should().Contain("no JSON object");
    }

    [Fact]
    public void MALFORMED_json_names_itself_as_malformed()
    {
        DeliveredWorkReplies.ReadDecomposition("{\"steps\": [,]}").Reason.Should().Contain("not valid JSON");
    }

    [Fact]
    public void A_decomposition_needs_a_STEPS_array()
    {
        DeliveredWorkReplies.ReadDecomposition("{\"points\":[]}").Reason.Should().Contain("no \"steps\" array");
    }

    [Fact]
    public void Every_step_needs_an_ANCHOR_in_the_diff()
    {
        // An unanchored step is the cheapest thing in the world to invent, and nothing downstream could
        // check it against the change it claims to describe.
        DeliveredWorkReplies.ReadDecomposition("""{"steps":[{"key":"s1","what":"did a thing"}]}""")
            .Reason.Should().Contain("s1").And.Contain("needs an anchor");
    }

    [Fact]
    public void A_DUPLICATE_step_key_is_refused_and_names_itself_once()
    {
        var reply = DeliveredWorkReplies.ReadDecomposition(
            """{"steps":[{"key":"s1","anchor":"a.cs"},{"key":"s1","anchor":"b.cs"}]}""");

        // The reason must name the key ONCE. An earlier shape read each step twice — to match and then to
        // report — and the second read saw its own first as a duplicate.
        reply.Reason.Should().Be("duplicate step key s1");
    }

    [Fact]
    public void A_CAP_and_its_reason_are_carried_rather_than_judged_here()
    {
        var reply = DeliveredWorkReplies.ReadDecomposition(
            """{"steps":[{"key":"s1","anchor":"a.cs","what":"x"}],"capped":true,"reason":"boilerplate"}""");

        // The GATE decides what a reason is worth. A parser that refused a thin one would take that
        // decision away from the place that has the measurements behind it.
        var value = reply.Should().BeOfType<Reply<Decomposition>.Ok>().Subject.Value;
        value.Capped.Should().BeTrue();
        value.Reason.Should().Be("boilerplate");
    }

    [Fact]
    public void An_ABSENT_cap_reads_as_not_capped_rather_than_as_missing()
    {
        var value = DeliveredWorkReplies.ReadDecomposition("""{"steps":[{"key":"s1","anchor":"a.cs"}]}""")
            .Should().BeOfType<Reply<Decomposition>.Ok>().Subject.Value;

        value.Capped.Should().BeFalse();
        value.Reason.Should().BeEmpty();
    }

    [Fact]
    public void Scores_come_back_in_the_order_they_were_ASKED()
    {
        var reply = DeliveredWorkReplies.ReadScores(
            """{"scores":[{"key":"s2","score":3,"why":"b"},{"key":"s1","score":5,"why":"a"}]}""", TwoKeys);

        // A caller zipping scores against steps must not depend on a model's ordering.
        var scores = reply.Should().BeOfType<Reply<IReadOnlyList<UnitScore>>.Ok>().Subject.Value;
        scores.Select(s => s.Key).Should().ContainInOrder("s1", "s2");
        scores[0].Score.Should().Be(5);
    }

    [Fact]
    public void A_score_OFF_THE_SCALE_is_refused_rather_than_clamped()
    {
        DeliveredWorkReplies.ReadScores("""{"scores":[{"key":"s1","score":11},{"key":"s2","score":1}]}""", TwoKeys)
            .Reason.Should().Contain("11 is outside 0-10");
    }

    [Fact]
    public void The_ZERO_score_is_admitted_because_the_band_is_the_whole_instrument()
    {
        var reply = DeliveredWorkReplies.ReadScores(
            """{"scores":[{"key":"s1","score":0,"why":"serves nothing"},{"key":"s2","score":4}]}""", TwoKeys);

        // A parser that rejected 0 as "missing" would restore the floor of 1 the zero band exists to
        // remove, and the padding would be paid for again.
        ((Reply<IReadOnlyList<UnitScore>>.Ok)reply).Value[0].Score.Should().Be(0);
    }

    [Fact]
    public void A_NON_INTEGER_score_is_refused()
    {
        DeliveredWorkReplies.ReadScores("""{"scores":[{"key":"s1","score":"high"},{"key":"s2","score":1}]}""", TwoKeys)
            .Reason.Should().Contain("must be an integer");
    }

    [Fact]
    public void A_MISSING_key_is_refused_because_a_dropped_step_drops_work_from_the_score()
    {
        DeliveredWorkReplies.ReadScores("""{"scores":[{"key":"s1","score":5}]}""", TwoKeys)
            .Reason.Should().Contain("missing: [s2]");
    }

    [Fact]
    public void An_INVENTED_key_is_refused_because_it_prices_a_step_the_diff_never_had()
    {
        DeliveredWorkReplies.ReadScores(
            """{"scores":[{"key":"s1","score":5},{"key":"s2","score":1},{"key":"s9","score":9}]}""", TwoKeys)
            .Reason.Should().Contain("unknown: [s9]");
    }

    [Fact]
    public void A_DUPLICATE_score_is_refused_before_the_coverage_check_can_be_fooled_by_it()
    {
        // Two entries for s1 and none for s2 would otherwise satisfy a naive count.
        DeliveredWorkReplies.ReadScores(
            """{"scores":[{"key":"s1","score":5},{"key":"s1","score":1}]}""", TwoKeys)
            .Reason.Should().Be("duplicate score for s1");
    }

    [Fact]
    public void A_score_entry_with_NO_key_is_refused()
    {
        DeliveredWorkReplies.ReadScores("""{"scores":[{"score":5}]}""", TwoKeys)
            .Reason.Should().Contain("missing its \"key\"");
    }

    [Fact]
    public void A_reply_with_no_SCORES_array_is_refused()
    {
        DeliveredWorkReplies.ReadScores("""{"result":[]}""", TwoKeys).Reason.Should().Contain("no \"scores\" array");
    }
}
