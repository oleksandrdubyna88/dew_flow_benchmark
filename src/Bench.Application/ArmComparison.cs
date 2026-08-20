using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;

namespace Bench.Application;

/// <param name="MetricName">Required, with no default — the same refusal <see cref="RunReport"/> makes, for
/// the same reason: a metric chosen for the reader makes every number on screen about that choice.</param>
/// <param name="Runs">How far back to look. A cap rather than a filter, so what was left out is countable.</param>
/// <param name="MinLegs">Below this an arm's average is shown and the RANKING withheld. Two, because one leg
/// is a sample and this is the number the run report already uses.</param>
/// <param name="Baseline">The arm the others are read against. Empty means no verdict at all — this never
/// nominates one by score, because an arm that is the baseline BECAUSE it won cannot then be beaten.</param>
public sealed record ArmComparisonRequest(
    string MetricName,
    int Runs = 50,
    int MinLegs = 2,
    string Baseline = "");

/// <summary>One compute arm, folded across every run measured on it inside one scope.</summary>
/// <param name="Runs">How many runs contributed. Beside the leg count because an average over one run and an
/// average over five are different evidence about a backend, and a reader holding only the mean cannot
/// tell.</param>
public sealed record ArmAverage(
    string Arm,
    double Average,
    int Legs,
    int Runs,
    HalfReading Selection,
    HalfReading HeldOut,
    ProofState Proof,
    double Margin)
{
    /// <summary>What this arm COST. A quality average alone answers half the question: two backends that
    /// return the same files in 2.6 s and 16.4 s score identically and are not the same measurement.</summary>
    public ArmCost Cost { get; init; } = ArmCost.None;
}

/// <summary>The cost side of one arm, folded across its legs.</summary>
/// <param name="RetrievalMs">MEAN of the engine's own per-leg figure, not a total: legs differ in number
/// between arms, so a sum would rank the arm that ran more.</param>
/// <param name="WallSeconds">Total time those legs were open. Mostly the model answering rather than the
/// engine retrieving, which is why it never stands in for <paramref name="RetrievalMs"/>.</param>
/// <param name="PeakVramBytes">The highest reading any of this arm's legs saw, with the sample count that
/// says how thin the evidence is. Zero samples is NOT zero bytes.</param>
public readonly record struct ArmCost(
    double RetrievalMs,
    double WallSeconds,
    double Hits,
    double Files,
    long PeakVramBytes,
    int VramSamples)
{
    public static ArmCost None { get; } = new(0, 0, 0, 0, 0, 0);
}

/// <summary>The arms inside ONE comparison scope — one target, one suite stamp.
/// <para>
/// The scope is not decoration. It is the pair that must be identical before two numbers may be put beside
/// each other, and mixing it is the most expensive mistake this project has on record: a series measured over
/// weeks turned out to have been scored against a corpus rebuilt underneath it, which demoted every
/// conclusion back to a hypothesis.
/// </para></summary>
/// <param name="RankingRefusal">Empty when these arms may be ranked. Non-empty when they may not, naming
/// what decided it. The averages are present either way.</param>
public sealed record ScopedArms(
    string TargetCanonical,
    string SuiteStamp,
    IReadOnlyList<ArmAverage> Arms,
    string Baseline,
    string RankingRefusal)
{
    public string ScopeDescribe => $"{TargetCanonical} / {SuiteStamp}";
}

/// <param name="Excluded">Runs left out, each with the reason. A comparison that silently dropped rows would
/// be a comparison of an unknown population.</param>
/// <param name="Refusal">Empty when at least one scope holds two arms. Otherwise the reason there is nothing
/// to compare, which is a different piece of news from "these cannot be compared".</param>
public sealed record ArmComparisonView(
    string MetricName,
    IReadOnlyList<ScopedArms> Scopes,
    IReadOnlyList<string> Excluded,
    string Refusal);

/// <summary>Comparing the compute backends — the question this benchmark was pointed at, answered at last
/// with the guard that makes the answer worth anything.
///
/// <para><b>Why it must be cross-run, and why that was deferred until now.</b> The backend is recorded on the
/// RUN (<c>EngineRef.Backend</c>), because a run is measured against one engine on one machine. So two arms
/// are two runs, and <see cref="RunReport"/> — single-run by construction — cannot see them both.
/// <c>research/PLAN_run_report.md</c> §3.9 excluded this deliberately rather than by omission: two runs are
/// only comparable inside one <see cref="ComparisonScope"/>, and a comparison that ignored the scope would
/// produce numbers indistinguishable from a real result.</para>
///
/// <para><b>The scope is applied by PARTITIONING, not by refusing.</b> An earlier sketch refused whenever the
/// runs spanned more than one scope, which is correct and useless: nine real runs here span three suites and
/// two targets, so the honest answer would have been a permanent blank. Instead each scope becomes its own
/// comparison, and the ones with a single arm say so. Nothing crosses a scope boundary.</para>
///
/// <para><b>An undeclared backend is excluded and counted, never folded in.</b> A run whose engine echoed
/// nothing cannot be attributed to an arm; putting it in one would attach a measurement to hardware nobody
/// showed did the work — the error indistinguishable from a correct result afterwards. It is reported as an
/// exclusion with its reason, so the population is knowable.</para>
///
/// <para>Every judgement it makes about winners is <see cref="ArmVerdict"/>, shared with the run report.
/// There is one rule about false winners in this codebase.</para>
/// </summary>
public static class ArmComparison
{
    public const string NoMetricNamed = RunReport.NoMetricNamed;

