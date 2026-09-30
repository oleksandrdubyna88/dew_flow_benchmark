using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bench.Application;
using Bench.Application.Gate;
using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;

namespace Bench.Cli;

/// <summary><c>bench gate run | resume | status | sweep</c> — the driver's campaign verbs. Exit codes: 0 legs produced ·
/// 3 environment · 4 configuration · 5 nothing produced (a stop, or an empty drain — resumable, never a pass).</summary>
public static class GateRunCommand
{
    public const int DefaultRepeats = 3;
    public const int DefaultCellTimeoutMinutes = 300;
    public const string PredictionFile = "prediction.txt";
    public const string RunSettingsFile = "run-settings.json";

    public static async Task<int> RunAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var refusal = RunRefusal(command);
        if (refusal.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, refusal);
        }

        var (code, inputs) = await GateCliInputs.LoadAsync(command, [.. command.List("reviewers")], error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            return await PlanAndDriveAsync(command, inputs, output, error, cancellationToken);
        }
    }

    public static async Task<int> ResumeAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return Refuse(error, ExitCodes.Configuration, "gate resume needs --run <guid>");
        }

        if (command.Has("dry-run"))
        {
            return await StatusAsync(command, output, error, cancellationToken);
        }

        var (known, unknown, reviewers) = await GateCliInputs.ReviewersOfAsync(command, runId, cancellationToken);
        if (known != ExitCodes.Pass)
        {
            return Refuse(error, known, unknown);
        }

        var (code, inputs) = await GateCliInputs.LoadAsync(command, reviewers, error, cancellationToken);
        if (inputs is null)
        {
            return code;
        }

        await using (inputs)
        {
            return await ResumeRunAsync(command, inputs, runId, output, error, cancellationToken);
        }
    }

    /// <summary><c>bench gate status --run &lt;id&gt;</c> — before anything is claimed: the cells by state, the owner and age
    /// of each claim, the cause of each abandonment, the pins the run has seen and its data-directory mode. Claims nothing.</summary>
    public static async Task<int> StatusAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId) || GateCliInputs.Connection(command).Length == 0)
        {
            return Refuse(error, ExitCodes.Configuration, "gate status needs --run <guid> and --db (or BENCH_DB)");
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        var store = new PostgresGateStore(db, TimeProvider.System);

        try
        {
            return await store.LoadAsync(runId, cancellationToken) switch
            {
                Outcome<GateRun>.Ok ok => Print(ok.Value, await store.CellsAsync(runId, cancellationToken), output),
                Outcome<GateRun>.Fail fail => Refuse(error, ExitCodes.Configuration, fail.Reason),
                _ => throw new InvalidOperationException("unreachable"),
            };
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
        {
            return Refuse(error, ExitCodes.Environment, $"the store is not reachable or not migrated — {ex.Message.Split('\n')[0]}");
        }
    }

    /// <summary><c>bench gate sweep</c> — hands back the claims whose owner is provably gone (ownership-checked, never a
    /// cell of a run that ended) and removes the gate clones of runs that ended.</summary>
    public static async Task<int> SweepAsync(CommandLine command, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (GateCliInputs.Connection(command).Length == 0)
        {
            return Refuse(error, ExitCodes.Configuration, "gate sweep needs --db (or BENCH_DB)");
        }

        await using var db = GateCliInputs.Context(GateCliInputs.Connection(command));
        var store = new PostgresGateStore(db, TimeProvider.System);
        var swept = await store.SweepAsync(TimeSpan.Zero, cancellationToken);
        var ended = (await store.RecentAsync(1000, cancellationToken)).Where(r => r.IsTerminal).Select(r => r.Id).ToList();
        var checkouts = new GateCloneCheckouts(new NoCheckouts(), command.Value("checkout-root", RunCommand.DefaultCheckoutRoot));
        var removed = await checkouts.RemoveFinishedAsync(ended, cancellationToken);

        output.WriteLine($"swept          {swept.Requeued} requeued, {swept.Abandoned} abandoned; {removed} gate clone(s) of finished runs removed");
        return ExitCodes.Pass;
    }

    private static async Task<int> PlanAndDriveAsync(CommandLine command, GateCliInputs inputs, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var gate = Enum.Parse<GateKind>(command.Value("gate"), ignoreCase: true);
        var tasks = inputs.Suite.TasksFor(gate);
        var settings = GateCliInputs.Extras(command).Match(GateRunSettings.With, Outcome<GateRunSettings>.Failure);
        var mode = command.Has("shared-data-dir") ? DataDirMode.Shared : DataDirMode.Isolated;

        var refusal = (tasks, settings) switch
        {
            (Outcome<IReadOnlyList<GateTask>>.Fail f, _) => f.Reason,
            (_, Outcome<GateRunSettings>.Fail f) => f.Reason,
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, refusal);
        }

        var moved = await ProductMovedAsync(inputs, gate, command.Has("allow-product-change"), cancellationToken);
        if (moved.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, moved);
        }

        var hosting = ((Outcome<IReadOnlyList<GateTask>>.Ok)tasks).Value;
        var matrix = GateMatrix.Plan([.. hosting.Select(t => t.Id)], [.. inputs.Reviewers.Select(r => r.Id)], command.Int("repeats", DefaultRepeats));

        if (matrix is Outcome<IReadOnlyList<GateMatrixCell>>.Fail noMatrix)
        {
            return Refuse(error, ExitCodes.Configuration, noMatrix.Reason);
        }

        // The suite's tasks (E6), so the report can put this run's calibration tasks apart without the suite file.
        var recorded = await GateReportCommand.RecordAsync(inputs.SuiteTasks, inputs.Suite, output, cancellationToken);
        if (recorded.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, recorded);
        }

        var run = await PersistAsync(command, inputs, gate, mode, ((Outcome<GateRunSettings>.Ok)settings).Value, ((Outcome<IReadOnlyList<GateMatrixCell>>.Ok)matrix).Value, cancellationToken);
        output.WriteLine($"planned        gate run {run.Id} — {gate.ToString().ToLowerInvariant()} gate, {hosting.Count} task(s) × {inputs.Reviewers.Count} reviewer(s) × {command.Int("repeats", DefaultRepeats)} repeat(s), {mode.ToString().ToLowerInvariant()} data directories");
        output.WriteLine($"product        {inputs.Pin.Describe}");

        return await DriveAsync(command, inputs, run, ((Outcome<GateRunSettings>.Ok)settings).Value, inputs.Pin, output, error, cancellationToken);
    }

    private static async Task<int> ResumeRunAsync(CommandLine command, GateCliInputs inputs, Guid runId, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var loaded = await inputs.Store.LoadAsync(runId, cancellationToken);
        var run = loaded.Match(r => GateRunResume.Resume(r, Requested(command)), Outcome<GateRun>.Failure);

        if (run is not Outcome<GateRun>.Ok { Value: var resumable })
        {
            return Refuse(error, ExitCodes.Configuration, ((Outcome<GateRun>.Fail)run).Reason);
        }

        var cells = await inputs.Store.CellsAsync(runId, cancellationToken);
        var campaignPin = cells.Where(c => c.Pin.IsPinned).OrderByDescending(c => c.ClaimedAt).Select(c => c.Pin).FirstOrDefault() ?? inputs.Pin;
        var settings = GateRunSettings.FromJson(await File.ReadAllTextAsync(Path.Combine(inputs.ArtifactRoot, CellPaths.RunsFolder, runId.ToString("D"), RunSettingsFile), cancellationToken));

        var refusal = (resumable.SuiteStamp == inputs.Suite.Stamp, campaignPin.Matches(inputs.Pin) || resumable.AllowProductChange, settings) switch
        {
            (false, _, _) => $"gate run {runId} was planned over suite {resumable.SuiteStamp}; the suite file given is {inputs.Suite.Stamp} — a resume measures the same suite",
            (_, false, _) => ((Outcome<ProductPin>.Fail)ProductPin.Continue(campaignPin, inputs.Pin)).Reason,
            (_, _, Outcome<GateRunSettings>.Fail f) => f.Reason,
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, refusal);
        }

        var drifted = await ReferencesDriftedAsync(inputs, runId, cancellationToken);
        if (drifted.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, drifted);
        }

        var recorded = await GateReportCommand.RecordAsync(inputs.SuiteTasks, inputs.Suite, output, cancellationToken);
        if (recorded.Length > 0)
        {
            return Refuse(error, ExitCodes.Configuration, recorded);
        }

        await inputs.Store.SweepAsync(TimeSpan.Zero, cancellationToken);
        output.WriteLine($"resuming       gate run {runId} — {cells.Count(c => c.State == CellState.Pending)} pending cell(s)");

        return await DriveAsync(command, inputs, resumable, ((Outcome<GateRunSettings>.Ok)settings).Value, campaignPin, output, error, cancellationToken);
    }

    private static async Task<int> DriveAsync(
        CommandLine command, GateCliInputs inputs, GateRun run, GateRunSettings settings, ProductPin campaignPin, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        await inputs.Store.AdvanceAsync(run.Id, GateRunStatus.Running, cancellationToken);
        var campaignInputs = new GateCampaignInputs(
            run, campaignPin, inputs.Reviewers.ToDictionary(r => r.Id.Value), inputs.Suite.Tasks.ToDictionary(t => t.Id.Value), settings, inputs.Key, inputs.ProductExecutable);
        var options = new GateCampaignOptions(
            command.Int("parallel", run.Mode == DataDirMode.Shared ? 1 : GateCampaignOptions.DefaultParallel),
            command.Int("per-endpoint", GateCampaignOptions.DefaultPerEndpoint),
            DrainLimits.Default)
        {
            Progress = Printer(output),
        };

        var report = await new GateCampaign(new ProductPinReader(), new LegDrain(inputs.Logs.CreateLogger<LegDrain>()), TimeProvider.System)
            .RunAsync(campaignInputs, options, n => inputs.Lane(n, command), cancellationToken);

        if (report is Outcome<GateCampaignReport>.Fail refused)
        {
            return Refuse(error, ExitCodes.Configuration, refused.Reason);
        }

        return await FinishAsync(inputs, run, ((Outcome<GateCampaignReport>.Ok)report).Value, output, error);
    }

    private static async Task<int> FinishAsync(GateCliInputs inputs, GateRun run, GateCampaignReport report, TextWriter output, TextWriter error)
    {
        var cells = await inputs.Store.CellsAsync(run.Id, CancellationToken.None);
        if (report.Stop == CampaignStop.Drained && cells.All(c => c.IsTerminal))
        {
            await inputs.Store.AdvanceAsync(run.Id, GateRunStatus.Finished, CancellationToken.None);
        }

        output.WriteLine($"campaign       {report.Settled} cell(s) settled, {report.Refused} refused, {report.Faulted} faulted — {report.Stop}: {report.Reason}");
        output.WriteLine($"pins seen      {string.Join("; ", report.PinsSeen.Select(p => p.Describe))}");
        output.WriteLine($"footprint      run {run.Id}: {(await inputs.Artifacts.FootprintAsync(run.Id, CancellationToken.None)).Describe}");
        if (Pending(cells) is { Length: > 0 } pending)
        {
            output.WriteLine($"pending        {pending}");
        }

        return report.Stop switch
        {
            CampaignStop.ProductMoved => Refuse(error, ExitCodes.Configuration, report.Reason),
            CampaignStop.PinUnreadable or CampaignStop.TooManyFailures => Refuse(error, ExitCodes.Environment, report.Reason),
            CampaignStop.AccountOut => Refuse(error, ExitCodes.Environment, $"{report.Reason} (bench gate resume --run {run.Id})"),
            CampaignStop.Drained when report.Settled > 0 => ExitCodes.Pass,
            _ => Refuse(error, ExitCodes.NoReport, $"no cell was produced — {report.Reason}; the run is resumable (bench gate resume --run {run.Id})"),
        };
    }

    /// <summary>The cells still pending, per reviewer — what a benched reviewer left for the resume.</summary>
    private static string Pending(IReadOnlyList<GateCell> cells) =>
        string.Join(" · ", cells.Where(c => c.State == CellState.Pending)
            .GroupBy(c => c.Reviewer.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key} {g.Count()}"));

    private static async Task<GateRun> PersistAsync(
        CommandLine command, GateCliInputs inputs, GateKind gate, DataDirMode mode, GateRunSettings settings, IReadOnlyList<GateMatrixCell> matrix, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        var prediction = command.Value("prediction");
        var runDirectory = Path.Combine(inputs.ArtifactRoot, CellPaths.RunsFolder, id.ToString("D"));
        Directory.CreateDirectory(runDirectory);
        await File.WriteAllTextAsync(Path.Combine(runDirectory, RunSettingsFile), settings.ToJson(), cancellationToken);

        if (prediction.Length > 0)
        {
            await File.WriteAllTextAsync(Path.Combine(runDirectory, PredictionFile), prediction, cancellationToken);
        }

        var run = GateRun.Planned(id, gate, inputs.Suite.Stamp, mode, DateTimeOffset.UtcNow) with
        {
            PredictionHash = prediction.Length > 0 ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prediction))) : string.Empty,
            AllowProductChange = command.Has("allow-product-change"),
        };

        await inputs.Store.PlanAsync(run, [.. matrix.Select(c => GateCell.Pending(Guid.CreateVersion7(), id, c))], cancellationToken);
        return run;
    }

    /// <summary>A product that moved since an earlier native run of this gate over this suite measured any of these
    /// reviewers is refused naming both shas — unless the operator allows the change, which starts a new scope.</summary>
    private static async Task<string> ProductMovedAsync(GateCliInputs inputs, GateKind gate, bool allow, CancellationToken cancellationToken)
    {
        if (allow)
        {
            return string.Empty;
        }

        var reviewers = inputs.Reviewers.Select(r => r.Id.Value).ToHashSet(StringComparer.Ordinal);

        foreach (var earlier in (await inputs.Store.RecentAsync(200, cancellationToken)).Where(r => r.Gate == gate && r.SuiteStamp == inputs.Suite.Stamp && r.Source is RunSource.Native))
        {
            var pinned = (await inputs.Store.CellsAsync(earlier.Id, cancellationToken))
                .Where(c => c.Pin.IsPinned && reviewers.Contains(c.Reviewer.Value))
                .OrderByDescending(c => c.ClaimedAt)
                .FirstOrDefault();

            if (pinned is not null && !pinned.Pin.Matches(inputs.Pin))
            {
                return ((Outcome<ProductPin>.Fail)ProductPin.Continue(pinned.Pin, inputs.Pin)).Reason + $" (the campaign of gate run {earlier.Id})";
            }
        }

        return string.Empty;
    }

    /// <summary>A reviewer whose references resolve on this machine to something other than what its settled cells were
    /// measured under — an endpoint re-pointed, a CLI moved — is refused naming the reviewer, never the values.</summary>
    private static async Task<string> ReferencesDriftedAsync(GateCliInputs inputs, Guid runId, CancellationToken cancellationToken)
    {
        var stored = await inputs.Store.ReferenceHashesAsync(runId, cancellationToken);

        foreach (var reviewer in inputs.Reviewers)
        {
            var now = inputs.ReferencesOf(reviewer).Match(r => r.Hash, reason => reason);
            if (stored.Any(s => s.Reviewer.Value == reviewer.Id.Value && s.ReferencesHash != now))
            {
                return $"reviewer '{reviewer.Id}' resolves its references differently on this machine than its settled cells of run {runId} were measured under — a re-pointed endpoint is another configuration; add it as a new reviewer row";
            }
        }

        return string.Empty;
    }

    private static int Print(GateRun run, IReadOnlyList<GateCell> cells, TextWriter output)
    {
        var now = DateTimeOffset.UtcNow;
        output.WriteLine($"gate run       {run.Id} — {run.Gate.ToString().ToLowerInvariant()}, {run.Status}, {run.Mode.ToString().ToLowerInvariant()} data directories, suite {run.SuiteStamp}");
        output.WriteLine($"pending        {cells.Count(c => c.State == CellState.Pending)}");

        foreach (var cell in cells.Where(c => c.State == CellState.Claimed))
        {
            output.WriteLine($"claimed        {cell.Task}/{cell.Reviewer}/r{cell.Repeat} attempt {cell.Attempts} by {cell.Owner.Canonical}, {(now - cell.ClaimedAt).TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} min ago");
        }

        foreach (var cell in cells.Where(c => c.State == CellState.Abandoned))
        {
            output.WriteLine($"abandoned      {cell.Task}/{cell.Reviewer}/r{cell.Repeat} after {cell.Attempts} attempt(s): {cell.OutcomeDetail}");
        }

        output.WriteLine($"settled        {cells.Count(c => c.State == CellState.Settled)} ({cells.Count(c => c.OutcomeKind == GateCellOutcomeKind.Failed && c.State == CellState.Settled)} failed)");
        output.WriteLine($"pins seen      {string.Join("; ", cells.Where(c => c.Pin.IsPinned).Select(c => c.Pin.Describe).Distinct(StringComparer.Ordinal))}");

        return ExitCodes.Pass;
    }

    /// <summary>One line per cell as it ends, written under a lock — lanes finish at once, and interleaved halves of two
    /// lines would be worse than none.</summary>
    private static Action<string> Printer(TextWriter output)
    {
        var gate = new Lock();
        return line =>
        {
            lock (gate)
            {
                output.WriteLine(line);
            }
        };
    }

    private static RequestedDataDir Requested(CommandLine command) =>
        (command.Has("shared-data-dir"), command.Has("isolated-data-dir")) switch
        {
            (true, _) => RequestedDataDir.Shared,
            (_, true) => RequestedDataDir.Isolated,
            _ => RequestedDataDir.AsStored,
        };

    private static string RunRefusal(CommandLine command) =>
        (Enum.TryParse<GateKind>(command.Value("gate"), ignoreCase: true, out _), command.List("reviewers").Count > 0, command.Int("repeats", DefaultRepeats) >= 1) switch
        {
            (false, _, _) => "gate run needs --gate plan|code|feature",
            (_, false, _) => "gate run needs --reviewers <id,…> — an id from `bench gate reviewers list`; a run measures somebody",
            (_, _, false) => "--repeats must be at least 1 (three is the floor for a variance)",
            _ => string.Empty,
        };

    internal static int Refuse(TextWriter error, int code, string reason)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }

    /// <summary>The sweep removes clones only; it never makes a checkout.</summary>
    private sealed class NoCheckouts : ICheckoutProvider
    {
        public Task<Outcome<string>> EnsureAsync(Bench.Domain.Targets.MeasurementTarget target, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Failure("the sweep makes no checkout"));
    }
}
