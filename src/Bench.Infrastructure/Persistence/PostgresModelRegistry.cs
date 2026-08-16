using Bench.Application.Registry;
using Bench.Domain;
using Bench.Domain.Registry;
using Microsoft.EntityFrameworkCore;

namespace Bench.Infrastructure.Persistence;

/// <summary>The durable model registry.
/// <para>
/// A row is read back through <see cref="ModelConfigJson.Read"/>, which re-applies the references-only
/// rule. That is the point of reading rather than materialising: a row edited by hand to hold a url or an
/// absolute path is refused HERE, before it reaches a run and before it is published.
/// </para></summary>
public sealed class PostgresModelRegistry(BenchDbContext db) : IModelRegistry
{
    public async Task<Outcome<RegisteredModel>> AddAsync(
        RegisteredModel model, CancellationToken cancellationToken)
    {
        if (await db.Models.AnyAsync(m => m.Key == model.Key.Value, cancellationToken))
        {
            return Taken(model.Key.Value);
        }

        db.Models.Add(ToRow(model));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Taken(model.Key.Value);
        }

        return Outcome<RegisteredModel>.Success(model);
    }

    public async Task<Outcome<IReadOnlyList<RegisteredModel>>> ListAsync(
        bool includeDisabled, CancellationToken cancellationToken)
    {
        var rows = await db.Models.AsNoTracking()
            .Where(m => includeDisabled || m.Enabled)
            .OrderBy(m => m.Key)
            .ToListAsync(cancellationToken);

        var models = new List<RegisteredModel>(rows.Count);

        foreach (var row in rows)
        {
            // A row that cannot be read fails the whole listing by name. Skipping it would render a
            // registry quietly missing a model somebody is about to select as a subject.
            if (ToDomain(row) is not Outcome<RegisteredModel>.Ok(var model))
            {
                return Outcome<IReadOnlyList<RegisteredModel>>.Failure(Reason(ToDomain(row)));
            }

            models.Add(model);
        }

        return Outcome<IReadOnlyList<RegisteredModel>>.Success(models);
    }

    public async Task<Outcome<RegisteredModel>> FindAsync(string key, CancellationToken cancellationToken)
    {
        var row = await db.Models.AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, cancellationToken);

        return row is null
            ? Outcome<RegisteredModel>.Failure(
                $"no model '{key}' in the registry — every role draws from that one list, so add it before naming it")
            : ToDomain(row);
    }

    public async Task<Outcome<RegisteredModel>> SetEnabledAsync(
        string key, bool enabled, CancellationToken cancellationToken)
    {
        var changed = await db.Models
            .Where(m => m.Key == key)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Enabled, enabled), cancellationToken);

        return changed == 0
            ? Outcome<RegisteredModel>.Failure($"no model '{key}' in the registry")
            : await FindAsync(key, cancellationToken);
    }

    private static Outcome<RegisteredModel> Taken(string key) =>
        Outcome<RegisteredModel>.Failure(
            $"the key '{key}' is already in the registry — a model is disabled, never replaced, so every run that "
            + "names it still resolves");

    private static ModelRow ToRow(RegisteredModel model) => new()
    {
        Id = model.Id,
        Key = model.Key.Value,
        DisplayName = model.DisplayName,
        Runtime = model.Runtime,
        Hosting = model.Hosting,
        ConfigJson = ModelConfigJson.Write(model.Config),
        Enabled = model.Enabled,
        CreatedAt = model.CreatedAt,
    };

    private static Outcome<RegisteredModel> ToDomain(ModelRow row) =>
        ModelConfigJson.Read(row.ConfigJson).Match(
            config => RegisteredModel.Rehydrate(
                row.Id, row.Key, row.DisplayName, row.Runtime, row.Hosting, config, row.Enabled, row.CreatedAt),
            reason => Outcome<RegisteredModel>.Failure($"model '{row.Key}' cannot be read — {reason}"));

    private static string Reason<T>(Outcome<T> outcome) => outcome.Match(_ => string.Empty, reason => reason);
}

