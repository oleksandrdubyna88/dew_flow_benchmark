using Bench.Domain.Trace;
using ModelArms.Core.Launch;

namespace ModelArms.Core.Ports;

/// <summary>How one launch of a target tool ended: its exit code (<c>Unavailable</c> when it was killed at its wall or never started — never a zero), whether the wall killed it, whether it started at all, and how long it ran.</summary>
public sealed record ToolRun(CapturedCount Exit, bool KilledAtWall, bool Started, TimeSpan Elapsed);

/// <summary>Launches one of the target's tools (plan C14): by the executable the command names — a REAL path, decided by the verb — under the environment the adapter was built with, from the command's folder, with the tool's output lines
/// written into the <paramref name="log"/> the VERB opened (exclusively, before the tool starts).</summary>
public interface ITargetTools
{
    Task<ToolRun> RunAsync(ToolCommand command, Stream log, CancellationToken stopping);
}
