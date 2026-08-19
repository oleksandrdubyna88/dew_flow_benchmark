using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>The comparison, assembled from stored evidence.
/// <para>
/// Every rule under test here existed, tested, and was called by NOTHING before this type:
/// <c>SeedSplit.Proof</c>, <c>Discrimination.Over</c>, <c>MetricByDimension.Legs</c>. The split in
/// particular is the guard against the one failure this kind of harness reliably produces — three
/// configurations upstream were chosen on convincing numbers and reversed by a wider check — and it had
/// never once been consulted. So these tests are not about arithmetic; they are about whether a false
/// winner can still be announced as a result.
/// </para></summary>
public sealed class RunReportTests
{
    /// <summary>The same id the shared double stamps its run with — taken from there rather than repeated,
    /// because a probe that split on a different id than the run carries would test nothing.</summary>
    private const string SuiteId = ScriptedRun.SuiteId;

    private const string Metric = "Anchor recall";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_variant_that_won_only_where_it_was_chosen_is_unproven_and_not_ranked()
    {
        // The candidate beats the control on the half that chose it and matches it on the half that did
        // not — which is the exact shape of every false winner this split exists to catch.
        var view = await ReportAsync(
            Arm("-", selection: 0.5, heldOut: 0.5),
            Arm("cand", selection: 1.0, heldOut: 0.5));

        var candidate = ArmOf(view, ReportDimension.Variant, "cand");

        candidate.Proof.Should().Be(ProofState.Unproven,
            "won only where it was chosen — that is not a smaller win, it is a different word");
        candidate.Selection.Average.Should().Be(1.0);
        candidate.HeldOut.Average.Should().Be(0.5);
    }

    [Fact]
    public async Task A_variant_that_won_on_both_halves_is_confirmed_and_carries_its_margin()
    {
        var view = await ReportAsync(
            Arm("-", selection: 0.25, heldOut: 0.25),
            Arm("cand", selection: 1.0, heldOut: 0.75));

        var candidate = ArmOf(view, ReportDimension.Variant, "cand");

        candidate.Proof.Should().Be(ProofState.Confirmed, "it survived the half that did not choose it");
        candidate.Margin.Should().BeApproximately(0.5, 1e-9,
            "the margin is reported beside the verdict and never turned into a threshold — a floor nobody "
            + "has measured would be a quality claim");
    }

    [Fact]
    public async Task A_variant_that_won_only_on_the_held_out_half_is_reported_as_suspicious_rather_than_hidden()
    {
        var view = await ReportAsync(
            Arm("-", selection: 1.0, heldOut: 0.25),
            Arm("cand", selection: 0.5, heldOut: 0.75));

        ArmOf(view, ReportDimension.Variant, "cand").Proof.Should().Be(ProofState.Suspicious,
            "more likely a split artefact than a discovery, and worth seeing rather than hiding");
    }

    [Fact]
    public async Task An_arm_with_no_legs_on_one_half_is_unmeasured_there_rather_than_beaten()
    {
        var halves = Halves(2);

        // The candidate answered only the selection half. It has not lost the other one; nobody ran it.
        var view = await ReportAsync(
            [
                .. Legs(halves.Selection, "-", 0.5), .. Legs(halves.HeldOut, "-", 0.5),
                .. Legs(halves.Selection, "cand", 1.0),
            ],
            halves);

        var candidate = ArmOf(view, ReportDimension.Variant, "cand");

        candidate.HeldOut.Measured.Should().BeFalse("an absent measurement and a defeat are different facts");
        candidate.HeldOut.Describe.Should().Be("unmeasured");
        candidate.Proof.Should().Be(ProofState.Unproven,
            "winning where it was chosen and being unrun elsewhere is unproven, never confirmed");
        candidate.Margin.Should().Be(0, "there is no margin over a half nobody measured");
    }

