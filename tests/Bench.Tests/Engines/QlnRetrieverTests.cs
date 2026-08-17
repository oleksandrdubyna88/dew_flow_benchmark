using System.Net;
using System.Text;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Domain.Variants;
using Bench.Infrastructure.Engines;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Engines;

/// <summary>The retrieval half of the QLN adapter, against a stubbed wire.
/// <para>
/// Every payload here is the shape <c>dew_flow_rag_qln</c> emits (<c>Platform.Contracts.SearchResponse</c>),
/// so these tests are the cross-repository contract from this side: if that engine renames a field or stops
/// echoing its axes, one of these goes red HERE rather than producing a column of zeroes in a run three
/// weeks from now.
/// </para></summary>
public sealed class QlnRetrieverTests
{
    private static readonly Guid Project = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string Funnel =
        """
        "funnel": {
          "contractVersion": "trace/v0", "totalMs": 3104, "absent": ["graph-enrich"],
          "stages": [
            { "name": "collection", "in": 1, "out": 1, "ms": 4 },
            { "name": "embed-query", "in": 1, "out": 1, "ms": 380 },
            { "name": "retrieve", "in": 200, "out": 145, "ms": 210 },
            { "name": "fuse", "in": 145, "out": 145, "ms": 2 },
            { "name": "collapse", "in": 145, "out": 96, "ms": 6 },
            { "name": "rerank", "in": 50, "out": 20, "ms": 2500 },
            { "name": "cut", "in": 96, "out": 20, "ms": 1 }
          ]
        }
        """;

    /// <summary>The axes as the engine echoes them: AS APPLIED, after its own clamping.</summary>
    private const string Echo =
        """
        "axes": { "limit": 20, "dense": true, "sparse": true, "denseWidth": 100, "sparseWidth": 100,
                  "denseWeight": 1, "sparseWeight": 1, "rrfK": 60, "collapseMembers": true,
                  "rerank": true, "rerankPool": 50, "rerankFloor": -1000 }
        """;

    private const string Hit =
        """
        {
          "id": "abc", "memberKey": "csharp|Polly.Retry|RetryHelper|DecorrelatedJitterBackoffV2`0|(int, TimeSpan)",
          "relativePath": "src/Polly.Core/Retry/RetryHelper.cs", "startLine": 75, "endLine": 111,
          "typeName": "RetryHelper", "memberName": "DecorrelatedJitterBackoffV2",
          "signature": "internal static TimeSpan DecorrelatedJitterBackoffV2(int attempt, TimeSpan baseDelay)",
          "text": "internal static TimeSpan DecorrelatedJitterBackoffV2(...) { ... }",
          "score": 0.913, "ordering": "rerank", "fusedScore": 0.41,
          "channels": ["dense", "sparse"], "ranks": [3, 7]
        }
        """;

    private static string Answer(string hits = Hit) =>
        $$"""{ "hits": [{{hits}}], "collection": "code_ab12cd34", {{Echo}}, {{Funnel}} }""";

    [Fact]
    public async Task A_retrieval_returns_the_hits_with_everything_a_metric_and_a_prompt_need()
    {
        var (retriever, _) = Build(Answer());

        var context = (await Retrieve(retriever)).Ok();

        var hit = context.Hits.Should().ContainSingle().Subject;
        hit.Rank.Should().Be(1, "the ORDER is the measurement, so it is stored rather than inferred later");
        hit.RelativePath.Should().Be("src/Polly.Core/Retry/RetryHelper.cs");
        hit.StartLine.Should().Be(75);
        hit.EndLine.Should().Be(111);
        hit.Member.Should().Be("RetryHelper.DecorrelatedJitterBackoffV2", "this is the form a suite's anchors use");
        hit.MemberKey.Should().StartWith("csharp|", "and this is the engine's own, stored verbatim and never matched on");
        hit.Channels.Should().Equal("dense", "sparse");
        // Which head found it and how deep: what separates a recall failure from a ranking failure.
        hit.Ranks.Should().Equal([3, 7]);
        hit.Snippet.HasText.Should().BeTrue();
        context.Collection.Should().Be("code_ab12cd34");
    }

    [Fact]
    public async Task The_recipe_reaches_the_wire_as_this_engines_own_axis_names()
    {
        var (retriever, calls) = Build(Answer());

        await Retrieve(retriever, Recipe(RetrievalChannels.Sparse, limit: 7, rerankPool: 30));

        var body = calls.Single().Body;
        body.Should().Contain("\"limit\":7");
        body.Should().Contain("\"dense\":false").And.Contain("\"sparse\":true");
        body.Should().Contain("\"rerank\":true").And.Contain("\"rerankPool\":30");
        body.Should().Contain("\"rrfK\":60");
    }

