using System.Globalization;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts schedule --run &lt;pilot run&gt; --out schedule.json</c> (E2 S6) — the caps and walls E3's preliminary run launches
/// under, derived from the PILOT's settled cells: per arm the largest settled <c>total_cost_usd</c> × 1.5 (up to the cent) and the slowest cell's
/// session × 2 (up to the second) — <see cref="ScoutArmBudgets.FromPilot"/>. Written under the pilot's campaign string with the pilot's run id,
/// so a later run can refuse a schedule made for another campaign. Refused (4) for a run that is not a pilot, and for an arm with no settled
/// cell carrying a cost — a cap nobody measured is a default. Launches nothing, claims nothing.</summary>
public static class ScoutsScheduleCommand
{
    public static Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (command.Value("out").Length == 0)
        {
            return Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, "scouts schedule needs --out <schedule.json> — what bench scouts run --kind preliminary takes its caps and walls from"));
        }

        return ScoutsReadCommand.WithRunAsync(command, artifactsRequired: false, "schedule", error, cancellationToken, async (inputs, run) =>
        {
            if (run.Kind != ScoutRunKind.Pilot)
            {
                return GateRunCommand.Refuse(error, ExitCodes.Configuration,
                    $"scout run {run.Id} is a {ScoutRunKinds.Word(run.Kind)} run — caps and walls are derived from a PILOT run's settled cells (§3.4), never from a measuring run's");
            }

            var settled = (await inputs.Store.CellsAsync(run.Id, cancellationToken)).Where(c => c.State == CellState.Settled).ToList();

            return ScoutArmBudgets.FromPilot(run.Arms, settled) switch
            {
                Outcome<IReadOnlyList<ScoutArmBudget>>.Ok ok => await WriteAsync(command.Value("out"), run, settled.Count, ok.Value, output, cancellationToken),
                Outcome<IReadOnlyList<ScoutArmBudget>>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => ExitCodes.Configuration,
            };
        });
    }

    private static async Task<int> WriteAsync(string file, ScoutRun run, int settled, IReadOnlyList<ScoutArmBudget> budgets, TextWriter output, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(file, ScoutFiles.WriteSchedule(new ScoutScheduleFile(run.Campaign, run.Id.ToString(), budgets)), cancellationToken);

        foreach (var budget in budgets)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"schedule       {ScoutArms.Word(budget.Arm),-4} cap ${budget.CapUsd} (the largest settled cell × {ScoutArmBudgets.CapFactor}) · wall {budget.WallSeconds} s (the slowest cell × {ScoutArmBudgets.WallFactor})"));
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"written        {budgets.Count} arm budget(s) from {settled} settled cell(s) of pilot run {run.Id} to {file} — campaign '{run.Campaign}'"));
        return ExitCodes.Pass;
    }
}
