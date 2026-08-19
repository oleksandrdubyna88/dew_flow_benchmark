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
    private const string SuiteId = "s";
    private const string Metric = "Anchor recall";
    private static readonly DateTimeOffset Noon = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

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
                new Leg(easy, "fast", "-", 1.0), new Leg(easy, "slow", "-", 1.0),
                new Leg(hard, "fast", "-", 1.0), new Leg(hard, "slow", "-", 0.0),
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
            run, new ScriptedResults([]), new RunReportRequest(run.Run.Id, string.Empty), Ct);

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
            (IEnumerable<Leg>)[.. Legs(halves.Selection, a.Name, a.Selection), .. Legs(halves.HeldOut, a.Name, a.HeldOut)]);

        return await ReportAsync([.. legs], halves);
    }

    private async Task<RunReportView> ReportAsync(IReadOnlyList<Leg> legs, SplitProbe halves, int minLegs = 1)
    {
        var run = new ScriptedRun(halves.All);
        var built = await RunReport.BuildAsync(
            run, new ScriptedResults(legs), new RunReportRequest(run.Run.Id, Metric, minLegs), Ct);

        return built.Should().BeOfType<Outcome<RunReportView>.Ok>().Subject.Value;
    }

    private static ArmReading ArmOf(RunReportView view, ReportDimension dimension, string arm) =>
        view.Dimensions.Single(d => d.Dimension == dimension).Arms.Single(a => a.Arm == arm);

    private static IEnumerable<Leg> Legs(IReadOnlyList<string> questions, string variant, double value) =>
        questions.Select(q => new Leg(q, "m", variant, value));

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

    /// <summary>A run whose only real content is its suite stamp and its planned questions — the two things
    /// the split is derived from. The stamp is a FROZEN one (<c>s@v3#…</c>) on purpose: the report must
    /// split on the suite id inside it, so a stamp that was merely the id would let a wrong implementation
    /// pass.</summary>
    private sealed class ScriptedRun(IReadOnlyList<string> questions) : IRunStore
    {
        public BenchRun Run { get; } = BenchRun.Planned(
            "report",
            MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), CommitSha.Parse(new string('c', 40)).Ok()),
            new EngineRef(EngineKind.Qln, "http://localhost:5080", "1.0", "fp"),
            $"{SuiteId}@v3#abcdef012345",
            Noon);

        public Task<Outcome<BenchRun>> LoadAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(runId == Run.Id
                ? Outcome<BenchRun>.Success(Run)
                : Outcome<BenchRun>.Failure($"no run {runId}"));

        public Task<IReadOnlyList<string>> QuestionIdsAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(questions);

        // The queue half of the port. A report never claims, settles or sweeps, and a double that quietly
        // answered these would hide a report that had started doing so.
        public Task<Outcome<BenchRun>> CreateAsync(BenchRun run, IReadOnlyList<RunCell> cells, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report does not create runs");

        public Task<Outcome<RunCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report does not claim cells");

        public Task<Outcome<RunCell>> SettleAsync(Guid cellId, WorkerIdentity owner, LegOutcome outcome, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report does not settle cells");

        public Task<SweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report does not sweep");

        public Task<RunProgress> ProgressAsync(Guid runId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report reads results, not progress");
    }

    /// <summary>Scored legs held as values, aggregated the way the port promises: grouped, averaged, and
    /// with an absent pair ABSENT rather than zero.</summary>
    private sealed class ScriptedResults(IReadOnlyList<Leg> legs) : IResultStore
    {
        public Task<IReadOnlyList<MetricByDimension>> AverageByAsync(
            Guid runId, ReportDimension dimension, string metricName, QuestionScope scope, CancellationToken cancellationToken)
        {
            IReadOnlyList<MetricByDimension> grouped = metricName != Metric
                ? []
                : [.. In(scope)
                    .GroupBy(leg => Key(leg, dimension), StringComparer.Ordinal)
                    .Select(g => new MetricByDimension(g.Key, g.Average(l => l.Value), g.Count()))
                    .OrderBy(x => x.Dimension, StringComparer.Ordinal)];

            return Task.FromResult(grouped);
        }

        public Task<IReadOnlyList<QuestionPassRate>> PassRateByQuestionAndSubjectAsync(
            Guid runId, string metricName, CancellationToken cancellationToken)
        {
            IReadOnlyList<QuestionPassRate> rates =
                [.. legs
                    .GroupBy(l => (l.Question, l.Subject))
                    .Select(g => new QuestionPassRate(g.Key.Question, g.Key.Subject, g.Average(l => l.Value)))];

            return Task.FromResult(rates);
        }

        public Task<RunScoreboard> ScoreboardAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(new RunScoreboard(legs.Count, legs.Count(l => l.Value > 0)));

        private IEnumerable<Leg> In(QuestionScope scope) =>
            scope is QuestionScope.Some some ? legs.Where(l => some.Ids.Contains(l.Question)) : legs;

        private static string Key(Leg leg, ReportDimension dimension) =>
            dimension switch
            {
                ReportDimension.Variant => leg.Variant,
                ReportDimension.Subject => leg.Subject,
                ReportDimension.Lane => "native",
                _ => "Qln",
            };

        // The write half. A report appends nothing and prunes nothing.
        public Task<Outcome<LegResult>> SaveAsync(LegResult result, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report writes no results");

        public Task<SnippetPruning> PruneHitSnippetsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report prunes nothing");

        public Task<bool> HasResultAsync(Guid cellId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report does not re-enter a leg");

        public Task<IReadOnlyList<LegResult>> ForRunAsync(Guid runId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report aggregates rather than hydrating the run — that is the point");

        public Task<IReadOnlyList<JudgeableLeg>> WithoutMetricAsync(Guid runId, string metricName, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report is not the judge lane");

        public Task<Outcome<int>> AppendMetricsAsync(Guid resultId, IReadOnlyList<StoredMetric> metrics, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a report appends no metrics");
    }
}
