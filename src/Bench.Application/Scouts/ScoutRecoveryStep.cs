using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>What the entry step did: the owner-checked sweep, then recovery of every open attempt nobody live holds — each settled exactly
/// once (§3.10).</summary>
public sealed record ScoutPrepareReport(ScoutSweepReport Sweep, int Released, int Settled, int ChargedUnknown)
{
    public int Recovered => Released + Settled + ChargedUnknown;
}

/// <summary>The entry step every scout verb runs before it claims (§3.10, "money survives a crash"):
/// <list type="number">
/// <item><b>the owner-checked sweep</b> — a claim whose owner is provably gone goes back to Pending (three attempts abandon it); the sweep
/// moves no money;</item>
/// <item><b>recovery</b> — every attempt still Reserved or Launching whose cell no LIVE owner holds at that attempt is decided by
/// <see cref="ScoutAttemptLifecycle.Recover"/>: no launching mark → its reservation is released (it provably never started); launching
/// with a <c>result</c> that survived in its stream on disk → settled from that <c>total_cost_usd</c>; launching without one → charged its
/// WHOLE reservation as unknown cost, never released after. Each move is the store's guarded UPDATE on the state it was decided on, so an
/// attempt is settled once, never twice, whoever recovers it.</item>
/// </list></summary>
public sealed class ScoutRecoveryStep(IScoutStore store, IScoutStreams streams, OwnerLiveness liveness)
{
    public async Task<ScoutPrepareReport> PrepareAsync(Guid runId, TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var swept = await store.SweepAsync(staleAfter, cancellationToken);
        var cells = (await store.CellsAsync(runId, cancellationToken)).ToDictionary(c => c.Id);
        var orphans = (await store.AttemptsAsync(runId, cancellationToken)).Where(a => a.IsOpen && !HeldLive(a, cells)).ToList();
        var (released, settled, charged) = (0, 0, 0);

        foreach (var attempt in orphans)
        {
            switch (await RecoverAsync(attempt, cancellationToken))
            {
                case ScoutRecovery.Release:
                    released++;
                    break;
                case ScoutRecovery.Settle:
                    settled++;
                    break;
                case ScoutRecovery.ChargeUnknown:
                    charged++;
                    break;
                default:
                    break;
            }
        }

        return new ScoutPrepareReport(swept, released, settled, charged);
    }

    /// <summary>The decision and its guarded move. A move the store refuses (another process recovered it first) counts as nothing.</summary>
    private async Task<ScoutRecovery> RecoverAsync(ScoutAttempt attempt, CancellationToken cancellationToken)
    {
        var decision = ScoutAttemptLifecycle.Recover(attempt, streams.SurvivingCost(attempt));
        var moved = decision switch
        {
            ScoutRecovery.Release => await store.ReleaseAsync(attempt.Id, cancellationToken),
            ScoutRecovery.Settle settle => await store.SettleAttemptAsync(attempt.Id, settle.ActualUsd, cancellationToken),
            ScoutRecovery.ChargeUnknown => await store.ChargeUnknownAsync(attempt.Id, cancellationToken),
            _ => Outcome<ScoutAttempt>.Failure("untouched"),
        };

        return moved is Outcome<ScoutAttempt>.Ok ? decision : new ScoutRecovery.Untouched();
    }

    /// <summary>An attempt a LIVE owner is running right now: its cell is claimed at this attempt's number by an owner that is not provably
    /// gone. Every other open attempt is an orphan — its owner died, or the cell moved on without it.</summary>
    private bool HeldLive(ScoutAttempt attempt, IReadOnlyDictionary<Guid, ScoutCell> cells) =>
        cells.TryGetValue(attempt.CellId, out var cell)
        && cell.State == CellState.Claimed
        && cell.Attempts == attempt.Number
        && !cell.Owner.IsProvablyGoneOn(liveness.Host, liveness.ProcessIsAlive);
}
