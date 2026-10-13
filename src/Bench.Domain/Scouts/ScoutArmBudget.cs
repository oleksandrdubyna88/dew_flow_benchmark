using System.Globalization;

namespace Bench.Domain.Scouts;

/// <summary>One arm's money and time (§3.4, §3.10): the <c>--max-budget-usd</c> cap its cells launch under — the ledger reserves it plus
/// 20 % (<see cref="SpendLedger.ReservationFor"/>) — and the wall at which a hung cell's whole process tree is killed. Both are fixed per
/// arm in E2 from the pilot (the largest observed cost × 1.5, the slowest cell × 2); until then the verb's defaults apply, and either
/// way they are frozen on the run.</summary>
public sealed record ScoutArmBudget(ScoutArmId Arm, decimal CapUsd, int WallSeconds)
{
    /// <summary>The plan's default wall until E2 fixes one per arm: twenty minutes.</summary>
    public const int DefaultWallSeconds = 20 * 60;

    public TimeSpan Wall => TimeSpan.FromSeconds(WallSeconds);

    public decimal ReservationUsd => SpendLedger.ReservationFor(CapUsd);

    public static Outcome<ScoutArmBudget> Of(ScoutArmId arm, decimal capUsd, int wallSeconds) =>
        (capUsd > 0m, wallSeconds > 0) switch
        {
            (false, _) => Outcome<ScoutArmBudget>.Failure($"arm {ScoutArms.Word(arm)}: a cap of ${capUsd} is not a cap — every cell spends, so every arm is capped"),
            (_, false) => Outcome<ScoutArmBudget>.Failure($"arm {ScoutArms.Word(arm)}: a wall of {wallSeconds}s is not a wall"),
            _ => Outcome<ScoutArmBudget>.Success(new ScoutArmBudget(arm, capUsd, wallSeconds)),
        };
}

/// <summary>The budgets of a run's arms — from the verb's flags on a PILOT (<see cref="For"/>: one default cap for every arm, overridden per arm
/// by <c>A1=1.5,B3=0.9</c>; an override naming an arm the run does not hold, or one that is not a number, is refused by name), from a pilot's
/// settled cells for the schedule (<see cref="FromPilot"/>), and from <c>schedule.json</c> on a preliminary or decision run (<see cref="FromSchedule"/>).</summary>
public static class ScoutArmBudgets
{
    /// <summary>The cap is the largest settled cell cost of the arm × this.</summary>
    public const decimal CapFactor = 1.5m;

    /// <summary>The wall is the slowest settled cell of the arm × this.</summary>
    public const int WallFactor = 2;

    /// <summary>E2 S6: per arm, the largest settled cell cost (the envelope's <c>total_cost_usd</c>) × 1.5, rounded UP to the cent, and the
    /// slowest cell's session (<c>duration_ms</c>) × 2, rounded up to the second. Every settled cell with a captured cost counts — a void cell
    /// spent money too. An arm with no such cell is refused by name: a cap nobody measured is a default, and a cell is never launched under one.</summary>
    public static Outcome<IReadOnlyList<ScoutArmBudget>> FromPilot(IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutCell> settled)
    {
        var measured = settled.Where(c => c.Facts.ReportedUsd.WasCaptured && c.Facts.SessionMs.WasCaptured).ToLookup(c => c.Arm);
        var missing = arms.Where(arm => !measured[arm].Any()).ToList();

        return missing.Count > 0
            ? Outcome<IReadOnlyList<ScoutArmBudget>>.Failure($"arm {string.Join(", ", missing.Select(ScoutArms.Word))} has no settled cell with a reported cost and session in the pilot — a cap nobody measured is a default, and a cell is never launched under one")
            : Collect([.. arms.Select(arm => ScoutArmBudget.Of(arm, PilotCap(measured[arm]), PilotWall(measured[arm])))]);
    }

    /// <summary>The largest reported cost × 1.5, rounded UP to the cent.</summary>
    private static decimal PilotCap(IEnumerable<ScoutCell> cells) => decimal.Ceiling(cells.Max(c => c.Facts.ReportedUsd.Value) * CapFactor * 100m) / 100m;

