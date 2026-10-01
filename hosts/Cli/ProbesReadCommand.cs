using System.Globalization;
using System.Text.Json;
using Bench.Application.Probes;
using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Runs;

namespace Bench.Cli;

/// <summary><c>bench probes status | sweep | report | prune</c> — the verbs that read a probe run or keep its storage, and launch nothing.
/// <list type="bullet">
/// <item><c>status --run</c> — every cell, EVERY generation, by state: who holds a claim and for how long, why a cell was abandoned
/// or handed back. Claims nothing.</item>
/// <item><c>sweep [--run]</c> — the entry step alone (S2's <see cref="ProbeCampaign.PrepareAsync"/>), over one run or every run.</item>
/// <item><c>report --run [--json]</c> — <see cref="ProbeReport"/>: the object S4's API answers, as JSON or as text.</item>
/// <item><c>prune --run</c> — finding 5: refused while any cell is Pending or Claimed; else the run is flagged pruned FIRST (the guarded
/// UPDATE) and its artefacts deleted after, so a flag can over-state a deletion but never hide one. Idempotent: a prune of an
/// already-pruned run deletes whatever is left and exits 0 once nothing is (0 folders deleted is an answer, not a refusal).</item>
/// </list></summary>
public static class ProbesReadCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static async Task<int> StatusAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        await WithRunAsync(command, artifactsRequired: false, "status", error, cancellationToken, async (inputs, run) =>
            Print(run, await inputs.Store.CellsAsync(run.Id, cancellationToken), output));

    public static async Task<int> ReportAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        await WithRunAsync(command, artifactsRequired: false, "report", error, cancellationToken, async (inputs, run) =>
        {
            var report = ProbeReport.Of(run, await inputs.Reads.CellsAsync(run.Id, cancellationToken));
            output.WriteLine(command.Has("json") ? JsonSerializer.Serialize(report, Web) : ProbeReportText.Of(report));
            return ExitCodes.Pass;
        });

    public static async Task<int> PruneAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken) =>
        await WithRunAsync(command, artifactsRequired: true, "prune", error, cancellationToken, async (inputs, run) =>
        {
            if (await inputs.Store.MarkArtifactsPrunedAsync(run.Id, cancellationToken) is Outcome<ProbeRun>.Fail open)
            {
                return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"{open.Reason} — nothing was deleted");
            }

            return inputs.Artifacts.DeleteRun(run.Id) switch
            {
                Outcome<int>.Ok deleted => Said(output, $"pruned         probe run {run.Id} — {deleted.Value} cell folder(s) of artefacts deleted; its verdicts are no longer auditable from disk"),
                Outcome<int>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Environment, $"probe run {run.Id} is flagged pruned, but {fail.Reason}"),
                _ => throw new InvalidOperationException("unreachable"),
            };
        });

    /// <summary><c>bench probes sweep [--run &lt;id&gt;]</c> — without a run, every probe run in the store.</summary>
    public static async Task<int> SweepAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var named = command.Has("run");
        if (named && !Guid.TryParse(command.Value("run"), out _))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, "probes sweep --run takes a run id");
        }

        var (code, inputs) = await ProbesInputs.LoadAsync(command, artifactsRequired: false, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            var runs = named
                ? (await inputs.Store.LoadAsync(Guid.Parse(command.Value("run")), cancellationToken)).Match(r => (IReadOnlyList<ProbeRun>)[r], _ => [])
                : await inputs.Reads.RecentRunsAsync(1000, cancellationToken);

            if (named && runs.Count == 0)
            {
                return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"no probe run {command.Value("run")} is in this database");
            }

            foreach (var run in runs)
            {
                await ProbesCommand.PrepareAsync(command, inputs, run, output, cancellationToken);
            }

            return ExitCodes.Pass;
        }
    }

    /// <summary>The run a read verb is about, through the loader (4 for the flags and roots, 3 for the database), or 4 for an id the
    /// store does not hold.</summary>
    private static async Task<int> WithRunAsync(
        CommandLine command, bool artifactsRequired, string verb, TextWriter error, CancellationToken cancellationToken, Func<ProbesInputs, ProbeRun, Task<int>> body)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return GateRunCommand.Refuse(error, ExitCodes.Configuration, $"probes {verb} needs --run <guid>");
        }

        var (code, inputs) = await ProbesInputs.LoadAsync(command, artifactsRequired, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            return await inputs.Store.LoadAsync(runId, cancellationToken) switch
            {
                Outcome<ProbeRun>.Ok ok => await body(inputs, ok.Value),
                Outcome<ProbeRun>.Fail fail => GateRunCommand.Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
    }

    private static int Print(ProbeRun run, IReadOnlyList<ProbeCell> cells, TextWriter output)
    {
        var now = DateTimeOffset.UtcNow;
        var progress = ProbeRunProgress.Of(cells);

        output.WriteLine($"probe run      {run.Id} — {run.Subjects.Count} subject(s), {run.Repeats} repeat(s), oracle @openai/codex {run.Oracle.Version} ({ProbeReport.Word(run.Oracle.Source)})"
                         + (run.ArtifactsPruned ? " — artefacts PRUNED" : string.Empty));
        output.WriteLine($"cells          {progress.Pending} pending · {progress.Claimed} claimed · {progress.Settled} settled · {progress.Abandoned} abandoned — {(progress.IsOpen ? "open" : "finished")}");

        foreach (var cell in cells)
        {
            output.WriteLine($"{ProbeReport.Word(cell.State),-14} {ProbesCommand.Name(cell)} attempt {cell.Attempts}{Detail(cell, now)}");
        }

        return ExitCodes.Pass;
    }

    private static string Detail(ProbeCell cell, DateTimeOffset now) => cell.State switch
    {
        CellState.Claimed => $" by {cell.Owner.Canonical}, {(now - cell.ClaimedAt).TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} min ago",
        CellState.Settled => $" — {ProbeReport.Word(cell.Facts.Kind)} · {cell.Pin.VersionText}",
        _ when cell.Reason != ProbeReason.None => $" — {ProbeReport.Word(cell.Reason)}",
        _ => string.Empty,
    };

    private static int Said(TextWriter output, string line)
    {
        output.WriteLine(line);
        return ExitCodes.Pass;
    }
}
