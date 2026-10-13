using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>Money before the first paid call (plan §3.10, E2 S6), pure: a pilot's bounds as a table; a preliminary or decision run taking its
/// caps from <c>schedule.json</c> only; an overshoot settled at its REAL total (never clipped) and counted, after which the next reservation is
/// refused when it no longer fits; <c>FromPilot</c> against a hand-computed table; the schedule file round-tripped.</summary>
public sealed class ScoutRunLimitsTests
{
    private static readonly ScoutArmId[] Arms = [ScoutArmId.A1, ScoutArmId.B1];

    // ── the pilot's bounds, by name ────────────────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<decimal, int, decimal, string> PilotBounds => new()
    {
        // cap, wall s, ceiling, what the refusal names (empty = allowed)
        { 3.00m, 1200, 60m, "" },
        { 3.01m, 1200, 60m, "A1" },
        { 3.01m, 1200, 60m, "$3" },
        { 1m, 1201, 60m, "20 min" },
        { 1m, 1201, 60m, "1201" },
        { 1m, 1200, 60.01m, "$60" },
        { 0.5m, 60, 7.5m, "" },
    };

    [Theory]
    [MemberData(nameof(PilotBounds))]
    public void A_pilot_refuses_by_name_a_cap_above_3_a_wall_above_20_minutes_and_a_ceiling_above_60(decimal capUsd, int wallSeconds, decimal ceilingUsd, string named)
    {
        var budgets = ScoutArmBudgets.For(Arms, capUsd, [], wallSeconds).Ok();

        var refusal = ScoutRunLimits.PilotRefusal(budgets, ceilingUsd);

        if (named.Length == 0)
        {
            refusal.Should().BeEmpty();
        }
        else
        {
            refusal.Should().Contain(named).And.Contain("pilot");
        }
    }

    [Fact]
    public void The_reservation_stays_the_cap_plus_twenty_percent_on_a_pilot()
    {
        ScoutArmBudgets.For(Arms, 3m, [], 1200).Ok().Should().OnlyContain(b => b.ReservationUsd == 3.6m);
    }

    // ── a preliminary or decision run: caps and walls from schedule.json only ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(ScoutRunKind.Preliminary, "--cap-usd", true, "--cap-usd")]
    [InlineData(ScoutRunKind.Decision, "--arm-caps", true, "--arm-caps")]
    [InlineData(ScoutRunKind.Preliminary, "--cell-timeout-minutes", true, "--cell-timeout-minutes")]
    [InlineData(ScoutRunKind.Decision, "", false, "--schedule")]
    public void A_preliminary_or_decision_run_refuses_a_budget_flag_by_name_and_a_missing_schedule(ScoutRunKind kind, string flag, bool scheduleNamed, string named)
    {
        var refusal = ScoutRunLimits.SourceRefusal(kind, flag.Length > 0 ? [flag] : [], scheduleNamed);

        refusal.Should().Contain(named).And.Contain("schedule.json");
    }

    [Fact]
    public void A_pilot_takes_its_budgets_from_the_flags_and_the_source_rule_says_nothing()
    {
        ScoutRunLimits.SourceRefusal(ScoutRunKind.Pilot, ["--cap-usd", "--cell-timeout-minutes"], scheduleNamed: false).Should().BeEmpty();
        ScoutRunLimits.SourceRefusal(ScoutRunKind.Preliminary, [], scheduleNamed: true).Should().BeEmpty();
    }

    [Fact]
    public void A_schedule_missing_an_arms_cap_is_refused_by_arm_name()
    {
        var scheduled = new[] { ScoutArmBudget.Of(ScoutArmId.A1, 0.78m, 190).Ok() };

        ScoutArmBudgets.FromSchedule(Arms, scheduled).Reason().Should().Contain("B1").And.Contain("schedule.json");
        ScoutArmBudgets.FromSchedule([ScoutArmId.A1], scheduled).Ok().Should().ContainSingle().Which.CapUsd.Should().Be(0.78m);
    }

    // ── the overshoot ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_overshoot_settles_at_its_real_total_never_clipped_and_the_next_reservation_is_refused_when_it_no_longer_fits()
    {
        // ceiling $7.50, cap $3 → reservation $3.60; the attempt costs $4.10.
        var ledger = SpendLedger.Empty(7.5m);
        var reserved = SpendLedger.Reserve(ledger, SpendLedger.ReservationFor(3m)).Ok();

        var settled = SpendLedger.Settle(reserved, reservedUsd: 3.6m, actualUsd: 4.10m);

        settled.SettledUsd.Should().Be(4.10m, "the real total, not the $3 cap");
        settled.FreeUsd.Should().Be(3.4m);
        SpendLedger.Reserve(settled, 3.6m).Reason().Should().Contain("does not fit").And.Contain("$3.4");
    }

    [Fact]
    public void Overshoots_are_the_settled_attempts_above_their_arms_cap_at_the_cap_and_unknown_charges_are_not()
    {
        var budgets = ScoutArmBudgets.For(Arms, 3m, ["B1=1"], 1200).Ok();
        var (over, atCap, unknown, sonnet) = (Cell(ScoutArmId.A1), Cell(ScoutArmId.A1), Cell(ScoutArmId.A1), Cell(ScoutArmId.B1));
        IReadOnlyList<ScoutAttempt> attempts =
        [
            Attempt(over, ScoutAttemptState.Settled, settled: 4.10m),
            Attempt(atCap, ScoutAttemptState.Settled, settled: 3m),
            Attempt(unknown, ScoutAttemptState.ChargedUnknown, settled: 0m),
            Attempt(sonnet, ScoutAttemptState.Settled, settled: 1.25m),
        ];

        var overshoots = ScoutOvershoots.Of(attempts, [over, atCap, unknown, sonnet], budgets);

        overshoots.Select(o => (o.CellId, o.Arm, o.CapUsd, o.SettledUsd, o.OverUsd)).Should().Equal((over.Id, ScoutArmId.A1, 3m, 4.10m, 1.10m), (sonnet.Id, ScoutArmId.B1, 1m, 1.25m, 0.25m));
    }

