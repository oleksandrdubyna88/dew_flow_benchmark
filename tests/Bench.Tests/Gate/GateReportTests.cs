using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The report's refusals, in words, and its denominators. Every one of these is a number that was
/// once published wrong somewhere in this family: a variance stated over two repeats, a nobody-looked rendered
/// as zero, two rubrics averaged into one column, a failed run quietly dropped from the count, two products
/// folded into one mean.</summary>
public sealed class GateReportTests
{
    private static readonly string StrictHash = StableHash.Of("strict");
    private static readonly string LenientHash = StableHash.Of("lenient");
    private static readonly RubricCatalog Catalog = new(
    [
        Rubric.Of("strict-v1", RubricKind.Strict, StrictHash).Ok(),
        Rubric.Of("lenient-worth-v1", RubricKind.LenientWorth, LenientHash).Ok(),
    ]);

    private static readonly ProductPin PinA = ProductPin.Hashed(new string('a', 64), "0.0.0+aaaaaaa", "aaaaaaa", CapturedCount.Number(0), "src").Ok();
    private static readonly ProductPin PinB = ProductPin.Hashed(new string('b', 64), "0.0.0+bbbbbbb", "bbbbbbb", CapturedCount.Number(0), "src").Ok();

    [Fact]
    public void Variance_is_withheld_below_three_repeats_and_stated_at_three()
    {
        var two = Input([Run("cs2", "grok", 1), Run("cs2", "grok", 2)]);
        var three = Input([Run("cs2", "grok", 1, findings: 2), Run("cs2", "grok", 2, findings: 5), Run("cs2", "grok", 3, findings: 3)]);

        GateReport.Variance(Scope(PinA), RubricKind.Strict, two).Single().State.Should().Be(VarianceState.Withheld,
            "a spread of two is a difference, not a spread — not enough repeats to state variance");

        var stated = GateReport.Variance(Scope(PinA), RubricKind.Strict, three).Single();
        stated.State.Should().Be(VarianceState.Unassessed, "no verdict yet — the findings spread is stated, the seeds spread is not");
        stated.FindingsMin.Should().Be(2);
        stated.FindingsMax.Should().Be(5);
    }

    [Fact]
    public void An_unassessed_reviewer_reads_as_a_dash_never_as_zero()
    {
        var input = Input([Run("cs2", "grok", 1, findings: 4), Run("cs2", "grok", 2, findings: 3)]);

        var row = GateReport.PerModel(Scope(PinA), RubricKind.Strict, input).Rows.Single();

        row.SupportedPct.State.Should().Be(FigureState.Unassessed, "nobody looked — a dash, never 0 %");
        row.SeedsHitMean.State.Should().Be(FigureState.Unassessed);
        row.HighValuePerRun.State.Should().Be(FigureState.Unassessed);
        row.AssessedRuns.Should().Be(0);
        row.FindingsPerRun.Should().Be(Figure.Of(3.5), "findings are counted from the reply, assessed or not");
        GateReport.PerTask(Scope(PinA), RubricKind.Strict, input).Single().SeedsHit
            .Should().OnlyContain(f => f.State == FigureState.Unassessed);
    }

    [Fact]
    public void A_mean_across_two_rubric_kinds_is_refused_by_name()
    {
        var run = Run("cs2", "grok", 1, findings: 2);
        var mixed = new[]
        {
            Verdict(run, 0, Strict(StrictReading.Supported), StrictHash),
            Verdict(run, 1, new Verdict.Lenient(true), LenientHash),
        };

        GateReport.SupportedRate(mixed).Reason().Should().Contain("Strict").And.Contain("LenientWorth").And.Contain("no aggregate across");
        GateReport.SupportedRate([mixed[0]]).Ok().Should().Be(Figure.Of(100));

        var strictOnly = GateReport.PerModel(Scope(PinA), RubricKind.Strict, Input([run], mixed)).Rows.Single();
        strictOnly.Assessed.Should().Be(1, "the lenient verdict is another population and never enters the strict table");
    }

    [Fact]
    public void Assessment_failures_are_counted_apart_and_never_in_supported_pct()
    {
        var run = Run("cs2", "grok", 1, findings: 3);
        var verdicts = new[]
        {
            Verdict(run, 0, Strict(StrictReading.Supported), StrictHash),
            Verdict(run, 1, Strict(StrictReading.Refuted), StrictHash),
            Verdict(run, 2, new Verdict.AssessmentFailure(AssessmentFailureCause.Truncated), StrictHash),
        };

        var row = GateReport.PerModel(Scope(PinA), RubricKind.Strict, Input([run], verdicts)).Rows.Single();

        row.AssessmentFailed.Should().Be(1, "shown as its own count — never read as unassessed, never as refuted");
        row.SupportedPct.Should().Be(Figure.Of(50), "1 supported of 2 JUDGED — the failed batch is not in the denominator");
        row.Assessed.Should().Be(2);
    }