    public const string NothingDeclared =
        "no run carries a compute backend, so there is no arm to compare. An engine has to ECHO what it "
        + "computed on — a healthy sidecar reporting its host, provider and device — before a run can be "
        + "attributed to one.";

    public const string OneArmOnly =
        "every run that declared a backend declared the SAME one, so there is nothing to put beside it. "
        + "Measure the same target and suite on a second backend and both will appear here.";

    public static async Task<Outcome<ArmComparisonView>> BuildAsync(
        IRunStore runs,
        IResultStore results,
        ArmComparisonRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MetricName.Length == 0)
        {
            return Outcome<ArmComparisonView>.Failure(NoMetricNamed);
        }

        var recent = await runs.RecentAsync(request.Runs, cancellationToken);
        var declared = recent.Where(Declares).ToList();
        var excluded = Excluded(recent);

        var scopes = new List<ScopedArms>();

        foreach (var group in declared.GroupBy(run => run.Scope))
        {
            scopes.Add(await ScopeAsync(results, group.Key, [.. group], request, cancellationToken));
        }

        return Outcome<ArmComparisonView>.Success(new ArmComparisonView(
            request.MetricName,
            [.. scopes.OrderByDescending(scope => scope.Arms.Count).ThenBy(scope => scope.SuiteStamp, StringComparer.Ordinal)],
            excluded,
            Refuse(declared.Count, scopes)));
    }

    /// <summary>The metrics the ARMS actually carry — what a console may offer.
    /// <para>
    /// Scoped to the same population the comparison folds: runs that declared a backend. A page offering a
    /// metric no arm measured leads to an empty table and reads as a broken run rather than as a metric
    /// nobody recorded, and the fix-lane and tool-lane names would do exactly that beside a set of retrieval
    /// runs. Asked of the data rather than a published list, because a list cannot know what a population
    /// measured.
    /// </para></summary>
    public static async Task<IReadOnlyList<string>> MetricsAsync(
        IRunStore runs, IResultStore results, int window, CancellationToken cancellationToken)
    {
        var recent = await runs.RecentAsync(window, cancellationToken);

        return await results.MetricNamesAsync([.. recent.Where(Declares).Select(run => run.Id)], cancellationToken);
    }

    private static bool Declares(BenchRun run) => run.Engine.Backend is BackendDeclaration.Declared;

    private static string ArmOf(BenchRun run) => run.Engine.Backend.Canonical;

    /// <summary>The runs left out, grouped by reason rather than listed one per line: "6 runs declared no
    /// backend" is what a reader can act on, where six identical sentences are noise.</summary>
    private static IReadOnlyList<string> Excluded(IReadOnlyList<BenchRun> recent)
    {
        var undeclared = recent.Count(run => !Declares(run));

        return undeclared == 0
            ? []
            : [$"{undeclared} run(s) declared no compute backend and cannot be attributed to an arm"];
    }

    private static async Task<ScopedArms> ScopeAsync(
        IResultStore results,
        ComparisonScope scope,
        IReadOnlyList<BenchRun> runs,
        ArmComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var ids = runs.Select(run => run.Id).ToList();
        var legs = await results.LegsAsync(ids, request.MetricName, cancellationToken);
        var vitals = await results.VitalsAsync(ids, cancellationToken);
        var armOf = runs.ToDictionary(run => run.Id, ArmOf);

        var byArm = legs
            .Where(leg => armOf.ContainsKey(leg.RunId))
            .GroupBy(leg => armOf[leg.RunId], StringComparer.Ordinal)
            .ToList();

        var halves = byArm.ToDictionary(
            group => group.Key,
            group => Halves(scope.SuiteStamp, [.. group]),
            StringComparer.Ordinal);

        var baseline = Baseline(request.Baseline, halves.Keys);

        var costOf = vitals
            .Where(v => armOf.ContainsKey(v.RunId))
            .GroupBy(v => armOf[v.RunId], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Cost([.. g]), StringComparer.Ordinal);

        var arms = byArm
            .Select(group => Average(group.Key, [.. group], halves, baseline, armOf) with
            {
                Cost = costOf.TryGetValue(group.Key, out var cost) ? cost : ArmCost.None,
            })
            .OrderBy(arm => arm.Arm, StringComparer.Ordinal)
            .ToList();

        return new ScopedArms(scope.TargetCanonical, scope.SuiteStamp, arms, baseline, Refusal(arms, request.MinLegs));
    }

    /// <summary>One arm's cost, folded from its legs.
    /// <para>
    /// Retrieval time is a MEAN and wall time a SUM, deliberately: arms differ in leg count, so a mean answers
    /// "what does one retrieval cost here" while a sum would simply rank whichever arm ran more. VRAM is a
    /// PEAK and carries its sample count — a peak from one sample and a peak from thirty are different
    /// claims, and zero samples is not zero bytes.
    /// </para></summary>
    private static ArmCost Cost(IReadOnlyList<LegVitals> legs)
    {
        var sampled = legs.Where(leg => leg.VramSamples > 0).ToList();

        return new ArmCost(
            legs.Average(leg => leg.RetrievalMs),
            legs.Sum(leg => leg.WindowSeconds),
            legs.Average(leg => (double)leg.Hits),
            legs.Average(leg => (double)leg.Files),
            sampled.Count == 0 ? 0 : sampled.Max(leg => leg.PeakVramBytes),
            sampled.Sum(leg => leg.VramSamples));
    }

    /// <summary>A baseline only when it is actually present here. A named arm that this scope never measured
    /// would silently leave every other arm unjudged, which reads as "nothing won" rather than as "your
    /// baseline is not in this comparison".</summary>
    private static string Baseline(string requested, IEnumerable<string> arms) =>
        requested.Length > 0 && arms.Contains(requested, StringComparer.Ordinal) ? requested : string.Empty;

    private static ArmAverage Average(
        string arm,
        IReadOnlyList<MetricLeg> legs,
        IReadOnlyDictionary<string, (HalfReading Selection, HalfReading HeldOut)> halves,
        string baseline,
        IReadOnlyDictionary<Guid, string> armOf)
    {
        var (selection, heldOut) = halves[arm];
        var (baselineSelection, baselineHeldOut) = baseline.Length > 0 && halves.TryGetValue(baseline, out var b)
            ? b
            : (HalfReading.Unmeasured, HalfReading.Unmeasured);

        var verdict = ArmVerdict.Of(arm, baseline, selection, heldOut, baselineSelection, baselineHeldOut);

        return new ArmAverage(
            arm,
            legs.Average(leg => leg.Value),
            legs.Count,
            legs.Select(leg => leg.RunId).Distinct().Count(),
            selection,
            heldOut,
            verdict.State,
            verdict.Margin);
    }

    /// <summary>Both halves of one arm, from its legs.
    /// <para>
    /// The half is derived per LEG from its question id, not from the run's question list, because an arm
    /// here spans runs and only the legs say which questions were actually scored. A half with no legs is
    /// <see cref="HalfReading.Unmeasured"/> and never a zero — an absence and a failure are opposite readings
    /// of the same blank.
    /// </para></summary>
    private static (HalfReading Selection, HalfReading HeldOut) Halves(
        string suiteStamp, IReadOnlyList<MetricLeg> legs)
    {
        var assigned = legs
            .Select(leg => (leg.Value, Half: ArmVerdict.HalfOf(suiteStamp, leg.QuestionId)))
            .Where(x => x.Half is Outcome<SplitHalf>.Ok)
            .Select(x => (x.Value, Half: ((Outcome<SplitHalf>.Ok)x.Half).Value))
            .ToList();

        return (Reading(assigned, SplitHalf.Selection), Reading(assigned, SplitHalf.HeldOut));
    }

    private static HalfReading Reading(IReadOnlyList<(double Value, SplitHalf Half)> assigned, SplitHalf half)
    {
        var mine = assigned.Where(x => x.Half == half).Select(x => x.Value).ToList();

        return mine.Count == 0 ? HalfReading.Unmeasured : HalfReading.Of(mine.Average(), mine.Count);
    }

    /// <summary>Why these arms may not be ranked, or empty when they may.</summary>
    private static string Refusal(IReadOnlyList<ArmAverage> arms, int minLegs)
    {
        var thin = arms.Where(arm => arm.Legs < minLegs).ToList();

        if (arms.Count < 2)
        {
            return "one arm in this scope — nothing to rank it against.";
        }

        return thin.Count == 0
            ? string.Empty
            : $"not ranked: {string.Join(", ", thin.Select(arm => $"{arm.Arm} has {arm.Legs} leg(s)"))}, "
                + $"below the {minLegs} this comparison requires. The averages above are still what was measured.";
    }

    /// <summary>The top-level refusal. Three different pieces of news, never collapsed: nothing echoed a
    /// backend, everything echoed the same one, or there is a comparison to read.</summary>
    private static string Refuse(int declared, IReadOnlyList<ScopedArms> scopes) =>
        (declared, scopes.Any(scope => scope.Arms.Count >= 2)) switch
        {
            (0, _) => NothingDeclared,
            (_, false) => OneArmOnly,
            _ => string.Empty,
        };
}
