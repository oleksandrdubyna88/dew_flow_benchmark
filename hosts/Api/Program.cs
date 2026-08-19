using Bench.Api;
using Bench.Application;
using Bench.Diagnostics;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Before Build(), per the family's logging rule: a host that fails while wiring itself up is exactly when
// the log matters, and a logger installed afterwards has nothing to say about it.
builder.AddDewFlowLogging("bench-api");

// Everything after the logger is inside the try, INCLUDING the refusal below: an early return past the
// finally would skip CloseAndFlush, and the one line an operator needs is the last one such a process
// writes. It survived by luck here — the file sink happens to be synchronous — which is not a guarantee
// worth relying on.
try
{
    var connection = builder.Configuration.GetConnectionString("bench") ?? string.Empty;

    if (connection.Length == 0)
    {
        // The same refusal every stateful CLI verb makes, and for the same reason: a default connection
        // would serve a benchmark's results out of whatever database happened to be listening.
        Serilog.Log.Fatal("no database — set ConnectionStrings__bench");
        return 4;
    }

    builder.Services.AddDbContext<BenchDbContext>(options => options.UseNpgsql(connection));

    // Read ports only. This host binds nothing that writes: no claim path is exercised by a GET, and a
    // write port it never registers cannot be reached by a route that does not exist.
    builder.Services.AddScoped<IRunStore>(services =>
        new PostgresRunStore(services.GetRequiredService<BenchDbContext>(), TimeProvider.System));
    builder.Services.AddScoped<IResultStore>(services =>
        new PostgresResultStore(services.GetRequiredService<BenchDbContext>(), TimeProvider.System));

    var app = builder.Build();

    // It applies NO migrations, alone among this repository's database hosts, and deliberately: two
    // processes racing Migrate() against one database is a defect, the CLI already owns the schema, and a
    // read surface that quietly created an empty one would answer "no run" for a database that had simply
    // never been set up — which reads as "your id is wrong" when the truth is "this is the wrong database".
    app.MapBenchApi();
    app.Run();

    return 0;
}
catch (Exception ex)
{
    Serilog.Log.Fatal(ex, "bench-api stopped unexpectedly");
    return 3;
}
finally
{
    Serilog.Log.CloseAndFlush();
}