    [Fact]
    public void A_failed_run_stays_in_the_denominator_and_is_named_by_cause()
    {
        var input = Input([
            Run("cs2", "grok", 1, findings: 3),
            Run("cs2", "grok", 2, findings: 4),
            Run("cs2", "grok", 3, valid: false, failure: FailureKind.LengthCut),
        ]);

        var row = GateReport.PerModel(Scope(PinA), RubricKind.Strict, input).Rows.Single();

        row.Runs.Should().Be(3, "a failed run is IN the denominator");
        row.ValidRuns.Should().Be(2);
        row.ValidPct.Should().Be(Figure.Of(66.7));
        row.Failures.Should().Equal([new FailureCount(FailureKind.LengthCut, 1)]);
    }

    [Fact]
    public void A_scope_with_two_product_versions_partitions()
    {
        var input = Input([Run("cs2", "grok", 1, pin: PinA), Run("cs2", "grok", 2, pin: PinB), Run("rs3", "grok", 1, pin: PinA)]);

        var scopes = GateReport.Scopes(input.Runs);

        scopes.Should().HaveCount(2, "two products in this scope — partitioned, never averaged");
        scopes.Select(s => s.ProductVersion).Should().BeEquivalentTo(["0.0.0+aaaaaaa", "0.0.0+bbbbbbb"]);
        GateReport.PerModel(Scope(PinA), RubricKind.Strict, input).Rows.Single().Runs.Should().Be(2);
        GateReport.PerModel(Scope(PinB), RubricKind.Strict, input).Rows.Single().Runs.Should().Be(1);
    }

    [Fact]
    public void Cells_claimed_under_two_pins_in_one_campaign_land_in_two_partitions()
    {
        var campaign = Guid.CreateVersion7();
        var input = Input([Run("cs2", "grok", 1, pin: PinA, campaign: campaign), Run("cs2", "grok", 2, pin: PinB, campaign: campaign)]);

        GateReport.Scopes(input.Runs).Should().HaveCount(2, "a claimed cell finishes under the pin it started with, and the report partitions by pin");
    }

    [Fact]
    public void Cost_is_unknown_for_a_reviewer_no_run_metered_and_not_applicable_per_seed_when_none_was_hit()
    {
        var cli = Input([Run("cs2", "codex-cli", 1, cost: CapturedUsd.Unavailable("the CLI reports no cost"))]);
        var row = GateReport.PerModel(Scope(PinA), RubricKind.Strict, cli).Rows.Single();

        row.CostPerRun.State.Should().Be(FigureState.Unknown, "cost unknown — never free");
        row.CostTotal.State.Should().Be(FigureState.Unknown);
        row.CostPerSeed.State.Should().Be(FigureState.Unknown);

        var metered = Run("cs2", "grok", 1, findings: 1, cost: CapturedUsd.Amount(0.19m));
        var noHit = GateReport.PerModel(Scope(PinA), RubricKind.Strict,
            Input([metered], [Verdict(metered, 0, Strict(StrictReading.Refuted), StrictHash)])).Rows.Single();
        noHit.CostPerRun.Should().Be(Figure.Of(0.19));
        noHit.CostPerSeed.State.Should().Be(FigureState.NotApplicable, "no seed was hit, so a cost per seed divides by nothing");
    }

    [Fact]
    public void Seeds_hit_counts_distinct_seeds_per_run_and_cross_epic_apart()
    {
        var run = Run("cs2", "grok", 1, findings: 3);
        var verdicts = new[]
        {
            Verdict(run, 0, Strict(StrictReading.Supported, seed: "cs2-S1"), StrictHash),
            Verdict(run, 1, Strict(StrictReading.Partial, seed: "cs2-S1"), StrictHash),
            Verdict(run, 2, Strict(StrictReading.Supported, seed: "cs2-S2", value: ValueLevel.High), StrictHash),
        };

        var row = GateReport.PerModel(Scope(PinA), RubricKind.Strict, Input([run], verdicts)).Rows.Single();

        row.SeedsHitMean.Should().Be(Figure.Of(2), "two findings hit one seed — it is one seed");
        row.SeedsHitMin.Should().Be(2);
        row.DistinctSeeds.Should().Be(2);
        row.DistinctCrossEpicSeeds.Should().Be(1, "cs2-S2 is the cross-epic seed of the fixture task");
        row.HighValuePerRun.Should().Be(Figure.Of(1));
        row.SupportedOrPartialPct.Should().Be(Figure.Of(100));
        row.CostPerSeed.Should().Be(Figure.Of(0.095), "0.19 over two seeds");
    }

