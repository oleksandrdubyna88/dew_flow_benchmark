using System.Globalization;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>Who claims, which CLI binary the pins are read from, and where a line goes as each cell ends.</summary>
public sealed record ScoutCampaignOptions(WorkerIdentity Owner, string Executable)
{
    public Action<string> Progress { get; init; } = static _ => { };
}

/// <summary>Why a campaign ended. Everything but <see cref="Drained"/> and <see cref="Cancelled"/> is a STOP the verb exits 3 on — resumable
/// once the cause is fixed.</summary>
public enum ScoutStop
{
    /// <summary>Nothing pending is left.</summary>
    Drained,

    /// <summary>A stop was requested; the run is resumable.</summary>
    Cancelled,

    /// <summary>A pin read before a claim moved (§3.4) — nothing was claimed.</summary>
    PinMoved,

    /// <summary>The next cell's reservation does not fit under the ceiling (§3.10) — nothing was launched.</summary>
    DoesNotFit,

    /// <summary>A cell settled STOPPED — the path audit, a moved alias, the cost check, a changed tree.</summary>
    CellStopped,

    /// <summary>A cell settled <i>aborted:spend</i> — a quota or a budget cap.</summary>
    Aborted,

    /// <summary>The runner could not launch (a refused argv, a missing executable, an unbuildable tree); its reservation was released.</summary>
    NotLaunched,

    /// <summary>The environment refused — a pin unreadable, the store, a budget the run does not carry.</summary>
    Environment,
}

public sealed record ScoutCampaignReport(int Settled, ScoutStop Stop, string Reason);

/// <summary>One scout run, one cell at a time, in schedule order (§3.4). Each step: the pins this machine can read are compared with the
/// run's frozen ones BEFORE the claim (a change stops the run, nothing claimed); the next pending cell is claimed under the CLI binary's
/// pin; its arm's reservation is booked against the ceiling (one that does not fit stops the run with what was free and what was asked,
/// nothing launched); the runner launches it; the attempt's money is settled — from <c>total_cost_usd</c> when the result reached the
/// stream, as an unknown charge of the whole reservation when it did not, released when nothing launched — and then the cell. A cell that
/// settles STOPPED or ABORTED stops the whole campaign (§3.2, §3.10). The entry step (<see cref="ScoutRecoveryStep"/>) is the verb's to run
/// first.</summary>
public sealed class ScoutCampaign(IScoutStore store, IScoutRunner runner, IScoutPinReader pins, IScoutCheckpoints checkpoints)
{
    private sealed record Step(bool Go, int Settled, ScoutStop Stop, string Reason)
    {
        public static Step Next(int settled) => new(true, settled, ScoutStop.Drained, string.Empty);

        public static Step End(ScoutStop stop, string reason, int settled = 0) => new(false, settled, stop, reason);
    }

    public async Task<ScoutCampaignReport> RunAsync(ScoutRun run, IReadOnlyList<ScoutTask> tasks, ScoutCampaignOptions options, CancellationToken stopping)
    {
        var settled = 0;

        while (!stopping.IsCancellationRequested)
        {
            var step = await StepAsync(run, tasks, options, stopping);
            settled += step.Settled;

            if (!step.Go)
            {
                return new ScoutCampaignReport(settled, step.Stop, step.Reason);
            }
        }

        return new ScoutCampaignReport(settled, ScoutStop.Cancelled, "a stop was requested; the run is resumable");
    }

    private async Task<Step> StepAsync(ScoutRun run, IReadOnlyList<ScoutTask> tasks, ScoutCampaignOptions options, CancellationToken cancellationToken)
    {
        var local = await PinsAsync(run, options, cancellationToken);

        if (local is Outcome<ProductPin>.Fail stop)
        {
            return Step.End(stop.Reason.StartsWith(ScoutStopWords.PinMoved, StringComparison.Ordinal) ? ScoutStop.PinMoved : ScoutStop.Environment, stop.Reason);
        }

        var claim = await store.ClaimNextAsync(run.Id, options.Owner, ((Outcome<ProductPin>.Ok)local).Value, cancellationToken);

        return claim switch
        {
            Outcome<ScoutCell>.Ok claimed => await ReserveAsync(run, tasks, claimed.Value, options, cancellationToken),
            Outcome<ScoutCell>.Fail { Reason: var r } when r.Contains(ScoutStopWords.NoPendingCell, StringComparison.Ordinal) =>
                Step.End(ScoutStop.Drained, "every pending cell of the run is settled"),
            Outcome<ScoutCell>.Fail fail => Step.End(ScoutStop.Environment, fail.Reason),
            _ => Step.End(ScoutStop.Environment, "unreachable"),
        };
    }

