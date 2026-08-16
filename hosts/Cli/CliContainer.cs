using Bench.Application;
using Bench.Application.Bank;
using Bench.Application.Registry;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Bench.Cli;

/// <summary>The container every working verb runs on, composed in ONE place.
/// <para>
/// It was two places until 2026-08-16, and they had already drifted: both registered a logging stack
/// with no providers at all, so every <c>ILogger</c> call in the live path — the crash-recovery warning
/// that says a leg was scored but never settled, the failed metric write — went nowhere. Code that
/// looks instrumented and says nothing is worse than code with no logging, because nobody goes looking
/// for the gap.
/// </para></summary>
public static class CliContainer
{
    /// <summary>`bench run` — the stores, a model runtime, the checkout cache, one leg at a time, and the
    /// drain around them.</summary>
    public static ServiceProvider ForRun(
        string connectionString, string checkoutRoot, Serilog.ILogger logger) =>
        Model(Store(connectionString, logger))
            .AddSingleton(CheckoutCacheOptions.Under(checkoutRoot))
            .AddScoped<ICheckoutProvider, GitCheckoutProvider>()
            .AddScoped<LegRunner>()
            .AddSingleton<LegDrain>()
            .BuildServiceProvider();

    /// <summary>`bench judge` — stored answers and an arbiter; no run store, no leg runner.</summary>
    public static ServiceProvider ForJudge(string connectionString, Serilog.ILogger logger) =>
        Model(Store(connectionString, logger))
            .AddScoped<JudgeRunner>()
            .BuildServiceProvider();

    /// <summary>`bench sweep` — the run store and nothing else. Recovery must not need a model endpoint.</summary>
    public static ServiceProvider ForSweep(string connectionString, Serilog.ILogger logger) =>
        Store(connectionString, logger).BuildServiceProvider();

    /// <summary>`bench questions` — the bank and nothing else. Importing or reviewing questions must not
    /// need a model endpoint any more than recovery does.</summary>
    public static ServiceProvider ForBank(string connectionString, Serilog.ILogger logger) =>
        Store(connectionString, logger).BuildServiceProvider();

    private static IServiceCollection Store(string connectionString, Serilog.ILogger logger) =>
        new ServiceCollection()
            .AddDbContext<BenchDbContext>(options => options.UseNpgsql(connectionString))
            .AddScoped<PostgresRunStore>()
            .AddScoped<PostgresResultStore>()
            .AddScoped<IRunStore>(s => s.GetRequiredService<PostgresRunStore>())
            .AddScoped<IResultStore>(s => s.GetRequiredService<PostgresResultStore>())
            .AddScoped<IQuestionBank, PostgresQuestionBank>()
            .AddScoped<IRunQuestionStore, PostgresRunQuestionStore>()
            .AddScoped<IModelRegistry, PostgresModelRegistry>()
            .AddScoped<IRunRoleStore, PostgresRunRoleStore>()
            // The one place a secret or a machine path enters the process — the registry stores the NAME.
            .AddSingleton<ISecretSource, EnvironmentSecrets>()
            .AddSingleton(TimeProvider.System)
            // dispose: false — the logger belongs to the PROCESS, which flushes it in its own finally.
            // A container that closed it would silence whatever the shutdown path still has to say.
            .AddLogging(builder => builder.ClearProviders().AddSerilog(logger, dispose: false));

    private static IServiceCollection Model(IServiceCollection services) =>
        services
            .AddHttpClient()
            .AddScoped<IModelRuntime, OpenAiCompatibleRuntime>();
}
