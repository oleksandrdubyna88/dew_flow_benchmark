using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using Bench.Domain.Variants;
using Bench.Infrastructure.Engines;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Engines;

/// <summary>The QLN adapter against a RUNNING engine.
/// <para>
/// Everything else about the white-box path is proven against stubs and a fixture, and that was always
/// the weak part: a fixture proves this codec agrees with our idea of the other repository. This class
/// is the only place the two halves meet — a real daemon, a real index, a real funnel — and it is what
/// promotes <c>trace/v0</c> from "authored from a stage list" to "observed from a payload".
/// </para>
/// <para>
/// Excluded from the default run by its trait, and SKIPPED rather than failed when the endpoint is not
/// configured: a suite that goes red because somebody's daemon is not running teaches people to ignore
/// red. Run it deliberately:
/// <code>
/// $env:BENCH_QLN_URL="http://127.0.0.1:63183/"; $env:BENCH_QLN_PROJECT="&lt;guid&gt;"
/// ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-trait "Category=Live"
/// </code>
/// </para></summary>
[Trait("Category", "Live")]
public sealed class QlnEngineLiveTests
{
    private const string UrlVariable = "BENCH_QLN_URL";
    private const string ProjectVariable = "BENCH_QLN_PROJECT";

    [Fact]
    public async Task A_live_search_produces_a_funnel_this_build_accepts_as_white_box()
    {
        var (engine, recorder) = Live();

        var answer = await engine.InvokeAsync(
            QlnEngine.SearchCode,
            """{"query":"where is the retry delay with jitter computed","limit":3}""",
            TestContext.Current.CancellationToken);

        answer.Should().BeOfType<Bench.Domain.Engines.ToolAnswer.Ok>();

        var trace = recorder.Assemble();
        trace.FunnelNote.Should().BeEmpty("a live funnel that this build refuses would say why here");
        trace.IsWhiteBox.Should().BeTrue();
        trace.Funnel.ContractVersion.Should().Be(TraceContract.V0);
    }

    [Fact]
    public async Task Every_stage_a_live_engine_emits_is_one_the_contract_defines()
    {
        var (engine, recorder) = Live();

        await engine.InvokeAsync(
            QlnEngine.SearchCode, """{"query":"how is a request cancelled","limit":3}""",
            TestContext.Current.CancellationToken);

        var funnel = recorder.Assemble().Funnel;

        // The whole point of a named contract: an engine that renamed a stage would be refused here
        // rather than producing a column of zeros in a report months later.
        funnel.Stages.Should().NotBeEmpty();
        funnel.Stages.Select(s => TraceContract.BaseStage(s.Name)).Should()
            .OnlyContain(name => TraceContract.Stages.Contains(name));
        funnel.Absent.Should().OnlyContain(name => TraceContract.Stages.Contains(name));
    }

    [Fact]
    public async Task The_funnel_accounts_for_nearly_all_of_the_call_it_measured()
    {
        var (engine, recorder) = Live();

        await engine.InvokeAsync(
            QlnEngine.SearchCode, """{"query":"where are options validated","limit":3}""",
            TestContext.Current.CancellationToken);

        var funnel = recorder.Assemble().Funnel;

        // The number the contract exists for, read from a real call rather than a fixture. The total is
        // measured end to end and the stages are measured individually, so a large remainder means a
        // stage nobody instrumented — the failure a summed total cannot show.
        funnel.TotalMs.Should().BeGreaterThan(0);
        funnel.UnattributedMs.Should().BeLessThan(funnel.TotalMs / 4,
            "more than a quarter of the call belonging to no stage means the funnel is missing one");
    }

    [Fact]
    public async Task Warming_confirms_the_index_answers_rather_than_that_the_process_is_up()
    {
        var (engine, _) = Live();

        var warmed = await engine.WarmAsync("unused", TestContext.Current.CancellationToken);

        // It asks a real question, so the answer names the collection that served it — a process that is
        // up and a collection that was never built are identical to a health check.
        warmed.Ok().Should().Contain("collection=");
    }

