using Bench.Application;
using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The readiness of each corpus, with every transition guarded where concurrency actually happens.
/// <para>
/// The pattern is <see cref="PostgresRunStore"/>'s, deliberately: the whole owner triple is in the WHERE of
/// every update, so a worker cannot close or beat a pass another one is watching, and losing that race is a
/// refusal rather than a silent overwrite.
/// </para></summary>
public sealed class PostgresPreparationStore(BenchDbContext db, TimeProvider clock) : IPreparationStore
{
    public async Task<Outcome<IndexPreparation>> RequestAsync(CorpusKey key, CancellationToken cancellationToken)
    {
        var existing = await FindAsync(key, cancellationToken);

        if (existing is Outcome<IndexPreparation>.Ok)
        {
            return existing;
        }

        db.Preparations.Add(ToRow(IndexPreparation.Requested(key, clock.GetUtcNow())));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index absorbing a race: another run asked for this corpus between the read above and
            // this insert. Its row is the answer — there is exactly one readiness per index by construction.
            db.ChangeTracker.Clear();
            return await FindAsync(key, cancellationToken);
        }

        return await FindAsync(key, cancellationToken);
    }

    public async Task<Outcome<IndexPreparation>> FindAsync(CorpusKey key, CancellationToken cancellationToken)
    {
        var row = await Rows(key).AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? Outcome<IndexPreparation>.Failure($"no preparation for corpus {key.Canonical}")
            : ToDomain(row);
    }

    public async Task<Outcome<IndexPreparation>> StartAsync(
        CorpusKey key, WorkerIdentity owner, string passId, CancellationToken cancellationToken)
    {
        if (!owner.CanClaim)
        {
            return Outcome<IndexPreparation>.Failure(
                "a preparation needs an owner with a host and a pid — one nobody can vouch for can never be swept");
        }

        var started = await Rows(key).Where(p => p.State == PreparationState.Requested)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.State, PreparationState.Building)
                    .SetProperty(p => p.Owner, owner.Label)
                    .SetProperty(p => p.OwnerHost, owner.Host)
                    .SetProperty(p => p.OwnerPid, owner.Pid)
                    .SetProperty(p => p.PassId, passId)
                    .SetProperty(p => p.Heartbeat, clock.GetUtcNow()),
                cancellationToken);

        return started == 1
            ? await FindAsync(key, cancellationToken)
            : await RefuseAsync(key, "started", cancellationToken);
    }

    public async Task<Outcome<IndexPreparation>> BeatAsync(
        CorpusKey key, WorkerIdentity owner, CancellationToken cancellationToken)
    {
        var beaten = await Held(key, owner)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.Heartbeat, clock.GetUtcNow()), cancellationToken);

        return beaten == 1
            ? await FindAsync(key, cancellationToken)
            : await RefuseAsync(key, "refreshed", cancellationToken);
    }

    public async Task<Outcome<IndexPreparation>> EndAsync(
        CorpusKey key,
        WorkerIdentity owner,
        PreparationState state,
        string reason,
        CancellationToken cancellationToken)
    {
        if (state is not (PreparationState.Ready or PreparationState.Failed))
        {
            return Outcome<IndexPreparation>.Failure($"{state} is not an ending — a preparation ends Ready or Failed");
        }

        var ended = await Held(key, owner)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.State, state)
                    .SetProperty(p => p.Owner, string.Empty)
                    .SetProperty(p => p.OwnerHost, string.Empty)
                    .SetProperty(p => p.OwnerPid, 0)
                    .SetProperty(p => p.Reason, reason)
                    .SetProperty(p => p.Heartbeat, clock.GetUtcNow()),
                cancellationToken);

        return ended == 1
            ? await FindAsync(key, cancellationToken)
            : await RefuseAsync(key, "ended", cancellationToken);
    }

    /// <summary>Strands what a dead worker left building, and nothing else.
    /// <para>
    /// Loads only the stale CANDIDATES and asks the domain about each: staleness selects, ownership decides. A
    /// blanket time-based update is what would requeue a colleague's twenty-four-minute pass — and this
    /// repository has already paid for that mistake once, in the cell sweep.
    /// </para></summary>
    public async Task<PreparationSweep> SweepAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - window;

        var candidates = await db.Preparations
            .Where(p => p.State == PreparationState.Building && p.Heartbeat <= cutoff)
            .ToListAsync(cancellationToken);

        var stranded = 0;

        foreach (var row in candidates)
        {
            if (Strand(row, now, window) is { } reason)
            {
                row.State = PreparationState.Failed;
                row.Owner = string.Empty;
                row.OwnerHost = string.Empty;
                row.OwnerPid = 0;
                row.Reason = reason;
                stranded++;
            }
        }

        if (stranded > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new PreparationSweep(stranded);
    }

    public async Task<IReadOnlyList<IndexPreparation>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Preparations.AsNoTracking()
            .OrderBy(p => p.RequestedAt)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToDomain).OfType<Outcome<IndexPreparation>.Ok>().Select(ok => ok.Value)];
    }

    /// <summary>The reason a row should be stranded, or null when it should be left alone. The decision itself
    /// is the domain's — this only supplies the machine's own name and its process table.</summary>
    private static string? Strand(PreparationRow row, DateTimeOffset now, TimeSpan window)
    {
        var preparation = ToDomain(row);

        if (preparation is not Outcome<IndexPreparation>.Ok(var current))
        {
            return null;
        }

        // WorkerLiveness, not a second probe: the cell sweep already owns this question, and its three catch
        // clauses encode a decision a fresh copy would get wrong — being REFUSED an answer about a pid is not
        // being told the process ended, so an unanswerable owner counts as alive.
        var swept = current.Strand(now, window, WorkerLiveness.ThisHost, WorkerLiveness.ProcessIsAlive);

        return swept.State == PreparationState.Failed ? swept.Reason : null;
    }

    private async Task<Outcome<IndexPreparation>> RefuseAsync(
        CorpusKey key, string verb, CancellationToken cancellationToken)
    {
        var current = await FindAsync(key, cancellationToken);

        return current.Match(
            row => Outcome<IndexPreparation>.Failure(
                $"corpus {key.Canonical} could not be {verb}: it is {row.State}"
                + (row.Owner.IsTraceable ? $", held by {row.Owner.Canonical}" : string.Empty)),
            reason => Outcome<IndexPreparation>.Failure(reason));
    }

    private IQueryable<PreparationRow> Rows(CorpusKey key) =>
        db.Preparations.Where(p =>
            p.CommitSha == key.Commit.Value
            && p.RecipeHash == key.RecipeHash
            && p.EngineEndpoint == key.EngineEndpoint);

    /// <summary>The whole owner triple in the WHERE, never the label alone: two machines may honestly both
    /// call themselves "indexer", and a guard on the label would let one close the other's pass.</summary>
    private IQueryable<PreparationRow> Held(CorpusKey key, WorkerIdentity owner) =>
        Rows(key).Where(p =>
            p.State == PreparationState.Building
            && p.Owner == owner.Label
            && p.OwnerHost == owner.Host
            && p.OwnerPid == owner.Pid);

    private static PreparationRow ToRow(IndexPreparation preparation) => new()
    {
        Id = preparation.Id,
        CommitSha = preparation.Key.Commit.Value,
        RecipeHash = preparation.Key.RecipeHash,
        EngineEndpoint = preparation.Key.EngineEndpoint,
        State = preparation.State,
        Owner = preparation.Owner.Label,
        OwnerHost = preparation.Owner.Host,
        OwnerPid = preparation.Owner.Pid,
        PassId = preparation.PassId,
        Reason = preparation.Reason,
        RequestedAt = preparation.RequestedAt,
        Heartbeat = preparation.Heartbeat,
    };

    /// <summary>Rebuilds a row. The commit is re-parsed rather than trusted: a hand-edited row is a real
    /// event, and a preparation naming a sha nobody can read is refused rather than served.</summary>
    private static Outcome<IndexPreparation> ToDomain(PreparationRow row) =>
        CommitSha.Parse(row.CommitSha).Match(
            commit => Outcome<IndexPreparation>.Success(new IndexPreparation(
                row.Id,
                new CorpusKey(commit, row.RecipeHash, row.EngineEndpoint),
                row.State,
                WorkerIdentity.Stored(row.Owner, row.OwnerHost, row.OwnerPid),
                row.PassId,
                row.Reason,
                row.RequestedAt,
                row.Heartbeat)),
            reason => Outcome<IndexPreparation>.Failure($"preparation {row.Id} names an unreadable commit: {reason}"));
}
