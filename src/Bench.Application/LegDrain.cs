using Bench.Domain;
using Bench.Domain.Runs;
using Microsoft.Extensions.Logging;

namespace Bench.Application;

/// <summary>How a drain ended. The three cases are the whole reason this is a value rather than a bool:
/// an orchestrator reading "the queue is empty" must never confuse it with "we stopped early".</summary>
public enum DrainStop
{
    /// <summary>Every pending cell of the run is settled — the campaign finished.</summary>
    Drained,

    /// <summary>A stop was requested (Ctrl+C / SIGTERM). The run is resumable, not finished.</summary>
    Cancelled,

    /// <summary>Too many legs failed in a row. The environment is broken, not the subject.</summary>
    TooManyFailures,
}

/// <summary>What one leg did, for whoever is rendering the campaign.</summary>
public abstract record LegEvent
{
    private LegEvent() { }

    public sealed record Scored(LegResult Result) : LegEvent;

    /// <summary>The leg produced no result but was RECORDED on its cell — an unreachable endpoint, a
    /// claim race lost, a cell already scored. Ordinary; it counts against the failure budget.</summary>
    public sealed record Refused(string Reason) : LegEvent;

    /// <summary>The leg threw. One failed unit is skipped, never fatal.</summary>
    public sealed record Faulted(string Reason) : LegEvent;
}

/// <param name="ConsecutiveFailureBudget">How many legs may fail in a ROW before the campaign is declared
/// broken. A run of ten thousand cells against a dead endpoint must end in minutes rather than burn the
/// per-leg wall clock for every remaining cell. Consecutive, never cumulative: a subject that answers
/// badly is a RESULT, and a breaker that counted those would stop a campaign that is working perfectly.</param>
/// <param name="Backoff">Applied per consecutive failure, bounded by <see cref="MaxBackoff"/> — so a
/// lost claim race cannot become a zero-delay spin against the database.</param>
/// <param name="Grace">How long the leg IN FLIGHT keeps its token after a stop is requested, so it can
/// settle its cell instead of stranding it. A planned stop that strands cells is a crash with manners.</param>
public readonly record struct DrainLimits(int ConsecutiveFailureBudget, TimeSpan Backoff, TimeSpan Grace)
{
    public static DrainLimits Default => new(20, TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(30));

    /// <summary>The ceiling on the backoff. Every wait has one.</summary>
    public static TimeSpan MaxBackoff => TimeSpan.FromSeconds(5);
}

/// <param name="Scored">Legs that produced a stored result, however the subject did on them.</param>
/// <param name="Refused">Legs that produced no result but were recorded on their cell.</param>
/// <param name="Faulted">Legs that threw. Skipped, logged, and counted — never silent.</param>
public readonly record struct DrainReport(int Scored, int Refused, int Faulted, DrainStop Stop, string Reason)
{
    public int Attempted => Scored + Refused + Faulted;

    public string Describe => $"{Scored} scored, {Refused} refused, {Faulted} faulted — {Reason}";
}

/// <summary>The campaign loop: claim a leg, run it, record it, take the next one.
/// <para>
/// It exists as its own unit because of what it is allowed to survive. A drain that runs ten thousand
/// cells overnight meets a transient <c>NpgsqlException</c>, an endpoint that goes away, and an operator
/// who stops it — and the difference between a harness and a script is that all three are outcomes here
/// rather than the end of the process. Three rules, in the order they were paid for:
/// </para>
/// <para>
/// <b>One failed unit is recorded and skipped.</b> Every leg runs inside its own <c>try</c>, and the
/// whole unit is inside it — the caller's delegate creates its own scope and resolves its own services,
/// so a resolution failure is caught here too rather than escaping through a side door.
/// </para>
/// <para>
/// <b>A systemically broken environment ends the campaign.</b> Failures in a ROW trip a budget, because
/// the first failure already said what the ten-thousandth would.
/// </para>
/// <para>
/// <b>A stop is planned, not a crash.</b> Once a stop is requested no further cell is claimed, and the
/// leg in flight keeps its token for <see cref="DrainLimits.Grace"/> so it can settle.
/// </para></summary>
public sealed class LegDrain(ILogger<LegDrain> logger)
{
    public async Task<DrainReport> DrainAsync(
        Func<CancellationToken, Task<Outcome<LegResult>>> leg,
        Action<LegEvent> report,
        DrainLimits limits,
        CancellationToken stopping)
    {
        // The leg's token is NOT the stopping token: a stop starts the grace clock instead of cutting the
        // work in flight, so the cell in progress can reach its settle.
        using var grace = new CancellationTokenSource();
        using var stop = stopping.Register(() => grace.CancelAfter(limits.Grace));

        var tally = Tally.Empty;

        while (!stopping.IsCancellationRequested)
        {
            var step = await StepAsync(leg, grace.Token);

            if (step is Outcome<LegEvent>.Fail ended)
            {
                return Ended(tally, stopping, ended.Reason);
            }

            var next = ((Outcome<LegEvent>.Ok)step).Value;
            tally = tally.Plus(next);
            report(next);

            if (tally.Consecutive >= limits.ConsecutiveFailureBudget)
            {
                logger.LogError("Stopping the campaign after {Failures} consecutive failed legs", tally.Consecutive);
                return tally.Ends(DrainStop.TooManyFailures, Broken(limits, next));
            }

            await PauseAsync(tally.Consecutive, limits, grace.Token);
        }

        return tally.Ends(DrainStop.Cancelled, "a stop was requested; no further cell was claimed");
    }

