using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The guards that each stand for a specific, expensive thing that went wrong upstream.</summary>
public sealed class ContractGuardTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unset_model_id_is_refused_and_never_defaulted(string id)
    {
        var parsed = ModelRef.Parse(id, ModelHosting.Local);

        parsed.Failed().Should().BeTrue();
        parsed.Reason().Should().Contain("refusal").And.Contain("never a fallback");
    }

    [Fact]
    public void Sampling_that_was_not_captured_is_distinguishable_from_a_captured_zero()
    {
        var requested = Sampling.Deterministic(7);

        var notCaptured = SamplingAsSent.NotCaptured("the runtime does not echo its request");
        var captured = SamplingAsSent.From(requested, "request-body");

        notCaptured.Captured.Should().BeFalse();
        notCaptured.Matches(requested).Should().BeFalse("an unknown must never pass as a match");
        captured.Matches(requested).Should().BeTrue();
    }

    [Fact]
    public void An_unverified_budget_says_so()
    {
        var budget = Budget.Of(BudgetKind.Context, BudgetScope.Question, 32_000);

        budget.IsVerified.Should().BeFalse();
        budget.Describe.Should().Contain("UNVERIFIED");
        budget.ConfirmedBy("claude-cli").Describe.Should().Contain("accepted by claude-cli");
    }

    [Fact]
    public void A_cap_hit_is_not_a_crash_and_neither_enters_a_paired_delta()
    {
        LegOutcome completed = new LegOutcome.Completed();
        LegOutcome capped = new LegOutcome.CapExceeded(BudgetKind.CostUsd, BudgetScope.Question, 1.25m, 1.31m);
        LegOutcome crashed = new LegOutcome.Crashed("socket reset");

        completed.CountsInPairedDelta.Should().BeTrue();
        capped.CountsInPairedDelta.Should().BeFalse("pairing a capped leg measures the ceiling, not the configuration");
        crashed.CountsInPairedDelta.Should().BeFalse();
        capped.Describe.Should().Contain("cap CostUsd/Question");
    }

    [Fact]
    public void Rows_from_different_targets_are_not_comparable()
    {
        var a = Tuple(new string('a', 40));
        var b = Tuple(new string('b', 40));

        MeasurementTuple.Comparable(a, b).Should().BeFalse();
        MeasurementTuple.ScopeOf([a, b]).Reason().Should().Contain("not comparable");
        MeasurementTuple.ScopeOf([a, a]).Ok().Should().Be(a.Scope);
    }

    [Fact]
    public void Exclusions_are_part_of_the_targets_identity()
    {
        var repo = RepoUrl.Parse("https://example.invalid/x.git").Ok();
        var bare = MeasurementTarget.At(repo, Commit);

        bare.Canonical.Should().NotBe(
            bare.Excluding("research/**").Canonical,
            "the same commit read with different blind spots is not the same measurement");
    }

    [Fact]
    public void An_abbreviated_commit_is_refused()
    {
        CommitSha.Parse("abc1234").Reason().Should().Contain("abbreviations are ambiguous");
    }

    [Fact]
    public void An_engine_with_no_index_fingerprint_reports_unknown_rather_than_current()
    {
        EngineRef.Filesystem().IndexFingerprint.Should().BeEmpty();
        EngineRef.Filesystem().MayBeWhiteBox.Should().BeFalse();
        new EngineRef(EngineKind.Qln, "http://localhost", "1.0", "abc").MayBeWhiteBox.Should().BeTrue();
    }

    private static MeasurementTuple Tuple(string sha) => new(
        MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), CommitSha.Parse(sha).Ok()),
        EngineRef.Filesystem(),
        "s@v1#abc",
        new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1)),
        Lane.Named("default"),
        0);
}
