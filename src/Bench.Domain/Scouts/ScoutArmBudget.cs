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

/// <summary>The budgets of a run's arms from the verb's flags: one default cap for every arm, overridden per arm by
/// <c>A1=1.5,B3=0.9</c>. An override naming an arm the run does not hold, or one that is not a number, is refused by name.</summary>
public static class ScoutArmBudgets
{
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
