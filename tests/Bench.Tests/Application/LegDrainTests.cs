using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>The campaign loop, which is the one place a 24/7 harness can lose ten thousand cells at once.
/// <para>
/// Every guarantee here was a real defect on 2026-08-16: the loop had no per-leg guard (one transient
/// <c>NpgsqlException</c> took the process down with every pending cell), no failure budget (a dead
/// endpoint would have burned the per-leg wall clock for every remaining cell), and no cancellation
/// (every orchestrator stop had the same effect as a crash).
/// </para></summary>
public sealed class LegDrainTests
{
    /// <summary>Zero backoff so the suite does not sleep through the failure paths; a small budget so a
    /// bail-out is three legs rather than twenty.</summary>
    private static readonly DrainLimits Fast = new(ConsecutiveFailureBudget: 3, Backoff: TimeSpan.Zero, Grace: TimeSpan.FromSeconds(5));

    [Fact]
    public async Task A_transient_store_failure_fails_one_leg_and_the_campaign_continues()
    {
        var legs = new ScriptedLegs(
            Scored(),
            Throws(new NpgsqlException("57P01: terminating connection due to administrator command")),
            Scored(),
            Drained());

        var (report, seen) = await DrainAsync(legs, Fast, CancellationToken.None);

        report.Scored.Should().Be(2, "a leg that faulted must not take the ones after it with it");
        report.Faulted.Should().Be(1);
        report.Stop.Should().Be(DrainStop.Drained, "the campaign is what the loop is for; one leg is never allowed to end it");
        seen.OfType<LegEvent.Faulted>().Single().Reason.Should().Contain("terminating connection",
            "a skipped leg that says nothing is indistinguishable from a leg that never existed");
    }

    [Fact]
    public async Task A_systemically_broken_environment_ends_the_campaign_instead_of_grinding_through_every_cell()
    {
        var legs = new ScriptedLegs(
            Refused("qwen3-coder:latest is unreachable: connection refused"),
            Refused("qwen3-coder:latest is unreachable: connection refused"),
            Refused("qwen3-coder:latest is unreachable: connection refused"),
            Drained());

        var (report, _) = await DrainAsync(legs, Fast, CancellationToken.None);

        report.Stop.Should().Be(DrainStop.TooManyFailures);
        report.Reason.Should().Contain("3").And.Contain("connection refused",
            "the bail-out has to name what broke, or the operator learns only that it stopped");
        legs.Calls.Should().Be(3, "the point of the budget is the ten thousand legs it does NOT attempt");
    }

    [Fact]
    public async Task A_leg_that_merely_scored_badly_never_trips_the_breaker()
    {
        // Alternating, so the RUN of failures never reaches the budget however many there are in total.
        var legs = new ScriptedLegs(
            Refused("the subject answered 404"), Scored(),
            Refused("the subject answered 404"), Scored(),
            Refused("the subject answered 404"), Scored(),
            Refused("the subject answered 404"), Scored(),
            Drained());

        var (report, _) = await DrainAsync(legs, Fast, CancellationToken.None);

        report.Stop.Should().Be(DrainStop.Drained, "a breaker that counts total failures would stop a campaign that is working");
        report.Scored.Should().Be(4);
        report.Refused.Should().Be(4);
    }

    [Fact]
    public async Task A_stop_request_lets_the_leg_in_flight_finish_and_claims_no_more_cells()
    {
        using var stopping = new CancellationTokenSource();
        var legs = new ScriptedLegs(
            async ct =>
            {
                // The operator presses Ctrl+C while this leg is mid-flight. It must still be able to
                // settle its cell: a cell abandoned at the moment of a PLANNED stop is a stranded cell.
                await stopping.CancelAsync();
                await Task.Delay(20, ct);
                return Scoring();
            },
            Drained());

        var (report, _) = await DrainAsync(legs, Fast, stopping.Token);

        legs.Calls.Should().Be(1, "a stop request means claim no more; the remaining cells stay Pending for the next run");
        report.Stop.Should().Be(DrainStop.Cancelled, "a planned stop is not a finished campaign, and an orchestrator must be able to tell");
        report.Scored.Should().Be(1, "the leg in flight was allowed to finish");
    }

    private static async Task<(DrainReport Report, List<LegEvent> Seen)> DrainAsync(
        ScriptedLegs legs, DrainLimits limits, CancellationToken stopping)
    {
        List<LegEvent> seen = [];
        var drain = new LegDrain(NullLogger<LegDrain>.Instance);
        var report = await drain.DrainAsync(legs.NextAsync, seen.Add, limits, stopping);
        return (report, seen);
    }

    private static Func<CancellationToken, Task<Outcome<LegResult>>> Scored() => _ => Task.FromResult(Scoring());

    private static Func<CancellationToken, Task<Outcome<LegResult>>> Refused(string reason) =>
        _ => Task.FromResult(Outcome<LegResult>.Failure(reason));

    private static Func<CancellationToken, Task<Outcome<LegResult>>> Throws(Exception ex) =>
        _ => Task.FromException<Outcome<LegResult>>(ex);

    /// <summary>The store's own "there is nothing left" phrase, through the constant both sides share.</summary>
    private static Func<CancellationToken, Task<Outcome<LegResult>>> Drained() =>
        _ => Task.FromResult(Outcome<LegResult>.Failure($"run {Guid.Empty} has {ClaimRefusal.NoPendingCell}"));

    private static Outcome<LegResult> Scoring() =>
        Outcome<LegResult>.Success(LegResult.Of(
            Guid.CreateVersion7(),
            "how does the retry compute its delay?",
            "with DecorrelatedJitter",
            [StoredMetric.Boolean("answer contains", true, "'DecorrelatedJitter' was present", false, "good")],
            DateTimeOffset.UtcNow));

    /// <summary>A leg that behaves differently on each call. The LAST step repeats forever, so every
    /// script must end in <see cref="Drained"/> — a test that hangs proves nothing at three in the morning.</summary>
    private sealed class ScriptedLegs(params Func<CancellationToken, Task<Outcome<LegResult>>>[] steps)
    {
        public int Calls { get; private set; }

        public Task<Outcome<LegResult>> NextAsync(CancellationToken cancellationToken)
        {
            var step = steps[Math.Min(Calls, steps.Length - 1)];
            Calls++;
            return step(cancellationToken);
        }
    }
}
