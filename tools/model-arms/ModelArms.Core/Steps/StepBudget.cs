using System.Globalization;

namespace ModelArms.Core.Steps;

/// <summary>The time a step has left (plan D7, D8): a step's budget in minutes, less the wall of every attempt that ended and was not cut short by a limit, floored so a nearly-spent step still gets a
/// chance to say something. A pure function of the timing lines; the shell's <c>used_seconds</c> read the same file the same way (<c>run-arm.sh</c> of the part-6 experiment).</summary>
public static class StepBudget
{
    /// <summary>The sum of <c>wall_s</c> over the <c>end</c> lines of <paramref name="step"/> that were not limited and whose wall was captured. A start with no end spends nothing (the shell of part 5 guessed
    /// from the stream file's mtime; this build does not), a <c>gate-wait</c> line spends nothing, and a wall that was not captured is not a wall of zero seconds.</summary>
    public static int UsedSeconds(IReadOnlyList<TimingLine> timing, string step) =>
        (int)Math.Clamp(timing.OfType<TimingLine.End>().Where(end => end.Step == step && !end.Limited && end.WallSeconds.WasCaptured).Sum(end => end.WallSeconds.Value), 0, int.MaxValue);

    /// <summary><c>max(minutes · 60 − used, floorSeconds)</c> (<c>run-arm.sh:84</c>): the floor is the configured minimum, 300 s by default.</summary>
    public static int RemainingSeconds(int minutes, IReadOnlyList<TimingLine> timing, string step, int floorSeconds) =>
        (int)Math.Clamp(minutes * 60L - UsedSeconds(timing, step), floorSeconds, int.MaxValue);

    /// <summary>The wall the launcher kills the tree at (D8): what is left plus the kill grace.</summary>
    public static TimeSpan Wall(int remainingSeconds, int killGraceSeconds) => TimeSpan.FromSeconds((long)remainingSeconds + killGraceSeconds);

    /// <summary><c>HH:mm UTC (HH:mm ±hh:mm)</c> of <c>start + remaining</c>: the UTC clock, then the local clock with the zone's UTC OFFSET at that instant (<c>+01:00</c>, <c>-04:00</c>, <c>+00:00</c>). The shell
    /// printed <c>%Z</c> (<c>CET</c>); .NET has no portable abbreviation and a zone's id differs by host (<c>Europe/Paris</c> on Linux, <c>Romance Standard Time</c> on Windows), so the offset is the one spelling
    /// every host writes the same and a reader can add. The clock, never the date — the shell's shape (E2 §8 item 32).</summary>
    public static string Deadline(DateTimeOffset startUtc, int remainingSeconds, TimeZoneInfo local)
    {
        var utc = startUtc.ToUniversalTime().AddSeconds(remainingSeconds);
        var zoned = TimeZoneInfo.ConvertTime(utc, local);
        return string.Create(CultureInfo.InvariantCulture, $"{utc:HH:mm} UTC ({zoned:HH:mm} {Offset(zoned.Offset)})");
    }

    private static string Offset(TimeSpan offset) => string.Create(CultureInfo.InvariantCulture, $"{(offset < TimeSpan.Zero ? '-' : '+')}{offset.Duration():hh\\:mm}");
}

/// <summary>The prompt a resumed session is given in place of its brief (<c>coai-exp2/run-arm.sh:98-101</c>).</summary>
public static class ResumeNotice
{
    /// <summary>The shell's four lines with <paramref name="baseSha"/> (the commit the experiment's work is counted from) and <paramref name="deadline"/> (a <see cref="StepBudget.Deadline"/>) substituted, its line
    /// breaks kept, ending with a newline.</summary>
    public static string For(string baseSha, string deadline) =>
        "This session was interrupted (a usage limit, the machine or the harness stopped it) and is now resumed. Nothing you did\n"
        + $"before is lost unless it was not saved: check `git status` and `git log --oneline {baseSha}..HEAD` first, then continue the\n"
        + $"same task from where you stopped. Your new deadline, for the time that is left of your budget, is: **{deadline}**. The rest of\n"
        + "your brief is unchanged.\n";
}
