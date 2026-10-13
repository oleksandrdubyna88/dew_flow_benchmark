using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>One cell's stream read into its settlement (§3.4–§3.6, §3.10), pure: stops outrank everything (the path audit, a moved alias),
/// then aborted:spend, then the cost check, then the read-back's voids; only a scored cell carries recall; every scout gets its record.</summary>
public sealed class ScoutCellReadingTests
{
    private const string Tree = "C:/work/tree";

    private static readonly ScoutTask Task = ScoutTask.Frozen(
        ScoutTaskId.Parse("t01").Ok(), "example-org/example-repo", Bench.Domain.Targets.CommitSha.Parse(new string('a', 40)).Ok(),
        Bench.Domain.Targets.CommitSha.Parse(new string('b', 40)).Ok(), ScoutQuestionKind.HowEndToEnd, "How does a cell reach its settlement after a crash?",
        ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"], [], [], new string('c', 64)).Ok();

    private static readonly IReadOnlyList<string> Files = ["README.md", "src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs", "src/Other.cs"];

    [Fact]
    public void A_clean_cell_is_scored_with_its_recall_its_cost_agreeing_and_one_record_per_scout()
    {
        var verdict = Read(ScoutArms.Of(ScoutArmId.B2), CellVariant.Clean);

        verdict.Settlement.Outcome.Should().Be(ScoutCellOutcome.Scored, string.Join(",", verdict.ReadBack.Voids.Concat(verdict.ReadBack.Stops)));
        verdict.Settlement.Facts.RecallFound.Should().Be(CapturedCount.Number(3), "the three scouts named the three files of G between them");
        verdict.Settlement.Facts.RecallTotal.Should().Be(CapturedCount.Number(3));
        verdict.Cost.Should().BeOfType<CostCheck.Agrees>();
        verdict.ReportedUsd.WasCaptured.Should().BeTrue();
        verdict.Scouts.Select(s => (s.Ordinal, s.Focus, s.InputModel, s.ResolvedModel)).Should().Equal(
            (1, ScoutFocus.SimilarFeatures, "sonnet", ScoutStreams.Sonnet), (2, ScoutFocus.Architecture, "sonnet", ScoutStreams.Sonnet), (3, ScoutFocus.CurrentImplementation, "sonnet", ScoutStreams.Sonnet));
        verdict.Scouts.Should().OnlyContain(s => s.KeyFiles.Contains("README.md") && s.ClaimsParsed == 2 && !s.Silent);
    }

    [Theory]
    [InlineData(CellVariant.Void, ScoutCellOutcome.Void, ScoutReason.SubagentsNotCompleted)]
    [InlineData(CellVariant.BudgetExhausted, ScoutCellOutcome.Aborted, ScoutReason.SpendLimit)]
    [InlineData(CellVariant.AliasMoved, ScoutCellOutcome.Stopped, ScoutReason.PinMoved)]
    [InlineData(CellVariant.CostOff, ScoutCellOutcome.Stopped, ScoutReason.CostMismatch)]
    [InlineData(CellVariant.OutsideRead, ScoutCellOutcome.Stopped, ScoutReason.OutsideReadReturnedContent)]
    public void Each_deviation_settles_as_its_own_outcome_and_reason(CellVariant variant, ScoutCellOutcome outcome, ScoutReason reason)
    {
        var verdict = Read(ScoutArms.Of(ScoutArmId.B1), variant);

        verdict.Settlement.Outcome.Should().Be(outcome);
        verdict.Settlement.Reason.Should().Be(reason);
        verdict.Settlement.Facts.RecallFound.WasCaptured.Should().BeFalse("only a scored cell carries recall");
        verdict.ReportedUsd.WasCaptured.Should().BeTrue("whatever the outcome, the money the CLI reported is what the attempt settles with");
    }

    [Fact]
    public void A_quota_marker_on_stderr_aborts_the_cell_for_spend()
    {
        var stream = ScoutCellStreams.For(Task, ScoutArms.Of(ScoutArmId.A1), tree: Tree);
        var verdict = ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(ScoutArms.Of(ScoutArmId.A1), "Claude AI usage limit reached|1760000000"), LinkTargets.None);

        verdict.Settlement.Should().Match<ScoutSettlement>(s => s.Outcome == ScoutCellOutcome.Aborted && s.Reason == ScoutReason.SpendLimit);
    }

    [Fact]
    public void A_stream_the_wall_cut_before_its_result_is_void_and_reports_no_cost()
    {
        var stream = ScoutStreams.Truncated(ScoutCellStreams.For(Task, ScoutArms.Of(ScoutArmId.A1), tree: Tree));
        var verdict = ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(ScoutArms.Of(ScoutArmId.A1)), LinkTargets.None);

