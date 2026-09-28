using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The gate's read side over the store (E6). It writes nothing: the verdict reads are the verdict store's own
/// mapping (<see cref="PostgresGateVerdictStore"/>, composed rather than copied), and the rubric catalog is built from
/// the distinct (id, kind, hash) the stored verdicts and hand-checks carry — a read host carries no prompt folder.</summary>
public sealed class PostgresGateReads(BenchDbContext db, TimeProvider clock) : IGateReads
{
    public Task<IReadOnlyList<GateRunRecord>> RecordsAsync(CancellationToken cancellationToken) =>
        new GateRecordReader(db).ReadAllAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskSummary>> TasksAsync(string suiteStamp, CancellationToken cancellationToken)
    {
        var rows = await db.GateSuiteTasks.AsNoTracking()
            .Where(t => t.SuiteStamp == suiteStamp).OrderBy(t => t.TaskId).ToListAsync(cancellationToken);

        return [.. rows.SelectMany(GateSuiteTaskMapping.ToDomain)];
    }

    public async Task<IReadOnlySet<string>> RecordedStampsAsync(CancellationToken cancellationToken) =>
        (await db.GateSuiteTasks.AsNoTracking().Select(t => t.SuiteStamp).Distinct().ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlyList<Rubric>> RubricsAsync(CancellationToken cancellationToken)
    {
        var fromVerdicts = await db.GateVerdicts.AsNoTracking()
            .Select(v => new { v.RubricId, v.RubricKind, v.RubricHash }).Distinct().ToListAsync(cancellationToken);
        var fromChecks = await db.GateHandChecks.AsNoTracking()
            .Select(c => new { c.RubricId, c.RubricKind, c.RubricHash }).Distinct().ToListAsync(cancellationToken);

        return [.. fromVerdicts.Concat(fromChecks)
            .Select(r => Rubric.Of(r.RubricId, r.RubricKind, r.RubricHash))
            .OfType<Outcome<Rubric>.Ok>().Select(ok => ok.Value)
            .Distinct()];
    }

    public Task<IReadOnlyList<GateVerdict>> VerdictsAsync(IReadOnlyCollection<Guid> runIds, RubricCatalog catalog, CancellationToken cancellationToken) =>
        new PostgresGateVerdictStore(db, clock).VerdictsAsync(runIds, catalog, cancellationToken);

    public Task<IReadOnlyList<HandCheck>> HandChecksAsync(RubricCatalog catalog, CancellationToken cancellationToken) =>
        new PostgresGateVerdictStore(db, clock).HandChecksAsync(catalog, cancellationToken);

    public async Task<string> PromptHashAsync(Guid runId, CancellationToken cancellationToken) =>
        await db.GateCells.AsNoTracking().Where(c => c.Id == runId).Select(c => c.PromptHash).FirstOrDefaultAsync(cancellationToken)
        ?? string.Empty;
}

/// <summary>Records a suite's task summaries (E6) — all of a stamp's rows in ONE transaction, the same set again a no-op,
/// a different row under a held stamp a refusal naming the task.</summary>
public sealed class PostgresGateSuiteTasks(BenchDbContext db, TimeProvider clock) : IGateSuiteTasks
{
    public async Task<Outcome<int>> RecordAsync(GateSuite suite, CancellationToken cancellationToken)
    {
        var held = await db.GateSuiteTasks.AsNoTracking().Where(t => t.SuiteStamp == suite.Stamp).ToListAsync(cancellationToken);
        var wanted = suite.Tasks.Select(t => t.Summary).ToList();
        var conflict = GateSuiteTaskMapping.Conflict(suite.Stamp, held, wanted);

        return conflict.Length > 0
            ? Outcome<int>.Failure(conflict)
            : await InsertAsync(suite.Stamp, [.. wanted.Where(t => held.All(h => h.TaskId != t.Id.Value))], cancellationToken);
    }

