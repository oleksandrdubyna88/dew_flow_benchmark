using Bench.Domain.Trace;
using Bench.Infrastructure.Process;
using ModelArms.Core.Launch;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>Launches the target's tools (plan D17 d, D26 b; C14): each by the executable its command names — the REAL path the verb resolved and printed, never a name — under the environment this adapter was built with (<see cref="TargetEnvironment"/>), from the folder the
/// command names, through the shared launcher's replacing overload and the command's own wall, which kills the child's whole tree. Both output pipes are written, a line at a time, into the log the VERB opened. An exit code is a count; a tool killed at its wall or never started has none —
/// <see cref="CapturedCount.Unavailable"/>, never a zero.
/// <para>
/// What this does not do: bound what the shared launcher keeps in memory (E3 §8 item 18 — a tool that prints gigabytes grows the harness until its wall); judge the target's own code, which runs with the operator's rights by design (item 17).
/// </para></summary>
public sealed class TargetTools(IReadOnlyDictionary<string, string> environment, TimeProvider clock) : ITargetTools
{
    private readonly IReadOnlyDictionary<string, string> _environment = new Dictionary<string, string>(environment, StringComparer.Ordinal);

    public TargetTools(IReadOnlyDictionary<string, string> environment)
        : this(environment, TimeProvider.System)
    {
    }

    public async Task<ToolRun> RunAsync(ToolCommand command, Stream log, CancellationToken stopping)
    {
        var lines = new LogLines(log);
        var started = clock.GetTimestamp();
        var attempt = await ProcessRunner.RunAsync(command.Executable, command.Arguments, command.WorkingDirectory, command.Wall, string.Empty, _environment, lines.Write, lines.Write, stopping);
        var elapsed = clock.GetElapsedTime(started);
        return attempt switch
        {
            ProcessAttempt.Completed done => new ToolRun(CapturedCount.Number(done.Result.ExitCode), KilledAtWall: false, Started: true, elapsed),
            ProcessAttempt.TimedOut => new ToolRun(CapturedCount.Unavailable($"killed at its wall of {command.Wall.TotalSeconds:0.#} s"), KilledAtWall: true, Started: true, elapsed),
            _ => new ToolRun(CapturedCount.Unavailable($"{command.Executable} could not be started"), KilledAtWall: false, Started: false, elapsed),
        };
    }
}