    /// <summary>The pins read right before the claim (§3.4), compared with the frozen ones; the binary's pin is what the claim stores.</summary>
    private async Task<Outcome<ProductPin>> PinsAsync(ScoutRun run, ScoutCampaignOptions options, CancellationToken cancellationToken)
    {
        var local = await pins.ReadAsync(options.Executable, cancellationToken);

        return local.Match(
            read => read.Against(run.Pins).Match(
                current => ScoutPins.Continue(run.Pins, current).Match(
                    _ => Outcome<ProductPin>.Success(read.Binary),
                    Outcome<ProductPin>.Failure),
                reason => Outcome<ProductPin>.Failure($"the pins read on this machine are not pins — {reason}")),
            reason => Outcome<ProductPin>.Failure($"the pins could not be read before the claim — {reason}"));
    }

    private async Task<Step> ReserveAsync(ScoutRun run, IReadOnlyList<ScoutTask> tasks, ScoutCell cell, ScoutCampaignOptions options, CancellationToken cancellationToken)
    {
        var task = tasks.Where(t => t.Id.Value == cell.Task.Value).ToList();
        var budget = run.BudgetOf(cell.Arm);

        if (task.Count == 0 || budget is Outcome<ScoutArmBudget>.Fail)
        {
            return Step.End(ScoutStop.Environment, task.Count == 0 ? $"{cell.Label} names task {cell.Task}, which the run does not hold" : ((Outcome<ScoutArmBudget>.Fail)budget).Reason);
        }

        var asked = ((Outcome<ScoutArmBudget>.Ok)budget).Value.ReservationUsd;

        return await store.ReserveAsync(cell.Id, options.Owner, Guid.CreateVersion7(), asked, cancellationToken) switch
        {
            ScoutReservation.Reserved reserved => await LaunchAsync(run, task[0], cell, reserved.Attempt, options, cancellationToken),
            ScoutReservation.DoesNotFit tight => Step.End(ScoutStop.DoesNotFit, DoesNotFit(run, cell, tight)),
            ScoutReservation.Refused refused => Step.End(ScoutStop.Environment, refused.Reason),
            _ => Step.End(ScoutStop.Environment, "unreachable"),
        };
    }

    private static string DoesNotFit(ScoutRun run, ScoutCell cell, ScoutReservation.DoesNotFit tight) =>
        string.Create(CultureInfo.InvariantCulture,
            $"the next reservation does not fit — {Name(cell)} asks ${tight.AskedUsd} (its arm's cap plus 20 %) and ${tight.FreeUsd} of the ${run.Ledger.CeilingUsd} ceiling is free; nothing was launched");

    private async Task<Step> LaunchAsync(ScoutRun run, ScoutTask task, ScoutCell cell, ScoutAttempt attempt, ScoutCampaignOptions options, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(run, task, cell, attempt, cancellationToken);
        checkpoints.Reached(ScoutCheckpoint.BeforeSettlement, attempt.Id);

        // Settlement lands tokenless: a stop that strands a measured cell is a crash with manners.
        var step = result switch
        {
            ScoutAttemptResult.Settled settled => await SettleAsync(cell, attempt, settled, options),
            ScoutAttemptResult.Stopped stopped => await StopBeforeLaunchAsync(cell, attempt, stopped, options),
            ScoutAttemptResult.NotLaunched notLaunched => await NotLaunchedAsync(attempt, notLaunched),
            _ => Step.End(ScoutStop.Environment, "unreachable"),
        };

        options.Progress(Line(cell, attempt, result, step));
        return step;
    }

