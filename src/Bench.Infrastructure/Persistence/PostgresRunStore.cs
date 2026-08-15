using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable run store.
/// <para>
/// Every state change is a <b>guarded</b> update — the condition that must hold is in the WHERE clause,
/// not in an <c>if</c> above it. That is the whole difference between "we checked it was Pending" and
/// "only one worker can move it out of Pending": the first is a check-then-act with a window in the
/// middle, and under two workers the window is not theoretical, it is the normal case.
/// </para></summary>
public sealed class PostgresRunStore(BenchDbContext db, TimeProvider clock) : IRunStore
{
    /// <summary>How many times a claim will step over a cell another worker won before giving up. Losing
    /// a race is ordinary; losing this many in a row means the queue is drained, not that we are unlucky.</summary>
    private const int ClaimAttempts = 8;

    public async Task<Outcome<BenchRun>> CreateAsync(
        BenchRun run, IReadOnlyList<RunCell> cells, CancellationToken cancellationToken)
    {
        if (cells.Count == 0)
        {
            return Outcome<BenchRun>.Failure("a run with no cells would look started and could never finish");
        }

        if (await db.Runs.AnyAsync(r => r.Id == run.Id, cancellationToken))
        {
            return Outcome<BenchRun>.Failure($"run {run.Id} already exists");
        }

        db.Runs.Add(ToRow(run));
        db.Cells.AddRange(cells.Select(ToRow));

        // One SaveChanges is one transaction: the run and every cell land together or not at all. This is
        // the "persist before enqueue" guarantee, and it is why nothing here returns before the write.
        await db.SaveChangesAsync(cancellationToken);

        return Outcome<BenchRun>.Success(run);
    }

    public async Task<Outcome<BenchRun>> LoadAsync(Guid runId, CancellationToken cancellationToken)
    {
        var row = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        return row is null
            ? Outcome<BenchRun>.Failure($"no run {runId}")
            : ToDomain(row);
    }

    public async Task<Outcome<RunCell>> ClaimNextAsync(Guid runId, string owner, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return Outcome<RunCell>.Failure("a claim needs an owner — an unowned claim can never be swept correctly");
        }

        for (var attempt = 0; attempt < ClaimAttempts; attempt++)
        {
            var candidate = await NextPendingIdAsync(runId, cancellationToken);

            if (candidate == Guid.Empty)
            {
                return Outcome<RunCell>.Failure($"run {runId} has no pending cell to claim");
            }

            if (await TryClaimAsync(candidate, owner, cancellationToken))
            {
                return await ReadAsync(candidate, cancellationToken);
            }
        }

