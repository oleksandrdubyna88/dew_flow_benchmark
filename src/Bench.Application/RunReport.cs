using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;
using Bench.Domain.Suites;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>What a report was asked for.</summary>
/// <param name="MetricName">Required, with no default. <c>Anchor recall</c> is the retrieval answer and
/// means nothing for the control arm, and a per-lane default would be a decision hidden in a flag — so an
/// operator who cannot name the metric does not yet have a question, and the refusal lists what the run
/// actually holds.</param>
/// <param name="MinLegs">Below this, a dimension prints its averages and withholds the RANKING. Two,
/// matching the measurement tuple's <c>n >= 2 to rank anything</c>.</param>
/// <param name="Baseline">The arm every other arm is compared against. Empty means derive it, which
/// succeeds only where a control arm exists — a baseline is never nominated silently.</param>
public sealed record RunReportRequest(
    Guid RunId,
    string MetricName,
    int MinLegs = 2,
    double MinSpread = Discrimination.DefaultMinSpread,
    string Baseline = "");

/// <summary>One arm's showing on one half of the split.
/// <para>
/// <see cref="Measured"/> rather than a bare average, following <c>Captured</c> / <c>CapturedCount</c>: a
/// half an arm never ran and a half it scored zero on are opposite readings of the same number, and only
/// one of them may enter a comparison. Neither of those types fits — this carries a mean and the count
/// behind it — so it repeats the discipline rather than the type.
/// </para></summary>
public readonly record struct HalfReading(bool Measured, double Average, int Legs)
{
    public static HalfReading Unmeasured { get; }

    public static HalfReading Of(double average, int legs) => new(true, average, legs);

    public string Describe => Measured ? $"{Average:0.###} ({Legs} leg(s))" : "unmeasured";
}

/// <param name="Arm">The dimension's value — a model id, a lane, an engine kind, a variant name.</param>
/// <param name="Proof">Whether this arm may be announced as a winner. <c>Unproven</c> is its own word and
/// never a smaller win: it is where every false winner this programme produced landed.</param>
/// <param name="Margin">How far this arm beat the baseline on the held-out half, or 0 when there is no
/// verdict. Reported beside the state deliberately, and NOT turned into a threshold: a floor nobody has
/// measured would be a quality claim, and this project refuses those rather than guessing one.</param>
public sealed record ArmReading(
    string Arm,
    double Average,
    int Legs,
    HalfReading Selection,
    HalfReading HeldOut,
    ProofState Proof,
    double Margin);

/// <param name="RankingRefusal">Empty when the arms may be ranked. Non-empty when they may not, and then
/// it names the count — the averages are still printed, because the numbers are real and whether to spend
/// more repeats is the operator's call.</param>
/// <param name="Baseline">The arm the others were compared against, or empty when none was stated.</param>
public sealed record DimensionReport(
    ReportDimension Dimension,
    IReadOnlyList<ArmReading> Arms,
    string Baseline,
    string RankingRefusal);

/// <summary>A run's comparison — the thing this whole harness exists to produce.</summary>
/// <param name="Warnings">What the reader must know before believing a row: a missing baseline, a half
/// nobody measured, a metric no arm carries.</param>
public sealed record RunReportView(
    Guid RunId,
    string Label,
    string TargetCanonical,
    string SuiteStamp,
    string EngineCanonical,
    string MetricName,
    RunScoreboard Scoreboard,
    int SelectionQuestions,
    int HeldOutQuestions,
    IReadOnlyList<DimensionReport> Dimensions,
    DiscriminationReport Discrimination,
    IReadOnlyList<string> Warnings)
{
    /// <summary>The machine that produced these numbers. <c>NotRecorded</c> for every run measured before a
    /// probe existed — which a reader must be told rather than left to infer from a blank line.</summary>
    public MachineFacts Machine { get; init; } = MachineFacts.NotRecorded;

    /// <summary>What that machine was doing across the run's legs. Peaks, never averages.</summary>
    public RunLoad Load { get; init; } = RunLoad.None;
}

/// <summary>Assembling a run's stored evidence into a comparison.
/// <para>
/// Every rule it applies was written, tested and called by nothing: <see cref="SeedSplit.Proof"/>,
/// <see cref="Domain.Runs.Discrimination"/>, <c>MetricByDimension.Legs</c>. This is the caller. The guard
/// against the failure mode that reversed three conclusions upstream had never once been consulted before
/// this type existed, which is the whole reason it comes before the surfaces that render it.
/// </para>
/// <para>
/// It decides; the CLI and the API render. A renderer that decides is a renderer whose decisions cannot be
/// tested without a process.
/// </para></summary>
public static class RunReport
{
    /// <summary>Why a report with no metric is refused — written ONCE and consumed by both sides.
    /// <para>
    /// The same shape as <see cref="ClaimRefusal.NoPendingCell"/>, and for the same reason: a surface has to
    /// act on this refusal — the CLI exits <c>4</c> for it and <c>3</c> for a run that is not there, since
    /// "you asked wrongly" and "what you asked about is not here" are the distinction its whole exit-code
    /// contract rests on. A phrase hand-typed here and hand-matched there is a contract that drifts silently.
    /// </para></summary>
    public const string NoMetricNamed =
        "name the metric to report on — there is no default, because the one that answers a retrieval "
        + "question means nothing for the control arm and a hidden default would decide the comparison";

