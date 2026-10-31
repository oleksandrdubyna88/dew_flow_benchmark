using FluentAssertions;
using ModelArms.Core.Tasks;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>E4.S2, D22 (pure): the pick record's two kinds of line and the skip decided from them. The record is read strictly; a torn tail is reported and dropped, a bad line before the last is refused; a completion is believed only when its commit is on the arm branch; the patch-id skip
/// is narrowed to a source with no line at all.
/// <para>
/// What this does NOT prove: that the commits exist (the adapter asks git, <c>TaskWorktreesTests</c>); that a patch-id is what git computes.
/// </para></summary>
public sealed class PickBackTests
{
    private static string Sha(char c) => new(c, 40);

    private static string IntentLine(char source, char head) => $"{{\"source\":\"{Sha(source)}\",\"headBefore\":\"{Sha(head)}\"}}";

    private static string PickedLine(char source, char picked, string patch = "") => $"{{\"source\":\"{Sha(source)}\",\"picked\":\"{Sha(picked)}\",\"patchId\":\"{patch}\"}}";

    [Fact]
    public void An_empty_text_is_an_empty_record()
    {
        PickRecord.Parse(string.Empty).Should().Be(new PickRecordRead.Lines([], false));
    }

    [Fact]
    public void One_intent_and_one_completion_are_read_and_written_back_byte_for_byte()
    {
        var text = IntentLine('a', 'b') + "\n" + PickedLine('a', 'c', Sha('d')) + "\n";

        var read = PickRecord.Parse(text).Should().BeOfType<PickRecordRead.Lines>().Subject;

        read.TornTail.Should().BeFalse();
        read.Valid.Should().Equal(new PickEntry.Intent(Sha('a'), Sha('b')), new PickEntry.Picked(Sha('a'), Sha('c'), Sha('d')));
        PickRecord.Write(read.Valid).Should().Be(text, "the repaired record and the marker are the same text");
    }

    [Fact]
    public void A_torn_tail_is_reported_and_the_valid_lines_are_kept()
    {
        var whole = IntentLine('a', 'b') + "\n";

        foreach (var torn in new[] { "{\"source\":\"", "garbage", IntentLine('c', 'd') })
        {
            var read = PickRecord.Parse(whole + torn).Should().BeOfType<PickRecordRead.Lines>().Subject;
            read.TornTail.Should().BeTrue($"'{torn}' has no final newline");
            read.Valid.Should().Equal(new PickEntry.Intent(Sha('a'), Sha('b')));
        }

        var terminatedGarbage = PickRecord.Parse(whole + "garbage\n").Should().BeOfType<PickRecordRead.Lines>().Subject;
        terminatedGarbage.TornTail.Should().BeTrue("the last segment is not one record");
        terminatedGarbage.Valid.Should().HaveCount(1);
    }

    [Fact]
    public void A_bad_line_before_the_last_is_unreadable_and_names_its_number()
    {
        PickRecord.Parse("{\"source\":\"x\"}\n" + IntentLine('a', 'b') + "\n").Should().BeOfType<PickRecordRead.Unreadable>().Which.LineNumber.Should().Be(1);
        PickRecord.Parse(IntentLine('a', 'b') + "\n" + PickedLine('a', 'c') + "\nnot json\n" + IntentLine('e', 'f') + "\n").Should().BeOfType<PickRecordRead.Unreadable>().Which.LineNumber.Should().Be(3);
    }

    [Theory]
    [InlineData("{\"source\":\"AAAA\",\"headBefore\":\"BBBB\"}")]
    [InlineData("{\"source\":\"zz\",\"headBefore\":\"zz\"}")]
    [InlineData("[]")]
    [InlineData("{\"source\":1,\"headBefore\":2}")]
    public void A_line_that_is_not_one_of_the_two_shapes_is_unreadable_before_the_last(string bad)
    {
        PickRecord.Parse(bad + "\n" + IntentLine('a', 'b') + "\n").Should().BeOfType<PickRecordRead.Unreadable>();
    }

    [Fact]
    public void A_fourth_member_a_mixed_member_set_or_a_non_hex_sha_is_unreadable()
    {
        var fourth = $"{{\"source\":\"{Sha('a')}\",\"picked\":\"{Sha('b')}\",\"patchId\":\"\",\"extra\":\"x\"}}";
        var mixed = $"{{\"source\":\"{Sha('a')}\",\"headBefore\":\"{Sha('b')}\",\"patchId\":\"\"}}";
        var nonHex = $"{{\"source\":\"{new string('g', 40)}\",\"headBefore\":\"{Sha('b')}\"}}";
        var duplicate = $"{{\"source\":\"{Sha('a')}\",\"source\":\"{Sha('a')}\"}}";
        var tail = IntentLine('c', 'd') + "\n";

        foreach (var bad in new[] { fourth, mixed, nonHex, duplicate })
        {
            PickRecord.Parse(bad + "\n" + tail).Should().BeOfType<PickRecordRead.Unreadable>(bad);
        }
    }

