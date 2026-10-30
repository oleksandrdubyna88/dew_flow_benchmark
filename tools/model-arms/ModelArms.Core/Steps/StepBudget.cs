namespace ModelArms.Core.Steps;

/// <summary>The time a step has left (plan D7, D8), computed from the timing lines.</summary>
public static class StepBudget
{
    public static int UsedSeconds(IReadOnlyList<TimingLine> timing, string step) => 0;

    public static int RemainingSeconds(int minutes, IReadOnlyList<TimingLine> timing, string step, int floorSeconds) => minutes * 60;

    public static TimeSpan Wall(int remainingSeconds, int killGraceSeconds) => TimeSpan.Zero;

    public static string Deadline(DateTimeOffset startUtc, int remainingSeconds, TimeZoneInfo local) => string.Empty;
}

/// <summary>The prompt a resumed session is given.</summary>
public static class ResumeNotice
{
    public static string For(string baseSha, string deadline) => string.Empty;
}
