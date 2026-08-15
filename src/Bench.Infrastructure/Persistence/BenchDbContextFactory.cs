using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bench.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without a host.
/// <para>
/// The connection string here is never used to connect — the tool only needs a provider so it can decide
/// what SQL a migration should emit. Schema changes therefore ship as versioned migrations rather than as
/// <c>EnsureCreated</c>, which works exactly once and then cannot evolve a database that has data in it.
/// </para></summary>
public sealed class BenchDbContextFactory : IDesignTimeDbContextFactory<BenchDbContext>
{
    public BenchDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BenchDbContext>()
            .UseNpgsql("Host=localhost;Database=bench_design_time;Username=bench;Password=bench")
            .Options);
}
