using System.Text;
using System.Text.Json.Nodes;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Domain.Trace;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Probes;

/// <summary>What the two probe runners share (S2c, review finding 8 — extracted from <see cref="CliProbeRunner"/> and
/// <see cref="CoaiApiProbeRunner"/>, which had each grown their own copy): committing an attempt's files, the launch record, the
/// settle and fault shapes, and the reading-as-a-value wrapper.
/// <list type="bullet">
/// <item><b>A commit is all or nothing</b> (finding 6): the first file the artefact root refuses stops the batch, and the caller hands the
/// attempt back <see cref="ProbeReason.ArtifactsNotCommitted"/> — a verdict nobody can audit from disk is not recorded, the cell waits
/// for a resume, and the subject is benched until the root is writable. Before S2c a refused commit was only logged and the cell
/// settled without its stdout.</item>
/// <item><b>The launch record</b> (<c>argv.json</c>, finding 3) is one object: the exact argv and the NAMES of the child's environment —
/// never a value.</item>
/// </list></summary>
public sealed class ProbeAttemptCommits(IProbeArtifacts artifacts, ILogger logger)
{
    /// <summary><c>argv.json</c>: <c>{"argv":[…],"environment":[…names…]}</c>. The caller scrubs it like every other text when the launch
    /// could have spelled a secret into it.</summary>
    public static string LaunchJson(IReadOnlyList<string> argv, IReadOnlyList<string> environmentNames) =>
        new JsonObject
        {
            ["argv"] = new JsonArray([.. argv.Select(a => (JsonNode)JsonValue.Create(a))]),
            ["environment"] = new JsonArray([.. environmentNames.Select(n => (JsonNode)JsonValue.Create(n))]),
        }.ToJsonString() + "\n";

    /// <summary>Commits <paramref name="files"/> in order and stops at the first refusal, naming the kind — the files already written
    /// stay (an artefact is never deleted), the attempt is the caller's to hand back.</summary>
    public async Task<Outcome<IReadOnlyList<ProbeArtifact>>> CommitAsync(ProbeAttemptScope scope, CancellationToken cancellationToken, params (ProbeArtifactKind Kind, string Text)[] files)
    {
        var committed = new List<ProbeArtifact>();

        foreach (var (kind, text) in files)
        {
            var commit = await artifacts.CommitAsync(scope, kind, Encoding.UTF8.GetBytes(text), cancellationToken);

            if (commit is Outcome<ProbeArtifact>.Fail refused)
            {
                return Outcome<IReadOnlyList<ProbeArtifact>>.Failure($"artefact {kind} of {scope} was not committed — {refused.Reason}");
            }

            committed.Add(((Outcome<ProbeArtifact>.Ok)commit).Value);
        }

        return Outcome<IReadOnlyList<ProbeArtifact>>.Success(committed);
    }

    /// <summary>Finding 6: evidence that is not on disk is a verdict nobody can audit — handed back unmeasured, the subject benched.</summary>
    public ProbeAttemptResult NotCommitted(ProbeCell claimed, string reason)
    {
        logger.LogError(
            "Probe cell {Cell}: {Reason}. The attempt is handed back unmeasured — a verdict nobody can audit from disk is not recorded — and the subject is benched until the artefact root is writable",
            claimed.Id, reason);

        return new ProbeAttemptResult.Unmeasured(ProbeReason.ArtifactsNotCommitted);
    }

    public static ProbeAttemptResult Settled(ProbeFacts facts, IReadOnlyList<ProbeArtifact> artifacts) =>
        new ProbeAttemptResult.Settled(new ProbeSettlement(facts, artifacts));

    /// <summary>A reader that threw (S2b, finding 6): <c>fault.txt</c> beside the raw evidence, the attempt settled <i>failed</i> with every
    /// fact <i>not captured</i> and the exit code kept — a parse fault is a fact about our reader, never a leg fault.</summary>
    public async Task<ProbeAttemptResult> FaultedAsync(ProbeCell claimed, ProbeAttemptScope scope, string reason, CapturedCount exit, IReadOnlyList<ProbeArtifact> raw, CancellationToken cancellationToken)
    {
        logger.LogError("Probe cell {Cell}: the reader faulted on this transcript — {Reason}. The attempt settles failed with every fact not captured; the raw evidence is kept to fix the reader against", claimed.Id, reason);

        return (await CommitAsync(scope, cancellationToken, (ProbeArtifactKind.Fault, reason + "\n"))).Match(
            fault => Settled(ProbeFacts.NothingCaptured(ProbeAttemptKind.Failed, exit), [.. raw, .. fault]),
            notCommitted => NotCommitted(claimed, notCommitted));
    }

    /// <summary>The reading as a VALUE (S2b, finding 6): a reader bug is an exception, and this is the one place it becomes an
    /// <see cref="Outcome{T}"/> the runner settles on, with the raw evidence already on disk.</summary>
    public static Outcome<T> Try<T>(Func<T> read)
    {
        try
        {
            return Outcome<T>.Success(read());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Outcome<T>.Failure($"{ex.GetType().FullName}: {ex.Message}");
        }
    }
}
