using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable probe store — <see cref="PostgresGateStore"/>'s two load-bearing shapes over probe cells: the claim is
/// one UPDATE guarded on <c>State == Pending</c>, and the sweep is ownership-checked (<see cref="WorkerIdentity.IsProvablyGoneOn"/>),
/// never time alone, its hand-back ONE guarded UPDATE re-checking everything the decision was taken on.
/// <para>
/// What differs from the gate: a probe run has no status (D3), so no <c>Live</c> filter narrows the sweep — every claimed probe
/// cell is its world; a lane claims FOR ITS SUBJECT in the matrix's order; the unmeasured hand-back keeps the attempt (D8); and
/// a re-run APPENDS a generation, with the unique index as the race guard (D2).
/// </para></summary>
public sealed class PostgresProbeStore(BenchDbContext db, TimeProvider clock) : IProbeStore
{
    /// <summary>How many lost claim races in a row mean "the queue is contended", as in <see cref="PostgresRunStore"/>.</summary>
    private const int ClaimAttempts = 8;

    /// <summary>The Application layer's phrase (<see cref="ProbeClaimRefusal.NoPendingCell"/>) — the campaign reads it off a refusal.</summary>
    public const string NoPendingCell = ProbeClaimRefusal.NoPendingCell;

    public async Task<Outcome<ProbeRun>> PlanAsync(ProbeRun run, IReadOnlyList<ProbeCell> cells, CancellationToken cancellationToken)
    {
        var refusal = (cells.Count, cells.All(c => c.RunId == run.Id), await db.ProbeRuns.AnyAsync(r => r.Id == run.Id, cancellationToken)) switch
        {
            (0, _, _) => "a probe run with no cells would look started and could never finish",
            (_, false, _) => $"every cell of run {run.Id} names that run — a cell planned under another run id would be orphaned",
            (_, _, true) => $"probe run {run.Id} already exists",
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<ProbeRun>.Failure(refusal);
        }

        db.ProbeRuns.Add(ProbeRowMapping.ToRow(run));
        db.ProbeCells.AddRange(cells.Select(ProbeRowMapping.ToRow));

        // One SaveChanges is one transaction: the run and every cell land together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return Outcome<ProbeRun>.Success(run);
    }

