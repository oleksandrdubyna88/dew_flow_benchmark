using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Steps;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>Where a step's next entry begins (plan D7, D8, D10; E2 §8 items 3 and 24), decided over facts the runner read from the step's files: the <c>.done</c> marker, the session, whether
/// claude left its transcript, the last attempt and how its stream ended, the timing lines and the gate's <c>.continue</c> marker. Nothing here reads a file — the decision is a function.
/// <para>
/// What this does NOT prove: that the runner reads those facts correctly from disk (E2.S4's restart matrix does), or that claude honours <c>--resume</c> of a session a limit stopped
/// (the pilot does). <c>LastEnd</c> is what a stream read back ALONE can say, so it can never be <c>HardDeadline</c>; that fact lives only in the timing line, and the last-attempt-on-its-deadline
/// scenario says so.
/// </para></summary>
public sealed class StepRecoveryTests
{
    private const string Step = "2-review";

    private static readonly Guid G = Guid.Parse("a03b4849-62f8-4edd-8042-7fccf7f379ef");

    private static readonly DateTimeOffset T = new(2026, 10, 28, 21, 32, 38, TimeSpan.Zero);

    private static readonly Limits Limits = new(1800, 6, 1800, 3, 300, 600);

    private static readonly StepSpec Spec = BriefExample.Experiment.Steps[new StepId("review")] with { Minutes = 40 };

    private static TimingLine.End Ended(string step, int attempt, int wall, bool limited = false, string state = "finished") =>
        new("C1", step, attempt, T, CapturedCount.Number(wall), CapturedCount.Number(0), true, limited, "df9634e9", state, false);

    private static readonly IReadOnlyList<TimingLine> SixHundred = [Ended(Step, 1, 600)];

    private static RecoveryFacts Facts(
        bool done = false,
        Outcome<Guid>? session = null,
        bool transcript = true,
        int last = 1,
        StepEnd? lastEnd = null,
        IReadOnlyList<TimingLine>? timing = null,
        Outcome<ContinueMarker>? marker = null) =>
        new(done, session ?? Outcome<Guid>.Success(G), transcript, last, lastEnd ?? new StepEnd.Interrupted("z"), timing ?? SixHundred, marker ?? Outcome<ContinueMarker>.Failure("no marker"));

    private static StepEntry Decide(RecoveryFacts facts) => StepRecovery.Decide(Step, facts, Spec, Limits);

    private static Outcome<ContinueMarker> Marker(int after) => Outcome<ContinueMarker>.Success(new ContinueMarker(after, "gate blocked: every provider rate-limited"));

    [Fact]
    public void A_done_marker_skips()
    {
        var entry = Decide(Facts(done: true, last: 2, lastEnd: new StepEnd.Limited("x")));

        entry.Should().BeOfType<StepEntry.Skip>("a step whose marker exists is done, whatever else the files say");
    }

    [Fact]
    public void A_last_stream_that_finished_without_a_marker_is_recovered_as_done_never_resumed()
    {
        var entry = Decide(Facts(last: 2, lastEnd: new StepEnd.Finished()));

        entry.Should().Be(new StepEntry.RecoveredDone(new StepEnd.Finished(), 2), "the previous generation's arm C was resumed here and told a finished agent it had been interrupted");
    }

    public static TheoryData<StepEnd> ErrorEnds => new()
    {
        new StepEnd.Errored("x"),
        new StepEnd.Limited("y"),
        new StepEnd.Interrupted("z"),
    };

    [Theory]
    [MemberData(nameof(ErrorEnds))]
    public void A_last_stream_whose_result_is_an_error_resumes(StepEnd end)
    {
        var entry = Decide(Facts(last: 1, lastEnd: end));

        entry.Should().Be(new StepEntry.Resume(G, 2, 1800), "an error result of any kind, a limit or an interruption is resumed with what is left of 40 minutes less the 600 s already spent");
    }