    [Fact]
    public async Task An_axis_this_run_did_not_set_is_LEFT_OUT_rather_than_sent_as_a_zero()
    {
        var (retriever, calls) = Build(Answer());

        await Retrieve(retriever, Recipe(RetrievalChannels.Hybrid, limit: 20, rerankPool: 0));

        // A reranker that is off has no pool, and serialising a C# default would send `rerankPool: 0` — which
        // the engine clamps to 1, producing a run that measured a configuration in no catalog row.
        calls.Single().Body.Should().NotContain("rerankPool");
        calls.Single().Body.Should().Contain("\"rerank\":false");
    }

    [Fact]
    public async Task The_request_carries_ONLY_fields_the_engines_own_axes_contract_defines()
    {
        var (retriever, calls) = Build(Answer());

        await Retrieve(retriever);

        // Found by loosening the ignore condition above and reading what actually went out: the computed
        // `Axes` property was being serialised INTO the request as a nested axes.axes object. Harmless while
        // the engine ignores members it does not know — and the sibling plan's next step is to make it refuse
        // them, which would have turned every retrieval here into a 400 on the day it landed.
        var axes = System.Text.Json.JsonDocument.Parse(calls.Single().Body).RootElement.GetProperty("axes");

        axes.EnumerateObject().Select(p => p.Name).Should().BeSubsetOf(
            ["limit", "dense", "sparse", "denseWidth", "sparseWidth", "denseWeight", "sparseWeight",
             "rrfK", "collapseMembers", "rerank", "rerankPool", "rerankFloor"],
            "an axis name this engine does not define is a field it will one day refuse");
    }

    [Fact]
    public async Task What_the_run_ASKED_and_what_the_engine_APPLIED_are_both_stored()
    {
        var (retriever, _) = Build(Answer());

        var context = (await Retrieve(retriever, Recipe(RetrievalChannels.Dense, limit: 5, rerankPool: 0))).Ok();

        context.Requested.Canonical.Should().Contain("limit=5").And.Contain("dense=true").And.Contain("sparse=false");

        // Every field the engine echoes, including ones this build never sets: the axis whose appearance
        // matters most is the one we did not know about.
        context.Applied.Canonical.Should().Contain("limit=20").And.Contain("collapseMembers=True");
        context.Applied.Values.Should().HaveCount(12);
    }

    [Fact]
    public async Task A_wsum_recipe_is_REFUSED_before_a_round_trip_rather_than_ignored_by_the_engine()
    {
        var (retriever, calls) = Build(Answer());

        var refused = await Retrieve(retriever, Weighted());

        // The engine's axes contract has no fusion-mode field and does not refuse unknown ones, so sending
        // this would be accepted, ignored, and recorded as a weighted sum that measured rank fusion. That is
        // the reranker scar exactly.
        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("wsum").And.Contain("rank fusion");
        refused.Reason().Should().Contain("PLAN_search_variant_axes", "the engine's half of this has a name and a home");
        calls.Should().BeEmpty("a recipe this engine cannot express must not cost it a query");
    }

    [Fact]
    public async Task A_recipe_for_another_engine_is_refused_by_this_adapter()
    {
        var (retriever, _) = Build(Answer());

        var refused = await Retrieve(retriever, Recipe(RetrievalChannels.Hybrid, 20, 50, EngineKind.Mindex));

        refused.Reason().Should().Contain("Mindex").And.Contain("Qln");
    }

    [Fact]
    public async Task The_funnel_arrives_in_the_context_rather_than_only_in_a_sink()
    {
        var (retriever, _) = Build(Answer());

        var context = (await Retrieve(retriever)).Ok();

        // The single-shot lane performs the retrieval itself, so the funnel travels back as a value and is
        // persisted from there — the sink is the tool lane's path.
        context.IsWhiteBox.Should().BeTrue();
        context.Funnel.ContractVersion.Should().Be(TraceContract.V0);
        context.Funnel.Stages.Single(s => s.Name == "collapse").Out.Should().Be(96);
        context.Funnel.UnattributedMs.Should().Be(3104 - 3103);
        context.FunnelNote.Should().BeEmpty();
    }

    [Fact]
    public async Task A_funnel_this_build_cannot_read_is_stored_as_a_DEGRADED_reading_with_its_reason()
    {
        var (retriever, _) = Build(
            $$"""{ "hits": [], "collection": "code_x", {{Echo}}, "funnel": { "contractVersion": "trace/v9", "totalMs": 1, "absent": [], "stages": [] } }""");

        var context = (await Retrieve(retriever)).Ok();

        // Both vantage points are data. Dropping the reason would make an engine that broke its own trace
        // contract render exactly like one that never claimed a contract.
        context.WasPerformed.Should().BeTrue();
        context.IsWhiteBox.Should().BeFalse();
        context.FunnelNote.Should().Contain("trace/v9").And.Contain("trace/v0");
    }

