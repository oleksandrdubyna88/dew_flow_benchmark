using Bench.Domain.Runs;

namespace Bench.Domain.Scouts;

/// <summary>One scout campaign — the cells it planned, the pins it is frozen to, its seed, and its spend ceiling.
/// <para>
/// <b>No status column</b>, as for the probes (their D3): open or finished is read off the cells (<see cref="ScoutRunProgress"/>), so a
/// re-run of a void cell appends a generation instead of reopening a finished run. The pins are FROZEN here, and a change of any of
/// them mid-run stops the run (<see cref="ScoutPins.Continue"/>). The ledger lives on the run row, moved only by guarded updates.
/// </para></summary>
public sealed record ScoutRun(
    Guid Id,
    DateTimeOffset CreatedAt,
    ScoutPins Pins,
    int Seed,
    int Repeats,
    IReadOnlyList<ScoutArmId> Arms,
    IReadOnlyList<ScoutTaskId> Tasks,
    SpendLedger Ledger)
{
    /// <summary>Each arm's budget cap and wall (§3.4, §3.10) — FROZEN with the run, so a resume launches under the caps the run was
    /// planned with, never under whatever a later invocation typed. Empty only on a run that has not been given its budgets.</summary>
    public IReadOnlyList<ScoutArmBudget> Budgets { get; init; } = [];

    /// <summary>What the run may conclude (D7) — frozen with it. A preliminary run's report carries its intervals and no verdict.</summary>
    public ScoutRunKind Kind { get; init; } = ScoutRunKind.Preliminary;

    /// <summary>The arm's budget, or a refusal naming it — a cell is never launched under a default cap.</summary>
    public Outcome<ScoutArmBudget> BudgetOf(ScoutArmId arm) =>
        Budgets.FirstOrDefault(b => b.Arm == arm) is { } budget
            ? Outcome<ScoutArmBudget>.Success(budget)
            : Outcome<ScoutArmBudget>.Failure($"scout run {Id} has no budget for arm {ScoutArms.Word(arm)} — a cell is never launched under a default cap");

    /// <summary>A run before any money moved. Refused: a ceiling that is not positive, no task, no arm, a task or an arm twice, and
    /// fewer than one repeat.</summary>
    public static Outcome<ScoutRun> Planned(
        Guid id, DateTimeOffset now, ScoutPins pins, int seed, int repeats, IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutTaskId> tasks, decimal ceilingUsd)
    {
        var refusal = Refusal(repeats, arms, tasks, ceilingUsd);

        return refusal.Length > 0
            ? Outcome<ScoutRun>.Failure(refusal)
            : Outcome<ScoutRun>.Success(new ScoutRun(id, now, pins, seed, repeats, [.. arms], [.. tasks], SpendLedger.Empty(ceilingUsd)));
    }

    private static string Refusal(int repeats, IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutTaskId> tasks, decimal ceilingUsd) =>
        (ceilingUsd > 0m, repeats >= 1) switch
        {
            (false, _) => $"a spend ceiling of ${ceilingUsd} is not a ceiling — a scout run spends money and must be capped",
            (_, false) => $"repeats must be at least 1, got {repeats}",
            _ => AxesRefusal(arms, tasks),
        };

    private static string AxesRefusal(IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutTaskId> tasks) =>
        (arms.Count > 0 && arms.Distinct().Count() == arms.Count, tasks.Count > 0 && tasks.Select(t => t.Value).Distinct(StringComparer.Ordinal).Count() == tasks.Count) switch
        {
            (false, _) => "a scout run needs at least one arm, each named once",
            (_, false) => "a scout run needs at least one task, each named once",
            _ => string.Empty,
        };
}

/// <summary>Where a run stands, read off its cells.</summary>
public sealed record ScoutRunProgress(int Pending, int Claimed, int Settled, int Abandoned)
{
    public bool IsOpen => Pending + Claimed > 0;

    public static ScoutRunProgress Of(IEnumerable<ScoutCell> cells)
    {
        var states = cells.Select(c => c.State).ToList();

        return new ScoutRunProgress(
            states.Count(s => s == CellState.Pending),
            states.Count(s => s == CellState.Claimed),
            states.Count(s => s == CellState.Settled),
            states.Count(s => s == CellState.Abandoned));
    }
}