    [Fact]
    public async Task A_search_returns_hits_a_subject_could_act_on()
    {
        var (engine, _) = Live();

        var answer = await engine.InvokeAsync(
            QlnEngine.SearchCode, """{"query":"where is the HTTP request pipeline built","limit":3}""",
            TestContext.Current.CancellationToken);

        var content = answer.Should().BeOfType<Bench.Domain.Engines.ToolAnswer.Ok>().Subject.Content;

        // A hit without a line span costs a whole file to follow up, and a score without a stated origin
        // cannot be compared to anything.
        content.Should().MatchRegex(@":\d+-\d+");
        content.Should().MatchRegex(@"\[(rerank|fusion) ");
    }

    [Fact]
    public async Task A_live_retrieval_collapses_members_and_echoes_the_axes_it_applied()
    {
        var retriever = LiveRetriever();

        var context = (await retriever.RetrieveAsync(
            new RetrievalRequest(
                "how is the retry delay with jitter computed",
                (VariantDefinition.RetrievalRecipe)VariantDefinition.Retrieval(
                    EngineKind.Qln,
                    RetrievalChannels.Hybrid,
                    FusionSpec.Rrf(60).Ok(),
                    CorpusSpec.Parse("member", 512, "bge-m3").Ok(),
                    Bench.Domain.Variants.RerankSpec.Pooled(50).Ok(),
                    limit: 5).Ok()),
            TestContext.Current.CancellationToken)).Ok();

        // `collapse` is the stage that was added to trace/v0 after the emitter shipped it and every funnel it
        // produced was refused by name. This is the end-to-end half of that repair: a real daemon, a real
        // collapse, and a consumer that accepts it (todo/PLAN_variant_matrix.md §5 step 4).
        var collapse = context.Funnel.Stages.Should().ContainSingle(s => s.Name == "collapse").Subject;
        collapse.Out.Should().BeLessThanOrEqualTo(
            collapse.In, "collapsing several chunks of one member cannot produce more than it was given");

        // And the echo, which is what step 5 will assert against the request. Storing it is worthless if a
        // live engine turns out not to send it.
        context.Applied.Values.Should().NotBeEmpty("the axes AS APPLIED are the proof of what served the query");
        context.Applied.Canonical.Should().Contain("limit=5");
        context.Collection.Should().NotBeEmpty();
        context.Hits.Should().OnlyContain(h => h.Rank > 0);
    }

    /// <summary>The engine, or a skip that says exactly what is missing.</summary>
    private static (QlnEngine Engine, LegRecorder Recorder) Live()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable) ?? string.Empty;
        var project = Environment.GetEnvironmentVariable(ProjectVariable) ?? string.Empty;

        Assert.SkipWhen(
            url.Length == 0 || !Guid.TryParse(project, out var projectId),
            $"set {UrlVariable} and {ProjectVariable} to run against a live QLN daemon");

        var recorder = new LegRecorder();
        var http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromMinutes(3) };
        var engine = new QlnEngine(
            http,
            Guid.Parse(project),
            branch: string.Empty,
            files: new FilesystemEngine(Path.GetTempPath()),
            funnels: recorder);

        return (engine, recorder);
    }

    /// <summary>The single-shot lane's retriever against the same live daemon. The funnel comes back in the
    /// return value here rather than through a sink, so no recorder is needed.</summary>
    private static QlnRetriever LiveRetriever()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable) ?? string.Empty;
        var project = Environment.GetEnvironmentVariable(ProjectVariable) ?? string.Empty;

        Assert.SkipWhen(
            url.Length == 0 || !Guid.TryParse(project, out _),
            $"set {UrlVariable} and {ProjectVariable} to run against a live QLN daemon");

        return new QlnRetriever(
            new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromMinutes(3) },
            Guid.Parse(project),
            branch: string.Empty,
            funnels: new NoFunnelSink());
    }
}
