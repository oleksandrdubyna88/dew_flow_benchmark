namespace Bench.Domain.Runs;

/// <summary>The claim itself — the four fields the lifecycle reads — as one value that two kinds of cell
/// compose rather than each flatten into its own columns.
/// <para>
/// Extracted from <see cref="RunCell"/> when the gate benchmark needed the same claim/settle/sweep rules
/// over a cell whose work is a multi-turn product session rather than a completion. The alternative was a
/// second copy of four fields and four transitions that would agree with the first until somebody edited
/// one; the rule that a cell is abandoned after <see cref="MaxAttempts"/> hand-backs is exactly the kind of
/// thing two copies drift on, and it was learned upstream by parking an item on its second attempt.
/// </para>
/// <para>
/// Every transition is a pure function returning a new value, so the rules are testable without a database
/// and the database's only job stays the one thing only it can do: make a claim atomic.
/// </para></summary>
public sealed record Claimable(CellState State, int Attempts, WorkerIdentity Owner, DateTimeOffset ClaimedAt)
{
    /// <summary>How many times a cell may be handed back before it is abandoned.
    /// <para>
    /// A sweep that re-queues forever is worse than one that gives up: a cell that kills its host is a
    /// cell that will kill the next host too, and an unbounded sweep turns that into a loop that survives
    /// reboots.
    /// </para></summary>
    public const int MaxAttempts = 3;

    /// <summary>A claim nobody has taken yet — the value every cell is persisted with before any work begins.</summary>
    public static Claimable Fresh { get; } = new(CellState.Pending, 0, WorkerIdentity.Nobody, default);

    /// <summary>Rebuilt from storage, exactly as recorded — no validation, because a row is a fact about the past.</summary>
    public static Claimable Stored(CellState state, int attempts, WorkerIdentity owner, DateTimeOffset claimedAt) =>
        new(state, attempts, owner, claimedAt);

    public bool IsTerminal => State is CellState.Settled or CellState.Abandoned;

    /// <param name="subject">How the cell names itself in a refusal — <c>cell 0198…</c> — so the sentence
    /// reads the same whichever kind of cell composed this claim.</param>
    public static Outcome<Claimable> Claim(Claimable claim, WorkerIdentity owner, DateTimeOffset now, string subject)
    {
        if (!owner.CanClaim)
        {
            return Outcome<Claimable>.Failure(
                "a claim needs an owner with a host and a pid — an unowned claim can never be swept correctly");
        }

        return claim.State == CellState.Pending
            ? Outcome<Claimable>.Success(claim with
            {
                State = CellState.Claimed,
                Owner = owner,
                ClaimedAt = now,
                Attempts = claim.Attempts + 1,
            })
            : Outcome<Claimable>.Failure($"{subject} is {claim.State}, not Pending");
    }

    public static Outcome<Claimable> Settle(Claimable claim, string subject) =>
        claim.State == CellState.Claimed
            ? Outcome<Claimable>.Success(claim with { State = CellState.Settled })
            : Outcome<Claimable>.Failure($"{subject} is {claim.State}, not Claimed — only a claimed cell can settle");

    /// <summary>The sweep decision for one claim. Not an <c>Outcome</c>: a sweep over a thousand cells asks
    /// this of every one of them, and "nothing to do here" is the normal answer rather than a failure. The
    /// composing cell decides what an abandonment means for ITS outcome columns; this decides only the claim.</summary>
    public static ReclaimDecision Reclaim(Claimable claim) =>
        claim.State != CellState.Claimed
            ? new ReclaimDecision.Untouched()
            : claim.Attempts >= MaxAttempts
                ? new ReclaimDecision.Abandoned(
                    claim with { State = CellState.Abandoned, Owner = WorkerIdentity.Nobody },
                    $"abandoned after {claim.Attempts} attempts — a cell that kills its host will kill the next one")
                : new ReclaimDecision.Requeued(
                    claim with { State = CellState.Pending, Owner = WorkerIdentity.Nobody, ClaimedAt = default });

    /// <summary>Whether a claim has gone quiet long enough to be a sweep CANDIDATE. Staleness alone is not
    /// grounds to take a cell back — <see cref="WorkerIdentity.IsProvablyGoneOn"/> answers that.</summary>
    public static bool IsStale(Claimable claim, DateTimeOffset now, TimeSpan staleAfter) =>
        claim.State == CellState.Claimed && now - claim.ClaimedAt >= staleAfter;
}

/// <summary>What a sweep decided about one claim. A closed hierarchy rather than a flag pair, because the
/// three answers carry different payloads and a caller has to handle each by name.</summary>
public abstract record ReclaimDecision
{
    private ReclaimDecision()
    {
    }

    /// <summary>Not claimed, so nothing to hand back — settled, abandoned, or still pending.</summary>
    public sealed record Untouched : ReclaimDecision;

    /// <summary>Handed back to the queue with its attempt count intact.</summary>
    public sealed record Requeued(Claimable Claim) : ReclaimDecision;

    /// <summary>Handed back too many times; terminal. The detail is the sentence the cell stores.</summary>
    public sealed record Abandoned(Claimable Claim, string Detail) : ReclaimDecision;
}
