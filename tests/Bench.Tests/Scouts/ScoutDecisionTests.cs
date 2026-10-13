using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.8's <see cref="ScoutDecisions.Decide"/> as a table: D5's margins held to the BOUNDS (lower Δrecall ≥ −0.03, upper Δwrong ≤ +0.5),
/// a failure and a crossing told apart, the rule changing only when W1, W2 and W3 all pass, a half that is not measured never passing, and a
/// PRELIMINARY run (D7) stating no verdict while its intervals stand.</summary>
public sealed class ScoutDecisionTests
{
    private static ScoutInterval I(double point, double lower, double upper, int clusters = 3) => new(true, point, lower, upper, clusters * 2, clusters);

    private static readonly ScoutInterval GoodRecall = I(0.01, -0.02, 0.04);

    private static readonly ScoutInterval GoodWrong = I(0.1, -0.2, 0.4);

    private static ScoutWorkloadReading W(ScoutWorkload w, ScoutInterval? recall = null, ScoutInterval? wrong = null, bool measured = true) =>
        new(w, recall ?? GoodRecall, wrong ?? GoodWrong, measured);

    private static IReadOnlyList<ScoutWorkloadReading> AllPass(ScoutWorkloadReading? replace = null) =>
        [.. Enum.GetValues<ScoutWorkload>().Select(w => replace is not null && replace.Workload == w ? replace : W(w))];

    /// <summary>The table, keyed by the case's sentence (the theory's argument is the key alone, so every row is a serialisable test case).</summary>
    private static readonly IReadOnlyDictionary<string, (ScoutWorkloadReading Reading, ScoutWorkloadOutcome Outcome, ScoutDecisionReason[] Reasons)> Rows =
        new Dictionary<string, (ScoutWorkloadReading, ScoutWorkloadOutcome, ScoutDecisionReason[])>(StringComparer.Ordinal)
        {
            ["pass — both bounds inside their margins"] = (W(ScoutWorkload.W1), ScoutWorkloadOutcome.Pass, []),
            ["fail on recall — even the upper bound is below −0.03"] = (W(ScoutWorkload.W1, recall: I(-0.10, -0.15, -0.05)), ScoutWorkloadOutcome.Fail, [ScoutDecisionReason.RecallBelowMargin]),
            ["fail on wrong claims — even the lower bound is above +0.5"] = (W(ScoutWorkload.W1, wrong: I(1.0, 0.7, 1.4)), ScoutWorkloadOutcome.Fail, [ScoutDecisionReason.WrongAboveMargin]),
            ["recall crossing — the point passes, the lower bound does not"] = (W(ScoutWorkload.W1, recall: I(0.0, -0.08, 0.06)), ScoutWorkloadOutcome.NotShown, [ScoutDecisionReason.RecallCrossesMargin]),
            ["wrong-claim crossing — the point passes, the upper bound does not"] = (W(ScoutWorkload.W1, wrong: I(0.2, -0.1, 0.7)), ScoutWorkloadOutcome.NotShown, [ScoutDecisionReason.WrongCrossesMargin]),
            ["a bound exactly on the margin passes"] = (W(ScoutWorkload.W1, recall: I(0.0, -0.03, 0.02), wrong: I(0.2, 0.0, 0.5)), ScoutWorkloadOutcome.Pass, []),
            ["not measured — the wrong-claim half is missing"] = (W(ScoutWorkload.W1, measured: false), ScoutWorkloadOutcome.NotShown, [ScoutDecisionReason.WrongNotMeasured]),
            ["one repository — the interval collapsed onto the point"] = (W(ScoutWorkload.W1, recall: I(0.0, 0.0, 0.0, clusters: 1), wrong: I(0.0, 0.0, 0.0, clusters: 1)), ScoutWorkloadOutcome.NotShown, [ScoutDecisionReason.OneRepository]),
            ["no paired task"] = (W(ScoutWorkload.W1, recall: ScoutInterval.None, wrong: ScoutInterval.None), ScoutWorkloadOutcome.NotShown, [ScoutDecisionReason.NoPairedTask, ScoutDecisionReason.WrongNotMeasured]),
        };

