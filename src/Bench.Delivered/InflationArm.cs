namespace Bench.Delivered;

/// <summary>One side of the arm: what a diff measured and what it scored.</summary>
public sealed record ArmReading(string Name, int CleanedLoc, int Score, int StepsScoredZero, int Steps);

/// <param name="Exponent">log(score ratio) / log(cleaned-LOC ratio). The one number the arm exists to
/// produce: at 0 volume buys nothing, at 1 score is proportional to size, and the source measured 0.615
/// across honest changes where size and scope moved together.</param>
/// <param name="ScoreRatio">What ten times the lines actually bought.</param>
public sealed record InflationVerdict(
    double Exponent,
    double ScoreRatio,
    double LocRatio,
    bool VolumeBoughtNothing,
    bool PaddingScoredZero,
    bool HonestScoreHeld,
    string Note)
{
    /// <summary>Whether the inherited badge may be retired. All three, deliberately: an exponent at zero
    /// with the honest score collapsed would mean the scale had stopped paying for anything, which is a
    /// different failure wearing a passing number.</summary>
    public bool Passed => VolumeBoughtNothing && PaddingScoredZero && HonestScoreHeld;

    public string Describe =>
        $"x{LocRatio:0.#} the lines bought x{ScoreRatio:0.00} the score (exponent {Exponent:+0.00;-0.00;0.00}) — "
        + (Passed ? "volume bought nothing" : $"NOT settled: {Note}");
}

/// <summary>The frozen-arms method: build the same change twice, once padded, and ask what the padding
/// bought.
///
/// <para><b>Why an arm rather than a correlation.</b> Fitting score against size across real changes cannot
/// separate the two — big changes ARE usually bigger work, and the source's own 0.615 exponent across 22
/// honest pull requests could not be attributed to either. Here the work is <em>identical by
/// construction</em>: the padded arm contains the same real change plus generated code that cannot alter
/// what it does. So whatever the exponent turns out to be, volume alone bought it.</para>
///
/// <para><b>Three conditions, all required.</b> An exponent at zero is not enough on its own — a scale that
/// had stopped paying for anything would produce one. The padded steps must actually land on zero (the band
/// discriminating rather than firing everywhere), and the honest arm's own score must hold (the band not
/// quietly deflating real work). The source's third check is the one most easily forgotten and the reason
/// it re-ran its honest arms rather than reusing their old scores.</para>
/// </summary>
public static class InflationArm
{
    /// <summary>An exponent at or below this counts as "volume bought nothing". Not zero exactly: the
    /// measurement has sampling noise, and demanding a negative number would fail an instrument that
    /// resisted inflation perfectly.
    /// <para><b>Inherited:</b> the source's passing arm measured −0.06.</para></summary>
    public const double NeutralExponent = 0.05;

    /// <summary>How far the honest arm's own score may move and still count as held.
    /// <para><b>Inherited:</b> the source's honest arms moved 51.7 → 48.6, about 6 %, and it called that
    /// "did not quietly deflate real work".</para></summary>
    public const double HonestTolerance = 0.15;

    /// <param name="honest">The real change, scored alone.</param>
    /// <param name="padded">The same change plus generated padding.</param>
    /// <param name="honestBefore">The honest arm's score under the PREVIOUS protocol, when there is one.
    /// Zero means there is nothing to compare against and that check is reported as unverified rather than
    /// passed — the same three-state honesty the rest of this codebase uses for an absent fact.</param>
    public static InflationVerdict Measure(ArmReading honest, ArmReading padded, int honestBefore = 0)
    {
        if (honest.CleanedLoc <= 0 || honest.Score <= 0 || padded.CleanedLoc <= honest.CleanedLoc)
        {
            return Unmeasurable(honest, padded);
        }

        var locRatio = (double)padded.CleanedLoc / honest.CleanedLoc;
        var scoreRatio = (double)padded.Score / honest.Score;
        var exponent = Math.Log(scoreRatio) / Math.Log(locRatio);

        var neutral = exponent <= NeutralExponent;
        var zeroed = padded.StepsScoredZero > honest.StepsScoredZero;
        var held = honestBefore <= 0
            || Math.Abs(honest.Score - honestBefore) / (double)honestBefore <= HonestTolerance;

        return new InflationVerdict(
            exponent, scoreRatio, locRatio, neutral, zeroed, held,
            Note(neutral, zeroed, held, honestBefore));
    }

    private static InflationVerdict Unmeasurable(ArmReading honest, ArmReading padded) =>
        new(
            0, 0, 0, false, false, false,
            honest.CleanedLoc <= 0 ? "the honest arm has no cleaned lines to compare against"
            : honest.Score <= 0 ? "the honest arm scored zero, so a ratio against it means nothing"
            : $"the padded arm is not larger ({padded.CleanedLoc} against {honest.CleanedLoc} cleaned lines)");

    private static string Note(bool neutral, bool zeroed, bool held, int honestBefore)
    {
        string[] failures =
        [
            .. neutral ? [] : (string[])[$"volume still bought score above the {NeutralExponent:0.00} exponent"],
            .. zeroed ? [] : (string[])["the padded steps were not scored zero, so the band is not discriminating"],
            .. held ? [] : (string[])["the honest arm's own score moved too far — the band may be deflating real work"],
        ];

        return failures.Length > 0
            ? string.Join("; ", failures)
            : honestBefore <= 0
                ? "volume bought nothing; the honest arm had no earlier score to hold against, so that leg is UNVERIFIED"
                : "volume bought nothing";
    }
}
