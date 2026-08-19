using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Suites;

/// <summary>Freezing is what makes a version a version. The failure being prevented is quiet: upstream,
/// a measured question set existed only in a database while the file that claimed to be it had drifted
/// several versions behind — nothing broke, the numbers simply stopped meaning what their label said.</summary>
public sealed class SuiteFreezeTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    [Fact]
    public void A_frozen_suite_refuses_to_be_edited_in_place()
    {
        var frozen = Draft("s", "q1").Freeze().Ok();

        var edited = frozen.With(Question.Ask("q2", "another"));

        edited.Failed().Should().BeTrue();
        edited.Reason().Should().Contain("frozen").And.Contain("Amend");
    }

    [Fact]
    public void Amending_a_frozen_suite_produces_the_next_version_and_leaves_the_first_intact()
    {
        var v1 = Draft("s", "q1").Freeze().Ok();

        var v2 = v1.Amend().Ok().With(Question.Ask("q2", "another")).Ok().Freeze().Ok();

        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);
        v1.Questions.Should().HaveCount(1, "the frozen version every earlier result names still resolves");
        v2.Questions.Should().HaveCount(2);
        v2.Hash.Should().NotBe(v1.Hash);
    }

    [Fact]
    public void The_hash_is_stable_across_instances_and_question_order()
    {
        var a = Draft("s", "q1", "q2").Freeze().Ok();
        var b = Draft("s", "q2", "q1").Freeze().Ok();

        b.Hash.Should().Be(a.Hash, "the hash identifies content, and authoring order is not content");
    }

    [Fact]
    public void Changing_one_expectation_changes_the_hash()
    {
        var before = Draft("s", "q1").Freeze().Ok();

        var after = Suite.Draft("s")
            .With(new Question("q1", "prompt q1", [Expectation.File(SourceAnchor.File("src/Other.cs", Commit))], string.Empty)).Ok()
            .Freeze().Ok();

        after.Hash.Should().NotBe(before.Hash, "a version whose content can change without changing its stamp is not a version");
    }

    [Fact]
    public void An_empty_suite_is_not_worth_a_version_number()
    {
        Suite.Draft("s").Freeze().Reason().Should().Contain("no questions");
    }

    [Fact]
    public void A_draft_has_no_identity_to_quote()
    {
        Suite.Draft("s").Stamp.Should().Be("s@draft");
        Draft("s", "q1").Freeze().Ok().Stamp.Should().StartWith("s@v1#");
    }

    [Fact]
    public void The_id_read_back_out_of_a_stamp_is_the_same_across_every_version_of_one_suite()
    {
        var v1 = Draft("polly-smoke", "q1").Freeze().Ok();
        var v2 = v1.Amend().Ok().With(Question.Ask("q2", "another")).Ok().Freeze().Ok();

        v1.Stamp.Should().NotBe(v2.Stamp, "two versions are two stamps, hash included");
        Suite.IdOf(v1.Stamp).Should().Be("polly-smoke");
        Suite.IdOf(v2.Stamp).Should().Be(Suite.IdOf(v1.Stamp),
            "the SPLIT is derived from this — if freezing a version changed it, every question would be "
            + "reassigned to the other half and a comparison spanning versions would silently compare two splits");
        Suite.IdOf(Suite.Draft("polly-smoke").Stamp).Should().Be("polly-smoke", "a draft's stamp still names its suite");
    }

    [Fact]
    public void A_value_carrying_no_version_is_already_an_id_rather_than_a_refusal()
    {
        Suite.IdOf("polly-smoke").Should().Be("polly-smoke");
        Suite.IdOf(string.Empty).Should().BeEmpty("an empty stamp belongs to no suite anyone can look up");
    }

    private static Suite Draft(string id, params string[] questionIds) =>
        questionIds.Aggregate(
            Suite.Draft(id),
            (suite, qid) => suite.With(
                new Question(qid, $"prompt {qid}", [Expectation.File(SourceAnchor.File($"src/{qid}.cs", Commit))], string.Empty)).Ok());
}