    [Fact]
    public void A_calibration_task_is_marked_on_its_per_task_row_and_a_task_with_no_seeds_has_no_recall()
    {
        var input = Input([Run("calib1", "grok", 1), Run("plain1", "grok", 1)]);

        var rows = GateReport.PerTask(Scope(PinA), RubricKind.Strict, input);

        rows.Single(r => r.Task.Value == "calib1").Calibration.Should().BeTrue("calibration task — reported apart");
        rows.Single(r => r.Task.Value == "plain1").SeedsHit.Single().State.Should().Be(FigureState.NotApplicable,
            "a task with no seeds has no seeded-recall column");
    }

    [Fact]
    public void Seconds_are_the_python_quantiles_and_turn_one_cache_marks_a_warm_run()
    {
        var input = Input([
            Run("cs2", "grok", 1, seconds: 100, turnOneCached: 2048),
            Run("cs2", "grok", 2, seconds: 300, turnOneCached: 80_384),
            Run("cs2", "grok", 3, seconds: 200, turnOneCached: 1024),
        ]);

        var row = GateReport.PerModel(Scope(PinA), RubricKind.Strict, input).Rows.Single();

        row.SecondsP50.Should().Be(Figure.Of(200));
        row.SecondsP90.Should().Be(Figure.Of(280), "python: q([100, 300, 200], 0.9) → 280.0");
        row.WarmRuns.Should().Be(1);
        row.TurnOneCachedMean.Should().Be(Figure.Of(27819), "(2048 + 80384 + 1024) / 3, rounded");
    }

    private static GateScope Scope(ProductPin pin) => GateScope.Of("gate-seeded#abc", GateKind.Feature, pin, StableHash.Of("settings"));

    private static GateReportInput Input(IReadOnlyList<GateRunRecord> runs, IReadOnlyList<GateVerdict>? verdicts = null) =>
        new(
            [
                Task("cs2", seeds: [("cs2-S1", false), ("cs2-S2", true)]),
                Task("rs3", seeds: [("rs3-S1", false)]),
                Task("calib1", seeds: [("calib1-S1", false)], calibration: true),
                Task("plain1", seeds: []),
            ],
            runs,
            verdicts ?? []);

    private static TaskSummary Task(string id, IReadOnlyList<(string Id, bool Cross)> seeds, bool calibration = false) =>
        new(GateTaskId.Parse(id).Ok(), "C#", calibration, HostedGates.All, [.. seeds.Select(s => new SeedRef(SeedId.Parse(s.Id).Ok(), s.Cross))]);

    private static GateRunRecord Run(
        string task, string reviewer, int repeat, bool valid = true, int findings = 2, double seconds = 180.7,
        CapturedUsd? cost = null, ProductPin? pin = null, Guid? campaign = null, FailureKind failure = FailureKind.None, long turnOneCached = 0)
    {
        var facts = new GateRunFacts(
            valid,
            valid ? GateVerdictWord.Proceed : GateVerdictWord.CallHuman,
            true,
            CapturedCount.Number(findings),
            2,
            2,
            ["stop", "stop"],
            [200, 200],
            CapturedCount.Number(160_000),
            CapturedCount.Number(12_000),
            CapturedCount.Number(80_000),
            CapturedCount.Unavailable("no reasoning tokens"),
            seconds,
            seconds - 3,
            [140.1, 37.5],
            [CapturedCount.Number(turnOneCached), CapturedCount.Number(80_384)],
            cost ?? CapturedUsd.Amount(0.19m),
            6,
            0,
            valid ? FailureCause.None : new FailureCause(failure, "call 2: finish_reason=length"));

        return new GateRunRecord(
            campaign ?? Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Scope(pin ?? PinA),
            GateTaskId.Parse(task).Ok(),
            GateReviewerId.Parse(reviewer).Ok(),
            repeat,
            1,
            facts,
            [],
            new RunSource.Native());
    }

    private static Verdict Strict(StrictReading reading, string seed = "none", ValueLevel value = ValueLevel.Medium) =>
        new Verdict.Strict(reading, value, SeverityFairness.Yes, Grounding.Yes, StableHash.Of("cs2:x"), SeedHit.Parse(seed));

    private static GateVerdict Verdict(GateRunRecord run, int ordinal, Verdict reading, string rubricHash) =>
        GateVerdict.Under(Catalog, rubricHash, run.RunId, ordinal, reading, GateReviewerId.Parse("codex-astra").Ok(), "b1", rubricHash, false).Ok();
}
