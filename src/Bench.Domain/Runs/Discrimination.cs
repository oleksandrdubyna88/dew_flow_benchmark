namespace Bench.Domain.Runs;

/// <summary>How one question behaved across the subjects that have attempted it — a pass rate per model,
/// accumulated over runs.
/// <para>
/// <b>Nothing here deletes a question.</b> Discrimination is not a property of a question; it is a
/// property of a question against a particular set of SUBJECTS. What a frontier model finds trivial may
/// be the hardest item in the set for a local 7B, and pruning by what saturates the strongest models
/// deletes exactly the range in which cheaper models still differ — which is where the commercially
/// interesting question lives, given that the cheap model on the cheap engine bought ~90 % of the
/// expensive result for ~20 % of the money (research/MEASURED_LESSONS.md §4).
/// </para></summary>
public sealed record QuestionSpread(string QuestionId, IReadOnlyDictionary<string, double> PassRateByModel)
{
    public static QuestionSpread Of(string questionId, IReadOnlyDictionary<string, double> passRateByModel) =>
        new(questionId, passRateByModel);

    /// <summary>Models in this comparison that have never attempted the question. They are reported, not
    /// counted as failures: an absent measurement and a score of zero are different facts, and folding
    /// one into the other invents data.</summary>
    public IReadOnlyList<string> Unmeasured(IReadOnlyCollection<string> models) =>
        [.. models.Where(m => !PassRateByModel.ContainsKey(m)).Order(StringComparer.Ordinal)];

    /// <summary>The gap between the best and the worst subject IN THIS COMPARISON. Refused below two
    /// measured subjects, because a spread needs two ends.</summary>
    public Outcome<double> SpreadAcross(IReadOnlyCollection<string> models)
    {
        var measured = Measured(models);

        return measured.Count < 2
            ? Outcome<double>.Failure(
                $"question '{QuestionId}' has {measured.Count} measured subject(s) of the {models.Count} compared — a spread needs two ends")
            : Outcome<double>.Success(measured.Values.Max() - measured.Values.Min());
    }

    /// <summary>Whether this question can separate the subjects of this comparison at all.</summary>
    public Outcome<Verdict> Judge(IReadOnlyCollection<string> models, double minSpread) =>
        SpreadAcross(models).Match(
            spread => Outcome<Verdict>.Success(Classify(Measured(models), spread, minSpread)),
            Outcome<Verdict>.Failure);

    /// <summary>The label the operator asked for — <em>"saturated at this tier and above"</em>. A
    /// statistic about the question, never a verdict on it.
    /// <para>
    /// Tiers arrive as DATA rather than as an ordering baked into this file. Adding a model must not be a
    /// code change, for the same reason models and lanes are sets rather than fields.
    /// </para></summary>
    public SaturationLabel SaturatedAt(IReadOnlyDictionary<string, int> tierByModel)
    {
        var ranks = PassRateByModel.Keys
            .Where(tierByModel.ContainsKey)
            .Select(m => tierByModel[m])
            .Distinct()
            .Order()
            .ToList();

        foreach (var rank in ranks)
        {
            var atOrAbove = PassRateByModel
                .Where(kv => tierByModel.TryGetValue(kv.Key, out var tier) && tier >= rank)
                .ToList();

            if (atOrAbove.Count > 0 && atOrAbove.All(kv => kv.Value >= 1.0))
            {
                return new SaturationLabel(true, rank, $"saturated at tier {rank} and above");
            }
        }

        return new SaturationLabel(false, 0, "not saturated for any measured tier");
    }

    private Dictionary<string, double> Measured(IReadOnlyCollection<string> models) =>
        PassRateByModel.Where(kv => models.Contains(kv.Key)).ToDictionary(StringComparer.Ordinal);

    private static Verdict Classify(Dictionary<string, double> measured, double spread, double minSpread) =>
        (spread >= minSpread, measured.Values.All(v => v >= 1.0), measured.Values.All(v => v <= 0.0)) switch
        {
            (true, _, _) => Verdict.Discriminates,
            (_, true, _) => Verdict.EveryonePasses,
            (_, _, true) => Verdict.NobodyPasses,
            _ => Verdict.TooClose,
        };

