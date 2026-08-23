using Bench.Api;
using Bench.Application.Sessions;
using Bench.Diagnostics;
using Bench.Domain.Sessions;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Before Build(), per the family's logging rule: a host that fails while wiring itself up is exactly when
// the log matters, and a logger installed afterwards has nothing to say about it.
builder.AddDewFlowLogging("bench-collector");

try
{
    var connection = builder.Configuration.GetConnectionString("bench") ?? string.Empty;

    if (connection.Length == 0)
    {
        // The same refusal every stateful verb makes: a default connection would write an operator's whole
        // working history into whatever database happened to be listening.
        Serilog.Log.Fatal("no database — set ConnectionStrings__bench");
        return 4;
    }

    // Loopback ONLY, and not merely by convention. This endpoint accepts unauthenticated writes describing
    // every file an operator touched; binding it to a routable address would publish that to the network.
    builder.WebHost.UseUrls($"http://127.0.0.1:{Port(builder.Configuration)}");

    builder.Services.AddDbContext<BenchDbContext>(options => options.UseNpgsql(connection));
    builder.Services.AddScoped<ISessionStore>(services =>
        new PostgresSessionStore(services.GetRequiredService<BenchDbContext>(), ToolTaxonomy.ClaudeCode));

    var app = builder.Build();

    var schema = await SchemaRefusalAsync(app);
    if (schema.Length > 0)
    {
        Serilog.Log.Fatal("{Refusal}", schema);
        return 4;
    }

    // Both halves here, and only here. bench-api serves the reads too; this host serves them as well so a
    // dashboard can follow a live session without the whole report stack being up.
    app.MapSessionIngest();
    app.MapSessionReads();

    Serilog.Log.Information(
        "bench-collector listening on http://127.0.0.1:{Port} — POST /api/bench/sessions/events",
        Port(builder.Configuration));

    app.Run();

    return 0;
}
catch (Exception ex)
{
    Serilog.Log.Fatal(ex, "bench-collector stopped unexpectedly");
    return 3;
}
finally
{
    Serilog.Log.CloseAndFlush();
}

/// <summary>The port the hook clients are configured against.
/// <para>
/// PINNED, and that is the requirement rather than a convenience: the address lives in a
/// <c>.claude/settings.json</c> inside every repository being measured, and a dynamic port would silently
/// point every one of them at nothing the next time this host restarted. The same lesson the AppHost
/// records about its Postgres port, which cost a day of 500s when it drifted.
/// </para></summary>
static int Port(IConfiguration configuration) =>
    int.TryParse(configuration["Collector:Port"], out var port) ? port : 5177;

/// <summary>Why this host will not serve, or an empty string when it will.
/// <para>
/// It does not migrate — the CLI owns the schema, and two processes racing <c>Migrate()</c> is a defect
/// this repository already refuses elsewhere. But it must not START without one either: a write surface
/// that accepts events and drops them loses data that cannot be re-read, and the failure would look like a
/// quiet afternoon rather than a broken instrument. So it checks, and names the fix.
/// </para></summary>
static async Task<string> SchemaRefusalAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<BenchDbContext>();

    try
    {
        var pending = await db.Database.GetPendingMigrationsAsync();

        return pending.Any()
            ? $"the bench schema is {pending.Count()} migration(s) behind — the CLI owns it: run `bench sessions list --db <connection>` once, then start this host"
            : string.Empty;
    }
    catch (Npgsql.NpgsqlException ex)
    {
        return $"database unreachable — {ex.Message}";
    }
}
