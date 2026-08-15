using Bench.Domain.Engines;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Collects what happened during one leg, so a black-box trace is a BY-PRODUCT of running it
/// rather than something reconstructed afterwards from logs.
/// <para>
/// Everything here starts as <see cref="Captured.Unavailable"/> and stays that way until something
/// actually reports it. That is the whole discipline: for some runtimes a tool result's text is simply
/// unobtainable — a CLI stream keeps a result's SIZE, not its body — and a recorder that initialised
/// those fields to empty strings would turn a gap in instrumentation into a claim about the subject.
/// </para></summary>
public sealed class LegRecorder
{
    private const string NotReported = "the runtime did not report it";

    private readonly List<ToolCall> _calls = [];

    private Captured _prompt = Captured.Unavailable(NotReported);
    private Captured _response = Captured.Unavailable(NotReported);
    private TimeSpan _tools;
    private TimeSpan _thinking;
    private TimeSpan _infrastructureWait;
    private TokenSplit _tokens;
    private decimal _cost;

    /// <summary>One tool call, with how it ENDED. The duration goes into the tools bucket here and
    /// nowhere else, so a call can never be counted twice or forgotten.</summary>
    public void Called(string tool, string argumentsJson, ToolAnswer answer, TimeSpan duration)
    {
        _calls.Add(new ToolCall(
            tool,
            argumentsJson,
            answer.WasRefused,
            answer is ToolAnswer.Ok ? string.Empty : answer.Text,
            duration));

        _tools += duration;
    }

    public void Thought(TimeSpan duration) => _thinking += duration;

    /// <summary>Time spent waiting on infrastructure — an accelerator lease, a queue, a cold start.
    /// <para>Its own bucket because a busy card otherwise reads as a slow model, which is a quality
    /// conclusion drawn from a scheduling fact.</para></summary>
    public void Waited(TimeSpan duration) => _infrastructureWait += duration;

    public void Spent(TokenSplit tokens, decimal costUsd)
    {
        _tokens = tokens;
        _cost = costUsd;
    }

    public void Sent(string prompt) => _prompt = Captured.Text(prompt);

    public void Answered(string response) => _response = Captured.Text(response);

    /// <summary>Records that a field could not be obtained, and why. Calling this is how "not captured"
    /// gets a REASON instead of being the silent default.</summary>
    public void CouldNotCapture(string what, string reason)
    {
        if (what == nameof(LegTrace.Prompt))
        {
            _prompt = Captured.Unavailable(reason);
            return;
        }

        _response = Captured.Unavailable(reason);
    }

    /// <summary>The trace as observed. A funnel is never invented here: a black-box observer sees the
    /// calls a subject made, and nothing about what happened inside the engine that served them.</summary>
    public LegTrace Assemble() => new(
        _prompt,
        _response,
        [.. _calls],
        new TimeBuckets(_tools, _thinking, _infrastructureWait),
        _tokens,
        _cost,
        RetrievalFunnel.None);
}