        verdict.Settlement.Should().Match<ScoutSettlement>(s => s.Outcome == ScoutCellOutcome.Void && s.Reason == ScoutReason.StreamIncomplete);
        verdict.ReportedUsd.WasCaptured.Should().BeFalse("no result envelope survived — the attempt is charged its reservation as unknown cost");
        verdict.Cost.Should().BeOfType<CostCheck.Unchecked>();
    }

    [Fact]
    public void The_main_session_answering_with_another_opus_id_is_a_moved_pin()
    {
        var pins = ScoutPins.Of("2.1.289", "p", "claude-opus-6-0", ScoutStreams.Sonnet, "s").Ok();
        var stream = ScoutCellStreams.For(Task, ScoutArms.Of(ScoutArmId.B1), tree: Tree);

        ScoutPinDrift.Moved(ClaudeSubagents.Read(stream), pins).Should().BeTrue();
        ScoutPinDrift.Moved(ClaudeSubagents.Read(stream), ScoutStoreFixtures.Pins()).Should().BeFalse();
    }

    [Fact]
    public void The_alias_check_compares_each_scouts_resolved_model_with_the_recorded_id()
    {
        var same = ClaudeSubagents.Read(ScoutStreams.Cell([new ChildSpec("p", "sonnet", ScoutStreams.Sonnet), new ChildSpec("p", "opus", ScoutStreams.Opus, ScoutStreams.Opus)]));
        var moved = ClaudeSubagents.Read(ScoutStreams.Cell([new ChildSpec("p", "sonnet", "claude-sonnet-6-0"), new ChildSpec("p", "opus", ScoutStreams.Opus, ScoutStreams.Opus)]));
        var none = ClaudeSubagents.Read(ScoutStreams.Cell([new ChildSpec("p", "sonnet", string.Empty), new ChildSpec("p", "opus", ScoutStreams.Opus, ScoutStreams.Opus)]));

        ScoutAliasCheck.Compare(same, ScoutStreams.Opus, ScoutStreams.Sonnet).Should().BeOfType<AliasVerdict.Same>();
        ScoutAliasCheck.Compare(moved, ScoutStreams.Opus, ScoutStreams.Sonnet).Should().BeOfType<AliasVerdict.Moved>()
            .Which.Changes.Should().Equal(("sonnet", ScoutStreams.Sonnet, "claude-sonnet-6-0"));
        ScoutAliasCheck.Compare(none, ScoutStreams.Opus, ScoutStreams.Sonnet).Should().BeOfType<AliasVerdict.NotMeasured>();
    }

    [Fact]
    public void Arm_budgets_default_per_arm_take_overrides_and_refuse_a_stranger_or_a_non_number()
    {
        var budgets = ScoutArmBudgets.For([ScoutArmId.A1, ScoutArmId.B1], 1.5m, ["B1=0.75"], 600).Ok();

        budgets.Should().Equal(new ScoutArmBudget(ScoutArmId.A1, 1.5m, 600), new ScoutArmBudget(ScoutArmId.B1, 0.75m, 600));
        budgets[0].ReservationUsd.Should().Be(1.8m, "the cap plus 20 % headroom");
        ScoutArmBudgets.For([ScoutArmId.A1], 1m, ["B3=1"], 600).Reason().Should().Contain("B3");
        ScoutArmBudgets.For([ScoutArmId.A1], 1m, ["A1=lots"], 600).Reason().Should().Contain("A1=lots");
        ScoutArmBudgets.For([ScoutArmId.A1], 0m, [], 600).Reason().Should().Contain("not a cap");
    }

    [Fact]
    public void The_prompts_are_a_pure_function_of_the_statement_the_focus_and_the_arm()
    {
        var b2 = ScoutPrompts.ForCell(Task, ScoutArms.Of(ScoutArmId.B2));

        b2.Scouts.Should().HaveCount(3).And.OnlyHaveUniqueItems("the focus areas are distinct, never copies");
        b2.Scouts.Should().OnlyContain(p => p.Contains(Task.Statement) && p.Contains("KEY FILES") && p.Contains("CLAIMS"));
        b2.Driver.Should().Contain("exactly 3 Agent call(s)").And.Contain("\"sonnet\"").And.Contain(ScoutArms.CodeExplorerType);
        b2.Scouts.Should().OnlyContain(p => b2.Driver.Contains(p), "each prompt rides the driver verbatim");
        ScoutPrompts.ForCell(Task, ScoutArms.Of(ScoutArmId.B2)).Should().BeEquivalentTo(b2);
    }

    private static ScoutCellVerdict Read(ScoutArm arm, CellVariant variant)
    {
        var stream = ScoutCellStreams.For(Task, arm, variant, Tree);
        return ScoutCellReading.Read(ClaudeSubagents.Read(stream), stream, Context(arm), LinkTargets.None);
    }

    private static ScoutCellContext Context(ScoutArm arm, string stderr = "") =>
        new(new ReadBackExpectation(arm, ScoutPrompts.ForCell(Task, arm).Sha256s, arm.Model == ScoutModel.Opus ? ScoutStreams.Opus : ScoutStreams.Sonnet, Tree),
            ScoutStoreFixtures.Pins(), ScoutCellStreams.Prices, Task.GroundTruth, Files, stderr);
}