    public enum Verdict
    {
        /// <summary>Usable for ranking these subjects.</summary>
        Discriminates,

        /// <summary>Trivial for everyone here. Kept, and likely central to a comparison of weaker models.</summary>
        EveryonePasses,

        /// <summary>Nobody here passes — which is a signal about the QUESTION or the harness at least as
        /// often as it is a signal about the subjects, and is therefore never merged with the row above.</summary>
        NobodyPasses,

        /// <summary>Measured, separated, but by less than the comparison asked for.</summary>
        TooClose,
    }
}

/// <param name="Rank">The lowest tier at which this question is trivial for everything at that tier and above.</param>
public readonly record struct SaturationLabel(bool IsSaturated, int Rank, string Describe);

/// <param name="Discriminating">Questions usable for ranking THESE subjects.</param>
/// <param name="EveryonePasses">Kept for weaker comparisons rather than retired.</param>
/// <param name="NobodyPasses">Worth a look before the next run: a question nobody answers is as likely to be broken as hard.</param>
/// <param name="TooClose">Separated by less than the comparison's floor.</param>
/// <param name="Unusable">Fewer than two measured subjects — no spread could be computed.</param>
public readonly record struct DiscriminationReport(
    int Discriminating, int EveryonePasses, int NobodyPasses, int TooClose, int Unusable)
{
    public int Total => Discriminating + EveryonePasses + NobodyPasses + TooClose + Unusable;

    public string Describe =>
        $"{Discriminating} of {Total} discriminate · {EveryonePasses} trivial here · {NobodyPasses} unanswered · "
        + $"{TooClose} too close · {Unusable} unmeasured";
}

/// <summary>Reading a whole set against ONE comparison's subjects.</summary>
public static class Discrimination
{
    /// <summary>The default floor. Not zero: with repeats a pass rate is fractional, and a gap smaller
    /// than this is inside the noise that repeat spread was measured to produce.</summary>
    public const double DefaultMinSpread = 0.25;

    public static DiscriminationReport Over(
        IReadOnlyList<QuestionSpread> questions, IReadOnlyCollection<string> models, double minSpread = DefaultMinSpread)
    {
        var report = new DiscriminationReport();

        foreach (var question in questions)
        {
            report = Add(report, question.Judge(models, minSpread));
        }

        return report;
    }

    /// <summary>The questions a report may actually rank on — and only those. A ranking that quietly
    /// includes questions every subject passed is a ranking diluted by items that voted for nobody.</summary>
    public static IReadOnlyList<QuestionSpread> Usable(
        IReadOnlyList<QuestionSpread> questions, IReadOnlyCollection<string> models, double minSpread = DefaultMinSpread) =>
        [.. questions.Where(q => q.Judge(models, minSpread) is Outcome<QuestionSpread.Verdict>.Ok
        {
            Value: QuestionSpread.Verdict.Discriminates,
        })];

    private static DiscriminationReport Add(DiscriminationReport report, Outcome<QuestionSpread.Verdict> verdict) =>
        verdict switch
        {
            Outcome<QuestionSpread.Verdict>.Ok { Value: QuestionSpread.Verdict.Discriminates } =>
                report with { Discriminating = report.Discriminating + 1 },
            Outcome<QuestionSpread.Verdict>.Ok { Value: QuestionSpread.Verdict.EveryonePasses } =>
                report with { EveryonePasses = report.EveryonePasses + 1 },
            Outcome<QuestionSpread.Verdict>.Ok { Value: QuestionSpread.Verdict.NobodyPasses } =>
                report with { NobodyPasses = report.NobodyPasses + 1 },
            Outcome<QuestionSpread.Verdict>.Ok => report with { TooClose = report.TooClose + 1 },
            _ => report with { Unusable = report.Unusable + 1 },
        };
}