    [Fact]
    public void An_intent_before_the_last_entry_with_no_completion_for_its_source_is_unreadable()
    {
        var text = IntentLine('a', 'b') + "\n" + IntentLine('c', 'b') + "\n";

        PickRecord.Parse(text).Should().Be(new PickRecordRead.Unreadable(1, "an intent to pick that was never completed, and a later line follows it"));
    }

    [Fact]
    public void An_intent_that_a_later_line_for_its_source_settles_is_history()
    {
        var retried = IntentLine('a', 'b') + "\n" + IntentLine('a', 'b') + "\n" + PickedLine('a', 'c') + "\n";
        var stopped = IntentLine('a', 'b') + "\n" + IntentLine('a', 'b') + "\n";

        PickRecord.Parse(retried).Should().BeOfType<PickRecordRead.Lines>().Which.Valid.Should().HaveCount(3);
        PickRecord.Parse(stopped).Should().BeOfType<PickRecordRead.Lines>("a pick that stopped twice leaves two intents: the pipeline's own retry is not an unreadable record");
    }

    [Fact]
    public void Open_is_the_last_entry_when_it_is_an_intent()
    {
        var intent = new PickEntry.Intent(Sha('a'), Sha('b'));
        var picked = new PickEntry.Picked(Sha('a'), Sha('c'), string.Empty);

        PickRecord.Open([]).Should().BeNull();
        PickRecord.Open([intent, picked]).Should().BeNull();
        PickRecord.Open([picked, intent]).Should().Be(intent);
    }

    private static PickSource S(char sha, string patch = "") => new(Sha(sha), patch);

    [Fact]
    public void A_source_is_carried_by_the_record_only_when_its_completion_is_believed()
    {
        var record = new PickEntry[] { new PickEntry.Picked(Sha('a'), Sha('1'), "p1"), new PickEntry.Picked(Sha('b'), Sha('2'), "p2") };

        var plan = PickBackOrder.Decide([S('a', "p1"), S('b', "p2"), S('c', "p3")], record, [S('1', "p1")]);

        plan.Steps.Should().Equal(new PickStep.Carried(Sha('a'), Sha('1'), "the record"), new PickStep.Pick(Sha('b'), "p2"), new PickStep.Pick(Sha('c'), "p3"));
        plan.NotBelieved.Should().Equal(new PickEntry.Picked(Sha('b'), Sha('2'), "p2"));
    }

    [Fact]
    public void A_source_with_no_line_at_all_and_a_matching_patch_id_is_carried_by_patch_id()
    {
        var plan = PickBackOrder.Decide([S('a', "same"), S('b', "other")], [], [S('9', "same")]);

        plan.Steps.Should().Equal(new PickStep.Carried(Sha('a'), Sha('9'), "patch-id of " + Sha('9')), new PickStep.Pick(Sha('b'), "other"));
    }

    [Fact]
    public void A_source_with_an_unbelieved_completion_and_a_matching_patch_id_is_picked_not_skipped()
    {
        var record = new PickEntry[] { new PickEntry.Picked(Sha('a'), Sha('1'), "same") };

        var plan = PickBackOrder.Decide([S('a', "same")], record, [S('9', "same")]);

        plan.Steps.Should().Equal(new PickStep.Pick(Sha('a'), "same"));
        plan.NotBelieved.Should().HaveCount(1);
    }

    [Fact]
    public void A_source_with_an_intent_only_is_picked_and_an_empty_patch_id_never_matches()
    {
        var record = new PickEntry[] { new PickEntry.Intent(Sha('a'), Sha('0')) };

        var plan = PickBackOrder.Decide([S('a', "same"), S('b')], record, [S('9', "same"), S('8')]);

        plan.Steps.Should().Equal(new PickStep.Pick(Sha('a'), "same"), new PickStep.Pick(Sha('b'), string.Empty));
    }

    [Fact]
    public void The_order_of_the_sources_is_kept()
    {
        var plan = PickBackOrder.Decide([S('c'), S('a'), S('b')], [], []);

        plan.Steps.Select(step => ((PickStep.Pick)step).Source).Should().Equal(Sha('c'), Sha('a'), Sha('b'));
    }
}
