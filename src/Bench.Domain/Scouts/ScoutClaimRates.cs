namespace Bench.Domain.Scouts;

/// <summary>Wrong claims and assessed claims, for one scout, one team or one run.</summary>
public sealed record ClaimTally(int Wrong, int Assessed)
{
    public static ClaimTally Zero { get; } = new(0, 0);

    public ClaimTally Plus(ClaimTally other) => new(Wrong + other.Wrong, Assessed + other.Assessed);
}

/// <summary>A wrong-claim rate per 10 claims, or the fact that there is none: a team with zero assessed claims has NO rate, and that
/// is never a zero (§3.6).</summary>
public sealed record ClaimRate(bool Measured, decimal PerTen)
{
    public static ClaimRate NotMeasured { get; } = new(false, 0m);
}

/// <summary>The wrong-claim arithmetic of §3.6, POOLED everywhere: a team's rate is Σ wrong ÷ Σ assessed × 10 over its scouts — a ratio
/// over the team's claims, not a mean of per-scout rates — and a task's rate is pooled the same way over its runs, so a run with one
/// claim does not weigh as much as a run with ten.</summary>
public static class ScoutClaimRates
{
    /// <summary>Σ wrong ÷ Σ assessed × 10 over the tallies; not measured when nothing was assessed.</summary>
    public static ClaimRate Pooled(IEnumerable<ClaimTally> tallies)
    {
        var total = tallies.Aggregate(ClaimTally.Zero, (sum, t) => sum.Plus(t));

        return total.Assessed == 0 ? ClaimRate.NotMeasured : new ClaimRate(true, (decimal)total.Wrong / total.Assessed * 10m);
    }
}
