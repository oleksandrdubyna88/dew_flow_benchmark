using System.Net;
using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>The driver against the REAL product binary — the one place the fake's idea of coai-mcp meets coai-mcp.
/// <para>
/// The vendor is a loopback stand-in answering every completion with an empty review, reached through a <c>local</c>
/// reviewer row whose endpoint is a REFERENCE (a loopback address is never a row value), so no quota is spent and nothing
/// leaves the machine; the product's data directory is the attempt's own, never the operator's. Excluded from the
/// default run by its trait and SKIPPED when unconfigured, the <c>QlnEngineLiveTests</c> shape:
/// <code>
/// $env:BENCH_GATE_COAI_EXE="&lt;path to coai-mcp&gt;"
/// ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-trait "Category=Live" --filter-class "*GateDriverLiveTests"
/// </code>
/// </para></summary>
[Trait("Category", "Live")]
[Collection("postgres")]
public sealed class GateDriverLiveTests(PostgresFixture postgres)
{
    private const string ExeVariable = "BENCH_GATE_COAI_EXE";
    private const string VendorReference = "BENCH_LIVE_VENDOR_URL";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_real_product_answers_providers_and_one_plan_round_and_the_run_is_stored()
    {
        var exe = Environment.GetEnvironmentVariable(ExeVariable) ?? string.Empty;
        Assert.SkipWhen(exe.Length == 0 || !File.Exists(exe), $"set {ExeVariable} to a coai-mcp binary to run against the real product");

        await using var vendor = await FakeVendor.StartAsync();
        await using var rig = await GateDriverRig.StartAsync(postgres);
        rig.Product = exe;
        rig.References = new Dictionary<string, string> { [VendorReference] = vendor.Url };
        var reviewer = GateReviewer.Create(
            GateReviewerId.Parse("live-local").Ok(),
            ReviewerDefinition.Parse(
                ReviewerRuntime.Local, "fake-model", ReviewerEndpoint.Parse(VendorReference).Ok(), string.Empty, string.Empty, string.Empty, string.Empty,
                ReviewerTransport.Parse("openai", "none", 2048, 5, 0, 10, false).Ok(), ReviewerPrices.Unknown, HostedGates.All).Ok(),
            GateStoreFixtures.Noon);

        var tools = await ProvidersAsync(exe, rig, reviewer, vendor.Url);
        tools.Should().Contain(["providers", "open", "review_plan", "review_code", "resolve"]);

        var (run, cells) = await rig.PlanAsync(GateKind.Plan, [reviewer], repeats: 1);
        rig.Pins.StartAt((await new ProductPinReader().ReadAsync(exe, Ct)).Ok());
        var report = await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        report.Settled.Should().Be(1, report.Reason);
        await using var db = PostgresFixture.Context(rig.Connection);
        var row = await db.GateCells.SingleAsync(c => c.Id == cells[0].Id, Ct);
        row.FactsRecorded.Should().BeTrue("the product session was driven to an end and stored");
        row.ServerVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+$", "serverInfo.version from the real handshake");
        TestContext.Current.TestOutputHelper?.WriteLine($"live: outcome {row.OutcomeKind}, verdict {row.Verdict}, valid {row.Valid}, turns {row.Turns}, failure {row.FailureKind}: {row.FailureText}; vendor calls {vendor.Calls}");
    }

    private static async Task<IReadOnlyList<string>> ProvidersAsync(string exe, GateDriverRig rig, GateReviewer reviewer, string vendorUrl)
    {
        var data = Path.Combine(rig.Root.Path, "probe-data");
        Directory.CreateDirectory(data);
        var run = GateRun.Planned(Guid.CreateVersion7(), GateKind.Plan, "probe", DataDirMode.Isolated, DateTimeOffset.UtcNow);
        var environment = CoaiEnvironment.For(
            new CoaiEnvironmentInputs(new ArtifactScope(run, Guid.NewGuid(), 1), reviewer, GateTaskId.Parse("probe").Ok(), 1,
                GateRunSettings.With(new Dictionary<string, string>()).Ok(), rig.Fake.Environment(), data),
            CoaiVendorsSetting.From([reviewer], GateKind.Plan, new ResolvedReferences(new Dictionary<string, string> { [VendorReference] = vendorUrl })).Ok());
        Directory.CreateDirectory(environment.DataDir);

        await using var session = (await new McpStdioSessionFactory().OpenAsync(
            new McpLaunch(exe, [], data, environment.WithSecret(Bench.Domain.Gate.SecretValue.None).Variables, Path.Combine(data, "stderr.txt"), TimeSpan.FromMinutes(2)), Ct)).Ok();
        var tools = (await session.ListToolsAsync(TimeSpan.FromMinutes(1), Ct)).Ok();
        (await session.CallToolAsync("providers", [], TimeSpan.FromMinutes(2), Ct)).Ok().Text.Should().NotBeEmpty();

        return tools;
    }

    /// <summary>A loopback OpenAI-shaped vendor that answers every completion with an empty review.</summary>
    private sealed class FakeVendor : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private int _calls;

        private FakeVendor(WebApplication app) => _app = app;

        public string Url { get; private set; } = string.Empty;

        public int Calls => _calls;

        public static async Task<FakeVendor> StartAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, 0));
            var app = builder.Build();
            var vendor = new FakeVendor(app);
            app.Run(async context =>
            {
                Interlocked.Increment(ref vendor._calls);
                context.Response.ContentType = "application/json";
                var content = new JsonObject { ["findings"] = new JsonArray(), ["notes"] = "nothing to add" }.ToJsonString();
                await context.Response.WriteAsync(context.Request.Path.Value!.EndsWith("/models", StringComparison.Ordinal)
                    ? """{"data":[{"id":"fake-model"}]}"""
                    : new JsonObject
                    {
                        ["id"] = "c1",
                        ["object"] = "chat.completion",
                        ["model"] = "fake-model",
                        ["choices"] = new JsonArray(new JsonObject { ["index"] = 0, ["finish_reason"] = "stop", ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content } }),
                        ["usage"] = new JsonObject { ["prompt_tokens"] = 100, ["completion_tokens"] = 10, ["total_tokens"] = 110 },
                    }.ToJsonString());
            });
            await app.StartAsync();
            vendor.Url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/') + "/v1";
            return vendor;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync(CancellationToken.None);
            await _app.DisposeAsync();
        }
    }
}