    /// <summary>One leg, guarded. Everything the delegate does — scope, resolution, database, HTTP — is
    /// inside this <c>try</c>, which is the entire point of the boundary.</summary>
    private async Task<Outcome<LegEvent>> StepAsync(
        Func<CancellationToken, Task<Outcome<LegResult>>> leg, CancellationToken legToken)
    {
        try
        {
            return Classify(await leg(legToken));
        }
        catch (OperationCanceledException) when (legToken.IsCancellationRequested)
        {
            return Outcome<LegEvent>.Failure("the leg in flight ran past the shutdown grace and was cancelled");
        }
        catch (Exception ex)
        {
            // A catch-all, deliberately: a list of anticipated types is a bet that the fourth type never
            // comes, and the fourth type here would take every remaining cell of the campaign with it.
            logger.LogError(ex, "A leg faulted and was skipped; the campaign continues");
            return Outcome<LegEvent>.Success(new LegEvent.Faulted(Describe(ex)));
        }
    }

    /// <summary>A <c>Fail</c> here means "there is no next leg" — the queue is empty, or we were stopped.
    /// Everything else, success or refusal, is a leg that HAPPENED and belongs in the tally.</summary>
    private static Outcome<LegEvent> Classify(Outcome<LegResult> outcome) =>
        outcome switch
        {
            Outcome<LegResult>.Ok ok => Outcome<LegEvent>.Success(new LegEvent.Scored(ok.Value)),
            Outcome<LegResult>.Fail empty when empty.Reason.Contains(ClaimRefusal.NoPendingCell, StringComparison.Ordinal)
                => Outcome<LegEvent>.Failure("every pending cell of this run is settled"),
            Outcome<LegResult>.Fail refused => Outcome<LegEvent>.Success(new LegEvent.Refused(refused.Reason)),
            _ => throw new InvalidOperationException("unreachable"),
        };

    private DrainReport Ended(Tally tally, CancellationToken stopping, string reason)
    {
        logger.LogInformation("The drain ended: {Scored} scored, {Refused} refused, {Faulted} faulted — {Reason}",
            tally.Scored, tally.Refused, tally.Faulted, reason);

        return tally.Ends(stopping.IsCancellationRequested ? DrainStop.Cancelled : DrainStop.Drained, reason);
    }

    /// <summary>Backoff between consecutive failures, bounded. A cancelled wait is a stop, not an error.</summary>
    private static async Task PauseAsync(int consecutive, DrainLimits limits, CancellationToken legToken)
    {
        if (consecutive == 0 || limits.Backoff <= TimeSpan.Zero)
        {
            return;
        }

        var delay = TimeSpan.FromTicks(Math.Min(limits.Backoff.Ticks * consecutive, DrainLimits.MaxBackoff.Ticks));

        try
        {
            await Task.Delay(delay, legToken);
        }
        catch (OperationCanceledException)
        {
            // The loop's own condition decides what happens next; a slept-through stop is not a failure.
        }
    }

    private static string Broken(DrainLimits limits, LegEvent last) =>
        $"{limits.ConsecutiveFailureBudget} legs failed in a row — that is the environment, not the subject; "
        + $"last: {Reason(last)}";

    private static string Reason(LegEvent step) =>
        step switch
        {
            LegEvent.Refused refused => refused.Reason,
            LegEvent.Faulted faulted => faulted.Reason,
            _ => "the leg was scored",
        };

    private static string Describe(Exception ex) =>
        $"{ex.GetType().Name}: {ex.Message.Split('\n')[0]}";

    private readonly record struct Tally(int Scored, int Refused, int Faulted, int Consecutive)
    {
        public static Tally Empty => default;

        /// <summary>A scored leg resets the run of failures — that is what makes the budget a breaker for
        /// a broken ENVIRONMENT rather than a cap on how badly a subject may do.</summary>
        public Tally Plus(LegEvent step) =>
            step switch
            {
                LegEvent.Scored => this with { Scored = Scored + 1, Consecutive = 0 },
                LegEvent.Refused => this with { Refused = Refused + 1, Consecutive = Consecutive + 1 },
                _ => this with { Faulted = Faulted + 1, Consecutive = Consecutive + 1 },
            };

        public DrainReport Ends(DrainStop stop, string reason) => new(Scored, Refused, Faulted, stop, reason);
    }
}
