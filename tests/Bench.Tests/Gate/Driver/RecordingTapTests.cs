using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Bench.Application.Gate;
using Bench.Infrastructure.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.7 — the recording tap against a loopback upstream: the header value never reaches the disk (not even
/// echoed back in a 401), a dripping upstream is cut at the deadline with the call marked, and a call still open when
/// the cell ends is closed and marked rather than left behind.</summary>
public sealed class RecordingTapTests
{
    private const string Bearer = "Bearer sk-sentinel-5aa91c0e77";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_recorded_files_never_contain_the_authorization_value_even_when_the_vendor_echoes_it()
    {
        await using var upstream = await Upstream.StartAsync(async context =>
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync($"{{\"error\":\"bad key {context.Request.Headers.Authorization}\"}}");
        });
        using var temp = GateStoreFixtures.NewRoot();
        await using var tap = (await new RecordingTapFactory().StartAsync(new TapLaunch(upstream.Url + "/v1", temp.Path, TimeSpan.FromSeconds(30)), Ct)).Ok();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, tap.Endpoint + "/chat/completions")
        {
            Content = new StringContent("{\"model\":\"m\",\"max_tokens\":8192}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(Bearer);
        using var response = await client.SendAsync(request, Ct);
        var seen = await response.Content.ReadAsStringAsync(Ct);
        await tap.CloseAsync(TimeSpan.FromSeconds(5), Ct);

        seen.Should().Contain("sk-sentinel-5aa91c0e77", "the product receives the vendor's bytes unmodified");
        upstream.Authorizations.Should().Equal([Bearer], "the header is forwarded, from memory");
        var files = Directory.EnumerateFiles(temp.Path).ToList();
        files.Select(Path.GetFileName).Should().BeEquivalentTo(["call-01.request.json", "call-01.json", "call-01.response.json"]);
        files.Select(File.ReadAllText).Should().NotContain(t => t.Contains("sk-sentinel-5aa91c0e77", StringComparison.Ordinal),
            "neither the request, the facts nor the echoing 401 may carry the key onto the disk");
        var facts = await File.ReadAllTextAsync(Path.Combine(temp.Path, "call-01.json"), Ct);
        facts.Should().Contain("\"status\":401").And.Contain("Content-Type").And.NotContain("Authorization", "header NAMES are listed, and this one is not");
        (await File.ReadAllTextAsync(Path.Combine(temp.Path, "call-01.request.json"), Ct)).Should().Contain("\"max_tokens\":8192");
    }

    [Fact]
    public async Task The_request_body_is_on_disk_before_the_upstream_has_answered()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var upstream = await Upstream.StartAsync(async context =>
        {
            await release.Task;
            await context.Response.WriteAsync("{}");
        });
        using var temp = GateStoreFixtures.NewRoot();
        await using var tap = (await new RecordingTapFactory().StartAsync(new TapLaunch(upstream.Url, temp.Path, TimeSpan.FromSeconds(30)), Ct)).Ok();
        using var client = new HttpClient();

        var call = client.PostAsync(tap.Endpoint + "/chat/completions", new StringContent("{\"planted\":true}"), Ct);
        await Eventually(() => File.Exists(Path.Combine(temp.Path, "call-01.request.json")));
        File.Exists(Path.Combine(temp.Path, "call-01.json")).Should().BeFalse("the upstream has not answered yet");

        release.SetResult();
        (await call).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_dripping_upstream_is_closed_at_the_deadline_and_the_call_is_marked()
    {
        var aborted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var upstream = await Upstream.StartAsync(async context =>
        {
            context.RequestAborted.Register(() => aborted.TrySetResult());
            context.Response.ContentType = "application/json";
            await context.Response.StartAsync();
            for (var i = 0; i < 600 && !context.RequestAborted.IsCancellationRequested; i++)
            {
                await context.Response.WriteAsync(" ", CancellationToken.None);
                await context.Response.Body.FlushAsync(CancellationToken.None);
                await Task.Delay(100, CancellationToken.None);
            }
        });
        using var temp = GateStoreFixtures.NewRoot();
        await using var tap = (await new RecordingTapFactory().StartAsync(new TapLaunch(upstream.Url, temp.Path, TimeSpan.FromSeconds(1)), Ct)).Ok();
        using var client = new HttpClient();

        var clock = Stopwatch.StartNew();
        using var response = await client.PostAsync(tap.Endpoint + "/chat/completions", new StringContent("{}"), Ct);

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the deadline is absolute — a byte every 100 ms does not extend it");
        response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        (await File.ReadAllTextAsync(Path.Combine(temp.Path, "call-01.json"), Ct)).Should().Contain("\"closed_by_deadline\":true");
        (await Task.WhenAny(aborted.Task, Task.Delay(TimeSpan.FromSeconds(10), Ct))).Should().Be(aborted.Task,
            "the upstream CONNECTION is closed, not merely abandoned while it keeps dripping");
    }

    [Fact]
    public async Task A_call_still_open_when_the_cell_ends_is_closed_and_marked_before_close_returns()
    {
        await using var upstream = await Upstream.StartAsync(async context => await Task.Delay(Timeout.Infinite, context.RequestAborted));
        using var temp = GateStoreFixtures.NewRoot();
        var tap = (await new RecordingTapFactory().StartAsync(new TapLaunch(upstream.Url, temp.Path, TimeSpan.FromMinutes(10)), Ct)).Ok();
        using var client = new HttpClient();

        var call = client.PostAsync(tap.Endpoint + "/chat/completions", new StringContent("{}"), Ct);
        await Eventually(() => File.Exists(Path.Combine(temp.Path, "call-01.request.json")));

        var marked = await tap.CloseAsync(TimeSpan.FromMilliseconds(300), Ct);

        marked.Should().Be(1);
        (await File.ReadAllTextAsync(Path.Combine(temp.Path, "call-01.json"), Ct)).Should().Contain("\"closed_at_cell_end\":true",
            "every forwarded request is answered or marked before the run finishes");
        await tap.DisposeAsync();
        await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(5), Ct));
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(20, Ct);
        }

        condition().Should().BeTrue();
    }

    /// <summary>A loopback vendor stand-in on a free port.</summary>
    private sealed class Upstream : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private Upstream(WebApplication app) => _app = app;

        public string Url { get; private set; } = string.Empty;

        public List<string> Authorizations { get; } = [];

        public static async Task<Upstream> StartAsync(RequestDelegate handler)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, 0));
            var app = builder.Build();
            var upstream = new Upstream(app);
            app.Run(async context =>
            {
                lock (upstream.Authorizations)
                {
                    upstream.Authorizations.Add(context.Request.Headers.Authorization.ToString());
                }

                await handler(context);
            });
            await app.StartAsync();
            upstream.Url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
            return upstream;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync(CancellationToken.None);
            await _app.DisposeAsync();
        }
    }
}
