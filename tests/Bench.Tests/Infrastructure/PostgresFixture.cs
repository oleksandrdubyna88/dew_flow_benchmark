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
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

/// <summary>A clock the test owns. Staleness is a decision about elapsed time, and a test that has to
/// really wait thirty minutes to check a thirty-minute rule is a test nobody runs.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
