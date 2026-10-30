using ModelArms.Core.Experiment;

namespace ModelArms.Core.Steps;

/// <summary>How many arms can be at the gate lock together with this arm, and where the number came from: the size of the largest round of the run that names the arm (<paramref name="FromRound"/>), or — when no run names it —
/// every arm of <c>arms.map.json</c>, an upper bound that includes closed arms of earlier runs.</summary>
public sealed record GateQueue(int Arms, bool FromRound);

/// <summary>The longest a gate step waits for the gate lock — a TOTAL time, not a heartbeat (E2 §8 items 59 and 61) — sized from what can be queued ahead of it. Under several arms at once each gate step holds the lock for its own wall (the step's
/// <c>minutes</c>) plus the grace it is killed after, PLUS the usage-limit waits the runner spends INSIDE the gate step while still holding the lock (<c>limits.tries</c> × <c>limits.waitSeconds</c>); every OTHER arm of the arm's own round may be ahead
/// in the queue, so the last arm of a round of N waits up to (N − 1) such spans. A ceiling below that stops it with exit 5 behind gates that are all alive, and a relaunch costs a paid attempt (§8 item 56). The round is the one of the run that names
/// the arm (<see cref="QueueOf"/>); an arm no run names is sized from the arms of the map and the caller says so. The result is never shorter than one span (a lone arm may still meet a holder that is not another arm of its round) and never shorter than
/// a minute, and never longer than <paramref name="maximum"/>, what a timer can carry. The arithmetic is in <see cref="long"/> with each factor clamped first: the numbers are the experiment file's, and a stranger's number is clamped before it is multiplied.
/// <para>
/// What this does not do: tell a wedged holder from a slow one — that is the heartbeat (the holder is alive, its stream grows), not built (E4); until then a wedged holder is told apart only when the ceiling runs out.
/// </para></summary>
public static class GateLockCeiling
{
    private const long SecondsPerMinute = 60;
    private const long FloorSeconds = 60;

    /// <summary>The queue the arm is in: the largest round, among the runs that name <paramref name="arm"/>, as the number of arms in it; else <paramref name="armsInMap"/> with <see cref="GateQueue.FromRound"/> false.</summary>
    public static GateQueue QueueOf(IReadOnlyList<RunSpec> runs, string arm, int armsInMap)
    {
        var rounds = runs.Where(run => run.Rounds.Any(round => round.Contains(arm))).SelectMany(run => run.Rounds).ToList();
        return rounds.Count == 0 ? new GateQueue(armsInMap, FromRound: false) : new GateQueue(rounds.Max(round => round.Count), FromRound: true);
    }

    /// <param name="arms">The arms of the queue, this one included.</param>
    public static TimeSpan For(int arms, int gateMinutes, int killGraceSeconds, int limitTries, int limitWaitSeconds, TimeSpan maximum)
    {
        var ceiling = (long)maximum.TotalSeconds;
        var held = Math.Max(gateMinutes, 0) * SecondsPerMinute + Math.Max(killGraceSeconds, 0) + Math.Max(limitTries, 0) * (long)Math.Max(limitWaitSeconds, 0);
        var span = Math.Min(held, ceiling);
        var ahead = Math.Min(Math.Max(1, (long)arms - 1), ceiling);
        return TimeSpan.FromSeconds(Math.Min(Math.Max(ahead * span, FloorSeconds), ceiling));
    }
}
