using System.Text;
using System.Text.Json.Nodes;
using Bench.Application;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Trace;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Probes;

/// <param name="Executables">Every subject's executable as resolved on this machine (subject id → absolute path).</param>
/// <param name="Wall">The ceiling on one CLI call (<c>--cell-timeout-minutes</c>, default five minutes): at it the process tree
/// is killed and the attempt settles <i>timed out</i>.</param>
public sealed record CliProbeSettings(IReadOnlyDictionary<string, string> Executables, TimeSpan Wall);

/// <summary>One CLI attempt end to end (S2): fixture → <see cref="ICliAgentTranscripts"/> (the one launcher, through
/// <c>CliArgv</c>) → the RAW evidence committed → the reading (<see cref="IProbeAttemptReader"/>) → the extracted artefacts →
/// a settlement, or the unmeasured hand-back.
/// <list type="bullet">
/// <item><b>The raw evidence goes to disk BEFORE anything is parsed</b> (S2b, finding 6): stdout, stderr, the exact argv and the
/// prompt. Two live codex cells faulted inside the reader and left no artefact; now a reader that throws settles the attempt
/// <i>failed</i> with every fact <i>not captured</i> and a <c>fault.txt</c> saying why — a parse fault is a fact about our reader,
/// never a leg fault, and the transcript is there to fix the reader against.</item>
/// <item>A quota marker on the CLI's own stderr or in its answer (<see cref="Domain.Gate.ReviewerAccountOut.CliReason"/>) hands the
/// attempt back <see cref="ProbeReason.AccountOut"/> — D8, a quota stop is not a measurement — with the artefacts kept.</item>
/// <item>The answer and the tool trace (<c>tools.json</c>: offered, used, denied — WHICH tool reached a file) are committed beside the
/// raw files; every artefact is referenced by hash, so a verdict is auditable from disk the moment it exists.</item>
/// <item>The fixture is deleted in <c>finally</c>, whatever happened.</item>
/// </list>
/// The <c>read-inside</c> control's voiding of the subject's read probes (<see cref="ProbeVerdicts.UnderControl"/>) is applied
/// where the subject's verdicts are read together — the report — not here, where one cell is measured on its own.</summary>
public sealed class CliProbeRunner(
    ICliAgentTranscripts agents, IProbeFixtures fixtures, IProbeArtifacts artifacts, CliProbeSettings settings, IProbeAttemptReader reader, ILogger<CliProbeRunner> logger) : IProbeRunner
{
    /// <summary>The production composition: the live reader.</summary>
    public CliProbeRunner(ICliAgentTranscripts agents, IProbeFixtures fixtures, IProbeArtifacts artifacts, CliProbeSettings settings, ILogger<CliProbeRunner> logger)
        : this(agents, fixtures, artifacts, settings, ProbeAttemptReader.Live, logger)
    {
    }

    public async Task<ProbeAttemptResult> RunAsync(ProbeRun run, ProbeSubject subject, ProbeCell claimed, CancellationToken cancellationToken)
    {
        var prepared = Prepare(subject, claimed);

        if (prepared is Outcome<Prepared>.Fail notPrepared)
        {
            logger.LogWarning("Probe cell {Cell} could not be prepared and settles failed: {Reason}", claimed.Id, notPrepared.Reason);
            return Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(notPrepared.Reason)), []);
        }

        var (scope, kind, executable, fixture) = ((Outcome<Prepared>.Ok)prepared).Value;

        try
        {
            var prompt = ProbeLaunch.Prompt(claimed.Probe, fixture);
            var ask = new AgentAsk(kind, executable, prompt, fixture.Cwd, settings.Wall, subject.ModelId)
            {
                Options = ProbeLaunch.OptionsFor(claimed.Probe, subject.Runtime, subject.Confinement, fixture),
            };

            return await (await agents.TranscriptAsync(ask, cancellationToken)).Match(
                transcript => MeasureAsync(run, subject, claimed, scope, fixture, prompt, transcript, cancellationToken),
                reason => Task.FromResult(Refused(claimed, reason)));
        }
        finally
        {
            fixtures.Delete(fixture);
        }
    }

    private sealed record Prepared(ProbeAttemptScope Scope, Domain.Registry.ModelRuntimeKind Kind, string Executable, ProbeFixture Fixture);

    /// <summary>Everything decided before the launch: the scope (a claimed cell's attempt), the CLI kind, the executable, the
    /// fixture. A refusal here is a cell that could not be measured as itself and settles <i>failed</i> with the reason as its
    /// uncaptured exit code's note.</summary>
    private Outcome<Prepared> Prepare(ProbeSubject subject, ProbeCell claimed) =>
        ProbeAttemptScope.Of(claimed).Match(
            scope => ProbeLaunch.RuntimeKind(subject.Runtime).Match(
                kind => Executable(subject).Match(
                    executable => fixtures.Begin(claimed.Probe, scope).Match(
                        fixture => Outcome<Prepared>.Success(new Prepared(scope, kind, executable, fixture)),
                        Outcome<Prepared>.Failure),
                    Outcome<Prepared>.Failure),
                Outcome<Prepared>.Failure),
            Outcome<Prepared>.Failure);

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
        ProbeRun run, ProbeSubject subject, ProbeCell claimed, ProbeAttemptScope scope, ProbeFixture fixture, string prompt, AgentTranscript transcript, CancellationToken cancellationToken)
    {
        // The raw evidence FIRST — whatever the readers then make of it.
        var raw = await CommitAsync(scope, cancellationToken,
            (ProbeArtifactKind.Stdout, transcript.Stdout), (ProbeArtifactKind.Stderr, transcript.Stderr), (ProbeArtifactKind.Argv, ArgvJson(transcript.Argv)), (ProbeArtifactKind.Prompt, prompt));

        return TryRead(run, subject, claimed.Probe, fixture.Tokens, transcript) switch
        {
            Outcome<ProbeAttemptReading>.Ok read => await SettleReadAsync(claimed, scope, read.Value, raw, cancellationToken),
            Outcome<ProbeAttemptReading>.Fail fault => await FaultedAsync(claimed, scope, fault.Reason, transcript, raw, cancellationToken),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>The one place a reader bug is allowed to land: as a value this runner settles FAILED on, with the raw transcript already
    /// on disk. Two live codex cells faulted this way (a duplicate JSON key) and left no artefact at all.</summary>
    private Outcome<ProbeAttemptReading> TryRead(ProbeRun run, ProbeSubject subject, ProbeKind probe, ProbeTokens tokens, AgentTranscript transcript)
    {
        try
        {
            return Outcome<ProbeAttemptReading>.Success(reader.Read(run, subject, probe, tokens, transcript));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Outcome<ProbeAttemptReading>.Failure($"{ex.GetType().FullName}: {ex.Message}");
        }
    }

    private async Task<ProbeAttemptResult> SettleReadAsync(ProbeCell claimed, ProbeAttemptScope scope, ProbeAttemptReading reading, IReadOnlyList<ProbeArtifact> raw, CancellationToken cancellationToken)
    {
        var extracted = await CommitAsync(scope, cancellationToken, (ProbeArtifactKind.Answer, reading.Answer), (ProbeArtifactKind.Tools, reading.Trace.ToJson()));

        if (reading.QuotaLine.Length > 0)
        {
            logger.LogWarning("Probe cell {Cell}: the subject's account is out — {Line}", claimed.Id, reading.QuotaLine);
            return new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut);
        }

        return Settled(reading.Facts, [.. raw, .. extracted]);
    }

    private async Task<ProbeAttemptResult> FaultedAsync(ProbeCell claimed, ProbeAttemptScope scope, string reason, AgentTranscript transcript, IReadOnlyList<ProbeArtifact> raw, CancellationToken cancellationToken)
    {
        logger.LogError("Probe cell {Cell}: the reader faulted on this transcript — {Reason}. The attempt settles failed with every fact not captured; the raw evidence is kept to fix the reader against", claimed.Id, reason);
        var fault = await CommitAsync(scope, cancellationToken, (ProbeArtifactKind.Fault, reason + "\n"));

        return Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, transcript.ExitCode), [.. raw, .. fault]);
    }

    /// <summary>The exact argv, one JSON array — a CLI launch carries no secret, so there is nothing to scrub here.</summary>
    private static string ArgvJson(IReadOnlyList<string> argv) => new JsonArray([.. argv.Select(a => (JsonNode)JsonValue.Create(a))]).ToJsonString() + "\n";

    private async Task<IReadOnlyList<ProbeArtifact>> CommitAsync(ProbeAttemptScope scope, CancellationToken cancellationToken, params (ProbeArtifactKind Kind, string Text)[] files)
    {
        var committed = new List<ProbeArtifact>();

        foreach (var (kind, text) in files)
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
