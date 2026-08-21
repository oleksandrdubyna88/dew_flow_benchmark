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

    [Fact]
    public async Task The_LEDGER_survives_the_round_trip_with_its_order_arguments_and_outcomes()
    {
        // The whole point of step 7. A metric says a tool was called; only this says in what ORDER, with
        // what arguments, and whether the engine took it — which is the difference between "the surface
        // moved the score" and any account of how it was worked. Without it the doctrine under test
        // ("locate before you read") is a claim nothing in the system can contradict.
        var stored = await LegAsync(
            Looping(),
            [Asks("read", """{"path":"a.txt"}"""), Asks("read", """{"path":"b.txt"}"""), Final("both read")]);

        var ledger = (await ReadBackAsync(stored)).Calls;

        ledger.Offered.Should().BeTrue();
        ledger.Source.Should().Be(ToolCallSource.Observed, "this harness drove every turn");
        // Repeats kept: "search, read, read" and "read, search, read" are the difference between a doctrine
        // followed and one ignored, and a deduplicated set cannot tell them apart.
        ledger.Sequence.Should().Equal(["read", "read"]);
        ledger.Entries.Select(e => e.Ordinal).Should().Equal(0, 1);
        ledger.Entries.Select(e => e.Turn).Should().Equal(1, 2);
        ledger.Entries[0].Call.ArgumentsJson.Should().Contain("a.txt");
        ledger.Entries[1].Call.ArgumentsJson.Should().Contain("b.txt");
        ledger.Entries.Should().OnlyContain(e => e.Phase == PhaseKind.Answer);
    }

    [Fact]
    public async Task A_REFUSAL_is_stored_as_a_refusal_with_its_reason_rather_than_as_a_call_that_worked()
    {
        // The distinction whose absence upstream let a false read-only guarantee stand for months: all the
        // ledger recorded was a result's length, so a refused call and an executed one looked identical.
        var stored = await LegAsync(
            Looping(ToolAnswer.Refusal("outside the workspace")),
            [Asks("read", """{"path":"/etc/passwd"}"""), Final("that path is outside the tree")]);

        var ledger = (await ReadBackAsync(stored)).Calls;

        ledger.Refused.Should().Be(1);
        ledger.Entries.Single().Call.Error.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_FLOOR_leg_reads_back_as_NOT_OFFERED_rather_than_as_an_empty_list()
    {
        // An empty row set alone cannot tell "this lane had no tools" from "the subject ignored four of
        // them", and only the second is evidence about the descriptions. That is the one fact a table of
        // calls structurally cannot carry, which is why it is a column of its own.
        var stored = await LegAsync(LaneRoster.Floor, [Final("it reads one.txt")]);

        (await ReadBackAsync(stored)).Calls.Offered.Should().BeFalse();
    }

    [Fact]
    public async Task A_lane_that_OFFERED_tools_to_a_subject_that_called_none_is_offered_with_no_entries()
    {
        var stored = await LegAsync(Looping(), [Final("I already know: it reads one.txt")]);

        var ledger = (await ReadBackAsync(stored)).Calls;

        ledger.Offered.Should().BeTrue("the tools were there — that the subject ignored them is the finding");
        ledger.Entries.Should().BeEmpty();
    }

    /// <summary>Read the leg back through the store, because a round trip is the only thing that proves a
    /// column was written rather than merely assigned.</summary>
    private async Task<LegResult> ReadBackAsync(LegResult stored)
    {
        var results = postgres.NewResults();
        await using var db = postgres.NewContext();
        var runId = db.Cells.Single(c => c.Id == stored.CellId).RunId;

        return (await results.ForRunAsync(runId, Ct)).Single(r => r.Id == stored.Id);
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
