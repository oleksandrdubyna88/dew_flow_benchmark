using Bench.Domain;
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
public sealed class LegRecorder : IFunnelSink
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
    private RetrievalFunnel _funnel = RetrievalFunnel.None;
    private string _funnelNote = string.Empty;

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

    /// <summary>Records that the prompt could not be obtained, and why.
    /// <para>
    /// Two named methods rather than one taking a field name. The first version took a string and fell
    /// back to the response for anything it did not recognise, so a typo would have silently
    /// overwritten the wrong field with the right reason — a mislabelled gap, which is worse than an
    /// unlabelled one because it reads as deliberate.
    /// </para></summary>
    public void CouldNotCapturePrompt(string reason) => _prompt = Captured.Unavailable(reason);

    /// <summary>Records that the response could not be obtained, and why. This is the common one: for
    /// some runtimes the answer's text is simply unobtainable — a CLI stream keeps a result's SIZE,
    /// not its body.</summary>
    public void CouldNotCaptureResponse(string reason) => _response = Captured.Unavailable(reason);

    /// <summary>A funnel an engine reported for a retrieval it performed during this leg.
    /// <para>
    /// A failure is recorded rather than discarded, and that is the whole reason this takes an
    /// <see cref="Outcome{T}"/>. An engine that claimed a trace contract and then sent something this
    /// build cannot read renders exactly like a black-box engine once the reason is dropped — and a
    /// contract mismatch that looks like a design decision is one nobody goes and fixes.
    /// </para>
    /// <para>
    /// Several retrievals in one leg keep the LAST funnel. That is a real limit rather than a
    /// preference, and it is stated here because a leg with three searches has three funnels: the
    /// per-question funnel is what the measured question needs ("was the target ever admitted"), and
    /// carrying all of them is a change to <see cref="LegTrace"/> rather than to this method.
    /// </para></summary>
    public void Retrieved(Outcome<RetrievalFunnel> funnel) =>
        funnel.Match(
            ok =>
            {
                _funnel = ok;
                _funnelNote = string.Empty;
                return 0;
            },
            reason =>
            {
                _funnel = RetrievalFunnel.None;
                _funnelNote = reason;
                return 0;
            });

    /// <summary>The trace as observed. A funnel is never invented here: a black-box observer sees the
    /// calls a subject made, and nothing about what happened inside the engine that served them — so
    /// what appears below is only ever what an engine reported through <see cref="Retrieved"/>.</summary>
    public LegTrace Assemble() => new(
        _prompt,
        _response,
        [.. _calls],
        new TimeBuckets(_tools, _thinking, _infrastructureWait),
        _tokens,
        _cost,
        _funnel)
    {
        FunnelNote = _funnelNote,
    };
}