    [Fact]
    public void A_session_without_a_transcript_starts_fresh_with_a_new_id()
    {
        Decide(Facts(last: 1, lastEnd: new StepEnd.Limited("x"), transcript: false)).Should().Be(new StepEntry.Fresh(2, 1800));
        Decide(Facts(last: 1, lastEnd: new StepEnd.Limited("x"), session: Outcome<Guid>.Failure("no session file"))).Should().Be(
            new StepEntry.Fresh(2, 1800), "a session that cannot be read cannot be resumed");
        Decide(Facts(last: 0, lastEnd: new StepEnd.NotStarted("nothing ran"), timing: [], session: Outcome<Guid>.Failure("no session file"), transcript: false)).Should().Be(
            new StepEntry.Fresh(1, 2400), "the first entry of a step is a fresh launch of attempt 1 with the whole budget");
    }

    [Fact]
    public void The_remaining_budget_excludes_limited_attempts_and_floors_at_the_minimum()
    {
        IReadOnlyList<TimingLine> timing =
        [
            Ended(Step, 1, 888, limited: true),
            Ended(Step, 2, 600),
            Ended("3-fix", 1, 999),
            new TimingLine.Start("C1", Step, 3, "resume", G.ToString("D"), "opus", T, 1800, "22:12 UTC (23:12 +01:00)", "df9634e9", "/e/out/2-review.a3.jsonl"),
        ];

        StepBudget.UsedSeconds(timing, Step).Should().Be(600, "a limited attempt, another step's end and a start with no end spend nothing");
        StepBudget.RemainingSeconds(40, timing, Step, 300).Should().Be(1800);
        StepBudget.RemainingSeconds(40, [.. timing, Ended(Step, 3, 2300)], Step, 300).Should().Be(300, "the floor is a decision: 100 s would be no time at all");
        StepBudget.Wall(1800, 600).Should().Be(TimeSpan.FromMinutes(40), "the wall is the budget plus the kill grace");

        IReadOnlyList<TimingLine> recorded = TimingLines.Read(
            "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":1,\"end_utc\":\"2026-10-28T21:47:26Z\",\"wall_s\":888,\"exit\":1,\"result\":true,\"limited\":false,\"head\":\"df9634e9\"}\n"
            + "{\"kind\":\"end\",\"arm\":\"C1\",\"step\":\"2-review\",\"attempt\":2,\"end_utc\":\"2026-10-29T08:46:17Z\",\"wall_s\":53,\"exit\":0,\"result\":true,\"limited\":false,\"head\":\"df9634e9\"}\n");
        StepBudget.UsedSeconds([recorded[0]], Step).Should().Be(888, "the 2026-10-28 attempt a 429 ended was recorded unlimited by the day's detector, so the day's arithmetic spent it — kept as evidence");
        StepBudget.RemainingSeconds(40, [recorded[0]], Step, 300).Should().Be(1512, "the resume of that day was budgeted 2400 - 888");

        StepBudget.UsedSeconds([Ended(Step, 1, 100) with { WallSeconds = CapturedCount.Unavailable("recovered") }], Step).Should().Be(0, "a wall that was not captured is not a wall of zero seconds that ever counts");
    }

    [Fact]
    public void A_wall_is_clamped_before_it_is_summed_so_none_throws_or_subtracts()
    {
        IReadOnlyList<TimingLine> Walls(params long[] walls) => [.. walls.Select((wall, index) => Ended(Step, index + 1, 0) with { WallSeconds = CapturedCount.Number(wall) })];

        StepBudget.UsedSeconds(Walls(9_000_000_000_000_000_000, 9_000_000_000_000_000_000), Step).Should().Be(int.MaxValue, "a huge wall counts as the largest and the sum never overflows");
        StepBudget.UsedSeconds(Walls(600, -3600), Step).Should().Be(600, "a negative wall counts as 0 and never subtracts from another attempt's");
        StepBudget.RemainingSeconds(40, Walls(9_000_000_000_000_000_000), Step, 300).Should().Be(300);
    }

