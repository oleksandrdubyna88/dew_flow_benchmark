using System.Globalization;
using System.Text.Json;
using Bench.Application.Scouts;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Cli;

/// <summary><c>bench scouts status | report</c> — the verbs that READ a scout run and launch nothing (E1 S3).
/// <list type="bullet">
/// <item><c>status --run</c> — every cell, every generation, by state and outcome, and the ledger: what is still reserved, what settled, the
/// unknown charges, and the ceiling left. Claims nothing.</item>
/// <item><c>report --run [--json] [--bootstrap-seed S] [--resamples N]</c> — <see cref="ScoutReport"/>: the object E3 S8's API will answer, as
/// JSON (byte-deterministic: the same run and seed print the same bytes) or as text. A PRELIMINARY run prints
/// "preliminary — not a verdict" where a decision run prints its verdict, and shows its intervals either way.</item>
/// </list>
/// Exit codes, as the probes': 0 · 3 the database is unreachable · 4 a flag, or a run this database does not hold.</summary>
public static class ScoutsReadCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Task<int> StatusAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        WithRunAsync(command, artifactsRequired: false, "status", error, cancellationToken, async (inputs, run) =>
        {
            var cells = await inputs.Store.CellsAsync(run.Id, cancellationToken);
            var attempts = await inputs.Store.AttemptsAsync(run.Id, cancellationToken);
            Print(run, cells, attempts, output);
            return ExitCodes.Pass;
        });

    public static Task<int> ReportAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var settings = BootstrapSettings.Of(command.Int("bootstrap-seed", BootstrapSettings.DefaultSeed), command.Int("resamples", BootstrapSettings.DefaultResamples));

        return settings is Outcome<BootstrapSettings>.Fail fail
            ? Task.FromResult(GateRunCommand.Refuse(error, ExitCodes.Configuration, $"--resamples: {fail.Reason}"))
            : WithRunAsync(command, artifactsRequired: false, "report", error, cancellationToken, async (inputs, run) =>
            {
                if (await ScoutReport.ReadAsync(inputs.Reads, run.Id, ((Outcome<BootstrapSettings>.Ok)settings).Value, cancellationToken) is not Outcome<Contracts.ScoutRunReportDto>.Ok { Value: var report })
                {
                    return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"scout run {run.Id} could not be read back");
                }

                output.WriteLine(command.Has("json") ? Json(report) : ScoutReportText.Of(report));
                return ExitCodes.Pass;
            });
    }

    /// <summary>The report's JSON — the web defaults, as the probes' report and the API serialise it.</summary>
    public static string Json(Contracts.ScoutRunReportDto report) => JsonSerializer.Serialize(report, Web);

    /// <summary>The run a read verb is about, through the loader (4 for the flags and roots, 3 for the database), or 4 for an id the store
    /// does not hold.</summary>
    internal static async Task<int> WithRunAsync(
        CommandLine command, bool artifactsRequired, string verb, TextWriter error, CancellationToken cancellationToken, Func<ScoutsInputs, ScoutRun, Task<int>> body)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"scouts {verb} needs --run <guid>");
        }

        var (code, inputs) = await ScoutsInputs.LoadAsync(command, artifactsRequired, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            return await inputs.Store.LoadAsync(runId, cancellationToken) switch
            {
                Outcome<ScoutRun>.Ok ok => await body(inputs, ok.Value),
                Outcome<ScoutRun>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
    }

    private static void Print(ScoutRun run, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutAttempt> attempts, TextWriter output)
    {
        var progress = ScoutRunProgress.Of(cells);
        var latest = ScoutGenerations.LatestSettled(cells);
        var now = DateTimeOffset.UtcNow;

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"scout run      {run.Id} — {ScoutRunKinds.Word(run.Kind)} · seed {run.Seed} · {run.Repeats} repeat(s) · {run.Tasks.Count} task(s) × {run.Arms.Count} arm(s)"));
        output.WriteLine($"cells          {progress.Pending} pending · {progress.Claimed} claimed · {progress.Settled} settled · {progress.Abandoned} abandoned — {(progress.IsOpen ? "open" : "finished")}");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"outcomes       {Count(latest, ScoutCellOutcome.Scored)} scored · {Count(latest, ScoutCellOutcome.Void)} void · {Count(latest, ScoutCellOutcome.Stopped)} stopped · {Count(latest, ScoutCellOutcome.Aborted)} aborted (each lineage at its latest settled generation)"));
        output.WriteLine(Ledger(run.Ledger, attempts));

        foreach (var cell in cells)
        {
            output.WriteLine($"{cell.State.ToString().ToLowerInvariant(),-14} {ScoutCampaign.Name(cell)} attempt {cell.Attempts}{Detail(cell, now)}");
        }
    }

    /// <summary>The ledger as <c>status</c> prints it: reserved, settled, unknown charges, spent, and the ceiling left.</summary>
    public static string Ledger(SpendLedger ledger, IReadOnlyList<ScoutAttempt> attempts) =>
        string.Create(CultureInfo.InvariantCulture,
            $"ledger         ${ledger.ReservedUsd} reserved · ${ledger.SettledUsd} settled · ${ledger.UnknownUsd} unknown ({attempts.Count(a => a.State == ScoutAttemptState.ChargedUnknown)} charge(s)) · ${ledger.SpentUsd} spent · ${ledger.FreeUsd} of the ${ledger.CeilingUsd} ceiling left · {attempts.Count} attempt(s)");

    private static int Count(IReadOnlyList<ScoutCell> cells, ScoutCellOutcome outcome) => cells.Count(c => c.Outcome == outcome);

    private static string Detail(ScoutCell cell, DateTimeOffset now) => cell.State switch
    {
        CellState.Claimed => string.Create(CultureInfo.InvariantCulture, $" by {cell.Owner.Canonical}, {(now - cell.Claim.ClaimedAt).TotalMinutes:0.#} min ago"),
        CellState.Settled => $" — {cell.Outcome.ToString().ToLowerInvariant()}{(cell.Reason == ScoutReason.None ? string.Empty : $" ({cell.Reason})")}",
        _ when cell.Reason != ScoutReason.None => $" — {cell.Reason}",
        _ => string.Empty,
    };
}
