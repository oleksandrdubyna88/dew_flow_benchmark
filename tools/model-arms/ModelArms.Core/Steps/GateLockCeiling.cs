namespace ModelArms.Core.Steps;

/// <summary>The longest a gate step waits for the gate lock — a TOTAL time, not a heartbeat (E2 §8 item 59) — sized from what can be queued ahead of it. Under several arms at once each gate step holds the lock for its own wall (the step's
/// <c>minutes</c>) and the grace it is killed after, and every OTHER arm of the experiment may be ahead in the queue, so a healthy queue's last arm waits up to (arms − 1) such spans: a ceiling below that stops it with exit 5 behind gates that are
/// all alive, and a relaunch costs a paid attempt (§8 item 56). The result is never shorter than one span (a lone arm may still meet a holder that is not another arm of this experiment) and never shorter than a minute, and never longer than
/// <paramref name="maximum"/>, what a timer can carry. The arithmetic is in <see cref="long"/> with each factor clamped first: the minutes and the grace are the experiment file's numbers, and a stranger's number is clamped before it is multiplied.
/// <para>
/// <c>run-arm</c> passes the arms of <c>arms.map.json</c> and the longest gate step of the experiment; E4's <c>run-all</c> passes the arms of its run. What this does not do: tell a healthy holder from a wedged one — that is the heartbeat the plan names as the
/// other answer (the holder is alive, its stream grows), not built; until then a wedged holder is told apart only when the ceiling runs out.
/// </para></summary>
public static class GateLockCeiling
{
    private const long SecondsPerMinute = 60;
    private const long FloorSeconds = 60;

    public static TimeSpan For(int arms, int gateMinutes, int killGraceSeconds, TimeSpan maximum)
    {
        var ceiling = (long)maximum.TotalSeconds;
        var span = Math.Min(Math.Max(gateMinutes, 0) * SecondsPerMinute + Math.Max(killGraceSeconds, 0), ceiling);
        var ahead = Math.Min(Math.Max(1, (long)arms - 1), ceiling);
        return TimeSpan.FromSeconds(Math.Min(Math.Max(ahead * span, FloorSeconds), ceiling));
    }
}
