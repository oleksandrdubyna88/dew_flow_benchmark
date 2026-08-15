using System.Net;
using System.Text;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Infrastructure.Engines;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Engines;

/// <summary>The QLN adapter, against a stubbed wire.
/// <para>
/// Every payload below is the shape <c>dew_flow_rag_qln</c> emitted on 2026-08-15, so these tests are
/// the cross-repository contract from this side: if that engine renames a stage or drops the version
/// stamp, one of these goes red HERE rather than producing a column of zeros in a run three weeks from
/// now.
/// </para></summary>
public sealed class QlnEngineTests
{
    private const string GoodFunnel =
        """
        "funnel": {
          "contractVersion": "trace/v0",
          "totalMs": 8010,
          "absent": ["graph-enrich"],
          "stages": [
            { "name": "collection", "in": 1, "out": 1, "ms": 4 },
            { "name": "embed-query", "in": 1, "out": 1, "ms": 380 },
            { "name": "retrieve", "in": 200, "out": 145, "ms": 210 },
            { "name": "retrieve:dense", "in": 100, "out": 100, "ms": 0 },
            { "name": "fuse", "in": 145, "out": 145, "ms": 2 },
            { "name": "rerank", "in": 50, "out": 20, "ms": 3500 },
            { "name": "cut", "in": 145, "out": 20, "ms": 1 }
          ]
        }
        """;

    private static string Answer(string funnel, string hits = Hit) =>
        $$"""{ "hits": [{{hits}}], "collection": "code_ab12cd34", {{funnel}} }""";

    private const string Hit =
        """
        {
          "relativePath": "src/Polly.Core/Retry/RetryHelper.cs",
          "startLine": 75, "endLine": 111,
          "typeName": "RetryHelper", "memberName": "DecorrelatedJitterBackoffV2",
          "signature": "internal static TimeSpan DecorrelatedJitterBackoffV2(int attempt, TimeSpan baseDelay)",
          "score": 0.913, "ordering": "rerank", "channels": ["dense", "sparse"]
        }
        """;

    [Fact]
    public async Task It_declares_itself_as_qln_and_as_capable_of_a_funnel()
    {
        var (engine, _, _) = Build(Answer(GoodFunnel));

        engine.Describe.Kind.Should().Be(EngineKind.Qln);
        engine.Describe.MayBeWhiteBox.Should().BeTrue();
        engine.TraceContractVersion.Should().Be(TraceContract.V0);
    }

    [Fact]
    public async Task It_offers_search_ALONGSIDE_the_file_tools_rather_than_instead_of_them()
    {
        var (engine, _, _) = Build(Answer(GoodFunnel));

        // The measured retrieval arm made 63 file reads beside its 39 searches. An engine offering
        // search alone would measure a subject working with one hand tied, against a baseline that has
        // both — and "retrieval against no retrieval" is only honest if retrieval is a superset.
        var names = engine.Tools.Select(t => t.Name).ToList();
        names.Should().Contain(QlnEngine.SearchCode);
        names.Should().Contain(FilesystemEngine.ReadFile);
        names.Should().Contain(FilesystemEngine.SearchLiteral);
    }

    [Fact]
    public async Task A_file_tool_call_is_served_by_the_inner_engine_and_never_reaches_the_wire()
    {
        var (engine, root, calls) = Build(Answer(GoodFunnel));
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "alpha", TestContext.Current.CancellationToken);

        var answer = await engine.InvokeAsync(FilesystemEngine.ReadFile, """{"path":"a.txt"}""", TestContext.Current.CancellationToken);