    private async Task<Step> SettleAsync(ScoutCell cell, ScoutAttempt attempt, ScoutAttemptResult.Settled settled, ScoutCampaignOptions options)
    {
        // The REAL total, never clipped to the arm's cap (S6, §3.10): the CLI's cap is an estimate, an overshoot is money spent, and a ledger that
        // booked the cap instead would let the next reservation fit into money that is already gone. Overshoots are counted, not hidden.
        var money = settled.ReportedUsd.WasCaptured
            ? await store.SettleAttemptAsync(attempt.Id, settled.ReportedUsd.Value, CancellationToken.None)
            : await store.ChargeUnknownAsync(attempt.Id, CancellationToken.None);

        if (money is Outcome<ScoutAttempt>.Fail notMoved)
        {
            return Step.End(ScoutStop.Environment, $"{attempt.Label}: {notMoved.Reason}");
        }

        var ended = await store.SettleAsync(cell.Id, options.Owner, settled.Settlement, settled.Scouts, CancellationToken.None);

        return (ended, settled.Settlement.Outcome) switch
        {
            (Outcome<ScoutCell>.Fail fail, _) => Step.End(ScoutStop.Environment, fail.Reason),
            (_, ScoutCellOutcome.Stopped) => Step.End(ScoutStop.CellStopped, $"{Name(cell)} stopped the campaign — {settled.Settlement.Reason}", 1),
            (_, ScoutCellOutcome.Aborted) => Step.End(ScoutStop.Aborted, $"{Name(cell)} hit a spend limit (aborted:spend) — the run stops and is never scored past it", 1),
            _ => Step.Next(1),
        };
    }

    private async Task<Step> StopBeforeLaunchAsync(ScoutCell cell, ScoutAttempt attempt, ScoutAttemptResult.Stopped stopped, ScoutCampaignOptions options)
    {
        await store.ReleaseAsync(attempt.Id, CancellationToken.None);
        var ended = await store.SettleAsync(cell.Id, options.Owner, new ScoutSettlement(ScoutCellOutcome.Stopped, stopped.Reason, ScoutCellFacts.None), [], CancellationToken.None);

        return ended is Outcome<ScoutCell>.Fail fail
            ? Step.End(ScoutStop.Environment, fail.Reason)
            : Step.End(ScoutStop.CellStopped, $"{Name(cell)} stopped the campaign before it launched — {stopped.Detail}", 1);
    }

    private async Task<Step> NotLaunchedAsync(ScoutAttempt attempt, ScoutAttemptResult.NotLaunched notLaunched)
    {
        await store.ReleaseAsync(attempt.Id, CancellationToken.None);
        return Step.End(ScoutStop.NotLaunched, $"{notLaunched.Reason} — nothing was launched and the reservation was released");
    }

    public static string Name(ScoutCell cell) =>
        string.Create(CultureInfo.InvariantCulture, $"{cell.Task}/{ScoutArms.Word(cell.Arm)}/r{cell.Repeat} g{cell.Generation}");

    private static string Line(ScoutCell cell, ScoutAttempt attempt, ScoutAttemptResult result, Step step) => result switch
    {
        ScoutAttemptResult.Settled s => string.Create(CultureInfo.InvariantCulture,
            $"settled        {Name(cell)} attempt {attempt.Number} — {s.Settlement.Outcome}{(s.Settlement.Reason == ScoutReason.None ? string.Empty : $" ({s.Settlement.Reason})")} · {(s.ReportedUsd.WasCaptured ? $"${s.ReportedUsd.Value}" : "unknown cost")}"),
        _ => $"not launched   {Name(cell)} attempt {attempt.Number} — {step.Reason}",
    };
}

/// <summary>The words the campaign reads a refusal by.</summary>
public static class ScoutStopWords
{
    /// <summary>How <see cref="ScoutPins.Continue"/> opens its refusal.</summary>
    public const string PinMoved = "a pin moved";

    /// <summary>The store's phrase for an empty queue (<c>PostgresScoutStore.NoPendingCell</c>).</summary>
    public const string NoPendingCell = "no pending scout cell to claim";
}