/// <summary>Who answers and who judges, per test.</summary>
public sealed class PostgresRunRoleStore(BenchDbContext db) : IRunRoleStore
{
    /// <summary>Adds subjects to a test. <b>Add-only, and adding LATER is legal</b> — that is the expansion
    /// the matrix is built around: a subject added to a settled test reopens it for exactly its new cells.
    /// Removing one is not, because its settled cells would dangle, and the same model twice is refused by
    /// the composite key rather than silently doubling a column.</summary>
    public Task<Outcome<int>> SaveSubjectsAsync(
        Guid runId, IReadOnlyList<ModelKey> subjects, DateTimeOffset at, CancellationToken cancellationToken) =>
        SaveAsync(
            runId,
            subjects,
            (key, ordinal) => db.RunSubjects.Add(new RunSubjectRow { RunId = runId, ModelKey = key.Value, AddedAt = at }),
            "subject",
            cancellationToken);

    /// <summary>Adds arbiters, continuing the order rather than restarting it: a second arbiter added a
    /// month later is second, and an ordinal that reset would make two models both claim to be primary.</summary>
    public async Task<Outcome<int>> SaveJudgesAsync(
        Guid runId, IReadOnlyList<ModelKey> judges, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var next = await db.RunJudges.AsNoTracking()
            .Where(j => j.RunId == runId)
            .CountAsync(cancellationToken);

        return await SaveAsync(
            runId,
            judges,
            (key, ordinal) => db.RunJudges.Add(
                new RunJudgeRow { RunId = runId, ModelKey = key.Value, Ordinal = next + ordinal, AddedAt = at }),
            "arbiter",
            cancellationToken);
    }

    public async Task<Outcome<IReadOnlyList<RunRole>>> SubjectsAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.RunSubjects.AsNoTracking()
            .Where(s => s.RunId == runId)
            .OrderBy(s => s.AddedAt).ThenBy(s => s.ModelKey)
            .ToListAsync(cancellationToken);

        return Roles(rows.Select((r, i) => (r.RunId, r.ModelKey, Ordinal: i, r.AddedAt)));
    }

    public async Task<Outcome<IReadOnlyList<RunRole>>> JudgesAsync(Guid runId, CancellationToken cancellationToken)
    {
        var rows = await db.RunJudges.AsNoTracking()
            .Where(j => j.RunId == runId)
            .OrderBy(j => j.Ordinal)
            .ToListAsync(cancellationToken);

        return Roles(rows.Select(r => (r.RunId, r.ModelKey, r.Ordinal, r.AddedAt)));
    }

    private async Task<Outcome<int>> SaveAsync(
        Guid runId,
        IReadOnlyList<ModelKey> keys,
        Action<ModelKey, int> add,
        string role,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return Outcome<int>.Failure($"run {runId} names no {role} — a test that measures nobody is not a test");
        }

        var ordinal = 0;

        foreach (var key in keys)
        {
            add(key, ordinal++);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The composite key held: this run already names that model in this role. Adding a NEW one
            // later is legal and is how a settled test reopens; adding the same one twice would double a
            // column in every report the test produces.
            return Outcome<int>.Failure(
                $"run {runId} already names one of these {role}s — a model appears once per role, though another may be added");
        }

        return Outcome<int>.Success(keys.Count);
    }

    private static Outcome<IReadOnlyList<RunRole>> Roles(
        IEnumerable<(Guid RunId, string ModelKey, int Ordinal, DateTimeOffset AddedAt)> rows)
    {
        var roles = new List<RunRole>();

        foreach (var row in rows)
        {
            if (ModelKey.Parse(row.ModelKey) is not Outcome<ModelKey>.Ok(var key))
            {
                return Outcome<IReadOnlyList<RunRole>>.Failure(
                    $"run {row.RunId} names a model key that cannot be read: '{row.ModelKey}'");
            }

            roles.Add(new RunRole(row.RunId, key, row.Ordinal, row.AddedAt));
        }

        return Outcome<IReadOnlyList<RunRole>>.Success(roles);
    }
}
