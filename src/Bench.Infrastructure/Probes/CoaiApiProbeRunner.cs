using System.Globalization;
using System.Text;
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
/// <param name="ParentEnvironment">The harness's own environment; every <c>COAI_*</c> is dropped from it before the launch.</param>
/// <param name="TimeoutSeconds">The product's own ceiling per vendor call (<c>--timeout-seconds</c>).</param>
public sealed record CoaiApiProbeSettings(string ProductExecutable, IReadOnlyDictionary<string, string> ParentEnvironment, TimeSpan Wall, int TimeoutSeconds = 60);

/// <summary>The api subject's attempt (D6): <c>coai-mcp --probe-api --vendor &lt;id&gt; --model &lt;id&gt; --endpoint &lt;base&gt;
/// --dialect &lt;name&gt; --timeout-seconds &lt;n&gt;</c>, through the one launcher, under the environment
/// <see cref="CoaiEnvironment.Bare"/> builds — the parent's minus every <c>COAI_*</c>, the vault key joined LAST under
/// <c>COAI_CREDS_KEY</c> and scrubbed from every text written. The bench never reads a vendor key: the product reads the vault.
/// <list type="bullet">
/// <item>No key on this machine, or the product's "no vault / no key" exit (78): nothing about the vendor can be measured now —
/// the attempt is handed back unmeasured and the subject is benched; a resume with the key in place measures it.</item>
/// <item>A refused KEY at the vendor (401/402/403, or a spent-account marker) IS the measurement: <c>accountOut = yes</c>,
/// <c>reachable = no</c> — that is what Q5 asks.</item>
/// <item>Exit 65 (bad arguments) settles <i>launch refused</i>; the wall settles <i>timed out</i>; any other non-zero exit <i>failed</i>.</item>
/// </list></summary>
public sealed class CoaiApiProbeRunner(IProbeArtifacts artifacts, IProbeSecrets secrets, CoaiApiProbeSettings settings, ILogger<CoaiApiProbeRunner> logger) : IProbeRunner
{
    public async Task<ProbeAttemptResult> RunAsync(ProbeRun run, ProbeSubject subject, ProbeCell claimed, CancellationToken cancellationToken)
    {
        var scope = ProbeAttemptScope.Of(claimed);

        if (scope is Outcome<ProbeAttemptScope>.Fail notClaimed)
        {
            logger.LogWarning("Probe cell {Cell} could not be prepared and settles failed: {Reason}", claimed.Id, notClaimed.Reason);
            return Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(notClaimed.Reason)), []);
        }

        var key = secrets.CredsKey();

        if (key is Outcome<SecretValue>.Fail noKey)
        {
            logger.LogWarning("Probe cell {Cell}: the vault key is unavailable — {Reason}; the api subject is benched", claimed.Id, noKey.Reason);
            return new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut);
        }

        var child = CoaiEnvironment.Bare(settings.ParentEnvironment).WithSecret(((Outcome<SecretValue>.Ok)key).Value);
        var attempt = await ProcessRunner.RunAsync(
            settings.ProductExecutable, Argv(subject), Path.GetDirectoryName(Path.GetFullPath(settings.ProductExecutable)) ?? ".", settings.Wall, string.Empty, child.Variables, cancellationToken);

        return await MeasureAsync(((Outcome<ProbeAttemptScope>.Ok)scope).Value, claimed, attempt, child, cancellationToken);
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

    private async Task<ProbeAttemptResult> MeasureAsync(ProbeAttemptScope scope, ProbeCell claimed, ProcessAttempt attempt, ChildEnvironment child, CancellationToken cancellationToken)
    {
        if (attempt is ProcessAttempt.NotFound missing)
        {
            logger.LogWarning("Probe cell {Cell}: {Reason}", claimed.Id, missing.Describe);
            return Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, CapturedCount.Unavailable(missing.Describe)), []);
        }

        // Scrubbed BEFORE anything reads or writes it: the product can echo its environment anywhere.
        var (exit, stdout, stderr) = attempt switch
        {
            ProcessAttempt.Completed c => (CapturedCount.Number(c.Result.ExitCode), child.Scrub(c.Result.StandardOutput), child.Scrub(c.Result.StandardError)),
            ProcessAttempt.TimedOut t => (CapturedCount.Unavailable($"the wall of {t.Budget.TotalSeconds:0.#}s ended the process"), child.Scrub(t.StandardOutput), child.Scrub(t.StandardError)),
            _ => throw new InvalidOperationException("unreachable"),
        };

        var committed = await CommitAsync(scope, stdout, stderr, cancellationToken);

        if (exit is { WasCaptured: true, Value: ProbeExits.CoaiNoVaultExit })
        {
            logger.LogWarning("Probe cell {Cell}: the product found no vault or no key (exit {Exit}); the api subject is benched", claimed.Id, ProbeExits.CoaiNoVaultExit);
            return new ProbeAttemptResult.Unmeasured(ProbeReason.AccountOut);
        }

        return Settled(Facts(exit, stdout, stderr), committed);
    }

    private static ProbeFacts Facts(CapturedCount exit, string stdout, string stderr)
    {
        var kind = exit.WasCaptured ? ProbeExits.Classify(ProbeRuntime.Api, (int)exit.Value, stderr) : ProbeAttemptKind.TimedOut;
        var statuses = ProbeApiOutput.Statuses(stdout);

        return kind switch
        {
            ProbeAttemptKind.Answered or ProbeAttemptKind.Failed => ProbeVerdicts.ApiReachable((int)exit.Value, statuses, statuses.Count > 0, ProbeApiOutput.KeyRefused(statuses, stdout + "\n" + stderr)) with { Kind = kind },
            _ => ProbeFacts.NothingCaptured(kind, exit),
        };
    }

    private async Task<IReadOnlyList<ProbeArtifact>> CommitAsync(ProbeAttemptScope scope, string stdout, string stderr, CancellationToken cancellationToken)
    {
        var committed = new List<ProbeArtifact>();

        foreach (var (kind, text) in new[] { (ProbeArtifactKind.Stdout, stdout), (ProbeArtifactKind.Stderr, stderr) })
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

/// <summary>The vault key from the machine's coai <c>settings.json</c> — <see cref="CoaiSettingsSecrets"/>, the gate's reading,
/// as the probes' <see cref="IProbeSecrets"/>.</summary>
public sealed class CoaiSettingsProbeSecrets(string settingsFile) : IProbeSecrets
{
    public Outcome<SecretValue> CredsKey() => CoaiSettingsSecrets.CredsKey(settingsFile);
}