    [Fact]
    public async Task A_dimension_with_fewer_legs_than_the_floor_prints_its_average_and_withholds_the_ranking()
    {
        var halves = Halves(1);

        var view = await ReportAsync(
            [.. Legs(halves.Selection, "-", 1.0), .. Legs(halves.HeldOut, "cand", 0.5)],
            halves,
            minLegs: 2);

        var variants = view.Dimensions.Single(d => d.Dimension == ReportDimension.Variant);

        variants.Arms.Should().HaveCount(2, "the numbers are real and are printed either way");
        variants.RankingRefusal.Should().Contain("1 leg(s)").And.Contain("fewer than the 2",
            "a mean over one leg and a mean over two hundred are different claims, and which to trust is "
            + "the operator's call rather than a suppressed table");
    }

    [Fact]
    public async Task A_run_with_no_control_arm_reports_arms_without_nominating_a_baseline()
    {
        var halves = Halves(2);

        // Two named variants and no control: nothing here is the thing the others are read against.
        var view = await ReportAsync(
            [
                .. Legs(halves.Selection, "alpha", 1.0), .. Legs(halves.HeldOut, "alpha", 1.0),
                .. Legs(halves.Selection, "beta", 0.5), .. Legs(halves.HeldOut, "beta", 0.5),
            ],
            halves);

        var variants = view.Dimensions.Single(d => d.Dimension == ReportDimension.Variant);

        variants.Baseline.Should().BeEmpty();
        variants.Arms.Should().OnlyContain(a => a.Proof == ProofState.NotAWinner,
            "picking a baseline by score would define the winner into existence");
        view.Warnings.Should().Contain(w => w.Contains("no baseline was stated"));
    }

    [Fact]
    public async Task A_one_sided_split_warns_that_nothing_can_be_confirmed()
    {
        // The run PLANS only questions the hash puts on one side, so the report derives an empty half —
        // the split is the report's own reading of the run's questions, never something a caller hands it.
        var oneSided = new SplitProbe(Halves(2).Selection, []);

        var view = await ReportAsync([.. Legs(oneSided.Selection, "-", 1.0)], oneSided);

        view.Warnings.Should().Contain(
            w => w.Contains("the split is one-sided") && w.Contains("surviving the half that did not choose it"),
            "confirming a winner means surviving the half that did not choose it — with one half there is "
            + "no such half, and a report that stayed silent would read as a clean result");
    }

    [Fact]
    public async Task A_question_every_subject_passes_is_trivial_here_and_never_unmeasured()
    {
        var halves = Halves(2);
        var easy = halves.Selection[0];
        var hard = halves.HeldOut[0];

        var view = await ReportAsync(
            [
                new ScriptedLeg(easy, "fast", "-", 1.0), new ScriptedLeg(easy, "slow", "-", 1.0),
                new ScriptedLeg(hard, "fast", "-", 1.0), new ScriptedLeg(hard, "slow", "-", 0.0),
            ],
            halves);

        view.Discrimination.EveryonePasses.Should().Be(1, "a question both subjects passed separates neither");
        view.Discrimination.Discriminating.Should().Be(1);
        view.Discrimination.Unusable.Should().Be(0,
            "unusable means fewer than two measured subjects — a trivial question is measured, just not useful here");
    }

    [Fact]
    public async Task No_part_of_a_report_proposes_retiring_a_question()
    {
        var halves = Halves(2);
        var view = await ReportAsync([.. Legs(halves.Selection, "-", 1.0), .. Legs(halves.HeldOut, "-", 1.0)], halves);

        // Discrimination is a property of a comparison, never of a question: what a frontier model finds
        // trivial may be the hardest item in the set for a local 7B, and pruning by what saturates the
        // strongest models deletes exactly the range where cheaper models still differ.
        var everything = string.Join(" ", [.. view.Warnings, view.Discrimination.Describe]);

        everything.Should().NotContainAny("retire", "delete", "prune", "drop");
    }

