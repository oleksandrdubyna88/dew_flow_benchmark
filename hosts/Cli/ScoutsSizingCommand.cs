using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts sizing --run &lt;pilot run&gt; --out &lt;sizing.md&gt; [--seed S] [--simulations N] [--resamples B]</c> (E2 S8, §3.8) —
/// how many tasks a decision run would need, simulated from a run's scored cells (<see cref="ScoutSizing"/>) and written as Markdown
/// (<see cref="ScoutSizingReport"/>). Under D7 it is REPORTED, never a gate: exit 0 whether a task count is found or not. What it prints and
/// writes carries counts and rates only — no task, label, repository or statement — so it is public by construction. Launches nothing, claims
/// nothing. Exit codes: 0 written · 3 the database is unreachable · 4 a flag, or a run this database does not hold · 5 nothing to size (no
/// workload with both deciding arms scored on one task).</summary>
public static class ScoutsSizingCommand
{
    public static Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var settings = ScoutSizingSettings.Of(
            command.Int("seed", ScoutSizingSettings.DefaultSeed), command.Int("simulations", ScoutSizingSettings.DefaultSimulations), command.Int("resamples", ScoutSizingSettings.DefaultResamples));

        return (command.Value("out"), settings) switch
        {
            ({ Length: 0 }, _) => Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts sizing needs --out <sizing.md> — the simulation is written there as well as printed")),
            (_, Outcome<ScoutSizingSettings>.Fail fail) => Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason)),
            (var file, Outcome<ScoutSizingSettings>.Ok ok) => ScoutsReadCommand.WithRunAsync(command, artifactsRequired: false, "sizing", error, cancellationToken,
                (inputs, run) => SizeAsync(inputs, run, ok.Value, file, output, error, cancellationToken)),
            _ => Task.FromResult(ExitCodes.Configuration),
        };
    }

    private static async Task<int> SizeAsync(
        ScoutsInputs inputs, Domain.Scouts.ScoutRun run, ScoutSizingSettings settings, string file, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var evidence = ScoutReport.Evidence(
            await inputs.Reads.TasksAsync(run.Id, cancellationToken),
            await inputs.Reads.CellsAsync(run.Id, cancellationToken),
            await inputs.Reads.ScoutRecordsAsync(run.Id, cancellationToken),
            await inputs.Reads.VerdictsAsync(run.Id, cancellationToken));

        if (ScoutSizing.Of(ScoutSizing.Samples(evidence), settings) is not Outcome<ScoutSizingResult>.Ok { Value: var result })
        {
            return GateRunCommand.Refuse(error, ExitCodes.NoReport, $"scout run {run.Id}: no workload has a task with both of its deciding arms scored — nothing to size from");
        }

        await File.WriteAllTextAsync(file, ScoutSizingReport.Markdown(run.Id, run.Kind, result), cancellationToken);

        foreach (var line in ScoutSizingReport.Lines(result))
        {
            output.WriteLine(line);
        }

        output.WriteLine($"written        the sizing of scout run {run.Id} to {file} — {ScoutSizingReport.ReportedNotAGate} (D7)");
        return ExitCodes.Pass;
    }
}
