using System.Globalization;

namespace Bench.Domain.Scouts;

/// <summary>Money before the first paid call (plan §3.10, E2 S6), pure:
/// <list type="bullet">
/// <item>a PILOT run refuses BY NAME a per-cell cap above <see cref="PilotMaxCapUsd"/>, a wall above <see cref="PilotMaxWallSeconds"/> and a run
/// ceiling above <see cref="PilotMaxCeilingUsd"/> — the pilot is the first paid call, and its bounds are the operator's (§4 E2 S6), not a default;</item>
/// <item>a PRELIMINARY or DECISION run takes its caps and walls from <c>schedule.json</c> only — the pilot's largest settled cost × 1.5 and slowest
/// cell × 2 (<see cref="ScoutArmBudgets.FromPilot"/>) — never from a flag: a budget flag named on such a run is refused by name, a missing
/// schedule is refused, a schedule missing an arm's cap is refused (<see cref="ScoutArmBudgets.FromSchedule"/>).</item>
/// </list>
/// The reservation stays the cap × 1.2 (<see cref="SpendLedger.ReservationFor"/>) whatever the kind.</summary>
public static class ScoutRunLimits
{
    public const decimal PilotMaxCapUsd = 3m;

    public const int PilotMaxWallSeconds = 20 * 60;

    public const decimal PilotMaxCeilingUsd = 60m;

    public const string ScheduleFlag = "--schedule";

    /// <summary>The flags that set a budget from the command line — a pilot's to use, refused by name on any other kind.</summary>
    public static readonly IReadOnlyList<string> BudgetFlags = ["--cap-usd", "--arm-caps", "--cell-timeout-minutes", "--cell-timeout-seconds"];

    /// <summary>Why a pilot may not run under <paramref name="budgets"/> and <paramref name="ceilingUsd"/>, naming the arm and the value against
    /// its bound; empty when every bound holds.</summary>
    public static string PilotRefusal(IReadOnlyList<ScoutArmBudget> budgets, decimal ceilingUsd) =>
        (budgets.FirstOrDefault(b => b.CapUsd > PilotMaxCapUsd), budgets.FirstOrDefault(b => b.WallSeconds > PilotMaxWallSeconds), ceilingUsd > PilotMaxCeilingUsd) switch
        {
            ({ } cap, _, _) => $"a pilot's per-cell cap is at most ${Usd(PilotMaxCapUsd)} — arm {ScoutArms.Word(cap.Arm)} asks ${Usd(cap.CapUsd)}; the pilot is the first paid call and runs under the operator's bounds",
            (_, { } wall, _) => $"a pilot's wall is at most {PilotMaxWallSeconds / 60} min — arm {ScoutArms.Word(wall.Arm)} asks {wall.WallSeconds.ToString(CultureInfo.InvariantCulture)} s",
            (_, _, true) => $"a pilot's run ceiling is at most ${Usd(PilotMaxCeilingUsd)} — ${Usd(ceilingUsd)} was asked",
            _ => string.Empty,
        };

    /// <summary>Why a run of <paramref name="kind"/> may not take its budgets the way it was asked: a budget flag named on a non-pilot run, or
    /// no schedule named; empty when the source is right.</summary>
    public static string SourceRefusal(ScoutRunKind kind, IReadOnlyList<string> budgetFlagsNamed, bool scheduleNamed) =>
        (kind == ScoutRunKind.Pilot, budgetFlagsNamed.Count > 0, scheduleNamed) switch
        {
            (true, _, _) => string.Empty,
            (_, true, _) => $"a {ScoutRunKinds.Word(kind)} run takes its caps and walls from schedule.json only — {string.Join(", ", budgetFlagsNamed)} is not for it; drop the flag, or run --kind pilot",
            (_, _, false) => $"a {ScoutRunKinds.Word(kind)} run needs {ScheduleFlag} <schedule.json> — its caps and walls come from the pilot (bench scouts schedule), never from a flag",
            _ => string.Empty,
        };

    internal static string Usd(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>One attempt that cost MORE than its arm's cap (§3.10 "the CLI's cap is an estimate and can overshoot"): settled at the real total,
/// never clipped, and counted — by <c>status</c>, by <c>report</c> — so the pilot's caps are set from what cells actually cost.</summary>
public sealed record ScoutOvershoot(Guid AttemptId, Guid CellId, ScoutArmId Arm, decimal CapUsd, decimal SettledUsd)
{
    public decimal OverUsd => SettledUsd - CapUsd;
}

public static class ScoutOvershoots
{
    /// <summary>Every SETTLED attempt whose <c>total_cost_usd</c> exceeds the cap of its cell's arm, in the attempts' order. An attempt at exactly
    /// the cap is not one; an unknown charge is not one (it is the reservation, not a measured cost).</summary>
    public static IReadOnlyList<ScoutOvershoot> Of(IReadOnlyList<ScoutAttempt> attempts, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutArmBudget> budgets)
    {
        var arms = cells.ToDictionary(c => c.Id, c => c.Arm);
        var caps = budgets.ToDictionary(b => b.Arm, b => b.CapUsd);

        return [.. attempts
            .Where(a => a.State == ScoutAttemptState.Settled && arms.TryGetValue(a.CellId, out var arm) && caps.TryGetValue(arm, out var cap) && a.SettledUsd > cap)
            .Select(a => new ScoutOvershoot(a.Id, a.CellId, arms[a.CellId], caps[arms[a.CellId]], a.SettledUsd))];
    }
}

/// <summary><c>schedule.json</c> (§3.12): the caps and walls a preliminary or decision run launches under, per arm, derived from a pilot run
/// (<see cref="FromRun"/>) under the campaign string, so a run can refuse a schedule made for another campaign.</summary>
public sealed record ScoutScheduleFile(string Campaign, string FromRun, IReadOnlyList<ScoutArmBudget> Budgets);