    /// <summary>The slowest session × 2, rounded up to the second.</summary>
    private static int PilotWall(IEnumerable<ScoutCell> cells) => (int)Math.Ceiling(cells.Max(c => c.Facts.SessionMs.Value) * WallFactor / 1000.0);

    /// <summary>The run's arms taken from a schedule: every arm must be there, by name; an arm the schedule names and the run does not hold is
    /// left where it is.</summary>
    public static Outcome<IReadOnlyList<ScoutArmBudget>> FromSchedule(IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutArmBudget> scheduled)
    {
        var missing = arms.Where(arm => scheduled.All(b => b.Arm != arm)).ToList();

        return missing.Count > 0
            ? Outcome<IReadOnlyList<ScoutArmBudget>>.Failure($"schedule.json carries no cap and wall for arm {string.Join(", ", missing.Select(ScoutArms.Word))} — a preliminary or decision run launches every arm under the pilot's measured budget, never a default")
            : Outcome<IReadOnlyList<ScoutArmBudget>>.Success([.. arms.Select(arm => scheduled.First(b => b.Arm == arm))]);
    }

    public static Outcome<IReadOnlyList<ScoutArmBudget>> For(IReadOnlyList<ScoutArmId> arms, decimal defaultCapUsd, IReadOnlyList<string> overrides, int wallSeconds)
    {
        var parsed = overrides.Select(Override).ToList();

        if (parsed.OfType<Outcome<(ScoutArmId, decimal)>.Fail>().FirstOrDefault() is { } bad)
        {
            return Outcome<IReadOnlyList<ScoutArmBudget>>.Failure(bad.Reason);
        }

        var caps = parsed.OfType<Outcome<(ScoutArmId, decimal)>.Ok>().Select(o => (Arm: o.Value.Item1, Cap: o.Value.Item2)).ToList();
        var stranger = caps.FirstOrDefault(c => !arms.Contains(c.Arm));

        return caps.Any(c => !arms.Contains(c.Arm))
            ? Outcome<IReadOnlyList<ScoutArmBudget>>.Failure($"--arm-caps names {ScoutArms.Word(stranger.Arm)}, which this run does not hold")
            : Collect([.. arms.Select(arm => ScoutArmBudget.Of(arm, caps.LastOrDefault(c => c.Arm == arm) is { Cap: > 0m } o ? o.Cap : defaultCapUsd, wallSeconds))]);
    }

    private static Outcome<IReadOnlyList<ScoutArmBudget>> Collect(IReadOnlyList<Outcome<ScoutArmBudget>> budgets) =>
        budgets.OfType<Outcome<ScoutArmBudget>.Fail>().FirstOrDefault() is { } fail
            ? Outcome<IReadOnlyList<ScoutArmBudget>>.Failure(fail.Reason)
            : Outcome<IReadOnlyList<ScoutArmBudget>>.Success([.. budgets.OfType<Outcome<ScoutArmBudget>.Ok>().Select(o => o.Value)]);

    private static Outcome<(ScoutArmId, decimal)> Override(string text)
    {
        var parts = text.Split('=', 2, StringSplitOptions.TrimEntries);
        var arm = ScoutArms.Parse(parts[0]);
        var ok = parts.Length == 2 && decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var cap) && cap > 0m;

        return (arm, ok) switch
        {
            (Outcome<ScoutArm>.Fail fail, _) => Outcome<(ScoutArmId, decimal)>.Failure($"--arm-caps: {fail.Reason}"),
            (_, false) => Outcome<(ScoutArmId, decimal)>.Failure($"--arm-caps: '{text}' is not <arm>=<positive USD with a '.' separator>"),
            (Outcome<ScoutArm>.Ok a, _) => Outcome<(ScoutArmId, decimal)>.Success((a.Value.Id, decimal.Parse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture))),
            _ => Outcome<(ScoutArmId, decimal)>.Failure("unreachable"),
        };
    }
}