    private async Task<Outcome<int>> InsertAsync(string stamp, IReadOnlyList<TaskSummary> fresh, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        db.GateSuiteTasks.AddRange(fresh.Select(t => GateSuiteTaskMapping.ToRow(stamp, t, now)));

        try
        {
            // One SaveChanges is one transaction: every row of the stamp, or none.
            await db.SaveChangesAsync(cancellationToken);
            return Outcome<int>.Success(fresh.Count);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            return Outcome<int>.Failure($"another process recorded suite {stamp} at the same moment — record it again; the same tasks are then a no-op");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}

/// <summary>A <c>gate_suite_tasks</c> row to and from a <see cref="TaskSummary"/>, and the one comparison a record is
/// refused by.</summary>
internal static class GateSuiteTaskMapping
{
    public static GateSuiteTaskRow ToRow(string stamp, TaskSummary task, DateTimeOffset now)
    {
        var seeds = task.Seeds.OrderBy(s => s.Id.Value, StringComparer.Ordinal).ToList();

        return new GateSuiteTaskRow
        {
            SuiteStamp = stamp,
            TaskId = task.Id.Value,
            Language = task.Language,
            IsCalibration = task.IsCalibration,
            Hosts = task.Hosts.Canonical,
            SeedIds = [.. seeds.Select(s => s.Id.Value)],
            SeedCrossEpic = [.. seeds.Select(s => s.CrossEpic)],
            RecordedAt = now,
        };
    }

    /// <summary>The row back, or nothing when a stored value no longer parses — skipped, the <c>RecentAsync</c> precedent.
    /// A skipped row makes the stamp's set differ from any suite's, so a later record of it is refused rather than silent.</summary>
    public static IEnumerable<TaskSummary> ToDomain(GateSuiteTaskRow row) =>
        (GateTaskId.Parse(row.TaskId), Seeds(row), Hosts(row.Hosts)) switch
        {
            (Outcome<GateTaskId>.Ok task, Outcome<IReadOnlyList<SeedRef>>.Ok seeds, Outcome<HostedGates>.Ok hosts) =>
                [new TaskSummary(task.Value, row.Language, row.IsCalibration, hosts.Value, seeds.Value)],
            _ => [],
        };

    /// <summary>Why <paramref name="wanted"/> cannot be recorded under <paramref name="stamp"/> beside what is
    /// <paramref name="held"/>, or empty. A held row that reads differently from the suite's — or a held task the suite does
    /// not have — is a second reading of one stamp.</summary>
    public static string Conflict(string stamp, IReadOnlyList<GateSuiteTaskRow> held, IReadOnlyList<TaskSummary> wanted)
    {
        var wantedById = wanted.ToDictionary(t => t.Id.Value, t => t.Canonical, StringComparer.Ordinal);
        var differing = held.Where(h => !wantedById.TryGetValue(h.TaskId, out var canonical) || !ReadsAs(h, canonical))
            .Select(h => h.TaskId).FirstOrDefault();

        return differing is null
            ? string.Empty
            : $"suite {stamp} is already recorded with task '{differing}' read differently — a stamp is the hash of its tasks, "
              + "so a second reading of it is a defect in how the summary is derived; nothing of this record was written";
    }

    private static bool ReadsAs(GateSuiteTaskRow row, string canonical) =>
        ToDomain(row).Any(t => string.Equals(t.Canonical, canonical, StringComparison.Ordinal));

    private static Outcome<IReadOnlyList<SeedRef>> Seeds(GateSuiteTaskRow row)
    {
        var ids = row.SeedIds.Select(SeedId.Parse).ToList();

        return ids.Count == row.SeedCrossEpic.Count && ids.All(id => id is Outcome<SeedId>.Ok)
            ? Outcome<IReadOnlyList<SeedRef>>.Success(
                [.. ids.OfType<Outcome<SeedId>.Ok>().Zip(row.SeedCrossEpic, (id, cross) => new SeedRef(id.Value, cross))])
            : Outcome<IReadOnlyList<SeedRef>>.Failure("the seed columns do not read back");
    }

    private static Outcome<HostedGates> Hosts(string canonical)
    {
        var gates = canonical.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(GateWord.Parse).ToList();

        return gates.All(g => g is Outcome<GateKind>.Ok)
            ? HostedGates.Of([.. gates.OfType<Outcome<GateKind>.Ok>().Select(g => g.Value)])
            : Outcome<HostedGates>.Failure($"'{canonical}' is not a list of gates");
    }
}