    [Fact]
    public async Task The_metric_has_no_default_and_a_report_without_one_is_refused()
    {
        var halves = Halves(1);
        var run = new ScriptedRun(halves.All);

        var refused = await RunReport.BuildAsync(
            run, new ScriptedResults([], Metric), new RunReportRequest(run.Run.Id, string.Empty), Ct);

        refused.Should().BeOfType<Outcome<RunReportView>.Fail>()
            .Which.Reason.Should().Contain("no default")
            .And.Contain("means nothing for the control arm");
    }

    [Fact]
    public async Task A_run_nobody_has_scored_says_so_rather_than_printing_an_empty_comparison()
    {
        var halves = Halves(2);
        var view = await ReportAsync([], halves);

        view.Scoreboard.Scored.Should().Be(0);
        view.Warnings.Should().Contain(w => w.Contains("nothing here to compare"));
        view.Dimensions.Should().OnlyContain(d => d.RankingRefusal.Contains("no leg of this run carries that metric"));
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    /// <summary>Two arms over a two-question-per-half split, each scoring one value on each half — the
    /// shorthand every proof test above is written in.</summary>
    private async Task<RunReportView> ReportAsync(params ArmScript[] arms)
    {
        var halves = Halves(2);

        var legs = arms.SelectMany(a =>
            (IEnumerable<ScriptedLeg>)[.. Legs(halves.Selection, a.Name, a.Selection), .. Legs(halves.HeldOut, a.Name, a.HeldOut)]);

        return await ReportAsync([.. legs], halves);
    }

    private async Task<RunReportView> ReportAsync(IReadOnlyList<ScriptedLeg> legs, SplitProbe halves, int minLegs = 1)
    {
        var run = new ScriptedRun(halves.All);
        var built = await RunReport.BuildAsync(
            run, new ScriptedResults(legs, Metric), new RunReportRequest(run.Run.Id, Metric, minLegs), Ct);

        return built.Should().BeOfType<Outcome<RunReportView>.Ok>().Subject.Value;
    }

    private static ArmReading ArmOf(RunReportView view, ReportDimension dimension, string arm) =>
        view.Dimensions.Single(d => d.Dimension == dimension).Arms.Single(a => a.Arm == arm);

    private static IEnumerable<ScriptedLeg> Legs(IReadOnlyList<string> questions, string variant, double value) =>
        questions.Select(q => new ScriptedLeg(q, "m", variant, value));

    /// <summary>The first <paramref name="perHalf"/> question ids that SeedSplit puts on each side.
    /// <para>
    /// Probed rather than hard-coded: the assignment is a hash of (suite id, question id), and a test that
    /// asserted <c>q1</c> is a selection question would be asserting the hash rather than the behaviour —
    /// and would go red the day the hash legitimately changed.
    /// </para></summary>
    private static SplitProbe Halves(int perHalf)
    {
        var selection = new List<string>();
        var heldOut = new List<string>();

        foreach (var id in Enumerable.Range(1, 64).Select(i => $"q{i}"))
        {
            var half = SeedSplit.Assign(SuiteId, id);
            var target = half is Outcome<SplitHalf>.Ok { Value: SplitHalf.Selection } ? selection : heldOut;

            if (target.Count < perHalf)
            {
                target.Add(id);
            }
        }

        selection.Should().HaveCount(perHalf, "the probe must find enough questions on both sides to say anything");
        heldOut.Should().HaveCount(perHalf);

        return new SplitProbe(selection, heldOut);
    }

    private readonly record struct ArmScript(string Name, double Selection, double HeldOut);

    private static ArmScript Arm(string name, double selection, double heldOut) => new(name, selection, heldOut);

    private sealed record Leg(string Question, string Subject, string Variant, double Value);

    private sealed record SplitProbe(IReadOnlyList<string> Selection, IReadOnlyList<string> HeldOut)
    {
        public IReadOnlyList<string> All => [.. Selection, .. HeldOut];
    }
}
