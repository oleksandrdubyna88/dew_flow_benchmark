using Bench.Application;
using Bench.Application.Bank;
using Bench.Application.Registry;
using Bench.Application.Lanes;
using Bench.Application.Variants;
using Bench.Infrastructure.Engines;
using Bench.Infrastructure.Git;
using Bench.Infrastructure.Machine;
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
    /// <summary>`bench run` — the stores, a model runtime, the retrieval engine, the checkout cache, one leg
    /// at a time, and the drain around them.</summary>
    public static ServiceProvider ForRun(
        string connectionString, string checkoutRoot, QlnEngineOptions engine, Serilog.ILogger logger) =>
        Retrieval(Model(Store(connectionString, logger)), engine)
            .AddSingleton(CheckoutCacheOptions.Under(checkoutRoot))
            .AddScoped<ICheckoutProvider, GitCheckoutProvider>()
            // The machine is read once per run, so a scoped probe is one object per verb rather than one
            // per leg — and it holds nothing between calls.
            .AddScoped<IMachineProbe, MachineProbe>()
            // A SINGLETON, and started with the container: it is one background loop for the whole verb,
            // and one per leg would spend its first ticks warming a processor-time delta it then discards.
            // Registered only here — the read-only verbs have no legs to sample beside.
            .AddSingleton<IHardwareSampler>(services => new HardwareSampler(
                services.GetRequiredService<ILogger<HardwareSampler>>(),
                TimeProvider.System,
                SamplingCadence.Default))
            .AddScoped<IVariantCatalog, PostgresVariantCatalog>()
            .AddScoped<ILaneCatalog, PostgresLaneCatalog>()
            // Scoped beside the runner that takes it: it holds no state between legs, and a singleton would
            // outlive the scope whose runtime it asks.
            .AddScoped<ToolLoopRunner>()
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

    /// <summary>`bench questions` and `bench models` — the bank, the registry, and a CLI agent runtime.
    /// <para>
    /// The agent is registered even for the verbs that never launch one: it costs a delegate, and the
    /// alternative is two containers whose difference nobody can predict from the verb name. Importing or
    /// reviewing still needs no model ENDPOINT — that is a different port, and it stays out.
    /// </para></summary>
    public static ServiceProvider ForBank(string connectionString, string checkoutRoot, Serilog.ILogger logger) =>
        Store(connectionString, logger)
            .AddSingleton<ICliAgentRuntime, CliAgentRuntime>()
            // The authoring pass needs the TARGET'S TREE on disk, because an author that cannot read the
            // repository cannot anchor a question in it. Found live: asked to write about a commit it had no
            // access to, the agent refused rather than inventing line numbers — which is the correct answer and
            // a defect in how it was called.
            .AddSingleton(CheckoutCacheOptions.Under(checkoutRoot))
            .AddScoped<ICheckoutProvider, GitCheckoutProvider>()
            // Pre-trusting that tree in the agent CLI's own config, scoped to this root and nothing else.
            // Measured 2026-08-18: two of four authoring groups produced nothing and burned their whole
            // 900-second wall, because the CLI refuses to act in an untrusted workspace and then waits for a
            // dialog no headless run can answer.
            .AddSingleton(new WorkspaceTrust(WorkspaceTrust.DefaultConfigPath, checkoutRoot))
            .BuildServiceProvider();

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

    /// <summary>The retrieval half. A run that named no engine gets <see cref="NoRetriever"/>, which refuses
    /// by name rather than answering with an empty context — an empty context stored as evidence would read
    /// as an index that found nothing.
    /// <para>
    /// The funnel sink is <see cref="NoFunnelSink"/> here on purpose: in the single-shot lane the harness
    /// performs the retrieval itself, so the funnel arrives inside <c>RetrievedContext</c> and is persisted
    /// from there. The sink is the tool lane's path, where a subject makes the call.
    /// </para></summary>
    private static IServiceCollection Retrieval(IServiceCollection services, QlnEngineOptions engine) =>
        services
            .AddSingleton(engine)
            .AddSingleton<IFunnelSink, NoFunnelSink>()
            .AddScoped<IRetriever>(provider => engine.IsConfigured
                ? new QlnRetriever(
                    Client(provider, engine),
                    engine.ProjectId,
                    engine.Branch,
                    provider.GetRequiredService<IFunnelSink>())
                : new NoRetriever());

    private static HttpClient Client(IServiceProvider provider, QlnEngineOptions engine)
    {
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("retrieval-engine");
        client.BaseAddress = new Uri(engine.BaseUrl + "/");

        return client;
    }
}
