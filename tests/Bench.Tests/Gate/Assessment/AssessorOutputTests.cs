using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.Assessment.AssessmentFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>S4.4 / S4.5 — an assessor's answer read against the batch it was given. Every way an answer can fail is a
/// named cause (never a gap); a row naming a seed of ANOTHER task is refused, never counted as a hit.</summary>
public sealed class AssessorOutputTests
{
    private static readonly IReadOnlyList<AssessmentRow> Batch = [Row("0000000a"), Row("0000000b")];
    private static readonly IReadOnlyList<Bench.Domain.Gate.SeedId> Cs2Seeds = [Seed("cs2-S1"), Seed("cs2-S2")];

    [Fact]
    public void A_full_answer_is_read_row_for_row_with_its_seed_hit()
    {
        var reading = Read(Answer(AnswerRow("0000000a", seed: "cs2-S1"), AnswerRow("0000000b", verdict: "partial")));

        var answered = reading.Should().BeOfType<BatchReading.Answered>().Subject;
        answered.Missing.Should().BeEmpty();
        answered.Rows.Should().HaveCount(2);
        answered.Rows[0].SeedHit.Should().Be(new SeedHit.Of(Seed("cs2-S1")));
        answered.Rows[1].Reading.Should().Be(StrictReading.Partial);
        answered.Rows[0].Note.Should().Contain("Export0.cs:10", "the note is kept whole for the artefact store");
    }

    [Fact]
    public void A_seed_of_another_task_is_refused_and_the_finding_is_asked_again_never_counted_as_a_hit()
    {
        var reading = (BatchReading.Answered)Read(Answer(AnswerRow("0000000a", seed: "rs3-S1"), AnswerRow("0000000b")));

        reading.Rows.Select(r => r.Id.Value).Should().Equal(["0000000b"]);
        reading.Missing.Select(m => m.Value).Should().Equal(["0000000a"]);
        reading.Refusals.Should().ContainSingle().Which.Should().Contain("rs3-S1").And.Contain("not a seed of task cs2");
    }

    [Fact]
    public void A_row_that_names_another_task_is_refused_the_same_way()
    {
        var reading = (BatchReading.Answered)Read(Answer(AnswerRow("0000000a", task: "rs3"), AnswerRow("0000000b")));

        reading.Missing.Select(m => m.Value).Should().Equal(["0000000a"]);
    }

    [Fact]
    public void An_id_missing_from_the_answer_is_named_missing_and_the_present_rows_are_kept()
    {
        var reading = (BatchReading.Answered)Read(Answer(AnswerRow("0000000b")));

        reading.Rows.Select(r => r.Id.Value).Should().Equal(["0000000b"]);
        reading.Missing.Select(m => m.Value).Should().Equal(["0000000a"]);
        reading.Refusals.Should().BeEmpty("an absent row is missing, not refused");
    }

    [Fact]
    public void An_answer_naming_an_id_the_batch_did_not_carry_fails_the_whole_batch_as_unknown_ids() =>
        Cause(Read(Answer(AnswerRow("0000000a"), AnswerRow("0000000b"), AnswerRow("0000000c")))).Should().Be(AssessmentFailureCause.UnknownIds);

    [Fact]
    public void A_json_document_cut_short_is_truncated_not_unparseable() =>
        Cause(Read(Answer(AnswerRow("0000000a"), AnswerRow("0000000b"))[..120])).Should().Be(AssessmentFailureCause.Truncated);

    [Theory]
    [InlineData("I could not read the repository, sorry.")]
    [InlineData("""{"verdicts": []}""")]
    [InlineData("""{"rows": [{"id": "0000000a"}]}""")]
    public void An_answer_that_is_prose_or_not_the_schema_is_unparseable(string text) =>
        Cause(Read(text)).Should().Be(AssessmentFailureCause.Unparseable);

    [Fact]
    public void A_verdict_word_outside_the_rubric_is_unparseable_rather_than_guessed() =>
        Cause(Read(Answer(AnswerRow("0000000a", verdict: "plausible"), AnswerRow("0000000b")))).Should().Be(AssessmentFailureCause.Unparseable);

    [Fact]
    public void An_id_answered_twice_is_unparseable() =>
        Cause(Read(Answer(AnswerRow("0000000a"), AnswerRow("0000000a"), AnswerRow("0000000b")))).Should().Be(AssessmentFailureCause.Unparseable);

    [Fact]
    public void Nothing_at_all_is_no_answer() => Cause(Read("   ")).Should().Be(AssessmentFailureCause.NoAnswer);

    [Fact]
    public void The_cluster_reaches_the_database_only_as_a_keyed_hash()
    {
        var row = ((BatchReading.Answered)Read(Answer(AnswerRow("0000000a"), AnswerRow("0000000b")))).Rows[0];
        var verdict = row.ToVerdict(Infrastructure.GateStoreFixtures.Key);

        verdict.ClusterHash.Should().MatchRegex("^[0-9a-f]{64}$").And.NotBe(Bench.Domain.StableHash.Of(row.Cluster),
            "a plain SHA-256 of a short kebab key is confirmed by hashing guesses; the key lives only in the artefact root");
    }

    private static BatchReading Read(string text) => AssessorOutput.Read(text, Batch, Cs2Seeds);

    private static AssessmentFailureCause Cause(BatchReading reading) => reading.Should().BeOfType<BatchReading.Failed>().Subject.Cause;
}
