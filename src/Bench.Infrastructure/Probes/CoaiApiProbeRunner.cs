using System.Globalization;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Trace;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Probes;

/// <param name="ProductExecutable">The <c>coai-mcp</c> binary the api subject's reference resolved to.</param>
/// <param name="ParentEnvironment">The harness's own environment; every <c>COAI_*</c>, every <c>BENCH_*</c> and every secret-named
/// variable is dropped from it before the launch (S2c).</param>
/// <param name="TimeoutSeconds">The product's own ceiling per vendor call (<c>--timeout-seconds</c>).</param>
public sealed record CoaiApiProbeSettings(string ProductExecutable, IReadOnlyDictionary<string, string> ParentEnvironment, TimeSpan Wall, int TimeoutSeconds = 60);

/// <summary>The api subject's attempt (D6): <c>coai-mcp --probe-api --vendor &lt;id&gt; --model &lt;id&gt; --endpoint &lt;base&gt;
/// --dialect &lt;name&gt; --timeout-seconds &lt;n&gt;</c>, through the one launcher, under the environment
/// <see cref="CoaiEnvironment.Bare"/> builds — the parent's minus every <c>COAI_*</c>, <c>BENCH_*</c> and secret-named variable
/// (S2c), the vault key joined LAST under <c>COAI_CREDS_KEY</c> and scrubbed from every text written. The bench never reads a
/// vendor key: the product reads the vault.
/// <list type="bullet">
/// <item>No key on this machine, or the product's "no vault / no key" exit (78): nothing about the vendor can be measured now —
/// the attempt is handed back unmeasured and the subject is benched; a resume with the key in place measures it.</item>
/// <item>A refused KEY at the vendor (401/402/403, or the credits / spending-limit wording) IS the measurement: <c>accountOut = yes</c>
/// (<see cref="ProbeApiOutput"/>, the JSON report coai-mcp 0.40.3 prints — S2b, finding 4) — that is what Q5 asks.</item>
/// <item>Exit 65 (bad arguments) settles <i>launch refused</i>; the wall settles <i>timed out</i>; any other non-zero exit <i>failed</i>.</item>
/// <item>The raw stdout, stderr and the scrubbed launch record are committed BEFORE the report is read (S2b, finding 6); a reader that
/// throws settles <i>failed</i> with every fact <i>not captured</i> and a <c>fault.txt</c>; evidence the artefact root refuses hands the
/// attempt back unmeasured (S2c, finding 6).</item>
/// </list></summary>
public sealed class CoaiApiProbeRunner(IProbeArtifacts artifacts, IProbeSecrets secrets, CoaiApiProbeSettings settings, ILogger<CoaiApiProbeRunner> logger) : IProbeRunner
{
    private readonly ProbeAttemptCommits _commits = new(artifacts, logger);

