using Bench.Domain.Runs;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>A real Postgres for the durability tests.
/// <para>
/// Claim, settle and sweep are guarantees about what happens when two workers reach the same row at the
/// same moment. A fake store would only ever prove that the fake agrees with what we assumed the database
/// does, which is precisely the assumption under test.
/// </para>
/// <para>
/// If Docker is not running this fixture fails <b>loudly</b> rather than skipping. A durability suite that
/// quietly does not run is worse than no suite: it reports green while guarding nothing.
/// </para></summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bench")
        .WithUsername("bench")
        .WithPassword("bench")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "The durability tests need Docker: they run against a real Postgres because claim/settle/sweep "
                + "are guarantees about concurrency that a fake cannot test. Start Docker and re-run.", ex);
        }

        // Applying the MIGRATION rather than EnsureCreated: this is also the only automatic check that the
        // generated migration actually runs against the database it claims to describe.
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public BenchDbContext NewContext() =>
        new(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>A store with its own context — EF's context is not thread-safe, so every concurrent
    /// worker in a test needs its own, exactly as it would in production.</summary>
    public PostgresRunStore NewStore(TimeProvider clock) => new(NewContext(), clock);

    /// <summary>The result store, with its own context and the system clock. It needs a clock because
    /// retention stamps the moment it released a hit's text; a test that cares about that moment passes
    /// its own through the overload below.</summary>
    public PostgresResultStore NewResults() => NewResults(TimeProvider.System);

    public PostgresResultStore NewResults(TimeProvider clock) => new(NewContext(), clock);

    /// <summary>A migrated database of its OWN on the same server.
    /// <para>
    /// For guarantees that are database-WIDE. Retention is the case that forced this: it releases the text of
    /// every hit older than a window, deliberately — a budget that only applied to the run which asked for it
    /// would not be a budget — so on the shared database the first retention test releases every other test's
    /// snippets and counts them as its own. Observed while writing those tests: five of nine failed on rows
    /// they had never written, and the pruned-at stamp came from whichever test pruned first.
    /// </para>
    /// <para>
    /// Cheap enough to be the right answer — one <c>CREATE DATABASE</c> and one migration on a container that
    /// is already running, rather than a second container per test.
    /// </para></summary>
    public async Task<string> NewDatabaseAsync(string name)
    {
        // CREATE DATABASE takes no parameters in any provider, so the name has to be interpolated. It is
        // checked against a character class first rather than trusted: the callers all build it from a Guid,
        // and a check that costs one line is cheaper than relying on that staying true.
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9_]{1,60}$"))
        {
            throw new ArgumentException($"'{name}' is not a safe database name for this fixture", nameof(name));
        }

        await using var server = NewContext();
#pragma warning disable EF1002 // Validated above; a database name cannot travel as a parameter.
        await server.Database.ExecuteSqlRawAsync($"CREATE DATABASE \"{name}\"");
#pragma warning restore EF1002

        var connection = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ToString();

        await using var db = Context(connection);
        await db.Database.MigrateAsync();

        return connection;
    }

    public static BenchDbContext Context(string connectionString) =>
        new(new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connectionString).Options);
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>The owners the durability tests claim as.
/// <para>
/// Since the sweep became ownership-checked, "a dead host" is a specific shape rather than any string: this
/// machine, and a process that cannot be running. Written once because two test classes strand cells and a
/// second copy of the magic pid is a second thing to keep true.
/// </para></summary>
internal static class TestWorkers
{
    /// <summary>A pid no operating system will hand out — Linux caps pids far below this and Windows' are
    /// lower still — so a claim recorded against it is a claim whose owner cannot be running.</summary>
    private const int NoSuchPid = int.MaxValue;

    public static WorkerIdentity Dead(string label) =>
        WorkerIdentity.Stored(label, Environment.MachineName, NoSuchPid);
}

/// <summary>A clock the test owns. Staleness is a decision about elapsed time, and a test that has to
/// really wait thirty minutes to check a thirty-minute rule is a test nobody runs.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