        return Outcome<RunCell>.Failure($"lost {ClaimAttempts} claim races in a row — the queue is contended, retry");
    }

    public async Task<Outcome<RunCell>> SettleAsync(
        Guid cellId, string owner, LegOutcome outcome, CancellationToken cancellationToken)
    {
        var (kind, detail) = LegOutcomeCodec.Encode(outcome);

        var settled = await db.Cells
            .Where(c => c.Id == cellId && c.State == CellState.Claimed && c.Owner == owner)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Settled)
                      .SetProperty(c => c.OutcomeKind, kind)
                      .SetProperty(c => c.OutcomeDetail, detail),
                cancellationToken);

        return settled == 1
            ? await ReadAsync(cellId, cancellationToken)
            : await ExplainSettleRefusalAsync(cellId, owner, cancellationToken);
    }

    public async Task<SweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - staleAfter;

        // Abandon first, then requeue. The two predicates are disjoint on Attempts, so the order does not
        // change the result — but abandoning first means a cell can never be requeued and abandoned in one
        // sweep, which would report it twice.
        var abandoned = await db.Cells
            .Where(c => c.State == CellState.Claimed && c.ClaimedAt <= cutoff && c.Attempts >= CellLifecycle.MaxAttempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Abandoned)
                      .SetProperty(c => c.Owner, string.Empty)
                      .SetProperty(c => c.OutcomeKind, LegOutcomeKind.Crashed)
                      .SetProperty(c => c.OutcomeDetail, $"abandoned after {CellLifecycle.MaxAttempts} attempts"),
                cancellationToken);

        var requeued = await db.Cells
            .Where(c => c.State == CellState.Claimed && c.ClaimedAt <= cutoff && c.Attempts < CellLifecycle.MaxAttempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Pending)
                      .SetProperty(c => c.Owner, string.Empty),
                cancellationToken);

        return new SweepReport(requeued, abandoned);
    }

    public async Task<RunProgress> ProgressAsync(Guid runId, CancellationToken cancellationToken)
    {
        var counts = await db.Cells.AsNoTracking()
            .Where(c => c.RunId == runId)
            .GroupBy(c => c.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.State, x => x.Count, cancellationToken);

        return new RunProgress(
            counts.GetValueOrDefault(CellState.Pending),
            counts.GetValueOrDefault(CellState.Claimed),
            counts.GetValueOrDefault(CellState.Settled),
            counts.GetValueOrDefault(CellState.Abandoned));
    }

    private async Task<Guid> NextPendingIdAsync(Guid runId, CancellationToken cancellationToken) =>
        await db.Cells.AsNoTracking()
            .Where(c => c.RunId == runId && c.State == CellState.Pending)
            .OrderBy(c => c.Position).ThenBy(c => c.QuestionId).ThenBy(c => c.Repeat)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>The atomic step. <c>State == Pending</c> lives in the WHERE, so exactly one of any number
    /// of concurrent callers can see a row change and the rest see zero.</summary>
    private async Task<bool> TryClaimAsync(Guid cellId, string owner, CancellationToken cancellationToken) =>
        await db.Cells
            .Where(c => c.Id == cellId && c.State == CellState.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.State, CellState.Claimed)
                      .SetProperty(c => c.Owner, owner)
                      .SetProperty(c => c.ClaimedAt, clock.GetUtcNow())
                      .SetProperty(c => c.Attempts, c => c.Attempts + 1),
                cancellationToken) == 1;

    private async Task<Outcome<RunCell>> ReadAsync(Guid cellId, CancellationToken cancellationToken)
    {
        var row = await db.Cells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row is null ? Outcome<RunCell>.Failure($"no cell {cellId}") : Outcome<RunCell>.Success(ToDomain(row));
    }

    private async Task<Outcome<RunCell>> ExplainSettleRefusalAsync(
        Guid cellId, string owner, CancellationToken cancellationToken)
    {
        var row = await db.Cells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cellId, cancellationToken);

        return row switch
        {
            null => Outcome<RunCell>.Failure($"no cell {cellId}"),
            { State: not CellState.Claimed } => Outcome<RunCell>.Failure(
                $"cell {cellId} is {row.State}, not Claimed — only a claimed cell can settle"),
            _ => Outcome<RunCell>.Failure(
                $"cell {cellId} is held by '{row.Owner}', not '{owner}' — a swept-away claim must not overwrite the retry that replaced it"),
        };
    }

    private static RunRow ToRow(BenchRun run) => new()
    {
        Id = run.Id,
        Label = run.Label,
        RepoUrl = run.Target.Repo.Value,
        CommitSha = run.Target.Commit.Value,
        Exclusions = [.. run.Target.Exclusions],
        EngineKind = run.Engine.Kind,
        EngineEndpoint = run.Engine.Endpoint,
        EngineVersion = run.Engine.Version,
        IndexFingerprint = run.Engine.IndexFingerprint,
        SuiteStamp = run.SuiteStamp,
        Status = run.Status,
        CreatedAt = run.CreatedAt,
    };

    private static CellRow ToRow(RunCell cell) => new()
    {
        Id = cell.Id,
        RunId = cell.RunId,
        QuestionId = cell.QuestionId,
        Repeat = cell.Repeat,
        Leg = cell.Leg,
        Position = cell.Position,
        State = cell.State,
        Attempts = cell.Attempts,
        Owner = cell.Owner,
        ClaimedAt = cell.ClaimedAt,
        OutcomeKind = cell.OutcomeKind,
        OutcomeDetail = cell.OutcomeDetail,
    };

    private static RunCell ToDomain(CellRow row) => new(
        row.Id, row.RunId, row.QuestionId, row.Repeat, row.Leg, row.Position,
        row.State, row.Attempts, row.Owner, row.ClaimedAt, row.OutcomeKind, row.OutcomeDetail);

    private static Outcome<BenchRun> ToDomain(RunRow row) =>
        RepoUrl.Parse(row.RepoUrl).Match(
            repo => CommitSha.Parse(row.CommitSha).Match(
                commit => Outcome<BenchRun>.Success(new BenchRun(
                    row.Id,
                    row.Label,
                    new MeasurementTarget(repo, commit, row.Exclusions),
                    new EngineRef(row.EngineKind, row.EngineEndpoint, row.EngineVersion, row.IndexFingerprint),
                    row.SuiteStamp,
                    row.Status,
                    row.CreatedAt)),
                Outcome<BenchRun>.Failure),
            Outcome<BenchRun>.Failure);
}
