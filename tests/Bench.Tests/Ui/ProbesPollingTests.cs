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
using static Bench.Tests.Ui.ProbeUiFixtures;

namespace Bench.Tests.Ui;

/// <summary>The Probes tab's live poll (§2 requirement 4, S4): every 3 s while any cell is Pending or Claimed, stopped when none is,
/// never after the page is gone, and never letting a late answer overwrite the run the reader is looking at. The clock is a
/// <see cref="ManualClock"/> — the timer fires when the test says so, so nothing here sleeps or races a wall clock.</summary>
public sealed class ProbesPollingTests : BunitContext
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private static readonly Guid RunA = Guid.Parse("01a0f800-0000-7000-8000-00000000000a");
    private static readonly Guid RunB = Guid.Parse("01a0f800-0000-7000-8000-00000000000b");

    private readonly ManualClock _clock = new();

    [Fact]
    public void The_page_polls_every_three_seconds_while_a_cell_is_in_flight_and_stops_when_none_is()
    {
        var api = new PollingBenchApi().Answers(RunsRoute, new[] { Summary(RunA, Noon, open: true) }).Answers(RunRoute(RunA), InFlight(RunA));
        var page = Render(api);
        _clock.ActiveTimers.Should().Be(1, "a cell is claimed, so the page polls");

        _clock.Advance(Interval - TimeSpan.FromMilliseconds(1));
        api.Reads(RunA).Should().Be(1, "nothing is asked before the interval has passed");
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        page.WaitForAssertion(() => api.Reads(RunA).Should().Be(2));

        api.Answers(RunRoute(RunA), Done(RunA));
        _clock.Advance(Interval);
        page.WaitForAssertion(() => page.Find("[data-test=probe-progress]").TextContent.Should().Contain("finished"));

        _clock.ActiveTimers.Should().Be(0, "nothing is Pending or Claimed any more, so the poll stopped itself");
        _clock.Advance(Interval * 5);
        api.Reads(RunA).Should().Be(3, "a stopped poll asks nothing more");
    }

    [Fact]
    public void A_run_with_nothing_in_flight_starts_no_poll()
    {
        var api = new PollingBenchApi().Answers(RunsRoute, new[] { Summary(RunA, Noon, open: false) }).Answers(RunRoute(RunA), Done(RunA));
        Render(api);

        _clock.ActiveTimers.Should().Be(0);
        _clock.Advance(Interval * 3);
        api.Reads(RunA).Should().Be(1);
    }

    [Fact]
    public async Task After_disposal_the_timer_is_gone_nothing_is_asked_and_nothing_renders()
    {
        var api = new PollingBenchApi().Answers(RunsRoute, new[] { Summary(RunA, Noon, open: true) }).Answers(RunRoute(RunA), InFlight(RunA));
        var page = Render(api);
        var renders = page.RenderCount;

        await DisposeComponentsAsync();
        _clock.Advance(Interval * 3);

        _clock.ActiveTimers.Should().Be(0, "a disposed page leaves no timer behind");
        api.Reads(RunA).Should().Be(1, "a disposed page asks the API nothing");
        page.RenderCount.Should().Be(renders);
    }

    [Fact]
    public async Task A_poll_still_waiting_when_the_page_is_disposed_leaves_no_timer_and_no_render_when_it_answers()
    {
        var api = new PollingBenchApi().Answers(RunsRoute, new[] { Summary(RunA, Noon, open: true) }).Answers(RunRoute(RunA), InFlight(RunA));
        var page = Render(api);
        var held = api.HoldNext(RunRoute(RunA));
        _clock.Advance(Interval);
        await held.Asked.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        var renders = page.RenderCount;

        // The handler signals "asked" from inside the tick's work item; one round-trip through the renderer's dispatcher lets that
        // work item finish parking on the HTTP call before the page is disposed (observed: without it bUnit never disposed the page).
        await page.InvokeAsync(() => { });
        await DisposeComponentsAsync();
        _clock.ActiveTimers.Should().Be(0, "the disposed page's timer is gone while its poll is still waiting on the API");
        api.Answers(RunRoute(RunA), Done(RunA));
        held.Release();
        await held.Answered.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        page.RenderCount.Should().Be(renders, "the page is gone; a late answer must not render it again");
        _clock.ActiveTimers.Should().Be(0);
    }

    [Fact]
    public async Task A_late_poll_answer_for_a_run_the_reader_left_never_replaces_the_run_they_chose()
    {
        var api = new PollingBenchApi()
            .Answers(RunsRoute, new[] { Summary(RunA, Noon.AddHours(1), open: true), Summary(RunB, Noon, open: false) })
            .Answers(RunRoute(RunA), InFlight(RunA)).Answers(RunRoute(RunB), Done(RunB));
        var page = Render(api);
        var held = api.HoldNext(RunRoute(RunA));
        _clock.Advance(Interval);
        await held.Asked.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        page.Find("#probe-run").Change(RunB.ToString());
        page.WaitForAssertion(() => page.Find("[data-test=probe-run-id]").TextContent.Should().Contain(RunB.ToString()));
        held.Release();
        await held.Answered.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        // A dropped answer has no positive signal — its only trace would be run A coming back. So this is the one bounded NEGATIVE
        // wait in the suite: a page that applied the late answer shows run A within milliseconds of the release; one that dropped
        // it never does. (Measured: without the ticket check the page shows run A again and this wait succeeds.)
        var lateAnswerShown = () => page.WaitForAssertion(
            () => page.Find("[data-test=probe-run-id]").TextContent.Should().Contain(RunA.ToString()), TimeSpan.FromSeconds(1));
        lateAnswerShown.Should().Throw<Exception>(
            "the late answer belongs to a run the reader already left, and a matrix under another run's id is a wrong fact");
        page.Find("[data-test=probe-run-id]").TextContent.Should().Contain(RunB.ToString());
        _clock.ActiveTimers.Should().Be(0, "run B has nothing in flight, so leaving run A stopped the poll");
    }

    private IRenderedComponent<ProbesBenchmark> Render(PollingBenchApi api)
    {
        Services.AddSingleton(new BenchConsoleApi(new HttpClient(api) { BaseAddress = ScriptedBenchApi.BaseAddress }));
        Services.AddSingleton<TimeProvider>(_clock);
        return Render<ProbesBenchmark>();
    }

    /// <summary>Routes answered by PATH (the query is the request's, never the routing), answers replaceable mid-test, every call
    /// counted, and the NEXT call to one path holdable until the test releases it.</summary>
    private sealed class PollingBenchApi : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentDictionary<string, object> _answers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<string> _calls = new();
        private Held? _held;

        public PollingBenchApi Answers(string path, object body)
        {
            _answers[path] = body;
            return this;
        }

        public int Reads(Guid runId) => _calls.Count(c => c == RunRoute(runId));

        public Held HoldNext(string path)
        {
            var held = new Held(path);
            _held = held;
            return held;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            _calls.Enqueue(path);

            if (_held is { } held && held.Path == path)
            {
                _held = null;
                held.SignalAsked();
                await held.Gate;
                var response = Answer(path);
                held.SignalAnswered();
                return response;
            }

            return Answer(path);
        }

        private HttpResponseMessage Answer(string path) =>
            _answers.TryGetValue(path, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body, Web), Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
    }

    private sealed class Held(string path)
    {
        private readonly TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _answered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Path { get; } = path;

        public Task Asked => _asked.Task;

        public Task Gate => _gate.Task;

        /// <summary>Completes once the held answer has been handed back to the client — the page's continuation runs after it.</summary>
        public Task Answered => _answered.Task;

        public void SignalAsked() => _asked.TrySetResult();

        public void SignalAnswered() => _answered.TrySetResult();

        public void Release() => _gate.TrySetResult();
    }
}
