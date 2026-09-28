using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.GateReportFixtures;

namespace Bench.Tests.Gate;

/// <summary>WHICH runs and WHICH verdicts a figure is computed over — the population, before any arithmetic.
/// Each test pins one way the population was wrong in the first cut of the report: two builds sharing a
/// version text averaged, a run that honestly found nothing dropped as unassessed, a variance "stated" from
/// one reading, an interrupted attempt counted as a run, one finding judged twice counted twice, a
/// calibration task inside the model table, and a group of nothing indexed at zero.</summary>
public sealed class GateReportPopulationTests
{
    [Fact]
    public void Two_builds_with_one_version_text_are_two_scopes_never_one_mean()
    {
        // A dirty rebuild at one HEAD answers --version with the same text and hashes to different bytes.
        var clean = ProductPin.Hashed(new string('c', 64), "0.17.1+5e71e7b", "5e71e7b", CapturedCount.Number(0), "src_mcp").Ok();
        var dirty = ProductPin.Hashed(new string('d', 64), "0.17.1+5e71e7b", "5e71e7b", CapturedCount.Number(3), "src_mcp").Ok();

        var input = Input([Run("cs2", "grok", 1, pin: clean), Run("cs2", "grok", 2, pin: dirty)]);

        GateReport.Scopes(input.Runs).Should().HaveCount(2,
            "the version text is a label and the binary sha is the identity — two byte-different products in one mean is two populations");
        GateReport.PerModel(Scope(clean), StrictRubric, input).Rows.Single().Runs.Should().Be(1);
        Scope(clean).ProductSha256.Should().Be(clean.BinarySha256);
    }

    [Fact]
    public void A_valid_run_that_found_nothing_is_assessed_with_zero_hits()
    {
        var empty1 = Run("cs2", "grok", 1, findings: 0);
        var empty2 = Run("cs2", "grok", 2, findings: 0);
        var five = Run("cs2", "grok", 3, findings: 5);
        var verdicts = new[]
        {
            Verdict(five, 0, Strict(StrictReading.Supported, seed: "cs2-S1"), StrictHash),
            Verdict(five, 1, Strict(StrictReading.Supported, seed: "cs2-S2", value: ValueLevel.High), StrictHash),
            Verdict(five, 2, Strict(StrictReading.Refuted), StrictHash),
            Verdict(five, 3, Strict(StrictReading.Partial), StrictHash),
            Verdict(five, 4, Strict(StrictReading.Unresolved), StrictHash),
        };

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, Input([empty1, empty2, five], verdicts)).Rows.Single();

