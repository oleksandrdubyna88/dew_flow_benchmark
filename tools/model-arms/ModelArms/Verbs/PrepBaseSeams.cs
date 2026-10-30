using ModelArms.Core.Launch;

namespace ModelArms.Verbs;

/// <summary>What <c>prep-base</c> is handed that production never changes: the clock every stamp, every duration and the baseline's instant come from, and a wall that replaces EVERY tool's (<see cref="TargetWalls"/>) — so a test whose shim never ends is bounded in seconds, not in the hours the real walls
/// allow. Internal, so only the tests can hand in another.</summary>
internal sealed record PrepBaseSeams(TimeProvider Clock, TimeSpan? Wall)
{
    public static PrepBaseSeams Real { get; } = new(TimeProvider.System, null);

    /// <summary>The command with the seam's wall, when there is one.</summary>
    public ToolCommand Walled(ToolCommand command) => Wall is { } wall ? command with { Wall = wall } : command;
}
