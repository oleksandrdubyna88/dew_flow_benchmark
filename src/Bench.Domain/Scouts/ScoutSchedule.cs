using System.Globalization;

namespace Bench.Domain.Scouts;

/// <summary>One planned cell: a task, an arm, a repeat, and where it runs — the block (<see cref="Slot"/>: one task in one repeat)
/// and its place inside the block (<see cref="Position"/>).</summary>
public sealed record ScheduledCell(ScoutTaskId Task, ScoutArmId Arm, int Repeat, int Slot, int Position);

/// <summary>The seeded schedule of §3.4: cells run one at a time, and the arms are INTERLEAVED inside each task so drift in the time
/// of day and the server's load falls on every arm. Repeats are outermost; inside a repeat the tasks come in a seeded order, and
/// inside each task's block the arms come in a seeded permutation of their own.
/// <para>
/// The order is drawn from <see cref="StableHash"/> of the seed and the cell's identity, never from <see cref="Random"/>: a schedule is
/// committed before cell one (<c>schedule.json</c>) and must be reproducible from its seed on any machine and any runtime, and
/// <c>Random</c>'s sequence for a seed is not a documented contract.
/// </para>
/// <para>
/// The Explore replication arms run only on the replication tasks (a quarter of them, chosen by the caller); the seven main arms run
/// on every task.
/// </para></summary>
public static class ScoutSchedule
{
    public static Outcome<IReadOnlyList<ScheduledCell>> Plan(
        IReadOnlyList<ScoutTaskId> tasks, IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutTaskId> replicationTasks, int repeats, int seed)
    {
        var refusal = Refusal(tasks, arms, replicationTasks, repeats);

        return refusal.Length > 0
            ? Outcome<IReadOnlyList<ScheduledCell>>.Failure(refusal)
            : Outcome<IReadOnlyList<ScheduledCell>>.Success(Cells(tasks, arms, Names(replicationTasks), repeats, seed));
    }

    private static IReadOnlyList<ScheduledCell> Cells(IReadOnlyList<ScoutTaskId> tasks, IReadOnlyList<ScoutArmId> arms, IReadOnlySet<string> replication, int repeats, int seed)
    {
        var blocks = Enumerable.Range(1, repeats)
            .SelectMany(repeat => tasks.OrderBy(t => Key(seed, repeat, t.Value), StringComparer.Ordinal).Select(task => (Repeat: repeat, Task: task)))
            .ToList();

        return [.. blocks.SelectMany((block, slot) => ArmsOf(block.Task, arms, replication)
            .OrderBy(arm => Key(seed, block.Repeat, block.Task.Value, ScoutArms.Word(arm)), StringComparer.Ordinal)
            .Select((arm, position) => new ScheduledCell(block.Task, arm, block.Repeat, slot, position)))];
    }

    private static IEnumerable<ScoutArmId> ArmsOf(ScoutTaskId task, IReadOnlyList<ScoutArmId> arms, IReadOnlySet<string> replication) =>
        arms.Where(arm => !ScoutArms.Of(arm).IsReplication || replication.Contains(task.Value));

    private static string Key(int seed, int repeat, params string[] identity) =>
        StableHash.Of(string.Join('\u001f', [seed.ToString(CultureInfo.InvariantCulture), repeat.ToString(CultureInfo.InvariantCulture), .. identity]));

    private static IReadOnlySet<string> Names(IReadOnlyList<ScoutTaskId> tasks) => tasks.Select(t => t.Value).ToHashSet(StringComparer.Ordinal);

    private static string Refusal(IReadOnlyList<ScoutTaskId> tasks, IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutTaskId> replicationTasks, int repeats) =>
        (tasks.Count > 0 && Names(tasks).Count == tasks.Count, arms.Count > 0 && arms.Distinct().Count() == arms.Count, repeats >= 1) switch
        {
            (false, _, _) => "a schedule needs at least one task, each named once",
            (_, false, _) => "a schedule needs at least one arm, each named once",
            (_, _, false) => $"repeats must be at least 1, got {repeats}",
            _ => ReplicationRefusal(tasks, arms, replicationTasks),
        };

    private static string ReplicationRefusal(IReadOnlyList<ScoutTaskId> tasks, IReadOnlyList<ScoutArmId> arms, IReadOnlyList<ScoutTaskId> replicationTasks)
    {
        var known = Names(tasks);
        var stranger = replicationTasks.FirstOrDefault(t => !known.Contains(t.Value));
        var asked = arms.Any(a => ScoutArms.Of(a).IsReplication);

        return (stranger, asked, replicationTasks.Count > 0) switch
        {
            ({ } t, _, _) => $"replication task '{t}' is not one of the run's tasks",
            (_, true, false) => "the Explore replication arms were asked for, and no replication task was named",
            (_, false, true) => "replication tasks were named, and no Explore replication arm was asked for",
            _ => string.Empty,
        };
    }
}
