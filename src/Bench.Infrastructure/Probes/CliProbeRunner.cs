using System.Text;
using Bench.Application;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Trace;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Probes;

/// <param name="Executables">Every subject's executable as resolved on this machine (subject id → absolute path).</param>
/// <param name="Wall">The ceiling on one CLI call (<c>--cell-timeout-minutes</c>, default five minutes): at it the process tree
/// is killed and the attempt settles <i>timed out</i>.</param>
public sealed record CliProbeSettings(IReadOnlyDictionary<string, string> Executables, TimeSpan Wall);

/// <summary>One CLI attempt end to end (S2): fixture → <see cref="ICliAgentTranscripts"/> (the one launcher, through
/// <c>CliArgv</c>) → the three artefacts committed → the facts read through <see cref="ProbeTranscripts"/> and
/// <see cref="ProbeVerdicts"/> → a settlement, or the unmeasured hand-back.
/// <list type="bullet">
/// <item>A quota marker on the CLI's own stderr or in its answer (<see cref="ReviewerAccountOut.CliReason"/>) hands the attempt
/// back <see cref="ProbeReason.AccountOut"/> — D8, a quota stop is not a measurement — with the artefacts kept.</item>
/// <item>A usage-error exit settles <i>launch refused</i>, every fact <i>not captured</i>, the exit code kept; the wall settles
/// <i>timed out</i>; any other non-zero exit, or a clean exit that said nothing, settles <i>failed</i>.</item>
/// <item>The artefacts are committed BEFORE the settlement is decided and referenced by hash, so a verdict is auditable from disk
/// the moment it exists; the fixture is deleted in <c>finally</c>, whatever happened.</item>
/// </list>
/// The <c>read-inside</c> control's voiding of the subject's read probes (<see cref="ProbeVerdicts.UnderControl"/>) is applied
/// where the subject's verdicts are read together — the report — not here, where one cell is measured on its own.</summary>
public sealed class CliProbeRunner(
    ICliAgentTranscripts agents, IProbeFixtures fixtures, IProbeArtifacts artifacts, CliProbeSettings settings, ILogger<CliProbeRunner> logger) : IProbeRunner
{
    public async Task<ProbeAttemptResult> RunAsync(ProbeRun run, ProbeSubject subject, ProbeCell claimed, CancellationToken cancellationToken)
    {
        var prepared = Prepare(subject, claimed);

        if (prepared is Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Fail notPrepared)
        {
            logger.LogWarning("Probe cell {Cell} could not be prepared and settles failed: {Reason}", claimed.Id, notPrepared.Reason);
            return Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(notPrepared.Reason)), []);
        }

        var (scope, kind, executable, fixture) = ((Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Ok)prepared).Value;

        try
        {
            var ask = new AgentAsk(kind, executable, ProbeLaunch.Prompt(claimed.Probe, fixture), fixture.Cwd, settings.Wall, subject.ModelId)
            {
                Options = ProbeLaunch.OptionsFor(claimed.Probe, subject.Runtime, fixture),
            };

            return await (await agents.TranscriptAsync(ask, cancellationToken)).Match(
                transcript => MeasureAsync(run, subject, claimed, scope, fixture, transcript, cancellationToken),
                reason => Task.FromResult(Refused(claimed, reason)));
        }
        finally
        {
            fixtures.Delete(fixture);
        }
    }

    /// <summary>Everything decided before the launch: the scope (a claimed cell's attempt), the CLI kind, the executable, the
    /// fixture. A refusal here is a cell that could not be measured as itself and settles <i>failed</i> with the reason as its
    /// uncaptured exit code's note.</summary>
    private Outcome<(ProbeAttemptScope Scope, Domain.Registry.ModelRuntimeKind Kind, string Executable, ProbeFixture Fixture)> Prepare(ProbeSubject subject, ProbeCell claimed) =>
        ProbeAttemptScope.Of(claimed).Match(
            scope => ProbeLaunch.RuntimeKind(subject.Runtime).Match(
                kind => Executable(subject).Match(
                    executable => fixtures.Begin(claimed.Probe, scope).Match(
                        fixture => Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Success((scope, kind, executable, fixture)),
                        Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Failure),
                    Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Failure),
                Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Failure),
            Outcome<(ProbeAttemptScope, Domain.Registry.ModelRuntimeKind, string, ProbeFixture)>.Failure);

    private Outcome<string> Executable(ProbeSubject subject) =>
        settings.Executables.TryGetValue(subject.Id.Value, out var executable) && executable.Length > 0
            ? Outcome<string>.Success(executable)
            : Outcome<string>.Failure($"no executable was resolved for subject '{subject.Id}' ({subject.ExecutableRef})");

    private ProbeAttemptResult Refused(ProbeCell claimed, string reason)
    {
        // The launch never happened — an executable not installed, an option the CLI has no flag for. Nothing was spent and
        // nothing was measured; the cell settles failed with the reason as its note, never as a fact about the CLI.
        logger.LogWarning("Probe cell {Cell}: the launch was refused — {Reason}", claimed.Id, reason);
        return Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(reason)), []);
    }

    private async Task<ProbeAttemptResult> MeasureAsync(
        ProbeRun run, ProbeSubject subject, ProbeCell claimed, ProbeAttemptScope scope, ProbeFixture fixture, AgentTranscript transcript, CancellationToken cancellationToken)
    {
        var answer = ProbeTranscripts.Answer(subject.Runtime, transcript.Stdout);
        var committed = await CommitAsync(scope, answer, transcript, cancellationToken);

        if (ReviewerAccountOut.CliReason(AccountOutText(transcript, answer)) is { Length: > 0 } quota)
        {
            logger.LogWarning("Probe cell {Cell}: the subject's account is out — {Line}", claimed.Id, quota);
            return new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut);
        }

        return Settled(Facts(run, subject, claimed, fixture, transcript, answer), committed);
    }

    /// <summary>What the quota reading sees: stderr and the model's own answer always; the whole transcript only when the exit
    /// was non-zero — a web-search result quoting "usage limit" inside a tool result must not bench a subject that answered.</summary>
    private static string AccountOutText(AgentTranscript transcript, string answer) =>
        transcript.ExitCode is { WasCaptured: true, Value: 0 } ? transcript.Stderr + "\n" + answer : transcript.Stderr + "\n" + answer + "\n" + transcript.Stdout;

    private static ProbeFacts Facts(ProbeRun run, ProbeSubject subject, ProbeCell claimed, ProbeFixture fixture, AgentTranscript transcript, string answer)
    {
        var kind = Kind(subject, transcript, answer);

        return kind == ProbeAttemptKind.Answered
            ? ProbeVerdicts.For(claimed.Probe, answer, ProbeTranscripts.Read(subject.Runtime, transcript.Stdout), fixture.Tokens, run.Oracle) with { ExitCode = transcript.ExitCode }
            : ProbeFacts.NothingCaptured(kind, transcript.ExitCode);
    }

    /// <summary>Timed out before anything else; then the exit as <see cref="ProbeExits"/> reads it; a clean exit that SAID
    /// nothing is a failure, not an answer — an empty answer would read every canary as <i>no</i>.</summary>
    private static ProbeAttemptKind Kind(ProbeSubject subject, AgentTranscript transcript, string answer) =>
        (transcript.TimedOut, transcript.ExitCode.WasCaptured ? ProbeExits.Classify(subject.Runtime, (int)transcript.ExitCode.Value, transcript.Stderr) : ProbeAttemptKind.Failed, answer.Length) switch
        {
            (true, _, _) => ProbeAttemptKind.TimedOut,
            (_, ProbeAttemptKind.Answered, 0) => ProbeAttemptKind.Failed,
            (_, var kind, _) => kind,
        };

    private async Task<IReadOnlyList<ProbeArtifact>> CommitAsync(ProbeAttemptScope scope, string answer, AgentTranscript transcript, CancellationToken cancellationToken)
    {
        var committed = new List<ProbeArtifact>();

        foreach (var (kind, text) in new[] { (ProbeArtifactKind.Answer, answer), (ProbeArtifactKind.Stdout, transcript.Stdout), (ProbeArtifactKind.Stderr, transcript.Stderr) })
        {
            switch (await artifacts.CommitAsync(scope, kind, Encoding.UTF8.GetBytes(text), cancellationToken))
            {
                case Outcome<ProbeArtifact>.Ok ok:
                    committed.Add(ok.Value);
                    break;
                case Outcome<ProbeArtifact>.Fail fail:
                    logger.LogError("Probe artefact {Kind} of {Scope} was not committed: {Reason}", kind, scope, fail.Reason);
                    break;
                default:
                    break;
            }
        }

        return committed;
    }

    private static ProbeAttemptResult Settled(ProbeFacts facts, IReadOnlyList<ProbeArtifact> artifacts) =>
        new ProbeAttemptResult.Settled(new ProbeSettlement(facts, artifacts));
}