    [Fact]
    public async Task A_hit_the_engine_sent_no_text_for_says_so_rather_than_carrying_an_empty_string()
    {
        var (retriever, _) = Build(Answer(hits: """
            { "relativePath": "src/A.cs", "startLine": 1, "endLine": 2, "typeName": "A", "memberName": "M",
              "signature": "void M()", "score": 0.5, "ordering": "fusion", "channels": ["sparse"], "ranks": [1] }
            """));

        var context = (await Retrieve(retriever)).Ok();

        var snippet = context.Hits.Single().Snippet;
        snippet.State.Should().Be(HitTextState.NotReported);
        snippet.Bytes.Should().Be(0);
        snippet.Reason.Should().NotBeEmpty("an empty snippet with no reason is a gap that reads as a claim");
    }

    [Fact]
    public async Task The_payload_size_and_the_wait_are_measured_here_rather_than_taken_from_the_funnel()
    {
        var (retriever, _) = Build(Answer());

        var context = (await Retrieve(retriever)).Ok();

        // Two clocks: what this process waited, and what the engine measured inside itself. Reporting one as
        // the other is how a slow network becomes a slow reranker.
        context.PayloadBytes.Should().BeGreaterThan(400, "log volume is recorded from what the harness observes itself");
        context.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);
        context.Funnel.TotalMs.Should().Be(3104);
    }

    [Fact]
    public async Task An_engine_that_is_down_is_a_recorded_refusal_rather_than_an_exception()
    {
        var retriever = new QlnRetriever(
            new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost:5311/") },
            Project, "main", new NoFunnelSink());

        var refused = await Retrieve(retriever);

        refused.Reason().Should().Contain("unreachable");
    }

    [Fact]
    public async Task A_rejected_request_carries_the_engines_own_explanation()
    {
        var (retriever, _) = Build(
            """{"title":"'code_x' does not exist: this project has not been indexed with these settings yet"}""",
            HttpStatusCode.BadRequest);

        var refused = await Retrieve(retriever);

        // "Never indexed" and "matched nothing" are opposite facts, and the engine already separates them.
        refused.Reason().Should().Contain("400").And.Contain("has not been indexed");
    }

    [Fact]
    public async Task A_body_that_is_not_the_expected_shape_is_a_refusal_rather_than_a_crash()
    {
        var (retriever, _) = Build("this is not json at all");

        (await Retrieve(retriever)).Reason().Should().Contain("could not be read");
    }

    private static Task<Outcome<RetrievedContext>> Retrieve(IRetriever retriever) =>
        Retrieve(retriever, Recipe(RetrievalChannels.Hybrid, 20, 50));

    private static Task<Outcome<RetrievedContext>> Retrieve(
        IRetriever retriever, VariantDefinition.RetrievalRecipe recipe) =>
        retriever.RetrieveAsync(
            new RetrievalRequest("how is the retry delay computed", recipe), TestContext.Current.CancellationToken);

    private static VariantDefinition.RetrievalRecipe Recipe(
        RetrievalChannels channels, int limit, int rerankPool, EngineKind engine = EngineKind.Qln) =>
        (VariantDefinition.RetrievalRecipe)VariantDefinition.Retrieval(
            engine,
            channels,
            FusionSpec.Rrf(60).Ok(),
            CorpusSpec.Parse("member", 512, "bge-m3").Ok(),
            rerankPool > 0 ? RerankSpec.Pooled(rerankPool).Ok() : RerankSpec.Off,
            limit).Ok();

    private static VariantDefinition.RetrievalRecipe Weighted() =>
        (VariantDefinition.RetrievalRecipe)VariantDefinition.Retrieval(
            EngineKind.Qln,
            RetrievalChannels.Hybrid,
            FusionSpec.Parse(FusionSpec.WeightedSum, 60, 0.7, 0.3, FusionSpec.MinMax).Ok(),
            CorpusSpec.Parse("member", 512, "bge-m3").Ok(),
            RerankSpec.Off,
            20).Ok();

    private static (QlnRetriever Retriever, List<Recorded> Calls) Build(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var calls = new List<Recorded>();
        var http = new HttpClient(new StubHandler(body, status, calls))
        {
            BaseAddress = new Uri("http://localhost:5311/"),
        };

        return (new QlnRetriever(http, Project, "main", new NoFunnelSink()), calls);
    }

    internal sealed record Recorded(string Path, string Body);

    private sealed class StubHandler(string body, HttpStatusCode status, List<Recorded> calls) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls.Add(new Recorded(
                request.RequestUri!.AbsolutePath,
                await request.Content!.ReadAsStringAsync(cancellationToken)));

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("No connection could be made because the target machine actively refused it");
    }
}
