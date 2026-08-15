using Bench.Application;
using Bench.Domain.Engines;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Trace;

/// <summary>The recorder, on its own. Every field it can report and every one it can decline to.
/// <para>
/// Worth testing directly rather than only through <c>LiveTrace</c>: this is the one object that
/// decides what a run is able to say about itself afterwards, and a field it silently defaults is a
/// claim nobody made.
/// </para></summary>
public sealed class LegRecorderTests
{
    [Fact]
    public void A_fresh_recorder_claims_nothing_and_says_why()
    {
        var trace = new LegRecorder().Assemble();

        // Everything starts as an admission. A recorder that initialised these to empty strings would
        // report a leg that answered nothing rather than a leg nobody watched.
        trace.Prompt.WasCaptured.Should().BeFalse();
        trace.Response.WasCaptured.Should().BeFalse();
        trace.Prompt.Reason.Should().NotBeEmpty();
        trace.ToolCalls.Should().BeEmpty();
        trace.Time.Total.Should().Be(TimeSpan.Zero);
        trace.CostUsd.Should().Be(0m);
    }

    [Fact]
    public void What_was_sent_and_answered_is_captured_with_its_text()
    {
        var recorder = new LegRecorder();
        recorder.Sent("where is the order total computed?");
        recorder.Answered("in OrderService.Total");

        var trace = recorder.Assemble();

        trace.Prompt.Should().Be(Captured.Text("where is the order total computed?"));
        trace.Response.Should().Be(Captured.Text("in OrderService.Total"));
    }

    [Fact]
    public void A_field_the_runtime_could_not_report_keeps_its_reason()
    {
        var recorder = new LegRecorder();
        recorder.Sent("a question");
        recorder.CouldNotCaptureResponse("this CLI stream reports a result's size, not its text");

        var trace = recorder.Assemble();

        // The prompt was captured and the response was not — an ordinary, honest combination that a
        // single "captured or not" flag over the whole leg could not express.
        trace.Prompt.WasCaptured.Should().BeTrue();
        trace.Response.WasCaptured.Should().BeFalse();
        trace.Response.Reason.Should().Contain("size, not its text");
        trace.Response.Value.Should().BeEmpty();
    }

    [Fact]
    public void An_uncapturable_prompt_does_not_touch_the_response()
    {
        var recorder = new LegRecorder();
        recorder.Answered("the answer");
        recorder.CouldNotCapturePrompt("the runtime composed it internally");

        var trace = recorder.Assemble();

        // The reason these are two named methods: the first version dispatched on a field NAME and fell
        // back to the response for anything it did not recognise, so a typo would have put the right
        // reason on the wrong field — a mislabelled gap reads as deliberate, which is worse than a
        // blank one.
        trace.Response.Should().Be(Captured.Text("the answer"));
        trace.Prompt.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public void Tokens_and_cost_are_recorded_as_the_runtime_reported_them()
    {
        var recorder = new LegRecorder();
        recorder.Spent(new TokenSplit(1200, 8000, 400), 0.0431m);

        var trace = recorder.Assemble();

        // The split matters, not just the total: a leg that read 8000 tokens from cache and one that
        // paid for 8000 fresh cost different money for the same work.
        trace.Tokens.Fresh.Should().Be(1200);
        trace.Tokens.CacheRead.Should().Be(8000);
        trace.Tokens.CacheWrite.Should().Be(400);
        trace.Tokens.Total.Should().Be(9600);
        trace.CostUsd.Should().Be(0.0431m);
    }

    [Fact]
    public void Every_call_adds_its_own_time_to_the_tools_bucket_exactly_once()
    {
        var recorder = new LegRecorder();
        recorder.Called("read_file", "{}", ToolAnswer.Success("x"), TimeSpan.FromMilliseconds(10));
        recorder.Called("read_file", "{}", ToolAnswer.Success("y"), TimeSpan.FromMilliseconds(15));
        recorder.Called("read_file", "{}", ToolAnswer.Refusal("no"), TimeSpan.FromMilliseconds(2));

        var trace = recorder.Assemble();

        // The duration goes into the bucket at the same place the call is recorded, so a call can
        // neither be counted twice nor forgotten by a caller who remembered one and not the other.
        trace.ToolCalls.Should().HaveCount(3);
        trace.Time.Tools.Should().Be(TimeSpan.FromMilliseconds(27));
    }

    [Fact]
    public void An_answered_call_carries_no_error_and_a_refused_one_carries_its_reason()
    {
        var recorder = new LegRecorder();
        recorder.Called("read_file", """{"path":"a"}""", ToolAnswer.Success("content"), TimeSpan.Zero);
        recorder.Called("read_file", """{"path":"b"}""", ToolAnswer.Refusal("outside the repository"), TimeSpan.Zero);
        recorder.Called("read_file", """{"path":"c"}""", ToolAnswer.Failure("the disk went away"), TimeSpan.Zero);

        var calls = recorder.Assemble().ToolCalls;

        calls[0].Refused.Should().BeFalse();
        calls[0].Error.Should().BeEmpty("an answered call has nothing to explain");

        // Refused and Failed are both non-answers, and only the first is the guard working. The flag
        // separates them; the text says which.
        calls[1].Refused.Should().BeTrue();
        calls[1].Error.Should().Be("outside the repository");
        calls[2].Refused.Should().BeFalse();
        calls[2].Error.Should().Be("the disk went away");
    }

    [Fact]
    public void The_arguments_of_a_call_are_kept_so_a_refusal_can_be_explained_later()
    {
        var recorder = new LegRecorder();
        recorder.Called("read_file", """{"path":"../secrets"}""", ToolAnswer.Refusal("outside"), TimeSpan.Zero);

        // "A call was refused" is half a fact; the other half is what it asked for.
        recorder.Assemble().ToolCalls.Single().ArgumentsJson.Should().Contain("../secrets");
    }

    [Fact]
    public void Thinking_and_waiting_accumulate_into_their_own_buckets()
    {
        var recorder = new LegRecorder();
        recorder.Thought(TimeSpan.FromSeconds(1));
        recorder.Thought(TimeSpan.FromSeconds(2));
        recorder.Waited(TimeSpan.FromSeconds(5));

        var time = recorder.Assemble().Time;

        time.Thinking.Should().Be(TimeSpan.FromSeconds(3));
        time.InfrastructureWait.Should().Be(TimeSpan.FromSeconds(5));
        time.Tools.Should().Be(TimeSpan.Zero, "no tool ran, and an empty bucket is not the same as an absent one");
    }

    [Fact]
    public void A_recorder_never_invents_a_funnel()
    {
        var recorder = new LegRecorder();
        recorder.Called("read_file", "{}", ToolAnswer.Success("x"), TimeSpan.FromMilliseconds(5));

        // A black-box observer sees the calls a subject made and nothing about what happened inside the
        // engine that served them. Assembling an empty funnel here would let a report print a retrieval
        // that admitted nothing.
        recorder.Assemble().Funnel.IsPresent.Should().BeFalse();
        recorder.Assemble().IsWhiteBox.Should().BeFalse();
    }
}