    // ── FromPilot — the hand-computed table ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void From_pilot_takes_the_largest_settled_cost_times_one_and_a_half_and_the_slowest_cell_times_two_per_arm()
    {
        //   arm   settled cells (total_cost_usd, duration_ms)          cap = ceil(max × 1.5 to the cent)   wall = ceil(max ms × 2 / 1000)
        //   A1    0.40 / 61 000 · 0.52 / 95 000 · void 0.30 / 12 000   0.52 × 1.5 = 0.78 → $0.78          95 000 × 2 = 190 000 ms → 190 s
        //   B1    0.3334 / 120 500                                     0.5001 → $0.51                      241 000 ms → 241 s
        //   a cell whose result did not survive (no cost, no duration) counts for nothing.
        IReadOnlyList<ScoutCell> settled =
        [
            Cell(ScoutArmId.A1, 0.40m, 61_000), Cell(ScoutArmId.A1, 0.52m, 95_000), Cell(ScoutArmId.A1, 0.30m, 12_000, ScoutCellOutcome.Void),
            Cell(ScoutArmId.B1, 0.3334m, 120_500), Cell(ScoutArmId.A1, null, null, ScoutCellOutcome.Void),
        ];

        var budgets = ScoutArmBudgets.FromPilot(Arms, settled).Ok();

        budgets.Select(b => (b.Arm, b.CapUsd, b.WallSeconds)).Should().Equal((ScoutArmId.A1, 0.78m, 190), (ScoutArmId.B1, 0.51m, 241));
        ScoutArmBudgets.FromPilot([ScoutArmId.A1, ScoutArmId.B2], settled).Reason().Should().Contain("B2").And.Contain("no settled cell");
    }

    // ── schedule.json, the kind, the decision ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Schedule_json_round_trips_and_a_malformed_arm_is_refused()
    {
        var file = new ScoutScheduleFile("scouts-preliminary-2026-10-12", Guid.Empty.ToString(), [ScoutArmBudget.Of(ScoutArmId.A1, 0.78m, 190).Ok(), ScoutArmBudget.Of(ScoutArmId.B1, 0.51m, 241).Ok()]);

        var json = ScoutFiles.WriteSchedule(file);

        var reread = ScoutFiles.Schedule(json).Ok();
        (reread.Campaign, reread.FromRun).Should().Be((file.Campaign, file.FromRun));
        reread.Budgets.Should().Equal(file.Budgets, "every arm's cap and wall round-trips");
        ScoutFiles.Schedule(json.Replace("\"A1\"", "\"Z9\"", StringComparison.Ordinal)).Reason().Should().Contain("Z9");
        ScoutFiles.Schedule("{}").Reason().Should().Contain("budgets");
    }

    [Fact]
    public void A_pilot_is_a_run_kind_and_never_states_a_verdict()
    {
        ScoutRunKinds.Parse("pilot").Ok().Should().Be(ScoutRunKind.Pilot);
        ScoutRunKinds.Word(ScoutRunKind.Pilot).Should().Be("pilot");
        ScoutRunKinds.Parse("rehearsal").Reason().Should().Contain("pilot, preliminary or decision");

        var decision = ScoutDecisions.Decide([new ScoutWorkloadReading(ScoutWorkload.W1, ScoutInterval.None, ScoutInterval.None, false)], ScoutRunKind.Pilot);

        decision.Verdict.Should().Be(ScoutVerdict.Preliminary);
        decision.Workloads.Should().OnlyContain(w => w.Outcome == ScoutWorkloadOutcome.Preliminary);
    }

    private static ScoutCell Cell(ScoutArmId arm, decimal? reportedUsd = 0.1m, long? sessionMs = 20_000, ScoutCellOutcome outcome = ScoutCellOutcome.Scored)
    {
        var facts = new ScoutCellFacts(
            CapturedCount.Number(1), CapturedCount.Number(2), CapturedCount.Number(5_000),
            sessionMs is { } ms ? CapturedCount.Number(ms) : CapturedCount.Unavailable("no result"),
            reportedUsd is { } computed ? CapturedUsd.Amount(computed) : CapturedUsd.Unavailable("no result"),
            reportedUsd is { } reported ? CapturedUsd.Amount(reported) : CapturedUsd.Unavailable("no result"));

        return new ScoutCell(
            Guid.CreateVersion7(), Guid.Empty, ScoutTaskId.Parse("t01").Ok(), arm, 1, 1, 0, 0,
            Claimable.Stored(CellState.Settled, 1, WorkerIdentity.Nobody, ScoutStoreFixtures.Noon), ScoutStoreFixtures.Pin(), outcome,
            outcome == ScoutCellOutcome.Void ? ScoutReason.WrongCallCount : ScoutReason.None, facts);
    }

    private static ScoutAttempt Attempt(ScoutCell cell, ScoutAttemptState state, decimal settled) =>
        new(Guid.CreateVersion7(), cell.RunId, cell.Id, 1, 3.6m, state, settled, ScoutStoreFixtures.Noon);
}
