using Bench.Application;
using Bench.Domain.Gate;
using Bench.Domain.Probes;

namespace Bench.Infrastructure.Probes;

/// <summary>What the readers extracted from one CLI transcript: the answer, how the attempt ended, its facts, the tool trace
/// (the <c>tools.json</c> artefact) and the quota line when the CLI said its account is out.</summary>
public sealed record ProbeAttemptReading(string Answer, ProbeAttemptKind Kind, ProbeFacts Facts, ToolTrace Trace, string QuotaLine);

/// <summary>The reading of one transcript, as a unit the runner composes (S2b, finding 6). It MAY throw — a reader bug is an
/// exception, not a value — and the RUNNER is where that exception becomes a value: the raw evidence is already on disk there, and
/// the attempt settles <i>failed</i> with a <c>fault.txt</c>. A test hands the runner a reader that throws and watches the artefacts
/// survive it.</summary>
public interface IProbeAttemptReader
{
    ProbeAttemptReading Read(ProbeRun run, ProbeSubject subject, ProbeKind probe, ProbeTokens tokens, AgentTranscript transcript);
}

/// <summary>The live reading: <see cref="ProbeTranscripts"/> for the answer and the tools, <see cref="ProbeExits"/> for the exit,
/// <see cref="ReviewerAccountOut.CliReason"/> for the quota, <see cref="ProbeVerdicts"/> for the facts.
/// <list type="bullet">
/// <item>A usage-error exit is <i>launch refused</i>, the wall <i>timed out</i>, any other non-zero exit <i>failed</i>; every fact <i>not captured</i>.</item>
/// <item>A clean exit that SAID nothing is <i>failed</i> — unless the grammar's stream reached its final event (S2b): then the silence
/// is the answer, the facts off the answer read <i>not captured</i>, and the tool facts are read off the stream (agy's headless
/// web-search cell of 2026-10-01: a <c>search_web</c> step, a <c>read_url</c> auto-denied, an empty response).</item>
/// </list></summary>
public sealed class ProbeAttemptReader : IProbeAttemptReader
{
    public static ProbeAttemptReader Live { get; } = new();

    public ProbeAttemptReading Read(ProbeRun run, ProbeSubject subject, ProbeKind probe, ProbeTokens tokens, AgentTranscript transcript)
    {
        var answer = ProbeTranscripts.Answer(subject.Runtime, transcript.Stdout);
        var trace = ProbeTranscripts.Trace(subject.Runtime, transcript.Stdout);
        var kind = Kind(subject, transcript, answer, trace);
        var facts = kind == ProbeAttemptKind.Answered
            ? ProbeVerdicts.For(probe, answer, ProbeTranscripts.Evidence(subject.Runtime, trace), tokens, run.Oracle) with { ExitCode = transcript.ExitCode }
            : ProbeFacts.NothingCaptured(kind, transcript.ExitCode);

        return new ProbeAttemptReading(answer, kind, facts, trace, ReviewerAccountOut.CliReason(AccountOutText(transcript, answer)));
    }

    /// <summary>What the quota reading sees: stderr and the model's own answer always; the whole transcript only when the exit
    /// was non-zero — a web-search result quoting "usage limit" inside a tool result must not bench a subject that answered.</summary>
    private static string AccountOutText(AgentTranscript transcript, string answer) =>
        transcript.ExitCode is { WasCaptured: true, Value: 0 } ? transcript.Stderr + "\n" + answer : transcript.Stderr + "\n" + answer + "\n" + transcript.Stdout;

    private static ProbeAttemptKind Kind(ProbeSubject subject, AgentTranscript transcript, string answer, ToolTrace trace) =>
        (transcript.TimedOut, Exit(subject, transcript), answer.Length == 0 && !trace.Complete) switch
        {
            (true, _, _) => ProbeAttemptKind.TimedOut,
            (_, ProbeAttemptKind.Answered, true) => ProbeAttemptKind.Failed,
            (_, var kind, _) => kind,
        };

    private static ProbeAttemptKind Exit(ProbeSubject subject, AgentTranscript transcript) =>
        transcript.ExitCode.WasCaptured ? ProbeExits.Classify(subject.Runtime, (int)transcript.ExitCode.Value, transcript.Stderr) : ProbeAttemptKind.Failed;
}
