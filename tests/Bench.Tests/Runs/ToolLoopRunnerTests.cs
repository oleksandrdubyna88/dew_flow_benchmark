using Bench.Application;
using Bench.Domain;
using Bench.Domain.Engines;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>
/// The loop the domain has been describing since <c>BudgetKind.Turns</c> was declared.
///
/// <para>Every assertion here is about a distinction the ledger must keep: a final answer against a turn
/// that asked for a tool, a refused call against an executed one, and a leg that ran out of turns against a
/// leg that answered wrongly. The last one is the expensive one — a model still working when the ceiling
/// arrived did not get anything wrong, and averaging it in reports the instrument's limit as the subject's
/// score.</para>
/// </summary>
public sealed class ToolLoopRunnerTests
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_final_answer_on_the_first_turn_ends_the_loop_and_invokes_nothing()
    {
        var engine = new RecordingEngine();

        var result = await Run(new ScriptedRuntime([Final("the total is in OrderService")]), engine);

        result.TurnsSpent.Should().Be(1);
        result.Exhausted.Should().BeFalse();
        result.Calls.Should().BeEmpty();
        result.Transcript.Should().BeEmpty("nothing happened between the question and the answer");
        engine.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task A_requested_call_is_invoked_and_the_conversation_continues()
    {
        var engine = new RecordingEngine();

        var result = await Run(
            new ScriptedRuntime([Asks("read", """{"path":"a.cs"}"""), Final("it is on line 12")]), engine);

        result.TurnsSpent.Should().Be(2);
        result.Exhausted.Should().BeFalse();
        engine.Invocations.Should().Equal(("read", """{"path":"a.cs"}"""));

        // The transcript is what the next turn replays and what the operator has to be able to read: the
        // assistant's turn, then the tool's answer.
        result.Transcript.Should().HaveCount(2);
        result.Transcript[0].Should().BeOfType<ModelTurn.Assistant>();
        result.Transcript[1].Should().BeOfType<ModelTurn.ToolResult>()
            .Which.Content.Should().Be("lines 1-3 of 3");
    }

    [Fact]
    public async Task A_refused_call_is_recorded_as_refused_and_the_loop_CONTINUES()
    {
        // Both halves matter. Recording it as an ordinary answer is the defect that let a false read-only
        // guarantee stand for months upstream; ending the leg on it would score a model down for a guard
        // doing its job, when correcting itself is exactly what it should do next.
        var engine = new RecordingEngine(ToolAnswer.Refusal("outside the workspace"));

        var result = await Run(
            new ScriptedRuntime([Asks("read", """{"path":"/etc/passwd"}"""), Final("I will stay in the repo")]), engine);

        result.Calls.Should().ContainSingle();
        result.Calls[0].Call.Refused.Should().BeTrue();
        result.Calls[0].Call.Error.Should().BeEmpty("a refusal is not an error — the tool understood and said no");
        result.TurnsSpent.Should().Be(2);
        result.Transcript.OfType<ModelTurn.ToolResult>().Single().Content.Should().Contain("outside the workspace");
    }

    [Fact]
    public async Task A_failed_call_carries_its_message_and_is_not_a_refusal()
    {
        var engine = new RecordingEngine(ToolAnswer.Failure("the file is locked"));

        var result = await Run(new ScriptedRuntime([Asks("read", "{}"), Final("done")]), engine);

        result.Calls[0].Call.Refused.Should().BeFalse();
        result.Calls[0].Call.Error.Should().Be("the file is locked");
    }

    [Fact]
    public async Task Every_call_carries_the_TURN_it_happened_on()
    {
        // It cannot be re-derived later: two calls on turn 3 and one on turn 4 are indistinguishable, from
        // an ordered list alone, from three calls on one turn.
        var result = await Run(
            new ScriptedRuntime([Asks("a", "{}"), Asks("b", "{}"), Final("done")]), new RecordingEngine());

        result.Calls.Select(c => c.Turn).Should().Equal(1, 2);
    }

    [Fact]
    public async Task A_turn_that_asks_for_two_tools_invokes_both_before_asking_again()
    {
        var engine = new RecordingEngine();

        var result = await Run(
            new ScriptedRuntime([Asks([("a", "{}"), ("b", "{}")]), Final("done")]), engine);

        engine.Invocations.Should().HaveCount(2);
        result.Calls.Select(c => c.Turn).Should().Equal(1, 1);
        result.Transcript.OfType<ModelTurn.ToolResult>().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_leg_that_never_stops_asking_ends_as_EXHAUSTED_rather_than_as_an_answer()
    {
        var result = await Run(
            new ScriptedRuntime([Asks("a", "{}"), Asks("b", "{}"), Asks("c", "{}")]),
            new RecordingEngine(),
            maxTurns: 3);

        result.Exhausted.Should().BeTrue();
        result.TurnsSpent.Should().Be(3);
    }

    [Fact]
    public async Task The_call_made_on_the_LAST_permitted_turn_is_still_recorded()
    {
        // The ceiling is checked after that turn's calls, not before them: "it was still working when the
        // ceiling arrived" is precisely what the cap reports, and a call dropped there would make a busy
        // leg look idle.
        var result = await Run(new ScriptedRuntime([Asks("a", "{}")]), new RecordingEngine(), maxTurns: 1);

        result.Exhausted.Should().BeTrue();
        result.Calls.Should().ContainSingle();
        result.Calls[0].Turn.Should().Be(1);
    }

    [Fact]
    public async Task A_runtime_failure_ends_the_loop_as_a_failure_rather_than_an_empty_answer()
    {
        var failed = await new ToolLoopRunner(new FailingRuntime(), NullLogger<ToolLoopRunner>.Instance)
            .RunAsync(Endpoint(), Sampling.Deterministic(7), "", "q", [], Surface(new RecordingEngine(), 3), Ct);

        failed.Should().BeOfType<Outcome<ToolLoopResult>.Fail>()
            .Which.Reason.Should().Contain("unreachable");
    }

    [Fact]
    public async Task The_doctrine_reaches_the_model_as_the_system_prompt()
    {
        // Lane.Preamble was declared in the founding tuple, documented, and read by nothing. This is the
        // assertion that it is finally read.
        var runtime = new ScriptedRuntime([Final("answered")]);

        await new ToolLoopRunner(runtime, NullLogger<ToolLoopRunner>.Instance).RunAsync(
            Endpoint(), Sampling.Deterministic(7), "retrieval first, then confirm", "q", [],
            Surface(new RecordingEngine(), 3), Ct);

        runtime.Seen[0].SystemPrompt.Should().Be("retrieval first, then confirm");
    }

    [Fact]
    public async Task Each_turn_replays_everything_that_happened_before_it()
    {
        var runtime = new ScriptedRuntime([Asks("a", "{}"), Final("done")]);

        await Run(runtime, new RecordingEngine());

        runtime.Seen[0].Transcript.Should().BeEmpty();
        runtime.Seen[1].Transcript.Should().HaveCount(2, "the assistant's turn and the tool's answer");
        runtime.Seen[1].Tools.Should().ContainSingle("the surface does not change between turns");
    }

    private async Task<ToolLoopResult> Run(
        IModelRuntime runtime, RecordingEngine engine, int maxTurns = 5) =>
        (await new ToolLoopRunner(runtime, NullLogger<ToolLoopRunner>.Instance)
            .RunAsync(Endpoint(), Sampling.Deterministic(7), "", "where is the total?", [], Surface(engine, maxTurns), Ct))
        .Should().BeOfType<Outcome<ToolLoopResult>.Ok>().Subject.Value;

    private static ToolSurface.Looping Surface(IEngine engine, int maxTurns) =>
        new(engine, [new EngineTool("read", "reads a file", """{"type":"object"}""")], maxTurns);

    private static ModelEndpoint Endpoint() =>
        ModelEndpoint.Parse(
            ModelRef.Parse("qwen3-coder:latest", ModelHosting.Local).Ok(), "http://127.0.0.1:11434/v1").Ok();

    private static ModelAnswer Final(string text) => Answer(text, []);

    private static ModelAnswer Asks(string tool, string arguments) => Asks([(tool, arguments)]);

    private static ModelAnswer Asks(IReadOnlyList<(string Tool, string Arguments)> calls) =>
        Answer("", [.. calls.Select((c, i) => new RequestedToolCall($"call_{i}", c.Tool, c.Arguments))]);

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

    /// <summary>A runtime that answers a prepared script and REMEMBERS what it was asked. The second half is
    /// the point: half these assertions are about the request, not the answer.</summary>
    private sealed class ScriptedRuntime(IReadOnlyList<ModelAnswer> answers) : IModelRuntime
    {
        public List<ModelRequest> Seen { get; } = [];

        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("scripted"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Seen.Add(request);
            return Task.FromResult(Outcome<ModelAnswer>.Success(answers[Seen.Count - 1]));
        }
    }

    private sealed class FailingRuntime : IModelRuntime
    {
        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("failing"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<ModelAnswer>.Failure("the model is unreachable"));
    }

    private sealed class RecordingEngine(ToolAnswer? answer = null) : IEngine
    {
        public List<(string Tool, string Arguments)> Invocations { get; } = [];

        public EngineRef Describe => EngineRef.Filesystem();

        public string TraceContractVersion => string.Empty;

        public IReadOnlyList<EngineTool> Tools => [new EngineTool("read", "reads a file", """{"type":"object"}""")];

        public Task<Outcome<string>> WarmAsync(string checkoutPath, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("warm"));

        public Task<ToolAnswer> InvokeAsync(string tool, string argumentsJson, CancellationToken cancellationToken)
        {
            Invocations.Add((tool, argumentsJson));
            return Task.FromResult(answer ?? ToolAnswer.Success("lines 1-3 of 3"));
        }
    }
}