    public async Task<ProbeAttemptResult> RunAsync(ProbeRun run, ProbeSubject subject, ProbeCell claimed, CancellationToken cancellationToken)
    {
        var scope = ProbeAttemptScope.Of(claimed);

        if (scope is Outcome<ProbeAttemptScope>.Fail notClaimed)
        {
            logger.LogWarning("Probe cell {Cell} could not be prepared and settles failed: {Reason}", claimed.Id, notClaimed.Reason);
            return ProbeAttemptCommits.Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(notClaimed.Reason)), []);
        }

        var key = secrets.CredsKey();

        if (key is Outcome<SecretValue>.Fail noKey)
        {
            logger.LogWarning("Probe cell {Cell}: the vault key is unavailable — {Reason}; the api subject is benched", claimed.Id, noKey.Reason);
            return new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut);
        }

        var child = CoaiEnvironment.Bare(settings.ParentEnvironment).WithSecret(((Outcome<SecretValue>.Ok)key).Value);
        var argv = Argv(subject);
        var attempt = await ProcessRunner.RunAsync(
            settings.ProductExecutable, argv, Path.GetDirectoryName(Path.GetFullPath(settings.ProductExecutable)) ?? ".", settings.Wall, string.Empty, child.Variables, cancellationToken);

        return await MeasureAsync(((Outcome<ProbeAttemptScope>.Ok)scope).Value, claimed, argv, attempt, child, cancellationToken);
    }

    /// <summary>D6's argv, spelled once.</summary>
    public static IReadOnlyList<string> Argv(ProbeSubject subject, int timeoutSeconds = 60) =>
    [
        "--probe-api",
        "--vendor", subject.Vendor,
        "--model", subject.ModelId,
        "--endpoint", subject.Endpoint,
        "--dialect", subject.Dialect,
        "--timeout-seconds", timeoutSeconds.ToString(CultureInfo.InvariantCulture),
    ];

    private IReadOnlyList<string> Argv(ProbeSubject subject) => Argv(subject, settings.TimeoutSeconds);

    /// <summary>What the launch produced, scrubbed: the exit (not captured when the wall ended it) and both pipes.</summary>
    private sealed record ProductOutput(CapturedCount Exit, string Stdout, string Stderr);

    private async Task<ProbeAttemptResult> MeasureAsync(
        ProbeAttemptScope scope, ProbeCell claimed, IReadOnlyList<string> argv, ProcessAttempt attempt, ChildEnvironment child, CancellationToken cancellationToken)
    {
        if (attempt is ProcessAttempt.NotFound missing)
        {
            logger.LogWarning("Probe cell {Cell}: {Reason}", claimed.Id, missing.Describe);
            return ProbeAttemptCommits.Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(missing.Describe)), []);
        }

        var output = Output(attempt, child);

        // The raw evidence FIRST (S2b, finding 6) — the launch record scrubbed like every other text, in case a secret is ever spelled into it.
        var raw = await _commits.CommitAsync(scope, cancellationToken,
            (ProbeArtifactKind.Stdout, output.Stdout), (ProbeArtifactKind.Stderr, output.Stderr), (ProbeArtifactKind.Argv, child.Scrub(ProbeAttemptCommits.LaunchJson(argv, child.Names))));

        return await raw.Match(
            files => SettleAsync(scope, claimed, output, files, cancellationToken),
            reason => Task.FromResult(_commits.NotCommitted(claimed, reason)));
    }

    /// <summary>Scrubbed BEFORE anything reads or writes it: the product can echo its environment anywhere.</summary>
    private static ProductOutput Output(ProcessAttempt attempt, ChildEnvironment child) => attempt switch
    {
        ProcessAttempt.Completed c => new ProductOutput(CapturedCount.Number(c.Result.ExitCode), child.Scrub(c.Result.StandardOutput), child.Scrub(c.Result.StandardError)),
        ProcessAttempt.TimedOut t => new ProductOutput(CapturedCount.Unavailable($"the wall of {t.Budget.TotalSeconds:0.#}s ended the process"), child.Scrub(t.StandardOutput), child.Scrub(t.StandardError)),
        _ => throw new InvalidOperationException("unreachable"),
    };

    private Task<ProbeAttemptResult> SettleAsync(ProbeAttemptScope scope, ProbeCell claimed, ProductOutput output, IReadOnlyList<ProbeArtifact> raw, CancellationToken cancellationToken)
    {
        if (output.Exit is { WasCaptured: true, Value: ProbeExits.CoaiNoVaultExit })
        {
            logger.LogWarning("Probe cell {Cell}: the product found no vault or no key (exit {Exit}); the api subject is benched", claimed.Id, ProbeExits.CoaiNoVaultExit);
            return Task.FromResult<ProbeAttemptResult>(new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut));
        }

        // The reading as a value: a reader that throws is a fault of ours on this report, never a leg fault.
        return ProbeAttemptCommits.Try(() => Facts(output)).Match(
            facts => Task.FromResult(ProbeAttemptCommits.Settled(facts, raw)),
            fault => _commits.FaultedAsync(claimed, scope, fault, output.Exit, raw, cancellationToken));
    }

    private static ProbeFacts Facts(ProductOutput output)
    {
        var kind = output.Exit.WasCaptured ? ProbeExits.Classify(ProbeRuntime.Api, (int)output.Exit.Value, output.Stderr) : ProbeAttemptKind.TimedOut;

        return kind switch
        {
            ProbeAttemptKind.Answered or ProbeAttemptKind.Failed => ProbeVerdicts.ApiReachable((int)output.Exit.Value, ProbeApiOutput.Read(output.Stdout)) with { Kind = kind },
            _ => ProbeFacts.NothingCaptured(kind, output.Exit),
        };
    }
}

/// <summary>The vault key from the machine's coai <c>settings.json</c> — <see cref="CoaiSettingsSecrets"/>, the gate's reading,
/// as the probes' <see cref="IProbeSecrets"/>.</summary>
public sealed class CoaiSettingsProbeSecrets(string settingsFile) : IProbeSecrets
{
    public Outcome<SecretValue> CredsKey() => CoaiSettingsSecrets.CredsKey(settingsFile);
}
