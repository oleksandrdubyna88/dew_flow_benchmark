using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Bench.Tests.Ui.GateUiFixtures;

namespace Bench.Tests.Ui;

/// <summary>The Gate page when answers arrive out of order (own review of E6): a reader who switches scope while the first
/// scope's table is still on its way must see the SECOND scope's table under the second scope's header — never the late
/// answer of the scope they left.</summary>
public sealed class GateScopeRaceTests : BunitContext
{
    [Fact]
    public void A_late_answer_for_a_scope_the_reader_left_never_replaces_the_table_of_the_scope_they_chose()
    {
        var a = Scope("aaaaaaaaaaa1", "0.39.0+aaa", true, Strict);
        var b = Scope("aaaaaaaaaaa2", "0.39.1+bbb", true, Strict);
        var api = new GatedBenchApi()
            .Answers("/api/bench/gate/scopes?gate=feature", new[] { a, b })
            .Answers($"/api/bench/gate/feature/runs?scope={a.Id}", Array.Empty<GateRunSummaryDto>())
            .Answers($"/api/bench/gate/feature/runs?scope={b.Id}", Array.Empty<GateRunSummaryDto>())
            .Held($"/api/bench/gate/feature/models?scope={a.Id}&rubric={Uri.EscapeDataString(Strict.Stamp)}", Table(a, Strict, [Row("reviewer-of-a")]))
            .Answers($"/api/bench/gate/feature/models?scope={b.Id}&rubric={Uri.EscapeDataString(Strict.Stamp)}", Table(b, Strict, [Row("reviewer-of-b")]));
        Services.AddSingleton(new BenchConsoleApi(new HttpClient(api) { BaseAddress = ScriptedBenchApi.BaseAddress }));
        var page = Render<GateFeature>();

        page.Find("#gate-scope").Change(a.Id);
        page.Find("#gate-scope").Change(b.Id);
        page.WaitForAssertion(() => page.Markup.Should().Contain("reviewer-of-b"));
        var renders = page.RenderCount;
        api.Release();

        page.WaitForState(() => api.Released && page.RenderCount > renders, TimeSpan.FromSeconds(10));
        page.Markup.Should().Contain("reviewer-of-b").And.NotContain("reviewer-of-a",
            "the late answer belongs to a scope the reader already left, and a table under another scope's header is a wrong number");
        page.Find("[data-test=gate-scope-header]").TextContent.Should().Contain("0.39.1+bbb");
    }

    /// <summary>Answers by path AND query (the ordering is the point, so the query is part of the key here), and holds ONE
    /// answer back until <see cref="Release"/>.</summary>
    private sealed class GatedBenchApi : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentDictionary<string, object> _answers = new(StringComparer.Ordinal);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _held = string.Empty;

        public bool Released { get; private set; }

        public GatedBenchApi Answers(string pathAndQuery, object body)
        {
            _answers[pathAndQuery] = body;
            return this;
        }

        public GatedBenchApi Held(string pathAndQuery, object body)
        {
            _held = pathAndQuery;
            return Answers(pathAndQuery, body);
        }

        public void Release() => _gate.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.RequestUri!.PathAndQuery;
            if (key == _held)
            {
                await _gate.Task;
                Released = true;
            }

            return _answers.TryGetValue(key, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body, Web), Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
        }
    }
}