    public async Task<Outcome<ProbeRun>> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await db.ProbeRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return row is null ? Outcome<ProbeRun>.Failure($"no probe run {runId}") : ProbeRowMapping.ToDomain(row);
    }

    public async Task<Outcome<ProbeCell>> ClaimNextAsync(
        Guid runId, ProbeSubjectId subject, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken)
    {
        var refusal = (owner.CanClaim, pin.IsPinned) switch
        {
            (false, _) => "a claim needs an owner with a host and a pid — an unowned claim can never be swept correctly",
            (_, false) => "a probe cell is claimed under a product pin, and none was given — the pin is stored per cell at claim time",
            _ => string.Empty,
        };

        return refusal.Length > 0 ? Outcome<ProbeCell>.Failure(refusal) : await ClaimLoopAsync(runId, subject, owner, pin, cancellationToken);
    }

    public async Task<Outcome<ProbeCell>> SettleAsync(Guid cellId, WorkerIdentity owner, ProbeSettlement settlement, CancellationToken cancellationToken)
    {
        // The domain decides whether this settlement may happen at all (the kind, the state); the database decides who holds the cell.
        var decided = (await CellAsync(cellId, cancellationToken)).Match(cell => ProbeCellLifecycle.Settle(cell, settlement), Outcome<ProbeCell>.Failure);

        if (decided is Outcome<ProbeCell>.Fail refused)
        {
            return refused;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The whole identity is in the WHERE, as in PostgresGateStore: a settle from a worker whose claim was swept away must
        // not overwrite the retry that replaced it.
        var settled = await db.ProbeCells
            .Where(c => c.Id == cellId && c.State == CellState.Claimed
                     && c.Owner == owner.Label && c.OwnerHost == owner.Host && c.OwnerPid == owner.Pid)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.State, CellState.Settled).SetProperty(c => c.Reason, ProbeReason.None), cancellationToken);

        if (settled != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ExplainSettleRefusalAsync(cellId, owner, cancellationToken);
        }

        var row = await db.ProbeCells.FirstAsync(c => c.Id == cellId, cancellationToken);
        ProbeRowMapping.Apply(row, settlement.Facts, settlement.Artifacts);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return await CellAsync(cellId, cancellationToken);
    }

    /// <summary>D8, ONE guarded UPDATE — still claimed, by this owner, at this attempt: back to Pending with the attempt KEPT, every
    /// fact <i>not captured</i>, the kind <i>unmeasured</i> and the reason on the row. Never a step toward Abandoned.</summary>
    public async Task<Outcome<ProbeCell>> HandBackUnmeasuredAsync(
        Guid cellId, WorkerIdentity owner, int attempt, ProbeReason reason, CancellationToken cancellationToken)
    {
        if (reason == ProbeReason.None)
        {
            return Outcome<ProbeCell>.Failure($"probe cell {cellId}: a hand-back says why — an account out, never nothing");
        }

        var returned = await db.ProbeCells
            .Where(c => c.Id == cellId && c.State == CellState.Claimed && c.Attempts == attempt
                        && c.Owner == owner.Label && c.OwnerHost == owner.Host && c.OwnerPid == owner.Pid)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Pending)
                      .SetProperty(c => c.Owner, string.Empty)
                      .SetProperty(c => c.OwnerHost, string.Empty)
                      .SetProperty(c => c.OwnerPid, 0)
                      .SetProperty(c => c.ClaimedAt, default(DateTimeOffset))
                      .SetProperty(c => c.AttemptKind, ProbeAttemptKind.Unmeasured)
                      .SetProperty(c => c.CanaryRead, ProbeFact.NotCaptured)
                      .SetProperty(c => c.ReadAttempted, ProbeFact.NotCaptured)
                      .SetProperty(c => c.AnswerCurrent, ProbeFact.NotCaptured)
                      .SetProperty(c => c.ToolEvidence, ProbeFact.NotCaptured)
                      .SetProperty(c => c.ShellUsed, ProbeFact.NotCaptured)
                      .SetProperty(c => c.ReaderOffered, ProbeFact.NotCaptured)
                      .SetProperty(c => c.Reachable, ProbeFact.NotCaptured)
                      .SetProperty(c => c.AccountOut, ProbeFact.NotCaptured)
                      .SetProperty(c => c.Reason, reason),
                cancellationToken);

        return returned == 1
            ? await CellAsync(cellId, cancellationToken)
            : Outcome<ProbeCell>.Failure($"probe cell {cellId} is not claimed by {owner.Canonical} at attempt {attempt} — nothing was handed back");
    }

    public async Task<ProbeSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var stranded = await StrandedAsync(now, now - staleAfter, cancellationToken);

        var requeued = 0;
        var abandoned = 0;

        foreach (var cell in stranded)
        {
            if (await HandBackAsync(cell, cancellationToken) == 1)
            {
                (requeued, abandoned) = cell.Attempts >= Claimable.MaxAttempts ? (requeued, abandoned + 1) : (requeued + 1, abandoned);
            }
        }

        return new ProbeSweepReport(requeued, abandoned);
    }

    /// <summary>D2: the lineage is read, the domain decides (<see cref="ProbeCellLifecycle.NextGeneration"/>), the unique index on
    /// (run, probe, subject, repeat, generation) refuses a second re-run racing for the same number.</summary>
    public async Task<Outcome<ProbeCell>> NextGenerationAsync(Guid cellId, Guid newId, CancellationToken cancellationToken)
    {
        var seed = await db.ProbeCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        if (seed is null)
        {
            return Outcome<ProbeCell>.Failure($"no probe cell {cellId}");
        }

        var lineage = await db.ProbeCells.AsNoTracking()
            .Where(c => c.RunId == seed.RunId && c.Probe == seed.Probe && c.SubjectId == seed.SubjectId && c.Repeat == seed.Repeat)
            .ToListAsync(cancellationToken);

        var next = ProbeCellLifecycle.NextGeneration([.. lineage.Select(ProbeRowMapping.ToDomain).OfType<Outcome<ProbeCell>.Ok>().Select(ok => ok.Value)], newId);

        return next is Outcome<ProbeCell>.Ok ok ? await AppendAsync(ok.Value, cancellationToken) : next;
    }

    private async Task<Outcome<ProbeCell>> AppendAsync(ProbeCell next, CancellationToken cancellationToken)
    {
        db.ProbeCells.Add(ProbeRowMapping.ToRow(next));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return await CellAsync(next.Id, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index fired: another re-run appended this generation between our read and our write. Nothing landed.
            return Outcome<ProbeCell>.Failure($"generation {next.Generation} of {next.Label}'s lineage was appended concurrently — nothing was written; read the lineage again");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>Finding 5: one guarded UPDATE that refuses while any cell of the run is Pending or Claimed.</summary>
    public async Task<Outcome<ProbeRun>> MarkArtifactsPrunedAsync(Guid runId, CancellationToken cancellationToken)
    {
        var marked = await db.ProbeRuns
            .Where(r => r.Id == runId && !db.ProbeCells.Any(c => c.RunId == runId && (c.State == CellState.Pending || c.State == CellState.Claimed)))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ArtifactsPruned, true), cancellationToken);

        if (marked == 1)
        {
            return await LoadAsync(runId, cancellationToken);
        }

        var open = await db.ProbeCells.AsNoTracking()
            .Where(c => c.RunId == runId && (c.State == CellState.Pending || c.State == CellState.Claimed))
            .Select(c => c.State)
            .ToListAsync(cancellationToken);

        return await db.ProbeRuns.AnyAsync(r => r.Id == runId, cancellationToken)
            ? Outcome<ProbeRun>.Failure(
                $"probe run {runId} has {open.Count(s => s == CellState.Pending)} Pending and {open.Count(s => s == CellState.Claimed)} Claimed cell(s) — "
                + "artefacts are pruned only when nothing is left to measure or being measured")
            : Outcome<ProbeRun>.Failure($"no probe run {runId}");
    }

    public async Task<Outcome<ProbeCell>> CellAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var row = await db.ProbeCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row is null ? Outcome<ProbeCell>.Failure($"no probe cell {cellId}") : ProbeRowMapping.ToDomain(row);
    }

    public async Task<IReadOnlyList<ProbeCell>> CellsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.ProbeCells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Slot).ThenBy(c => c.Position).ThenBy(c => c.Generation)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ProbeRowMapping.ToDomain).OfType<Outcome<ProbeCell>.Ok>().Select(ok => ok.Value)];
    }

    private async Task<Outcome<ProbeCell>> ClaimLoopAsync(
        Guid runId, ProbeSubjectId subject, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ClaimAttempts; attempt++)
        {
            var candidate = await NextPendingIdAsync(runId, subject.Value, cancellationToken);

            if (candidate == Guid.Empty)
            {
                return Outcome<ProbeCell>.Failure($"probe run {runId} has {NoPendingCell} for subject '{subject}'");
            }

            if (await TryClaimAsync(candidate, owner, pin, cancellationToken))
            {
                return await CellAsync(candidate, cancellationToken);
            }
        }

        return Outcome<ProbeCell>.Failure($"lost {ClaimAttempts} claim races in a row — the queue is contended, retry");
    }

    /// <summary>The next pending cell of ONE subject, in the MATRIX's order — slot, then position, then generation. A terminal
    /// cell is not Pending and is never a candidate.</summary>
    private async Task<Guid> NextPendingIdAsync(Guid runId, string subject, CancellationToken cancellationToken) =>
        await db.ProbeCells.AsNoTracking()
            .Where(c => c.RunId == runId && c.SubjectId == subject && c.State == CellState.Pending)
            .OrderBy(c => c.Slot).ThenBy(c => c.Position).ThenBy(c => c.Generation)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The atomic step: <c>State == Pending</c> in the WHERE, the attempt count incremented in the same statement, the pin
    /// written with the claim.</summary>
    private async Task<bool> TryClaimAsync(Guid cellId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken) =>
        await db.ProbeCells
            .Where(c => c.Id == cellId && c.State == CellState.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Claimed)
                      .SetProperty(c => c.Owner, owner.Label)
                      .SetProperty(c => c.OwnerHost, owner.Host)
                      .SetProperty(c => c.OwnerPid, owner.Pid)
                      .SetProperty(c => c.ClaimedAt, clock.GetUtcNow())
                      .SetProperty(c => c.Attempts, c => c.Attempts + 1)
                      .SetProperty(c => c.PinBinarySha256, pin.BinarySha256)
                      .SetProperty(c => c.PinVersionText, pin.VersionText)
                      .SetProperty(c => c.PinGitSha, pin.GitSha)
                      .SetProperty(c => c.PinDirtyCaptured, pin.DirtyFiles.WasCaptured)
                      .SetProperty(c => c.PinDirtyFiles, pin.DirtyFiles.Value)
                      .SetProperty(c => c.PinCheckedTree, pin.CheckedTree),
                cancellationToken) == 1;

    /// <summary>Stale claims whose owner is provably gone. A claim stamped AFTER now is clock skew, not a fresh claim (the gate's
    /// S7.3 lesson): it is a candidate like a stale one, and the ownership check decides.</summary>
    private async Task<List<ProbeCellRow>> StrandedAsync(DateTimeOffset now, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var stale = await db.ProbeCells.AsNoTracking()
            .Where(c => c.State == CellState.Claimed && (c.ClaimedAt <= cutoff || c.ClaimedAt > now))
            .ToListAsync(cancellationToken);

        return [.. stale.Where(IsOrphan)];
    }

    /// <summary>THE hand-back: one statement, guarded on every fact the decision read — state, owner triple, claim time, attempt
    /// count — choosing requeue or abandonment inside it. Zero rows means something else moved the cell first.</summary>
    private Task<int> HandBackAsync(ProbeCellRow seen, CancellationToken cancellationToken)
    {
        var abandon = seen.Attempts >= Claimable.MaxAttempts;

        return db.ProbeCells
            .Where(c => c.Id == seen.Id && c.State == CellState.Claimed
                     && c.Owner == seen.Owner && c.OwnerHost == seen.OwnerHost && c.OwnerPid == seen.OwnerPid
                     && c.ClaimedAt == seen.ClaimedAt && c.Attempts == seen.Attempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, abandon ? CellState.Abandoned : CellState.Pending)
                      .SetProperty(c => c.Owner, string.Empty)
                      .SetProperty(c => c.OwnerHost, string.Empty)
                      .SetProperty(c => c.OwnerPid, 0)
                      .SetProperty(c => c.Reason, abandon ? ProbeReason.Abandoned : ProbeReason.None),
                cancellationToken);
    }

    private static bool IsOrphan(ProbeCellRow row) =>
        WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid).IsProvablyGoneOn(WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive);

    private async Task<Outcome<ProbeCell>> ExplainSettleRefusalAsync(Guid cellId, WorkerIdentity owner, CancellationToken cancellationToken)
    {
        var row = await db.ProbeCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row switch
        {
            null => Outcome<ProbeCell>.Failure($"no probe cell {cellId}"),
            { State: not CellState.Claimed } => Outcome<ProbeCell>.Failure($"probe cell {cellId} is {row.State}, not Claimed — only a claimed cell can settle"),
            _ => Outcome<ProbeCell>.Failure(
                $"probe cell {cellId} is held by {WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid).Canonical}, not {owner.Canonical} "
                + "— a swept-away claim must not overwrite the retry that replaced it"),
        };
    }
}
