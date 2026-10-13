using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts sweep | prune</c> — the verbs that keep a scout run's storage and launch nothing.
/// <list type="bullet">
/// <item><c>sweep [--run]</c> — the entry step alone (the owner-checked sweep, then recovery of every orphaned attempt), over one run or every
/// run in the store. It needs the artefact root: recovery reads what survived of each attempt's stream there.</item>
/// <item><c>prune --run</c> — §3.11: the history-free trees of the run's tasks, ONLY those the tree builder made (its marker is there), each
/// path-checked under the trees root and never through a link; refused while any cell of the run is Pending or Claimed. The artefacts stay
/// — the raw streams are kept until the plan's promotion.</item>
/// </list></summary>
public static class ScoutsHousekeepingCommand
{
    public static async Task<int> SweepAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var named = command.Has("run");
        if (named && !Guid.TryParse(command.Value("run"), out _))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts sweep --run takes a run id");
        }

        var (code, inputs) = await ScoutsInputs.LoadAsync(command, artifactsRequired: true, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            var runs = named
                ? (await inputs.Store.LoadAsync(Guid.Parse(command.Value("run")), cancellationToken)).Match(r => (IReadOnlyList<ScoutRun>)[r], _ => [])
                : await inputs.Reads.RecentRunsAsync(1000, cancellationToken);

            if (named && runs.Count == 0)
            {
                return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"no scout run {command.Value("run")} is in this database");
            }

            foreach (var run in runs)
            {
                await ScoutsCommand.PrepareAsync(command, inputs, run, output, cancellationToken);
                output.WriteLine(ScoutsCommand.Ledger((await inputs.Store.LoadAsync(run.Id, cancellationToken)).Match(r => r.Ledger, _ => run.Ledger)));
            }

            return ExitCodes.Pass;
        }
    }

    public static async Task<int> PruneAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts prune needs --run <guid>");
        }

        var (code, inputs) = await ScoutsInputs.LoadAsync(command, artifactsRequired: false, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            var cells = await inputs.Store.CellsAsync(runId, cancellationToken);
            var open = cells.Count(c => !c.IsTerminal);

            return (cells.Count, open) switch
            {
                (0, _) => GateRunCommand.Refuse(error, ExitCodes.Configuration, $"no scout run {runId} is in this database"),
                (_, > 0) => GateRunCommand.Refuse(error, ExitCodes.Configuration, $"scout run {runId} still has {open} open cell(s) — its trees are still being measured; nothing was deleted"),
                _ => Pruned(inputs, runId, await inputs.Store.TasksAsync(runId, cancellationToken), output, error),
            };
        }
    }

    private static int Pruned(ScoutsInputs inputs, Guid runId, IReadOnlyList<ScoutTask> tasks, TextWriter output, TextWriter error)
    {
        var deleted = 0;

        foreach (var task in tasks)
        {
            switch (inputs.Trees.Delete(task.Id))
            {
                case Outcome<bool>.Ok { Value: true }:
                    deleted++;
                    break;
                case Outcome<bool>.Fail fail:
                    return GateRunCommand.Refuse(error, ExitCodes.Environment, fail.Reason);
                default:
                    break;
            }
        }

        output.WriteLine($"pruned         scout run {runId} — {deleted} history-free tree(s) of its {tasks.Count} task(s) deleted under {inputs.Roots.Trees}; nothing else was touched");
        return ExitCodes.Pass;
    }
}