    public static async Task<Outcome<RunReportView>> BuildAsync(
        IRunStore runs,
        IResultStore results,
        RunReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MetricName.Length == 0)
        {
            return Outcome<RunReportView>.Failure(NoMetricNamed);
        }

        var run = await runs.LoadAsync(request.RunId, cancellationToken);

        return run switch
        {
            Outcome<BenchRun>.Ok ok => await ViewAsync(runs, results, ok.Value, request, cancellationToken),
            Outcome<BenchRun>.Fail fail => Outcome<RunReportView>.Failure(fail.Reason),
            _ => Outcome<RunReportView>.Failure($"run {request.RunId} could not be read"),
        };
    }

    private static async Task<Outcome<RunReportView>> ViewAsync(
        IRunStore runs,
        IResultStore results,
        BenchRun run,
        RunReportRequest request,
        CancellationToken cancellationToken)
    {
        var questions = await runs.QuestionIdsAsync(run.Id, cancellationToken);
        var halves = Halves(run.SuiteStamp, questions);
        var scoreboard = await results.ScoreboardAsync(run.Id, cancellationToken);

        var dimensions = new List<DimensionReport>();

        foreach (var dimension in Enum.GetValues<ReportDimension>())
        {
            dimensions.Add(await DimensionAsync(results, run.Id, dimension, halves, request, cancellationToken));
        }

        var spread = await SpreadAsync(results, run.Id, request.MetricName, cancellationToken);
        var machine = await runs.MachineAsync(run.Id, cancellationToken);
        var load = LegSampling.Across(await results.LoadsAsync(run.Id, cancellationToken));

        return Outcome<RunReportView>.Success(new RunReportView(
            run.Id,
            run.Label,
            run.Target.Canonical,
            run.SuiteStamp,
            run.Engine.Canonical,
            request.MetricName,
            scoreboard,
            halves.Selection.Count,
            halves.HeldOut.Count,
            dimensions,
            Discrimination.Over(spread.Questions, spread.Models, request.MinSpread),
            [.. Warnings(scoreboard, halves, dimensions, machine)])
        {
            Machine = machine,
            Load = load,
        });
    }

    /// <summary>The suite's halves. The rule is <see cref="ArmVerdict.Halves"/>, shared with the arm
    /// comparison: one implementation of a split, or two reports disagree about which half a question is in.
    /// </summary>
    private static SplitHalves Halves(string suiteStamp, IReadOnlyList<string> questions) =>
        ArmVerdict.Halves(suiteStamp, questions);


    private static async Task<DimensionReport> DimensionAsync(
        IResultStore results,
        Guid runId,
        ReportDimension dimension,
        SplitHalves halves,
        RunReportRequest request,
        CancellationToken cancellationToken)
    {
        var whole = await results.AverageByAsync(
            runId, dimension, request.MetricName, QuestionScope.All, cancellationToken);
        var selection = await results.AverageByAsync(
            runId, dimension, request.MetricName, QuestionScope.Only(halves.Selection), cancellationToken);
        var heldOut = await results.AverageByAsync(
            runId, dimension, request.MetricName, QuestionScope.Only(halves.HeldOut), cancellationToken);

        var baseline = Baseline(request.Baseline, whole);
        var arms = whole
            .Select(arm => Arm(arm, Reading(selection, arm.Dimension), Reading(heldOut, arm.Dimension), baseline, selection, heldOut))
            .ToList();

        return new DimensionReport(dimension, arms, baseline, Refusal(whole, request.MinLegs));
    }

    /// <summary>The arm every other is read against.
    /// <para>
    /// An operator's choice when they made one; otherwise the CONTROL ARM — the leg that named no variant,
    /// whose canonical mark is <c>"-"</c>. Nothing else is nominated: a report that picks a baseline by
    /// score would define the winner into existence, and one that picks alphabetically would be arbitrary
    /// while looking deliberate.
    /// </para></summary>
    private static string Baseline(string requested, IReadOnlyList<MetricByDimension> arms) =>
        (requested.Length > 0, arms.Any(a => a.Dimension == ControlArm)) switch
        {
            (true, _) => requested,
            (_, true) => ControlArm,
            _ => string.Empty,
        };

    private static ArmReading Arm(
        MetricByDimension arm,
        HalfReading selection,
        HalfReading heldOut,
        string baseline,
        IReadOnlyList<MetricByDimension> onSelection,
        IReadOnlyList<MetricByDimension> onHeldOut)
    {
        var verdict = Proof(arm.Dimension, baseline, selection, heldOut, Reading(onSelection, baseline), Reading(onHeldOut, baseline));

        return new ArmReading(arm.Dimension, arm.Average, arm.Legs, selection, heldOut, verdict.State, verdict.Margin);
    }

    /// <summary>The verdict. Lives in <see cref="ArmVerdict"/> because the arm comparison asks the same
    /// question of a different population -- one run's variants against several runs' compute backends -- and a
    /// second copy of a rule about FALSE WINNERS is the one this repository can least afford to let drift.
    /// </summary>
    private static (ProofState State, double Margin) Proof(
        string arm,
        string baseline,
        HalfReading selection,
        HalfReading heldOut,
        HalfReading baselineSelection,
        HalfReading baselineHeldOut) =>
        ArmVerdict.Of(arm, baseline, selection, heldOut, baselineSelection, baselineHeldOut);


    private static HalfReading Reading(IReadOnlyList<MetricByDimension> half, string arm) =>
        half.Where(x => x.Dimension == arm)
            .Select(x => HalfReading.Of(x.Average, x.Legs))
            .DefaultIfEmpty(HalfReading.Unmeasured)
            .First();

    /// <summary>Why these arms may not be ranked, or empty when they may.
    /// <para>
    /// The averages are printed either way. A mean over two legs and a mean over two hundred are different
    /// claims — which is what <c>MetricByDimension.Legs</c> was carried for and what nothing read until
    /// now — and the answer to a thin one is more repeats, a decision that belongs to the operator rather
    /// than to a suppressed table.
    /// </para></summary>
    private static string Refusal(IReadOnlyList<MetricByDimension> arms, int minLegs)
    {
        var thin = arms.Where(a => a.Legs < minLegs).ToList();

        return (arms.Count < 2, thin.Count > 0) switch
        {
            (true, _) => arms.Count == 0 ? "no leg of this run carries that metric" : "one arm is not a comparison",
            (_, true) => $"ranked on nothing: {string.Join(", ", thin.Select(a => $"{a.Dimension} has {a.Legs} leg(s)"))}"
                + $" — fewer than the {minLegs} this comparison asks for",
            _ => string.Empty,
        };
    }

    private static async Task<(IReadOnlyList<QuestionSpread> Questions, IReadOnlyList<string> Models)> SpreadAsync(
        IResultStore results, Guid runId, string metricName, CancellationToken cancellationToken)
    {
        var rates = await results.PassRateByQuestionAndSubjectAsync(runId, metricName, cancellationToken);

        var questions = rates
            .GroupBy(r => r.QuestionId, StringComparer.Ordinal)
            .Select(g => QuestionSpread.Of(
                g.Key,
                g.ToDictionary(r => r.SubjectModelId, r => r.PassRate, StringComparer.Ordinal)))
            .ToList();

        return (questions, [.. rates.Select(r => r.SubjectModelId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    private static IEnumerable<string> Warnings(
        RunScoreboard scoreboard, SplitHalves halves, IReadOnlyList<DimensionReport> dimensions, MachineFacts machine)
    {
        // First, because it changes how every number below it reads. A card at ConfigManagerErrorCode 31 is
        // still enumerated while everything silently runs on the CPU: the numbers are real and they are CPU
        // numbers wearing a GPU's label.
        foreach (var faulted in machine.UnhealthyAdapters)
        {
            yield return
                $"{faulted.Describe} — this run's numbers may have been produced on the CPU, whatever the arm says";
        }

        if (!machine.Recorded)
        {
            yield return
                "the machine that produced these numbers was not recorded, so nothing here can say which host, "
                + "driver or operating system they belong to";
        }

        if (scoreboard.Scored == 0)
        {
            yield return "no leg of this run has been scored — there is nothing here to compare";
        }

        if (halves.Selection.Count == 0 || halves.HeldOut.Count == 0)
        {
            yield return
                $"the split is one-sided ({halves.Selection.Count} selection, {halves.HeldOut.Count} held out) — "
                + "no winner can be confirmed, because confirming one means surviving the half that did not choose it";
        }

        if (dimensions.Any(d => d.Arms.Count > 1 && d.Baseline.Length == 0))
        {
            yield return
                "no baseline was stated and this run has no control arm, so the arms are reported side by side "
                + "and none is called a winner — naming one by score would define the result into existence";
        }
    }

    /// <summary>The canonical mark of a leg that named no variant — <c>VariantSelection.NotApplicable</c>'s
    /// own. Written here rather than as a literal at each use so the report and a leg identity cannot drift
    /// into naming the control arm two different things.</summary>
    private const string ControlArm = "-";

}