    public static TheoryData<string> Table() => [.. Rows.Keys];

    [Theory]
    [MemberData(nameof(Table))]
    public void One_workload_reads_against_the_margins_by_its_bounds(string row)
    {
        var (reading, outcome, reasons) = Rows[row];
        var decided = ScoutDecisions.Workload(reading, ScoutRunKind.Decision);

        decided.Outcome.Should().Be(outcome, row);
        decided.Reasons.Should().Equal(reasons, row);
    }

    [Fact]
    public void The_rule_changes_only_when_all_three_workloads_pass_on_a_decision_run()
    {
        var decision = ScoutDecisions.Decide(AllPass(), ScoutRunKind.Decision);

        decision.Verdict.Should().Be(ScoutVerdict.RuleChangeShown);
        decision.Workloads.Should().HaveCount(3).And.OnlyContain(w => w.Outcome == ScoutWorkloadOutcome.Pass);
    }

    [Fact]
    public void One_workload_failing_keeps_Opus_while_the_other_two_pass()
    {
        var decision = ScoutDecisions.Decide(AllPass(W(ScoutWorkload.W2, recall: I(-0.10, -0.15, -0.05))), ScoutRunKind.Decision);

        decision.Verdict.Should().Be(ScoutVerdict.NotShown);
        decision.Workloads.Select(w => w.Outcome).Should().Equal(ScoutWorkloadOutcome.Pass, ScoutWorkloadOutcome.Fail, ScoutWorkloadOutcome.Pass);
    }

    [Fact]
    public void One_crossing_interval_is_not_shown_and_keeps_Opus_though_its_point_estimate_passes()
    {
        var crossing = W(ScoutWorkload.W3, recall: I(0.0, -0.08, 0.06));

        crossing.Recall.Point.Should().BeGreaterThan(ScoutDecisions.RecallMargin, "the planted case: the POINT passes, only the bound does not");
        ScoutDecisions.Decide(AllPass(crossing), ScoutRunKind.Decision).Verdict.Should().Be(ScoutVerdict.NotShown);
    }

    [Fact]
    public void A_workload_whose_wrong_claims_are_not_measured_cannot_pass_and_the_rule_stays()
    {
        var decision = ScoutDecisions.Decide(AllPass(W(ScoutWorkload.W1, measured: false)), ScoutRunKind.Decision);

        decision.Verdict.Should().Be(ScoutVerdict.NotShown);
        decision.Workloads[0].Reasons.Should().Equal(ScoutDecisionReason.WrongNotMeasured);
    }

    [Fact]
    public void A_run_that_measured_only_some_workloads_cannot_change_the_rule()
    {
        var decision = ScoutDecisions.Decide([W(ScoutWorkload.W1)], ScoutRunKind.Decision);

        decision.Verdict.Should().Be(ScoutVerdict.NotShown);
        decision.Workloads.Skip(1).Should().OnlyContain(w => w.Reasons.SequenceEqual(new[] { ScoutDecisionReason.WorkloadMissing }));
    }

    [Fact]
    public void A_preliminary_run_states_no_verdict_even_where_a_decision_run_would_pass()
    {
        var decision = ScoutDecisions.Decide(AllPass(), ScoutRunKind.Preliminary);

        decision.Verdict.Should().Be(ScoutVerdict.Preliminary);
        decision.Workloads.Should().OnlyContain(w => w.Outcome == ScoutWorkloadOutcome.Preliminary && w.Reasons.Count == 0);
    }

    [Fact]
    public void A_preliminary_run_keeps_what_was_not_measured_and_drops_where_its_intervals_sit_against_the_margins()
    {
        var reading = W(ScoutWorkload.W1, recall: I(-0.10, -0.15, -0.05), measured: false);

        ScoutDecisions.Workload(reading, ScoutRunKind.Preliminary).Reasons.Should().Equal(ScoutDecisionReason.WrongNotMeasured);
        ScoutDecisions.Workload(reading, ScoutRunKind.Decision).Reasons.Should().Equal(ScoutDecisionReason.RecallBelowMargin, ScoutDecisionReason.WrongNotMeasured);
    }
}
