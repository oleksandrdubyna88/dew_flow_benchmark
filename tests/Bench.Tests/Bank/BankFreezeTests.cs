using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Bank;

/// <summary>Freezing a bank selection into the suite a test measures.
/// <para>
/// The whole point of this step is that there is exactly ONE way to mint a suite stamp. A test created
/// from the bank and one created from a file both go through <see cref="Suite.Freeze"/>, so a result
/// cannot tell which door its questions came through — and the refusals a selection inherits (an empty
/// batch, two questions about the same lines) are the ones the authoring domain already wrote and tested,
/// not a second set written here.
/// </para></summary>
public sealed class BankFreezeTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    [Fact]
    public void The_same_selection_freezes_to_the_same_stamp_and_a_different_one_does_not()
    {
        var first = BankFreeze.Freeze("bank", [Entry("q1"), Entry("q2", member: "A.Bar")]).Ok();
        var again = BankFreeze.Freeze("bank", [Entry("q1"), Entry("q2", member: "A.Bar")]).Ok();
        var other = BankFreeze.Freeze("bank", [Entry("q1")]).Ok();

        // The stamp is the identity a result quotes. If it were not stable, two runs of one selection would
        // be uncomparable; if it did not change with the selection, two different sets would be reported as
        // one — the exact failure the suite hash exists to prevent.
        again.Stamp.Should().Be(first.Stamp);
        other.Stamp.Should().NotBe(first.Stamp);
        first.Suite.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void Only_questions_somebody_vouched_for_enter_a_test()
    {
        var mixed = BankFreeze.Freeze(
            "bank",
            [Entry("accepted"), Entry("proposed", state: CandidateState.Proposed, member: "A.Bar")]).Ok();

        mixed.Suite.Questions.Should().ContainSingle().Which.Id.Should().Be("accepted");
        mixed.Questions.Should().ContainSingle("the snapshot records what the test MEASURES, not what was looked at");
    }

    [Fact]
    public void A_selection_with_nothing_accepted_is_refused_rather_than_frozen_empty()
    {
        var empty = BankFreeze.Freeze("bank", [Entry("q1", state: CandidateState.Proposed)]);

        empty.Failed().Should().BeTrue();
        empty.Reason().Should().Contain(
            "nothing nobody vouched for", "the refusal is the authoring domain's own, not a second rule written here");
    }

    [Fact]
    public void Two_questions_about_the_same_lines_refuse_the_selection_by_name()
    {
        // Two sources routinely produce a question about the same member, and two genuinely different
        // questions about one member are also normal — so this is a reviewer's decision, and the freeze
        // refuses until somebody makes it. A suite that quietly held both would double-count that member in
        // every score it ever produced, undetectably.
        var collided = BankFreeze.Freeze("bank", [Entry("q1"), Entry("q2")]);

        collided.Failed().Should().BeTrue();
        collided.Reason().Should().Contain("collision").And.Contain("q1").And.Contain("q2");
    }

    [Fact]
    public void One_question_id_twice_is_refused_before_the_suite_is_built()
    {
        var duplicated = BankFreeze.Freeze("bank", [Entry("q1"), Entry("q1", member: "A.Bar")]);

        duplicated.Failed().Should().BeTrue();
        duplicated.Reason().Should().Contain("appears twice",
            "the id is what every cell and every result carries — one suite cannot hold it twice");
    }

    [Fact]
    public void The_snapshot_records_the_group_each_question_was_in_when_the_test_was_created()
    {
        var frozen = BankFreeze.Freeze(
            "bank",
            [
                Entry("q2", group: "semantic-intent", ordinal: 7, member: "A.Bar"),
                Entry("q1", group: "code-lookup", ordinal: 3),
            ]).Ok();

        // Group membership is versioned in the bank; this is what stops a re-filing next month from moving
        // a finished report's numbers into a different column.
        frozen.Questions.Select(q => (q.QuestionId, q.Group.Value, q.Ordinal))
            .Should().Equal([("q1", "code-lookup", 3), ("q2", "semantic-intent", 7)]);
    }

    [Fact]
    public void A_bank_question_is_admitted_under_the_authoring_domains_own_rule()
    {
        var noAnchor = BankQuestion.Create(
            Guid.CreateVersion7(),
            1,
            TaskKind.Reading,
            new Question("q", "where is X?", [new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File("", Commit), "X", true)], ""),
            string.Empty,
            AuthoringSource.RepositoryHistory,
            "opus",
            QuestionSeed.PullRequest("#1", Noon),
            RepoUrl.Parse("https://example.invalid/x.git").Ok(),
            Commit,
            Noon);

        // QuestionCandidate.Propose already says this, and says it in the one place authoring reads. A
        // second admission rule here would drift, and the one that drifted would be the unread one.
        noAnchor.Failed().Should().BeTrue();
        noAnchor.Reason().Should().Contain("no retrieval expectation");
    }

    [Fact]
    public void A_question_authored_by_a_model_that_is_not_named_is_refused()
    {
        var anonymous = Create("q", authorModel: string.Empty);

        anonymous.Failed().Should().BeTrue();
        anonymous.Reason().Should().Contain("ceiling", "a set's ceiling becomes its author's ceiling, so the author is recorded");
    }

    private static BankEntry Entry(
        string id,
        string group = "code-lookup",
        int ordinal = 1,
        CandidateState state = CandidateState.Accepted,
        string member = "A.Foo") =>
        new(Create(id, member: member, ordinal: ordinal).Ok().As(state),
            QuestionGroup.Create(group, group, ordinal).Ok());

    private static Outcome<BankQuestion> Create(
        string id, string member = "A.Foo", int ordinal = 1, string authorModel = "opus") =>
        BankQuestion.Create(
            Guid.CreateVersion7(),
            ordinal,
            TaskKind.Reading,
            new Question(
                id,
                $"where is {member}?",
                [Expectation.Member(SourceAnchor.Member("src/A.cs", member, new LineSpan(10, 20), Commit))],
                "in A"),
            string.Empty,
            AuthoringSource.RepositoryHistory,
            authorModel,
            QuestionSeed.PullRequest("#1", Noon),
            RepoUrl.Parse("https://example.invalid/x.git").Ok(),
            Commit,
            Noon);
}
