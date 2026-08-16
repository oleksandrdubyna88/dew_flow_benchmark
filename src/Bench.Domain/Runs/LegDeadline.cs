namespace Bench.Domain.Runs;

/// <summary>ONE deadline for a whole leg, and the remaining time each call inside it may spend.
/// <para>
/// <b>A per-call wall is not a budget; it is a budget multiplied by a number nobody bounded.</b> The
/// ceiling a runtime imposes applies once per completion — so a lane that answers in one call costs at
/// most the wall, and a lane that loops twenty-five times costs twenty-five walls. Measured against the
/// defaults this repository shipped with, that is 10 minutes against 4 h 10 m for a single hanging
/// endpoint, on a campaign whose premise is running unattended for weeks.
/// </para>
/// <para>
/// So the deadline is computed ONCE, when the leg starts, and every call is handed
/// <see cref="ForCall"/> — the same ceiling narrowed to what is actually left. A loop checks
/// <see cref="Exhausted"/> between turns and settles <see cref="Cap"/>, which is a recorded outcome the
/// campaign continues past rather than a crash or a silent truncation.
/// </para>
/// <para>
/// Unbounded is a legal state and stays visible as one: a plan that names no wall ceiling gets a
/// deadline that never fires, rather than a zero that would cap every leg the moment it started.
/// </para></summary>
/// <param name="Budgets">Every ceiling of the leg, so a call gets the whole list rather than the wall alone.</param>
/// <param name="Wall">The wall ceiling itself — kept so <see cref="Cap"/> can name what stopped the leg.</param>
/// <param name="StartedAt">When the leg's clock started. The reached figure is measured from here.</param>
/// <param name="At">When the leg must be over. <see cref="DateTimeOffset.MaxValue"/> when unbounded.</param>
public sealed record LegDeadline(
    IReadOnlyList<Budget> Budgets,
    Budget Wall,
    DateTimeOffset StartedAt,
    DateTimeOffset At)
{
    /// <summary>The deadline of a leg whose plan names a wall ceiling — and of one that does not.</summary>
    public static LegDeadline For(IReadOnlyList<Budget> budgets, DateTimeOffset startedAt)
    {
        var wall = budgets.FirstOrDefault(b => b.Kind == BudgetKind.Wall);

        return wall is null || wall.Limit <= 0
            ? new LegDeadline(budgets, Budget.Of(BudgetKind.Wall, BudgetScope.Question, 0), startedAt, DateTimeOffset.MaxValue)
            : new LegDeadline(budgets, wall, startedAt, startedAt.AddSeconds((double)wall.Limit));
    }

    public bool IsBounded => At < DateTimeOffset.MaxValue;

    /// <summary>What the leg has left. Never negative: an overrun is <see cref="Exhausted"/>, and a
    /// negative ceiling handed to a runtime is an argument exception wearing a budget.</summary>
    public TimeSpan Remaining(DateTimeOffset now) =>
        !IsBounded ? TimeSpan.MaxValue : At > now ? At - now : TimeSpan.Zero;

    public bool Exhausted(DateTimeOffset now) => IsBounded && now >= At;

    /// <summary>The budgets for the NEXT call: the same list, with the wall narrowed to the remainder.
    /// <para>
    /// This is the whole mechanism. A runtime reads the wall entry to set its own transport timeout, so
    /// handing it the remainder rather than the original limit is what makes twenty-five turns share one
    /// ceiling instead of each starting a fresh one.
    /// </para></summary>
    public IReadOnlyList<Budget> ForCall(DateTimeOffset now) =>
        !IsBounded
            ? Budgets
            : [.. Budgets.Select(b => b.Kind == BudgetKind.Wall
                ? b with { Limit = (decimal)Remaining(now).TotalSeconds }
                : b)];

    /// <summary>How a leg that spent its wall ends: a CAP naming the ceiling and what it reached.</summary>
    public LegOutcome Cap(DateTimeOffset now) =>
        new LegOutcome.CapExceeded(
            BudgetKind.Wall,
            Wall.Scope,
            Wall.Limit,
            Math.Round((decimal)(now - StartedAt).TotalSeconds, 1));

    public string Describe => IsBounded ? $"{Wall.Limit}s" : "no wall ceiling";
}
