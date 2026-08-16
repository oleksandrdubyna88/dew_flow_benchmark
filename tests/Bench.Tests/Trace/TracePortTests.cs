using Bench.Application;
using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>The trace port, in both of its modes.
/// <para>
/// The plan's condition is that TWO real implementations ship together — an interface with one
/// implementation fits its single caller by construction and proves nothing about its own shape. So
/// both are exercised here, and the guarantee that matters most is the last test: the same report
/// renders from either, and a black-box funnel prints as ABSENT rather than as zeroes.
/// </para></summary>
public sealed class TracePortTests
{
    [Fact]
    public async Task A_black_box_trace_records_every_call_with_how_it_ended()
    {
        var trace = new LiveTrace();
        var recorder = trace.Open(Tuple());

        recorder.Called("read_file", """{"path":"a.txt"}""", ToolAnswer.Success("lines 1-3 of 3"), Ms(12));
        recorder.Called("read_file", """{"path":"../etc"}""", ToolAnswer.Refusal("outside the repository"), Ms(1));

        var captured = (await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        // The field a whole read-only guarantee once turned on: a denied call and an executed one look
        // identical if the ledger records only the size of the result.
        captured.ToolCalls.Should().HaveCount(2);
        captured.ToolCalls[0].Refused.Should().BeFalse();
        captured.ToolCalls[1].Refused.Should().BeTrue();
        captured.ToolCalls[1].Error.Should().Contain("outside the repository");
    }

    [Fact]
    public async Task Tool_time_lands_in_the_tools_bucket_and_waiting_lands_in_its_own()
    {
        var trace = new LiveTrace();
        var recorder = trace.Open(Tuple());

        recorder.Called("read_file", "{}", ToolAnswer.Success("x"), Ms(30));
        recorder.Thought(Ms(500));
        recorder.Waited(Ms(8000));

        var captured = (await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        // Three buckets, not two. The third exists because a busy accelerator otherwise reads as a slow
        // model — a quality conclusion drawn from a scheduling fact.
        captured.Time.Tools.Should().Be(Ms(30));
        captured.Time.Thinking.Should().Be(Ms(500));
        captured.Time.InfrastructureWait.Should().Be(Ms(8000));
        captured.Time.Total.Should().Be(Ms(8530));
    }

    [Fact]
    public async Task An_unreported_prompt_stays_uncaptured_and_says_why()
    {
        var trace = new LiveTrace();
        trace.Open(Tuple());

        var captured = (await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        // For some runtimes a result's text is simply unobtainable — a CLI stream keeps a result's
        // SIZE, not its body. Defaulting these to empty strings turns that gap into a claim about the
        // subject's answer.
        captured.Prompt.WasCaptured.Should().BeFalse();
        captured.Prompt.Reason.Should().NotBeEmpty();
        captured.Response.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public async Task A_leg_nobody_recorded_is_a_failure_rather_than_an_empty_trace()
    {
        var trace = new LiveTrace();

        var captured = await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        // Returning LegTrace.Empty here would make "the leg did nothing" indistinguishable from "the
        // leg was never run" in every report built afterwards.
        captured.Failed().Should().BeTrue();
        captured.Reason().Should().Contain("no leg was recorded");
    }

    [Fact]
    public async Task A_black_box_trace_carries_no_funnel_and_does_not_pretend_to()
    {
        var trace = new LiveTrace();
        trace.Open(Tuple()).Called("read_file", "{}", ToolAnswer.Success("x"), Ms(5));

        var captured = (await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        captured.IsWhiteBox.Should().BeFalse();
        captured.Funnel.IsPresent.Should().BeFalse();
    }

    [Fact]
    public async Task A_white_box_trace_replays_the_funnel_a_black_box_observer_cannot_see()
    {
        var trace = new FixtureTrace(FixturePath("whitebox-funnel-v0.json"));

        var captured = (await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        trace.Mode.Should().Be(TraceMode.WhiteBox);
        captured.IsWhiteBox.Should().BeTrue();
        captured.Funnel.ContractVersion.Should().Be(TraceContract.V0);

        // The question the funnel exists to answer, answerable from these numbers alone: 145 candidates
        // survived fusion and 20 survived the reranker, so a target absent from the fused list was
        // never a ranking problem. Obtaining that answer once cost a purpose-built expedition.
        var fuse = captured.Funnel.Stages.Single(s => s.Name == "fuse");
        fuse.Out.Should().Be(145);
        captured.Funnel.Stages.Single(s => s.Name == "rerank").Out.Should().Be(20);
    }

    [Fact]
    public async Task A_funnel_reports_the_time_no_stage_claimed()
    {
        var funnel = (await new FixtureTrace(FixturePath("whitebox-funnel-v0.json"))
            .CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok().Funnel;

        // The number whose whole job is to expose a stage nobody instrumented. An instrument upstream
        // printed the sum of the stages it knew about and called it the total: ~4.3 s of an ~8 s call,
        // with half the time belonging to nobody and nothing saying so. A sum cannot show a missing
        // part — a remainder can, and it says which direction to look.
        funnel.TotalMs.Should().Be(8010);
        funnel.AccountedMs.Should().Be(4097);
        funnel.UnattributedMs.Should().Be(3913);
    }

    [Fact]
    public async Task A_stage_the_engine_does_not_perform_is_named_rather_than_reported_as_zero()
    {
        var funnel = (await new FixtureTrace(FixturePath("whitebox-funnel-v0.json"))
            .CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok().Funnel;

        // "graph-enrich ran and enriched nothing" and "this build does not do graph enrichment" are
        // different claims, and a 0/0 row says the first.
        funnel.Absent.Should().Equal("graph-enrich");
        funnel.Stages.Should().NotContain(s => s.Name == "graph-enrich");
    }

    [Fact]
    public async Task A_per_channel_breakdown_rides_as_a_qualifier_rather_than_as_a_new_stage()
    {
        var funnel = (await new FixtureTrace(FixturePath("whitebox-funnel-v0.json"))
            .CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok().Funnel;

        // The base name is the contract; the qualifier is data. A pool that admitted nothing has to be
        // able to say WHICH head went quiet, and putting the channel list into the contract would make
        // a third channel a contract change.
        var sparse = funnel.Stages.Single(s => s.Name == "retrieve:sparse");
        sparse.Out.Should().Be(45);
        TraceContract.BaseStage(sparse.Name).Should().Be("retrieve");
        TraceContract.Defines(sparse.Name).Should().BeTrue();
    }

    [Fact]
    public async Task A_fixture_that_does_not_say_who_authored_it_is_refused()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("bench-fixture").FullName, "unlabelled.json");
        await File.WriteAllTextAsync(
            path,
            """{"contractVersion":"trace/v0","stages":[{"name":"fuse","in":10,"out":5}]}""",
            TestContext.Current.CancellationToken);

        var captured = await new FixtureTrace(path).CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        // v0 was authored from a stage list rather than from a captured payload, and an unlabelled
        // fixture is exactly how a provisional contract becomes canonical without anyone deciding it.
        captured.Failed().Should().BeTrue();
        captured.Reason().Should().Contain("authored");
    }

    [Fact]
    public async Task A_fixture_naming_a_stage_the_contract_does_not_define_is_refused()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("bench-fixture").FullName, "invented.json");
        await File.WriteAllTextAsync(
            path,
            """{"contractVersion":"trace/v0","authored":"a test","stages":[{"name":"vibes","in":10,"out":5}]}""",
            TestContext.Current.CancellationToken);

        var captured = await new FixtureTrace(path).CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        // Generality belongs in the capability declaration, never in the payload. A stage nobody
        // declared is a column no report can compute on.
        captured.Failed().Should().BeTrue();
        captured.Reason().Should().Contain("vibes");
    }

    [Fact]
    public async Task Opening_one_leg_twice_keeps_writing_into_the_same_recording()
    {
        var trace = new LiveTrace();

        // A leg has phases — investigate, fix, verify — and each opens the recorder for the leg it
        // belongs to. Handing out a fresh one per call would silently throw away every phase but the
        // last, and the trace would still look perfectly well-formed.
        trace.Open(Tuple()).Called("read_file", "{}", ToolAnswer.Success("x"), Ms(5));
        trace.Open(Tuple()).Called("read_file", "{}", ToolAnswer.Success("y"), Ms(7));

        var captured = (await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        captured.ToolCalls.Should().HaveCount(2);
        captured.Time.Tools.Should().Be(Ms(12));
    }

    [Fact]
    public async Task A_captured_leg_is_evicted_from_the_trace_and_does_not_accumulate()
    {
        var trace = new LiveTrace();

        for (var leg = 1; leg <= 50; leg++)
        {
            trace.Open(Tuple(leg)).Called("read_file", "{}", ToolAnswer.Success("x"), Ms(5));
            (await trace.CaptureAsync(Tuple(leg), TestContext.Current.CancellationToken)).Failed().Should().BeFalse();
        }

        // Fifty legs measured, none in flight. Before this, the count here was 50 — one recorder per leg,
        // each holding its tool calls and its captured text, for the life of a three-week campaign.
        trace.Recording.Should().Be(0, "a black-box recorder is retired by the capture that hands its trace over");
    }

    [Fact]
    public async Task A_trace_already_captured_says_so_rather_than_claiming_the_leg_never_ran()
    {
        var trace = new LiveTrace();
        trace.Open(Tuple()).Called("read_file", "{}", ToolAnswer.Success("x"), Ms(5));
        await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        var second = await trace.CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        second.Failed().Should().BeTrue();
        second.Reason().Should().Contain("already captured", "eviction must not turn a second read into a claim about the leg");
    }

    [Fact]
    public void A_leg_that_crashed_before_its_trace_was_read_is_closed_rather_than_left_behind()
    {
        var trace = new LiveTrace();
        trace.Open(Tuple()).Called("read_file", "{}", ToolAnswer.Success("x"), Ms(5));

        trace.Close(Tuple()).Should().BeTrue("the abandon path retires a recording nobody will capture");
        trace.Recording.Should().Be(0);
        trace.Close(Tuple()).Should().BeFalse("and closing what is not there is a no-op, not an error");
    }

    [Fact]
    public async Task A_missing_fixture_file_is_a_failure_that_names_the_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "absent-" + Guid.NewGuid().ToString("N") + ".json");

        var captured = await new FixtureTrace(missing).CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        captured.Failed().Should().BeTrue();
        captured.Reason().Should().Contain(missing);
    }

    [Fact]
    public async Task A_malformed_fixture_is_refused_rather_than_thrown()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("bench-fixture").FullName, "broken.json");
        await File.WriteAllTextAsync(path, "{not json", TestContext.Current.CancellationToken);

        var captured = await new FixtureTrace(path).CaptureAsync(Tuple(), TestContext.Current.CancellationToken);

        captured.Failed().Should().BeTrue();
        captured.Reason().Should().Contain("malformed");
    }

    [Fact]
    public async Task A_replayed_trace_does_not_pretend_to_know_the_prompt_or_the_answer()
    {
        var captured = (await new FixtureTrace(FixturePath("whitebox-funnel-v0.json"))
            .CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        // The fixture carries the funnel — the one thing a black-box observer cannot see — and nothing
        // else. Filling in a prompt here would make a replay look like a measurement.
        captured.Prompt.WasCaptured.Should().BeFalse();
        captured.Response.WasCaptured.Should().BeFalse();
        captured.ToolCalls.Should().BeEmpty();
        captured.Prompt.Reason.Should().Contain("fixture");
    }

    [Fact]
    public async Task The_same_report_renders_from_both_modes_and_never_prints_an_unknown_as_zero()
    {
        var live = new LiveTrace();
        live.Open(Tuple()).Called("read_file", "{}", ToolAnswer.Success("x"), Ms(5));
        var blackBox = (await live.CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();
        var whiteBox = (await new FixtureTrace(FixturePath("whitebox-funnel-v0.json"))
            .CaptureAsync(Tuple(), TestContext.Current.CancellationToken)).Ok();

        var fromBlackBox = TraceReport.Render(blackBox);
        var fromWhiteBox = TraceReport.Render(whiteBox);

        // Same shape from either mode — that is what makes an engine without a funnel measurable at all.
        fromBlackBox.Should().HaveCount(fromWhiteBox.Count);
        fromBlackBox.Select(Label).Should().Equal(fromWhiteBox.Select(Label));

        // And the difference renders as ABSENCE. "The target survived 0 of 145 candidates" and "nobody
        // watched the candidates" are opposite readings, and only one is a finding about the engine.
        fromBlackBox.Single(l => l.StartsWith("funnel", StringComparison.Ordinal))
            .Should().Contain(TraceReport.NotCaptured).And.NotContain("0/0");
        fromWhiteBox.Single(l => l.StartsWith("funnel", StringComparison.Ordinal))
            .Should().Contain("retrieve 200/145").And.Contain("rerank 50/20");

        // The remainder is printed even when the engine is black-box, and it says "not captured" there
        // too: a missing accounting line and a zero one are the same glyph to a reader scanning a column.
        fromBlackBox.Single(l => l.StartsWith("accounted", StringComparison.Ordinal))
            .Should().Contain(TraceReport.NotCaptured);
        fromWhiteBox.Single(l => l.StartsWith("accounted", StringComparison.Ordinal))
            .Should().Contain("3913 ms unattributed");
    }

    private static string Label(string line) => line.Split(' ')[0];

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static MeasurementTuple Tuple(int repeat) => Tuple() with { Repeat = repeat };

    private static MeasurementTuple Tuple() => new(
        new MeasurementTarget(
            RepoUrl.Parse("https://example.invalid/x.git").Ok(),
            CommitSha.Parse(new string('e', 40)).Ok(),
            []),
        EngineRef.Filesystem(),
        "suite@1#abc",
        new Subject(ModelRef.Parse("qwen", ModelHosting.Local).Ok(), Sampling.Deterministic(1)),
        new Lane("native", "plain"),
        1);
}