    [Fact]
    public void Timing_of_a_real_arm_reads_back_every_line()
    {
        var lines = TimingLines.Read(ModelArmsFixtures.Read("timing-arm-C.jsonl"));

        lines.Should().HaveCount(8);
        lines.OfType<TimingLine.Start>().Should().HaveCount(4);
        lines.OfType<TimingLine.End>().Should().HaveCount(3);
        lines.OfType<TimingLine.ArmDone>().Should().HaveCount(1);
        lines.OfType<TimingLine.Other>().Should().BeEmpty();

        lines.OfType<TimingLine.Start>().Count(line => line.Step == "1-code" && line.Attempt == 1).Should().Be(1);
        lines.OfType<TimingLine.End>().Any(line => line.Step == "1-code" && line.Attempt == 1).Should().BeFalse("the first attempt of 1-code was cut off: a start and no end");
        lines.OfType<TimingLine.End>().Should().OnlyContain(line => !line.Limited && line.State == string.Empty && !line.Recovered, "part 5's generation wrote no limited, no end and no recovered");

        StepBudget.UsedSeconds(lines, "1-code").Should().Be(852);
        lines.OfType<TimingLine.Start>().Single(line => line.Step == "2-review").Deadline.Should().Be("19:45 UTC (20:45 CET)", "read as text and never re-spelled");
    }

    [Fact]
    public void A_last_attempt_whose_end_line_says_hard_deadline_is_recovered_as_done()
    {
        IReadOnlyList<TimingLine> timing = [Ended(Step, 1, 2400, state: "hard-deadline")];

        Decide(Facts(last: 1, lastEnd: new StepEnd.Interrupted("no result…"), timing: timing)).Should().Be(
            new StepEntry.RecoveredDone(new StepEnd.HardDeadline(), 1), "the stream read back alone cannot see the wall; the end line can");

        Decide(Facts(last: 2, lastEnd: new StepEnd.Interrupted("no result…"), timing: timing)).Should().BeOfType<StepEntry.Resume>(
            "an end line of an EARLIER attempt says nothing about the last one");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_continue_marker_naming_the_last_attempt_resumes_that_finished_session_whatever_done_says(bool doneMarker)
    {
        var entry = Decide(Facts(done: doneMarker, last: 2, lastEnd: new StepEnd.Finished(), marker: Marker(2)));

        entry.Should().Be(new StepEntry.Resume(G, 3, 1800), "the gate asked for this completed session to go on; a .done beside the marker is a gate that died between its two writes");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void A_stale_continue_marker_decides_nothing(int after)
    {
        Decide(Facts(last: 2, lastEnd: new StepEnd.Finished(), marker: Marker(after))).Should().Be(
            new StepEntry.RecoveredDone(new StepEnd.Finished(), 2), "a marker for another attempt is stale: the attempt it ordered has started, or it never could have");
        Decide(Facts(done: true, last: 2, lastEnd: new StepEnd.Finished(), marker: Marker(after))).Should().BeOfType<StepEntry.Skip>();
    }

    [Fact]
    public void A_continue_marker_that_cannot_be_read_decides_nothing()
    {
        Decide(Facts(last: 2, lastEnd: new StepEnd.Finished(), marker: Outcome<ContinueMarker>.Failure("no marker"))).Should().Be(new StepEntry.RecoveredDone(new StepEnd.Finished(), 2));
        Decide(Facts(done: true, last: 2, lastEnd: new StepEnd.Finished(), marker: Outcome<ContinueMarker>.Failure("no marker"))).Should().BeOfType<StepEntry.Skip>();
        Decide(Facts(last: 0, lastEnd: new StepEnd.NotStarted("x"), timing: [], marker: Marker(0), session: Outcome<Guid>.Failure("no session file"), transcript: false)).Should().Be(
            new StepEntry.Fresh(1, 2400), "a marker that names attempt 0 is no order: nothing finished, so nothing is continued");
        Decide(Facts(done: true, last: 0, lastEnd: new StepEnd.NotStarted("x"), timing: [], marker: Marker(0))).Should().BeOfType<StepEntry.Skip>("the same marker beside a .done is still no order");
    }

    [Fact]
    public void A_continue_marker_without_a_transcript_starts_the_step_fresh()
    {
        Decide(Facts(last: 1, lastEnd: new StepEnd.Finished(), marker: Marker(1), transcript: false)).Should().Be(new StepEntry.Fresh(2, 1800));
        Decide(Facts(last: 1, lastEnd: new StepEnd.Finished(), marker: Marker(1), session: Outcome<Guid>.Failure("no session file"))).Should().Be(new StepEntry.Fresh(2, 1800));
    }
}