        row.AssessedRuns.Should().Be(3, "a valid run with no findings has nothing to assess — it is a reading of zero, not a gap");
        row.SeedsHitMean.Should().Be(Figure.Of(0.67), "(0 + 0 + 2) / 3 — dropping the empty runs would report 2.0");
        row.SeedsHitMin.Should().Be(0);
        row.HighValuePerRun.Should().Be(Figure.Of(0.33));
        GateReport.PerTask(Scope(PinA), StrictRubric, Input([empty1, empty2, five], verdicts)).Single().SeedsHit
            .Should().Equal([Figure.Of(0), Figure.Of(0), Figure.Of(2)]);
    }

    [Fact]
    public void A_run_with_findings_and_no_verdict_is_still_unassessed_and_an_invalid_empty_run_too()
    {
        var unjudged = Run("cs2", "grok", 1, findings: 4);
        var failedEmpty = Run("cs2", "grok", 2, valid: false, findings: 0, failure: FailureKind.LengthCut);

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, Input([unjudged, failedEmpty])).Rows.Single();

        row.AssessedRuns.Should().Be(0, "only a VALID run that found nothing is a reading of zero; a cut-off answer found nothing because it never finished");
        row.SeedsHitMean.State.Should().Be(FigureState.Unassessed);
    }

    [Fact]
    public void Variance_of_seeds_needs_three_assessed_readings_and_the_findings_spread_is_judged_apart()
    {
        var runs = new[] { Run("cs2", "grok", 1, findings: 2), Run("cs2", "grok", 2, findings: 6), Run("cs2", "grok", 3, findings: 4) };
        var one = new[] { Verdict(runs[0], 0, Strict(StrictReading.Supported, seed: "cs2-S1"), StrictHash) };

        var reading = GateReport.Variance(Scope(PinA), StrictRubric, Input(runs, one)).Single();

        reading.SeedReadings.Should().Be(1);
        reading.SeedsState.Should().Be(VarianceState.Withheld, "three repeats with ONE assessed run is one seed reading — a spread of one is not a spread");
        reading.FindingsState.Should().Be(VarianceState.Stated, "the findings count is read off every reply, so three repeats state it");
        reading.FindingsMin.Should().Be(2);
        reading.FindingsMax.Should().Be(6);

        var all = runs.Select(r => Verdict(r, 0, Strict(StrictReading.Supported, seed: "cs2-S1"), StrictHash)).ToList();
        var full = GateReport.Variance(Scope(PinA), StrictRubric, Input(runs, all)).Single();
        full.SeedsState.Should().Be(VarianceState.Stated);
        full.SeedsHitMin.Should().Be(1);
        full.SeedsHitMax.Should().Be(1);
    }

    [Fact]
    public void An_interrupted_then_completed_cell_is_one_run_and_its_failed_attempt_is_counted_apart()
    {
        var campaign = Guid.CreateVersion7();
        var interrupted = Run("cs2", "grok", 1, valid: false, findings: 0, failure: FailureKind.Interrupted, attempt: 1, campaign: campaign);
        var completed = Run("cs2", "grok", 1, findings: 4, attempt: 2, campaign: campaign);
        var other = Run("cs2", "grok", 2, findings: 2, attempt: 1, campaign: campaign);

        var input = Input([interrupted, completed, other]);
        var row = GateReport.PerModel(Scope(PinA), StrictRubric, input).Rows.Single();

        row.Runs.Should().Be(2, "one run per cell (campaign, task, reviewer, repeat): the cell's LATEST attempt");
        row.ValidPct.Should().Be(Figure.Of(100), "the interrupted attempt is not a run of the model — the completed one is");
        row.FindingsPerRun.Should().Be(Figure.Of(3), "(4 + 2) / 2 — the interrupted attempt's zero is not averaged in");
        row.Failures.Should().BeEmpty();
        row.Attempts.Should().Be(3, "every attempt stays on the record, like the other harness's attempts column");
        row.AttemptsFailed.Should().Be(1);

        GateReport.PerTask(Scope(PinA), StrictRubric, input).Single().Runs.Should().Be(2);
        GateReport.Variance(Scope(PinA), StrictRubric, input).Single().Repeats.Should().Be(2);
    }

    [Fact]
    public void Two_campaigns_in_one_scope_are_separate_runs_and_never_collapse_into_one()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var input = Input([
            Run("cs2", "grok", 1, findings: 2, campaign: first),
            Run("cs2", "grok", 2, findings: 6, campaign: first),
            Run("cs2", "grok", 1, findings: 4, campaign: second),
        ]);

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, input).Rows.Single();

        row.Runs.Should().Be(3, "repeat 1 of one campaign and repeat 1 of another are two measurements, not two attempts at one");
        row.FindingsPerRun.Should().Be(Figure.Of(4));
        var variance = GateReport.Variance(Scope(PinA), StrictRubric, input).Single();
        variance.Repeats.Should().Be(3, "three cells are three readings, whatever their repeat numbers");
        variance.FindingsState.Should().Be(VarianceState.Stated);
        variance.FindingsMax.Should().Be(6);
    }

    [Fact]
    public void One_finding_judged_twice_under_one_rubric_counts_once_and_a_real_reading_beats_a_failure()
    {
        var run = Run("cs2", "grok", 1, findings: 2);
        var verdicts = new[]
        {
            Verdict(run, 0, new Verdict.AssessmentFailure(AssessmentFailureCause.Unparseable), StrictHash),
            Verdict(run, 0, Strict(StrictReading.Supported, seed: "cs2-S1"), StrictHash),
            Verdict(run, 1, Strict(StrictReading.Refuted), StrictHash),
            Verdict(run, 1, Strict(StrictReading.Refuted), StrictHash, assessor: "claude-check"),
        };

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, HandChecked(Input([run], verdicts))).Rows.Single();

        row.Assessed.Should().Be(2, "two findings, one verdict each — a re-ask or a second assessor is not a second finding");
        row.AssessmentFailed.Should().Be(0, "the later real reading supersedes the failure for that finding");
        row.SupportedPct.Should().Be(Figure.Of(50));
    }

    [Fact]
    public void Verdicts_of_another_rubric_of_the_same_kind_never_enter_the_table()
    {
        var edited = Rubric.Of("strict-v2", RubricKind.Strict, StableHash.Of("strict, edited")).Ok();
        var catalog = new RubricCatalog([StrictRubric, edited]);
        var run = Run("cs2", "grok", 1, findings: 1);
        var v1 = GateVerdict.Under(catalog, StrictHash, run.RunId, 0, Strict(StrictReading.Refuted), GateReviewerId.Parse("codex-astra").Ok(), "b1", StrictHash, false).Ok();
        var v2 = GateVerdict.Under(catalog, edited.Hash, run.RunId, 0, Strict(StrictReading.Supported), GateReviewerId.Parse("codex-astra").Ok(), "b2", edited.Hash, false).Ok();

        var table = GateReport.PerModel(Scope(PinA), StrictRubric, HandChecked(Input([run], [v1, v2])));

        table.Rubric.Should().Be(StrictRubric);
        table.Rows.Single().SupportedPct.Should().Be(Figure.Of(0), "strict-v2 is another wording — its verdicts are another rubric's, not a second opinion here");
    }

    [Fact]
    public void A_family_matched_verdict_is_counted_apart_and_an_independent_one_is_preferred()
    {
        var run = Run("cs2", "grok", 1, findings: 2);
        var verdicts = new[]
        {
            Verdict(run, 0, Strict(StrictReading.Supported), StrictHash, assessor: "grok-judge", familyMatches: true),
            Verdict(run, 0, Strict(StrictReading.Refuted), StrictHash, assessor: "codex-astra"),
            Verdict(run, 1, Strict(StrictReading.Supported), StrictHash, assessor: "grok-judge", familyMatches: true),
        };

        var row = GateReport.PerModel(Scope(PinA), StrictRubric, HandChecked(Input([run], verdicts))).Rows.Single();

        row.AssessorFamilyMatched.Should().Be(1, "finding 1 was judged only by its own family — flagged, never refused");
        row.SupportedPct.Should().Be(Figure.Of(50), "finding 0 is read by the INDEPENDENT assessor (refuted); finding 1 by the only one it has");
    }

    [Fact]
    public void Calibration_tasks_are_reported_apart_and_never_inside_the_model_table()
    {
        var input = Input([Run("cs2", "grok", 1, findings: 2), Run("calib1", "grok", 1, findings: 8)]);

        var table = GateReport.PerModel(Scope(PinA), StrictRubric, input);

        table.Rows.Single().Runs.Should().Be(1, "the calibration run settled a transport — its numbers describe the tuning as much as the model");
        table.Rows.Single().FindingsPerRun.Should().Be(Figure.Of(2));
        table.Calibration.Single().Runs.Should().Be(1);
        table.Calibration.Single().FindingsPerRun.Should().Be(Figure.Of(8));
    }

    [Fact]
    public void A_task_row_and_a_variance_over_no_runs_are_explicit_empty_states()
    {
        var task = GateTaskId.Parse("cs2").Ok();
        var reviewer = GateReviewerId.Parse("grok").Ok();

        var row = GateReport.TaskRowOf(task, reviewer, [], Input([]).Tasks[0], []);
        row.Task.Should().Be(task);
        row.Runs.Should().Be(0);
        row.Findings.Should().BeEmpty();

        var variance = GateReport.VarianceOf(task, reviewer, [], []);
        variance.Task.Should().Be(task);
        variance.Repeats.Should().Be(0);
        variance.SeedsState.Should().Be(VarianceState.Unassessed);
        variance.FindingsState.Should().Be(VarianceState.Withheld);

        var table = GateReport.PerModel(Scope(PinA), StrictRubric, Input([]));
        table.Rows.Should().BeEmpty();
        table.Calibration.Should().BeEmpty();
        table.PerTask.Should().BeEmpty();
        table.Variance.Should().BeEmpty();
    }
}
