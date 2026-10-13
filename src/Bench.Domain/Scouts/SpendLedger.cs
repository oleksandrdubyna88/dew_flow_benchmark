namespace Bench.Domain.Scouts;

/// <summary>The experiment-wide spend ledger (§3.10), carried on the run row and moved only by guarded updates. Four amounts in USD:
/// <list type="bullet">
/// <item><see cref="ReservedUsd"/> — the reservations of attempts still open (reserved or launching);</item>
/// <item><see cref="SettledUsd"/> — Σ <c>total_cost_usd</c> of the attempts settled from a surviving <c>result</c>;</item>
/// <item><see cref="UnknownUsd"/> — Σ unknown-cost charges: an attempt marked launching whose result did not survive is charged its
/// WHOLE reservation, booked the moment recovery finds it and never released, so a later reservation cannot fit into money that may
/// already be spent.</item>
/// </list>
/// The ceiling is measured against everything committed — open reservations included. The ledger identity the run ends on is
/// <see cref="SpentUsd"/> = Σ settled + Σ unknown, never a bare equality a crash would break.
/// <para>
/// These are the PURE transitions; the store applies the same arithmetic inside one guarded UPDATE so two writers cannot both fit
/// into the same headroom. Decimal, because money is summed across hundreds of cells.
/// </para></summary>
public sealed record SpendLedger(decimal CeilingUsd, decimal ReservedUsd, decimal SettledUsd, decimal UnknownUsd)
{
    /// <summary>The CLI's <c>--max-budget-usd</c> is an estimate that can overshoot, so a reservation is the cap plus 20 %.</summary>
    public const decimal Headroom = 1.2m;

    public static SpendLedger Empty(decimal ceilingUsd) => new(ceilingUsd, 0m, 0m, 0m);

    /// <summary>Everything the ceiling is measured against.</summary>
    public decimal CommittedUsd => ReservedUsd + SettledUsd + UnknownUsd;

    /// <summary>The ledger identity: what the run may have spent.</summary>
    public decimal SpentUsd => SettledUsd + UnknownUsd;

    public decimal FreeUsd => Math.Max(0m, CeilingUsd - CommittedUsd);

    /// <summary>The reservation for a cell whose cap is <paramref name="capUsd"/>: the cap plus its headroom.</summary>
    public static decimal ReservationFor(decimal capUsd) => decimal.Round(capUsd * Headroom, 6, MidpointRounding.AwayFromZero);

    /// <summary>Whether <paramref name="amountUsd"/> fits under the ceiling with everything already committed.</summary>
    public bool Fits(decimal amountUsd) => amountUsd > 0m && CommittedUsd + amountUsd <= CeilingUsd;

    public static Outcome<SpendLedger> Reserve(SpendLedger ledger, decimal amountUsd) =>
        ledger.Fits(amountUsd)
            ? Outcome<SpendLedger>.Success(ledger with { ReservedUsd = ledger.ReservedUsd + amountUsd })
            : Outcome<SpendLedger>.Failure($"a reservation of ${amountUsd} does not fit — ${ledger.FreeUsd} of the ${ledger.CeilingUsd} ceiling is free");

    /// <summary>An attempt settled from its <c>result</c>: its reservation leaves, what it actually cost is booked.</summary>
    public static SpendLedger Settle(SpendLedger ledger, decimal reservedUsd, decimal actualUsd) =>
        ledger with { ReservedUsd = ledger.ReservedUsd - reservedUsd, SettledUsd = ledger.SettledUsd + actualUsd };

    /// <summary>An attempt that was launching and left no result: its whole reservation becomes an unknown charge, for good.</summary>
    public static SpendLedger ChargeUnknown(SpendLedger ledger, decimal reservedUsd) =>
        ledger with { ReservedUsd = ledger.ReservedUsd - reservedUsd, UnknownUsd = ledger.UnknownUsd + reservedUsd };

    /// <summary>An attempt that provably never started (no launching mark): its reservation is given back.</summary>
    public static SpendLedger Release(SpendLedger ledger, decimal reservedUsd) =>
        ledger with { ReservedUsd = ledger.ReservedUsd - reservedUsd };
}

/// <summary>What a reservation request came to. A closed hierarchy because the campaign acts differently on each: a reservation
/// that does not fit stops the run with exit 3 and says so; a refusal is a configuration or ownership fault.</summary>
public abstract record ScoutReservation
{
    private ScoutReservation()
    {
    }

    public sealed record Reserved(ScoutAttempt Attempt) : ScoutReservation;

    /// <summary>The ceiling cannot take it: what was free and what was asked, so the stop line can say both.</summary>
    public sealed record DoesNotFit(decimal FreeUsd, decimal AskedUsd) : ScoutReservation;

    public sealed record Refused(string Reason) : ScoutReservation;
}
