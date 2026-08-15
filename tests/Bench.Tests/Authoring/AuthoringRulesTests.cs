using Bench.Domain.Authoring;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>The authoring rules: what a source may propose, what a reviewer must do, and the two things
/// that must never happen silently — a duplicate entering a suite, and a question measuring memory.</summary>
public sealed class AuthoringRulesTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('e', 40)).Ok();
    private static readonly DateTimeOffset Cutoff = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_machine_authored_candidate_must_record_which_model_wrote_it()
    {
        var refused = QuestionCandidate.Propose(
            AuthoringSource.RepositoryHistory, "  ", Seed("pr-1", Cutoff.AddMonths(2)), Question("q1", "src/A.cs"));

        refused.Reason().Should().Contain("ceiling becomes its author's ceiling");
    }

    [Fact]
    public void A_human_candidate_needs_no_author_model()
    {
        QuestionCandidate.Propose(
                AuthoringSource.Human, "", QuestionSeed.Person("olek", Cutoff), Question("q1", "src/A.cs"))
            .Failed().Should().BeFalse();
    }

    [Fact]
    public void A_candidate_with_nothing_to_find_in_the_code_is_refused()
    {
        var noAnchor = new Question("q1", "is this nice?", [], string.Empty);

        QuestionCandidate.Propose(AuthoringSource.Synthetic, "opus", Seed("m", Cutoff), noAnchor)
            .Reason().Should().Contain("nothing to score against the code");
    }

    [Fact]
    public void A_rejection_without_a_reason_is_refused_because_the_reason_is_the_evidence()
    {
        var candidate = Proposed("q1", "src/A.cs");

        candidate.Reject("  ").Reason().Should().Contain("only record of what a source gets wrong");
        candidate.Reject("the answer is in the prompt").Ok().State.Should().Be(CandidateState.Rejected);
    }

    [Fact]
    public void Two_sources_pointing_at_the_same_lines_collide_and_are_flagged_rather_than_dropped()
    {
        var fromHistory = Accepted("from-pr", "src/Orders.cs");
        var fromSynthetic = Accepted("from-index", "src/Orders.cs");

        var collisions = Dedup.Find([fromHistory, fromSynthetic]);

        collisions.Should().ContainSingle();
        collisions[0].CandidateIds.Should().Equal("from-index", "from-pr");
        collisions[0].Describe.Should().Contain("2 candidates share");
    }

    [Fact]
    public void The_collision_key_is_the_anchors_so_a_reworded_duplicate_is_still_caught()
    {
        var one = Accepted("a", "src/Orders.cs");
        var reworded = Accepted("b", "src/Orders.cs");

        Dedup.CollisionKey(reworded).Should().Be(
            Dedup.CollisionKey(one), "rewording defeats a text key immediately; two questions on the same lines is the case worth seeing");
    }

    [Fact]
    public void A_rejected_candidate_no_longer_collides_with_anything()
    {
        var kept = Accepted("a", "src/Orders.cs");
        var dropped = Proposed("b", "src/Orders.cs").Reject("duplicate of a").Ok();

        Dedup.Find([kept, dropped]).Should().BeEmpty();
    }

    [Fact]
    public void A_suite_refuses_to_form_while_a_collision_is_unresolved()
    {
        var promoted = AuthoringBatch.Promote("s", [Accepted("a", "src/Orders.cs"), Accepted("b", "src/Orders.cs")]);

        promoted.Reason().Should().Contain("unresolved collision")
            .And.Contain("src/Orders.cs", "the reviewer needs to know WHICH lines, not just that something clashed");
    }

    [Fact]
    public void Only_accepted_candidates_reach_a_suite_and_it_comes_out_frozen()
    {
        var suite = AuthoringBatch.Promote("s",
        [
            Accepted("a", "src/A.cs"),
            Proposed("b", "src/B.cs"),
            Proposed("c", "src/C.cs").Reject("wrong file").Ok(),
        ]).Ok();

        suite.IsFrozen.Should().BeTrue();
        suite.Version.Should().Be(1);
        suite.Questions.Select(q => q.Id).Should().ContainSingle()
            .Which.Should().Be("a", "a suite contains nothing nobody vouched for");
    }

    [Fact]
    public void A_batch_nobody_reviewed_produces_no_suite()
    {
        AuthoringBatch.Promote("s", [Proposed("a", "src/A.cs")])
            .Reason().Should().Contain("nothing nobody vouched for");
    }

    [Fact]
    public void The_batch_records_which_source_and_which_model_contributed_what()
    {
        var provenance = AuthoringBatch.Provenance(
        [
            Accepted("a", "src/A.cs"),
            Accepted("b", "src/B.cs"),
            Accepted("c", "src/C.cs", AuthoringSource.Synthetic, "qwen"),
        ]);

        provenance.Should().Contain("RepositoryHistory via opus: 2").And.Contain("Synthetic via qwen: 1");
    }

    [Theory]
    [InlineData(2, MemorisationRisk.Clear)]
    [InlineData(-2, MemorisationRisk.MayRecall)]
    public void Memorisation_risk_is_judged_per_subject_not_per_question(int monthsFromCutoff, MemorisationRisk expected)
    {
        var seed = Seed("pr-1", Cutoff.AddMonths(monthsFromCutoff));
        var cutoffs = new Dictionary<string, DateTimeOffset> { ["opus"] = Cutoff };

        Memorisation.For(seed, cutoffs, "opus").Should().Be(expected);
    }

    [Fact]
    public void A_subject_with_no_declared_cutoff_is_unknown_rather_than_clear()
    {
        Memorisation.For(Seed("pr-1", Cutoff.AddYears(1)), new Dictionary<string, DateTimeOffset>(), "mystery")
            .Should().Be(MemorisationRisk.Unknown, "nothing can be said, and saying 'clear' would be saying something");
    }

    [Fact]
    public void The_same_seed_is_safe_for_one_subject_and_risky_for_another()
    {
        var seed = Seed("pr-1", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var cutoffs = new Dictionary<string, DateTimeOffset>
        {
            ["old-model"] = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ["new-model"] = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
        };

        Memorisation.AtRisk(seed, cutoffs, ["old-model", "new-model", "mystery"])
            .Should().Equal("mystery", "new-model");
    }

    private static QuestionSeed Seed(string reference, DateTimeOffset at) => QuestionSeed.PullRequest(reference, at);

    private static Question Question(string id, string file) =>
        new(id, $"prompt {id}", [Expectation.File(SourceAnchor.File(file, Commit))], string.Empty);

    private static QuestionCandidate Proposed(string id, string file) =>
        QuestionCandidate.Propose(
            AuthoringSource.RepositoryHistory, "opus", Seed($"pr-{id}", Cutoff.AddMonths(2)), Question(id, file)).Ok();

    private static QuestionCandidate Accepted(
        string id, string file, AuthoringSource source = AuthoringSource.RepositoryHistory, string model = "opus") =>
        QuestionCandidate.Propose(source, model, Seed($"pr-{id}", Cutoff.AddMonths(2)), Question(id, file))
            .Ok().Accept("reviewed").Ok();
}
