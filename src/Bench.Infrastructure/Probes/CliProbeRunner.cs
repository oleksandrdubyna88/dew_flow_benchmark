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
/// <param name="ParentEnvironment">The harness's own environment — what <see cref="ProbeChildEnvironment"/> builds the CLI's minimal
/// environment FROM, and whose secret-named values are scrubbed from every text written (S2c, finding 3).</param>
public sealed record CliProbeSettings(IReadOnlyDictionary<string, string> Executables, TimeSpan Wall, IReadOnlyDictionary<string, string> ParentEnvironment);

/// <summary>One CLI attempt end to end (S2): fixture → <see cref="ICliAgentTranscripts"/> (the one launcher, through
/// <c>CliArgv</c>, under the MINIMAL environment) → the RAW evidence committed → the reading (<see cref="IProbeAttemptReader"/>) →
/// the extracted artefacts → a settlement, or the unmeasured hand-back.
/// <list type="bullet">
/// <item><b>The CLI inherits nothing the harness owns</b> (S2c, finding 3): the child gets <see cref="ProbeChildEnvironment"/>'s set by
/// name — never <c>BENCH_DB</c>, never a <c>*_KEY</c> — and every secret-named value of the bench's own environment is scrubbed from the
/// CLI's stdout and stderr (and so from the answer read off them) BEFORE anything is written. <c>argv.json</c> records the exact argv
/// and the environment's names.</item>
/// <item><b>The raw evidence goes to disk BEFORE anything is parsed</b> (S2b, finding 6): stdout, stderr, the launch record and the
/// prompt. A reader that throws settles the attempt <i>failed</i> with every fact <i>not captured</i> and a <c>fault.txt</c> saying why —
/// a parse fault is a fact about our reader, never a leg fault, and the transcript is there to fix the reader against. Evidence the
/// artefact root REFUSES hands the attempt back unmeasured (S2c, finding 6): a verdict nobody can audit from disk is not recorded.</item>
/// <item>A quota marker on the CLI's own stderr or in its own envelope (<see cref="Domain.Gate.ReviewerAccountOut.CliReason"/>) hands the
/// attempt back <see cref="ProbeReason.AccountOut"/> — D8, a quota stop is not a measurement — with the artefacts kept.</item>
/// <item>The answer and the tool trace (<c>tools.json</c>: offered, used, stopped, denied, unknown — WHICH tool reached a file) are
/// committed beside the raw files; every artefact is referenced by hash, so a verdict is auditable from disk the moment it exists.</item>
/// <item>The fixture is deleted in <c>finally</c>, whatever happened.</item>
/// </list>
/// The <c>read-inside</c> control's voiding of the subject's read probes (<see cref="ProbeVerdicts.UnderControl"/>) is applied
/// where the subject's verdicts are read together — the report — not here, where one cell is measured on its own.</summary>
public sealed class CliProbeRunner(
    ICliAgentTranscripts agents, IProbeFixtures fixtures, IProbeArtifacts artifacts, CliProbeSettings settings, IProbeAttemptReader reader, ILogger<CliProbeRunner> logger) : IProbeRunner
{
    private readonly ProbeAttemptCommits _commits = new(artifacts, logger);
    private readonly ChildEnvironment _child = ProbeChildEnvironment.Of(settings.ParentEnvironment);

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
            return ProbeAttemptCommits.Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(notPrepared.Reason)), []);
        }

        var (scope, kind, executable, fixture) = ((Outcome<Prepared>.Ok)prepared).Value;

        try
        {
            var prompt = ProbeLaunch.Prompt(claimed.Probe, fixture);
            var ask = new AgentAsk(kind, executable, prompt, fixture.Cwd, settings.Wall, subject.ModelId)
            {
                Options = ProbeLaunch.OptionsFor(claimed.Probe, subject.Runtime, subject.Confinement, fixture),
                Environment = AgentEnvironment.Only(_child.Variables),
            };

            return await (await agents.TranscriptAsync(ask, cancellationToken)).Match(
                transcript => MeasureAsync(run, subject, claimed, scope, fixture, prompt, Scrubbed(transcript), cancellationToken),
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
        return ProbeAttemptCommits.Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(reason)), []);
    }

    /// <summary>Both pipes scrubbed of every secret-named value of the bench's environment BEFORE they are read or written (S2c): the
    /// answer and the tools are read off the scrubbed stdout, so nothing downstream can see a value the child never had.</summary>
    private AgentTranscript Scrubbed(AgentTranscript transcript) =>
        transcript with { Stdout = _child.Scrub(transcript.Stdout), Stderr = _child.Scrub(transcript.Stderr) };

    private async Task<ProbeAttemptResult> MeasureAsync(
        ProbeRun run, ProbeSubject subject, ProbeCell claimed, ProbeAttemptScope scope, ProbeFixture fixture, string prompt, AgentTranscript transcript, CancellationToken cancellationToken)
    {
        // The raw evidence FIRST — whatever the readers then make of it; refused, the attempt is handed back (finding 6).
        var raw = await _commits.CommitAsync(scope, cancellationToken,
            (ProbeArtifactKind.Stdout, transcript.Stdout), (ProbeArtifactKind.Stderr, transcript.Stderr),
            (ProbeArtifactKind.Argv, ProbeAttemptCommits.LaunchJson(transcript.Argv, _child.Names)), (ProbeArtifactKind.Prompt, prompt));

        return await raw.Match(
            files => ReadAsync(run, subject, claimed, scope, fixture, transcript, files, cancellationToken),
            reason => Task.FromResult(_commits.NotCommitted(claimed, reason)));
    }

    /// <summary>The one place a reader bug is allowed to land: as a value this runner settles FAILED on, with the raw transcript already
    /// on disk. Two live codex cells faulted this way (a duplicate JSON key) and left no artefact at all.</summary>
    private Task<ProbeAttemptResult> ReadAsync(
        ProbeRun run, ProbeSubject subject, ProbeCell claimed, ProbeAttemptScope scope, ProbeFixture fixture, AgentTranscript transcript, IReadOnlyList<ProbeArtifact> raw, CancellationToken cancellationToken) =>
        ProbeAttemptCommits.Try(() => reader.Read(run, subject, claimed.Probe, fixture.Tokens, transcript)).Match(
            reading => SettleReadAsync(subject, claimed, scope, reading, raw, cancellationToken),
            fault => _commits.FaultedAsync(claimed, scope, fault, transcript.ExitCode, raw, cancellationToken));

    private async Task<ProbeAttemptResult> SettleReadAsync(ProbeSubject subject, ProbeCell claimed, ProbeAttemptScope scope, ProbeAttemptReading reading, IReadOnlyList<ProbeArtifact> raw, CancellationToken cancellationToken)
    {
        var extracted = await _commits.CommitAsync(scope, cancellationToken, (ProbeArtifactKind.Answer, reading.Answer), (ProbeArtifactKind.Tools, reading.Trace.ToJson(subject.Runtime)));

        if (extracted is Outcome<IReadOnlyList<ProbeArtifact>>.Fail notCommitted)
        {
            return _commits.NotCommitted(claimed, notCommitted.Reason);
        }

        if (reading.QuotaLine.Length > 0)
        {
            logger.LogWarning("Probe cell {Cell}: the subject's account is out — {Line}", claimed.Id, reading.QuotaLine);
            return new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut);
        }

        return ProbeAttemptCommits.Settled(reading.Facts, [.. raw, .. ((Outcome<IReadOnlyList<ProbeArtifact>>.Ok)extracted).Value]);
    }
}
