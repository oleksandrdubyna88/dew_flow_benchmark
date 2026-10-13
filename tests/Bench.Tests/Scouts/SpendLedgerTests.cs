using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The spend ledger's arithmetic and the attempt's launch boundary (§3.10), as pure functions — the store's guarded updates
/// are tested against a real database in <c>PostgresScoutStoreTests</c>.</summary>
public sealed class SpendLedgerTests
{
    [Fact]
    public void A_reservation_is_the_cap_plus_twenty_percent_headroom()
    {
        SpendLedger.ReservationFor(2.50m).Should().Be(3.00m);
        SpendLedger.ReservationFor(0.333333m).Should().Be(0.4m);
    }

    [Fact]
    public void A_reservation_that_does_not_fit_under_the_ceiling_with_everything_committed_is_refused()
    {
        var ledger = new SpendLedger(10m, ReservedUsd: 3m, SettledUsd: 4m, UnknownUsd: 2m);

        SpendLedger.Reserve(ledger, 1m).Ok().ReservedUsd.Should().Be(4m, "exactly the ceiling still fits");
        SpendLedger.Reserve(ledger, 1.01m).Reason().Should().Contain("does not fit").And.Contain("$1");
        SpendLedger.Reserve(ledger, 0m).Failed().Should().BeTrue();
    }

    [Fact]
    public void Settling_books_the_actual_cost_and_frees_the_reservation_even_when_the_cli_overshot_its_cap()
    {
        var settled = SpendLedger.Settle(new SpendLedger(10m, 3m, 0m, 0m), reservedUsd: 3m, actualUsd: 3.4m);

        settled.Should().Be(new SpendLedger(10m, 0m, 3.4m, 0m));
        settled.SpentUsd.Should().Be(3.4m);
    }

    [Fact]
    public void An_unknown_charge_books_the_whole_reservation_and_the_ledger_identity_counts_it()
    {
        var charged = SpendLedger.ChargeUnknown(new SpendLedger(10m, 3m, 1m, 0m), reservedUsd: 3m);

        charged.Should().Be(new SpendLedger(10m, 0m, 1m, 3m));
        (charged.SpentUsd, charged.FreeUsd).Should().Be((4m, 6m), "the charge stays booked — money that may already be spent is never free again");
    }

    [Fact]
    public void A_release_gives_the_reservation_back()
    {
        SpendLedger.Release(new SpendLedger(10m, 3m, 1m, 0m), 3m).Should().Be(new SpendLedger(10m, 0m, 1m, 0m));
    }

    [Fact]
    public void An_attempt_is_marked_launching_once_and_only_from_reserved()
    {
        var reserved = Attempt();

        var launching = ScoutAttemptLifecycle.MarkLaunching(reserved).Ok();

        launching.State.Should().Be(ScoutAttemptState.Launching);
        ScoutAttemptLifecycle.MarkLaunching(launching).Reason().Should().Contain("only a reserved attempt");
    }

    [Fact]
    public void Only_an_attempt_with_no_launching_mark_is_released()
    {
        ScoutAttemptLifecycle.Release(Attempt()).Ok().State.Should().Be(ScoutAttemptState.Released);
        ScoutAttemptLifecycle.Release(Attempt(ScoutAttemptState.Launching)).Reason().Should().Contain("never marked launching");
    }

    [Fact]
    public void A_launching_attempt_settles_or_is_charged_unknown_exactly_once()
    {
        var settled = ScoutAttemptLifecycle.Settle(Attempt(ScoutAttemptState.Launching), 1.25m).Ok();

        (settled.State, settled.ChargedUsd).Should().Be((ScoutAttemptState.Settled, 1.25m));
        ScoutAttemptLifecycle.Settle(settled, 1.25m).Reason().Should().Contain("settles, once");
        ScoutAttemptLifecycle.ChargeUnknown(settled).Failed().Should().BeTrue();

        var unknown = ScoutAttemptLifecycle.ChargeUnknown(Attempt(ScoutAttemptState.Launching)).Ok();
        (unknown.State, unknown.ChargedUsd).Should().Be((ScoutAttemptState.ChargedUnknown, 3m), "the whole reservation");
        ScoutAttemptLifecycle.Settle(Attempt(ScoutAttemptState.Reserved), 1m).Failed().Should().BeTrue("an attempt never launched has nothing to settle");
    }

    [Theory]
    [InlineData(ScoutAttemptState.Reserved, false, typeof(ScoutRecovery.Release))]
    [InlineData(ScoutAttemptState.Reserved, true, typeof(ScoutRecovery.Release))]
    [InlineData(ScoutAttemptState.Launching, true, typeof(ScoutRecovery.Settle))]
    [InlineData(ScoutAttemptState.Launching, false, typeof(ScoutRecovery.ChargeUnknown))]
    [InlineData(ScoutAttemptState.Settled, true, typeof(ScoutRecovery.Untouched))]
    [InlineData(ScoutAttemptState.ChargedUnknown, false, typeof(ScoutRecovery.Untouched))]
    [InlineData(ScoutAttemptState.Released, false, typeof(ScoutRecovery.Untouched))]
    public void Recovery_releases_what_never_launched_settles_what_left_a_result_and_charges_doubt(ScoutAttemptState state, bool resultSurvived, Type decision)
    {
        var survived = resultSurvived ? CapturedUsd.Amount(0.8m) : CapturedUsd.Unavailable("no result line");

        ScoutAttemptLifecycle.Recover(Attempt(state), survived).Should().BeOfType(decision);
    }

    private static ScoutAttempt Attempt(ScoutAttemptState state = ScoutAttemptState.Reserved) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, 3m, state, 0m, DateTimeOffset.UnixEpoch);
}
