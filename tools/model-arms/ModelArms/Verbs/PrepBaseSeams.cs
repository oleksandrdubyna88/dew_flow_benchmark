using ModelArms.Core.Launch;

namespace ModelArms.Verbs;

/// <summary>What <c>prep-base</c> is handed that production never changes: the clock every stamp, every duration and the baseline's instant come from, and a wall that replaces the SUITE's (<see cref="TargetWalls"/>) — so a test whose suite shim never ends is bounded in seconds, not in the hours the real wall allows; the other tools keep their real walls, so a loaded machine cannot kill a shim that merely starts slowly. Internal, so only the tests can hand in another.</summary>
internal sealed record PrepBaseSeams(TimeProvider Clock, TimeSpan? SuiteWall)
{
    public static PrepBaseSeams Real { get; } = new(TimeProvider.System, null);

    /// <summary>The command with the seam's wall, when it is the suite's and there is one.</summary>
    public ToolCommand Walled(ToolCommand command) => command.Label == "suite" && SuiteWall is { } wall ? command with { Wall = wall } : command;
}