        answer.Should().BeOfType<ToolAnswer.Ok>().Which.Content.Should().Contain("alpha");
        calls.Count.Should().Be(0, "reading a file is not a retrieval and must not cost a round trip");
    }

    [Fact]
    public async Task A_search_returns_hits_with_the_span_a_subject_needs_to_read_around()
    {
        var (engine, _, _) = Build(Answer(GoodFunnel));

        var answer = await Search(engine, "how is the retry delay computed");

        // A hit without a line span costs the subject a whole file to follow up, every time.
        var content = answer.Should().BeOfType<ToolAnswer.Ok>().Subject.Content;
        content.Should().Contain("src/Polly.Core/Retry/RetryHelper.cs:75-111");
        content.Should().Contain("DecorrelatedJitterBackoffV2");
    }

    [Fact]
    public async Task A_hit_says_why_it_matched_and_what_ordered_it()
    {
        var (engine, _, _) = Build(Answer(GoodFunnel));

        var content = (await Search(engine, "retry delay")).Text;

        // A score with no stated origin cannot be compared to anything — the engine carries the
        // distinction for that reason, and dropping it here would put it back on the reader.
        content.Should().Contain("rerank").And.Contain("dense+sparse");
    }

    [Fact]
    public async Task The_funnel_reaches_the_sink_as_a_white_box_reading()
    {
        var recorder = new LegRecorder();
        var (engine, _, _) = Build(Answer(GoodFunnel), sink: recorder);

        await Search(engine, "retry delay");

        var funnel = recorder.Assemble().Funnel;
        funnel.IsPresent.Should().BeTrue();
        funnel.ContractVersion.Should().Be(TraceContract.V0);
        funnel.Stages.Single(s => s.Name == "rerank").Out.Should().Be(20);

        // The number the whole contract exists for, arriving from a real payload rather than a fixture.
        funnel.UnattributedMs.Should().Be(8010 - 4097);
        funnel.Absent.Should().Equal("graph-enrich");
    }

    [Fact]
    public async Task A_payload_naming_a_stage_the_contract_does_not_define_degrades_and_says_why()
    {
        var invented = """
            "funnel": { "contractVersion": "trace/v0", "totalMs": 10, "absent": [],
              "stages": [{ "name": "vibes", "in": 10, "out": 5, "ms": 1 }] }
            """;
        var recorder = new LegRecorder();
        var (engine, _, _) = Build(Answer(invented), sink: recorder);

        var answer = await Search(engine, "anything");

        // The search still succeeded — a contract mismatch is not the subject's problem and must not
        // cost it a tool call.
        answer.Should().BeOfType<ToolAnswer.Ok>();

        var trace = recorder.Assemble();
        trace.Funnel.IsPresent.Should().BeFalse();
        trace.FunnelNote.Should().Contain("vibes");
    }

    [Fact]
    public async Task A_payload_with_no_version_stamp_degrades_rather_than_being_assumed_to_be_ours()
    {
        var unstamped = """
            "funnel": { "totalMs": 10, "absent": [],
              "stages": [{ "name": "fuse", "in": 10, "out": 5, "ms": 1 }] }
            """;
        var recorder = new LegRecorder();
        var (engine, _, _) = Build(Answer(unstamped), sink: recorder);

        await Search(engine, "anything");

        // Reading an unstamped payload as trace/v0 is the same mistake as reading a future contract as
        // this one — it is right until the day it silently is not.
        var trace = recorder.Assemble();
        trace.Funnel.IsPresent.Should().BeFalse();
        trace.FunnelNote.Should().Contain("none stamped");
    }

    [Fact]
    public async Task An_answer_with_no_funnel_at_all_is_reported_as_the_engine_breaking_its_own_claim()
    {
        var recorder = new LegRecorder();
        var (engine, _, _) = Build("""{ "hits": [], "collection": "code_x" }""", sink: recorder);

        await Search(engine, "anything");

        // This engine DECLARES trace/v0. An answer without a funnel is that declaration being wrong,
        // which is worth a sentence rather than looking like an engine that never claimed anything.
        recorder.Assemble().FunnelNote.Should().Contain("declared a trace contract");
    }

    [Fact]
    public async Task A_search_that_matches_nothing_says_so_in_words()
    {
        var (engine, _, _) = Build(Answer(GoodFunnel, hits: ""));

        var answer = await Search(engine, "nothing at all");

        // An index that answered with nothing is a fact about the query; a silent empty string is a
        // fact about nothing.
        answer.Should().BeOfType<ToolAnswer.Ok>().Which.Content.Should().Contain("no hits");
    }

    [Fact]
    public async Task An_engine_that_is_down_fails_the_call_without_ending_the_leg()
    {
        var engine = Unreachable();

        var answer = await Search(engine, "anything");

        // An unreachable engine is an environment fact the run records, not an exception that unwinds
        // ten thousand legs.
        answer.Should().BeOfType<ToolAnswer.Failed>().Which.Message.Should().Contain("unreachable");
    }

    [Fact]
    public async Task A_rejected_request_carries_the_engine_s_own_explanation()
    {
        var (engine, _, _) = Build(
            """{"title":"'code_x' does not exist: this project has not been indexed with these settings yet"}""",
            HttpStatusCode.BadRequest);

        var answer = await Search(engine, "anything");

        // "Never indexed" and "matched nothing" are opposite facts, and the engine already separates
        // them — throwing the sentence away here would merge them again on our side.
        answer.Should().BeOfType<ToolAnswer.Failed>()
            .Which.Message.Should().Contain("400").And.Contain("has not been indexed");
    }

    [Fact]
    public async Task A_search_without_a_query_is_refused_before_a_round_trip()
    {
        var (engine, _, calls) = Build(Answer(GoodFunnel));

        var answer = await engine.InvokeAsync(QlnEngine.SearchCode, """{}""", TestContext.Current.CancellationToken);

        answer.Should().BeOfType<ToolAnswer.Refused>().Which.Reason.Should().Contain("'query' is required");
        calls.Count.Should().Be(0, "a malformed call must not cost the engine a query");
    }

    [Fact]
    public async Task The_request_carries_the_project_the_branch_and_a_result_limit()
    {
        var (engine, _, calls) = Build(Answer(GoodFunnel));

        await Search(engine, "retry delay");

        var call = calls.Single();
        call.Path.Should().Contain(Project.ToString("D")).And.EndWith("/search");
        call.Body.Should().Contain("\"branch\":\"main\"").And.Contain("\"query\":\"retry delay\"");

        // The engine's own default is 150 members; a subject handed that spends its context on the tool
        // instead of the question, and the result count is a measured axis rather than a taste.
        call.Body.Should().Contain("\"limit\":20");
    }

    [Fact]
    public async Task Warming_asks_the_index_a_real_question_rather_than_trusting_a_health_check()
    {
        var (engine, root, calls) = Build(Answer(GoodFunnel));

        var warmed = await engine.WarmAsync(root, TestContext.Current.CancellationToken);

        // A process that is up and a collection that was never built are indistinguishable to a health
        // check, and upstream a stale pinned port left a cross-encoder dead for four measured arms while
        // the settings page still reported one.
        calls.Should().ContainSingle();
        warmed.Ok().Should().Contain("code_ab12cd34");
    }

    [Fact]
    public async Task Warming_fails_loudly_when_the_index_does_not_answer()
    {
        var warmed = await Unreachable().WarmAsync("anywhere", TestContext.Current.CancellationToken);

        warmed.Failed().Should().BeTrue();
        warmed.Reason().Should().Contain("did not answer");
    }

    private static readonly Guid Project = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static Task<ToolAnswer> Search(IEngine engine, string query) =>
        engine.InvokeAsync(
            QlnEngine.SearchCode,
            $$"""{"query":"{{query}}"}""",
            TestContext.Current.CancellationToken);

    private static (QlnEngine Engine, string Root, List<Recorded> Calls) Build(
        string body, HttpStatusCode status = HttpStatusCode.OK, IFunnelSink? sink = null)
    {
        var root = Directory.CreateTempSubdirectory("bench-qln").FullName;
        var calls = new List<Recorded>();
        var http = new HttpClient(new StubHandler(body, status, calls))
        {
            BaseAddress = new Uri("http://localhost:5311/"),
        };

        return (new QlnEngine(http, Project, "main", new FilesystemEngine(root), sink ?? new NullSink()), root, calls);
    }

    private static QlnEngine Unreachable()
    {
        var http = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://localhost:5311/") };
        return new QlnEngine(
            http, Project, "main", new FilesystemEngine(Path.GetTempPath()), new NullSink());
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

    private sealed class NullSink : IFunnelSink
    {
        public void Retrieved(Outcome<RetrievalFunnel> funnel)
        {
        }
    }
}
