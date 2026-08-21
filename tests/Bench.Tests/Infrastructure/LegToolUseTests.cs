using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>
/// What a tool leg leaves behind — the metric, and the three readings it has to keep apart.
///
/// <para>The loop produced a call ledger and the scorer had a rule for it, and NOTHING carried one to the
/// other: every leg scored as though no tools existed. That gap is invisible in a passing suite, because a
/// tool expectation with no observation reports "not applicable" — a sentence that reads like a considered
/// verdict and was in fact the wiring being absent.</para>
///
/// <para>So the three readings are the whole point. <b>Called</b> is a 1. <b>Offered and ignored</b> is a
/// real 0 — one of the more interesting results the wording experiment can produce, and it must not hide
/// behind the third. <b>Never offered</b> is the only not-applicable, because scoring the floor zero for
/// not calling a tool it never had would flatter every tool lane by exactly that much.</para>
/// </summary>
[Collection("postgres")]
public sealed class LegToolUseTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    private static readonly IReadOnlyList<EngineTool> Reads =
        [new EngineTool("read", "reads a file", """{"type":"object"}""")];

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_tool_the_subject_CALLED_scores_the_expectation()
    {
        var result = await LegAsync(Looping(), [Asks("read", """{"path":"one.txt"}"""), Final("it reads one.txt")]);

        var metric = ToolMetric(result);
        metric.Value.Should().Be("1");
        metric.Failed.Should().BeFalse();
        metric.Reason.Should().Contain("'read' was called");
    }

    [Fact]
    public async Task A_tool_that_was_OFFERED_and_ignored_is_a_real_zero_not_a_not_applicable()
    {
        // The distinction the whole wiring exists for. A subject that was handed a tool and reached for
        // none is evidence about the description; reporting it as "not applicable" would delete exactly
        // that, and it would read as a considered verdict while doing so.
        var result = await LegAsync(Looping(), [Final("I already know: it reads one.txt")]);

        var metric = ToolMetric(result);
        metric.Value.Should().Be("0");
        metric.Failed.Should().BeTrue();
        metric.Reason.Should().Contain("offered and never called");
    }

    [Fact]
    public async Task A_tool_expectation_in_the_FLOOR_lane_is_not_applicable_rather_than_a_miss()
    {
        // The no-tools arm exists to be compared fairly. Scoring it zero here would make the baseline look
        // worse than it is and flatter every tool lane by the same amount — the identical rule anchor
        // recall already applies, for the identical reason.
        var result = await LegAsync(LaneRoster.Floor, [Final("it reads one.txt")]);

        var metric = ToolMetric(result);
        metric.Rating.Should().Be("Unknown");
        metric.Failed.Should().BeFalse();
        metric.Reason.Should().Contain("this lane offers no tools");
    }

    [Fact]
    public async Task A_REFUSED_call_still_counts_as_called_because_the_expectation_is_about_SELECTION()
    {
        // A model that picked the right tool and handed it a path outside the checkout demonstrated the
        // selection this metric measures. The outcome is not lost — it lives on the ToolCall record, which
        // is where the different question ("did the calls WORK") has to be asked.
        var result = await LegAsync(
            Looping(ToolAnswer.Refusal("outside the workspace")),
            [Asks("read", """{"path":"/etc/passwd"}"""), Final("that path is outside the tree")]);

        ToolMetric(result).Value.Should().Be("1");
    }

    private static StoredMetric ToolMetric(LegResult result) =>
        result.Metrics.Single(m => m.Name.StartsWith(AnswerScoring.ToolUse, StringComparison.Ordinal));

    private static LaneRoster Looping(ToolAnswer? answer = null) =>
        LaneRoster.Of([new LaneChoice(
            "bridge",
            "Search before you read.",
            new ToolSurface.Looping(new FakeEngine(Reads, answer), Reads, MaxTurns: 5))]);

    /// <summary>One leg, end to end, against the real stores — because the defect this pins lived precisely
    /// in the seam between two components that each passed their own tests.</summary>
    private async Task<LegResult> LegAsync(LaneRoster lanes, IReadOnlyList<ModelAnswer> script)
    {
        var runtime = new ScriptedRuntime(script);
        var clock = new TestClock(Noon);
        var suite = SuiteOf();
        var target = MeasurementTarget.At(RepoUrl.Parse("https://github.com/App-vNext/Polly.git").Ok(), Commit);
        var run = BenchRun.Planned("tool-use", target, EngineRef.Filesystem(), suite.Stamp, Noon);
        var roster = SubjectRoster.Of([new RosterEntry(
            ModelEndpoint.Parse(ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), "http://127.0.0.1:11434/v1").Ok(),
            Sampling.Deterministic(7))]);

        var cells = Matrix.Plan(suite.Questions, repeats: 1, roster.Subjects, Axis(lanes)).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        var runs = new PostgresRunStore(postgres.NewContext(), clock);
        var results = postgres.NewResults();
        await runs.CreateAsync(run, cells, Ct);

        var runner = new LegRunner(
            runs, results, runtime, new NoRetriever(), new NoHardwareSampler(),
            new ToolLoopRunner(runtime, clock, NullLogger<ToolLoopRunner>.Instance), clock,
            NullLogger<LegRunner>.Instance);

        return (await runner.RunNextAsync(
            run.Id, WorkerIdentity.Here("worker-1"), LegPlan.Reading(suite, roster) with { Lanes = lanes }, Ct)).Ok();
    }

    /// <summary>The matrix axis for these lanes — the same projection `bench run` makes, so a cell planned
    /// here carries the lane name the roster is keyed by.</summary>
    private static IReadOnlyList<Lane> Axis(LaneRoster lanes) =>
        lanes.Entries.Count == 0
            ? [Lane.Named("no-tools")]
            : [.. lanes.Entries.Select(entry => new Lane(entry.Name, entry.Doctrine))];

    private static ModelAnswer Asks(string tool, string arguments) =>
        Answer(string.Empty, [new RequestedToolCall("call_0", tool, arguments)]);

    private static ModelAnswer Final(string text) => Answer(text, []);

    private static ModelAnswer Answer(string text, IReadOnlyList<RequestedToolCall> calls) =>
        new(
            text.Length > 0 ? Captured.Text(text) : Captured.Unavailable("asked for a tool"),
            CapturedCount.Unavailable("fake"),
            CapturedCount.Unavailable("fake"),
            TimeSpan.FromMilliseconds(5),
            SamplingAsSent.NotCaptured("fake"),
            StopReason.Completed,
            "stop")
        {
            ToolCalls = calls,
        };

    /// <summary>One question with a tool expectation — the minimum that makes the metric exist at all.</summary>
    private static Suite SuiteOf() =>
        Suite.Draft("tool-smoke").With(new Question(
            "reads-a-file",
            "What is in one.txt?",
            [
                new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File("", Commit), "one.txt", true),
                new Expectation(ExpectationKind.ToolUsed, SourceAnchor.File("", Commit), "read", true),
            ],
            string.Empty)).Ok().Freeze().Ok();

    /// <summary>Answers a prepared script, in order.</summary>
    private sealed class ScriptedRuntime(IReadOnlyList<ModelAnswer> answers) : IModelRuntime
    {
        private int _asked;

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("scripted"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<ModelAnswer>.Success(answers[_asked++]));
    }
}
