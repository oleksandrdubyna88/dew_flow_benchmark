using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.Assessment.AssessmentFixtures;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>S4.7 — the family match, and S4.4's pending set and batches.</summary>
public sealed class VendorFamilyAndPendingTests
{
    [Fact]
    public void A_codex_assessor_on_a_gpt_reviewer_is_its_own_family_and_on_a_grok_reviewer_is_not()
    {
        var assessor = Def("gpt-6-astra", ReviewerRuntime.Codex);

        VendorFamily.Matches(assessor, Def("gpt-5.6-terra", ReviewerRuntime.Api)).Should().BeTrue();
        VendorFamily.Matches(assessor, Def("grok-4.7", ReviewerRuntime.Api)).Should().BeFalse();
    }

    [Theory]
    [InlineData("OpenAI/gpt-4o", "openai")]
    [InlineData("azure/gpt-4o", "openai")]
    [InlineData("anthropic/claude-3.5-sonnet", "anthropic")]
    [InlineData("Qwen3.8-Max", "alibaba")]
    [InlineData("deepseek-v4", "deepseek")]
    public void A_route_prefix_and_the_case_do_not_hide_the_family(string model, string family) =>
        VendorFamily.Of(Def(model, ReviewerRuntime.Api)).Should().Be(family);

    [Fact]
    public void A_model_nobody_listed_is_its_own_family_unless_its_cli_says_whose_it_is()
    {
        VendorFamily.Of(Def("mystery-9", ReviewerRuntime.Api)).Should().Be("model:mystery-9");
        VendorFamily.Of(Def("mystery-9", ReviewerRuntime.Claude)).Should().Be("anthropic");
    }

    [Fact]
    public void A_finding_is_pending_until_this_assessor_reads_it_under_this_rubric_and_a_failure_does_not_count_as_read()
    {
        var run = Guid.CreateVersion7();
        var entries = Enumerable.Range(0, 4).Select(i => new BlindKeyEntry(Id($"0000000{i}"), Guid.Empty, run, i, Cs2, Grok)).ToList();
        var other = GateReviewerId.Parse("claude-opus").Ok();

        var verdicts = new[]
        {
            VerdictOf(run, 0, Codex, Strict(StrictReading.Supported)),
            VerdictOf(run, 1, Codex, new Verdict.AssessmentFailure(AssessmentFailureCause.Unparseable)),
            VerdictOf(run, 2, other, Strict(StrictReading.Refuted)),
        };

        AssessmentPending.Of(entries, verdicts, Codex, StrictRubric).Select(e => e.Ordinal).Should().Equal(
            [1, 2, 3], "0 is read; 1 only FAILED, so a later pass asks it again; 2 was read by another assessor, which is not this one's reading");
    }

    [Fact]
    public void Batches_never_span_two_tasks_and_hold_at_most_the_batch_size()
    {
        var run = Guid.CreateVersion7();
        var pending = Enumerable.Range(0, 30).Select(i => new BlindKeyEntry(Id($"{i:x8}"), Guid.Empty, run, i, i < 26 ? Cs2 : Rs3, Grok)).ToList();

        var batches = AssessmentPending.Batches(pending, BatchSize.Default);

        batches.Select(b => b.Count).Should().Equal([24, 2, 4]);
        batches.Should().OnlyContain(b => b.Select(e => e.Task).Distinct().Count() == 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void A_batch_size_outside_one_to_twenty_four_is_refused_at_the_flag(int size) =>
        BatchSize.Of(size).Reason().Should().Contain("1 to 24");

    private static ReviewerDefinition Def(string model, ReviewerRuntime runtime) =>
        GateReviewerTests.Definition(model: model, runtime: runtime, endpoint: runtime == ReviewerRuntime.Api ? "https://api.example.com/v1" : string.Empty).Ok();

    private static GateVerdict VerdictOf(Guid run, int ordinal, GateReviewerId assessor, Verdict reading) =>
        GateVerdict.Under(Catalog, StrictHash, run, ordinal, reading, assessor, "b1", StableHash.Of("p"), false).Ok();
}
