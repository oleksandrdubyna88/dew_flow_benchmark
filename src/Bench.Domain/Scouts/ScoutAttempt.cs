using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>Where one paid attempt stands. <see cref="Reserved"/> and <see cref="Launching"/> are open; the other three are terminal
/// and each is reached exactly once.</summary>
public enum ScoutAttemptState
{
    /// <summary>Money reserved against the ceiling; the CLI has NOT been invoked.</summary>
    Reserved,

    /// <summary>The durable launch boundary: written BEFORE the CLI is invoked, so recovery never has to guess whether it ran.</summary>
    Launching,

    /// <summary>Settled from a surviving <c>result</c> envelope's <c>total_cost_usd</c>.</summary>
    Settled,

    /// <summary>Launching with no surviving result — charged its whole reservation as unknown cost, never released.</summary>
    ChargedUnknown,

    /// <summary>Never launched — the reservation was given back.</summary>
    Released,
}

/// <summary>One attempt of one claimed cell, with its own reservation row (§3.10, "money survives a crash"). The attempt number is
/// the cell claim's attempt; <see cref="ReservedUsd"/> is the cap plus headroom (<see cref="SpendLedger.ReservationFor"/>).</summary>
public sealed record ScoutAttempt(
    Guid Id,
    Guid RunId,
    Guid CellId,
    int Number,
    decimal ReservedUsd,
    ScoutAttemptState State,
    decimal SettledUsd,
    DateTimeOffset ReservedAt)
{
    public bool IsOpen => State is ScoutAttemptState.Reserved or ScoutAttemptState.Launching;

    /// <summary>What this attempt contributes to the ledger identity: its settled cost, its whole reservation when charged unknown,
    /// nothing otherwise.</summary>
    public decimal ChargedUsd => State switch
    {
        ScoutAttemptState.Settled => SettledUsd,
        ScoutAttemptState.ChargedUnknown => ReservedUsd,
        _ => 0m,
    };

    public string Label => $"scout attempt {Id}";
}

/// <summary>What recovery does with one attempt it finds — a pure function of the attempt and of whether its <c>result</c> survived.</summary>
public abstract record ScoutRecovery
{
    private ScoutRecovery()
    {
    }

    /// <summary>Terminal already — settled exactly once, never twice.</summary>
    public sealed record Untouched : ScoutRecovery;

    /// <summary>No launching mark: it provably never started.</summary>
    public sealed record Release : ScoutRecovery;

    /// <summary>Launching, and the stream on disk carries a <c>result</c> with its cost.</summary>
    public sealed record Settle(decimal ActualUsd) : ScoutRecovery;

    /// <summary>Launching, and no result survived: doubt is charged — the whole reservation.</summary>
    public sealed record ChargeUnknown : ScoutRecovery;
}

/// <summary>The attempt's transitions, as pure functions — the store applies each as one guarded UPDATE that re-checks the state
/// the decision was taken on.</summary>
public static class ScoutAttemptLifecycle
{
    public static Outcome<ScoutAttempt> MarkLaunching(ScoutAttempt attempt) =>
        attempt.State == ScoutAttemptState.Reserved
            ? Outcome<ScoutAttempt>.Success(attempt with { State = ScoutAttemptState.Launching })
            : Outcome<ScoutAttempt>.Failure($"{attempt.Label} is {attempt.State} — only a reserved attempt is marked launching, once");

    public static Outcome<ScoutAttempt> Settle(ScoutAttempt attempt, decimal actualUsd) =>
        (attempt.State, actualUsd >= 0m) switch
        {
            (not ScoutAttemptState.Launching, _) => Outcome<ScoutAttempt>.Failure($"{attempt.Label} is {attempt.State} — only a launching attempt settles, once"),
            (_, false) => Outcome<ScoutAttempt>.Failure($"{attempt.Label}: a cost of ${actualUsd} is not a cost"),
            _ => Outcome<ScoutAttempt>.Success(attempt with { State = ScoutAttemptState.Settled, SettledUsd = actualUsd }),
        };

    public static Outcome<ScoutAttempt> ChargeUnknown(ScoutAttempt attempt) =>
        attempt.State == ScoutAttemptState.Launching
            ? Outcome<ScoutAttempt>.Success(attempt with { State = ScoutAttemptState.ChargedUnknown })
            : Outcome<ScoutAttempt>.Failure($"{attempt.Label} is {attempt.State} — only a launching attempt is charged as unknown");

    /// <summary>Only an attempt with NO launching mark is released: a mark means the CLI may have spent, and doubt is charged.</summary>
    public static Outcome<ScoutAttempt> Release(ScoutAttempt attempt) =>
        attempt.State == ScoutAttemptState.Reserved
            ? Outcome<ScoutAttempt>.Success(attempt with { State = ScoutAttemptState.Released })
            : Outcome<ScoutAttempt>.Failure($"{attempt.Label} is {attempt.State} — only an attempt that was never marked launching is released");

    /// <summary>Recovery's decision for one attempt, given the cost of the <c>result</c> that survived on disk, or none.</summary>
    public static ScoutRecovery Recover(ScoutAttempt attempt, CapturedUsd survivingResult) =>
        (attempt.State, survivingResult.WasCaptured) switch
        {
            (ScoutAttemptState.Reserved, _) => new ScoutRecovery.Release(),
            (ScoutAttemptState.Launching, true) => new ScoutRecovery.Settle(survivingResult.Value),
            (ScoutAttemptState.Launching, false) => new ScoutRecovery.ChargeUnknown(),
            _ => new ScoutRecovery.Untouched(),
        };
}
