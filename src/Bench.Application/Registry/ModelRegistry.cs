using Bench.Domain;
using Bench.Domain.Models;
using Bench.Domain.Registry;
using Bench.Domain.Runs;

namespace Bench.Application.Registry;

/// <summary>The list every role draws from: a test's subjects, its ordered arbiters, and later its
/// question authors and reviewers.
/// <para>
/// There is no <c>DeleteAsync</c>. A model is DISABLED, never removed, because runs name the key they
/// measured under and a deleted row would make a finished test's subject unreadable — the same rule the
/// variant catalog enforces by retiring rather than editing.
/// </para></summary>
public interface IModelRegistry
{
    Task<Outcome<RegisteredModel>> AddAsync(RegisteredModel model, CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<RegisteredModel>>> ListAsync(bool includeDisabled, CancellationToken cancellationToken);

    Task<Outcome<RegisteredModel>> FindAsync(string key, CancellationToken cancellationToken);

    Task<Outcome<RegisteredModel>> SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken);
}

/// <summary>Who answers and who judges, per test. Written when the test is created; add-only for subjects,
/// because removing one would leave its settled cells naming a subject the test no longer admits.</summary>
public interface IRunRoleStore
{
    Task<Outcome<int>> SaveSubjectsAsync(
        Guid runId, IReadOnlyList<ModelKey> subjects, DateTimeOffset at, CancellationToken cancellationToken);

    Task<Outcome<int>> SaveJudgesAsync(
        Guid runId, IReadOnlyList<ModelKey> judges, DateTimeOffset at, CancellationToken cancellationToken);

    Task<Outcome<IReadOnlyList<RunRole>>> SubjectsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The test's arbiters, IN ORDER. The first is the primary, and an unordered answer here would
    /// make that word mean nothing.</summary>
    Task<Outcome<IReadOnlyList<RunRole>>> JudgesAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>Where a stored REFERENCE is turned into the value it names, on this machine, at use.
/// <para>
/// A port rather than a call to <see cref="Environment"/> so that a test can resolve without setting
/// process-wide state, and so the one place secrets enter the process is nameable.
/// </para></summary>
public interface ISecretSource
{
    /// <summary>The value behind a reference, or a refusal naming the reference that did not resolve —
    /// never an empty string, which would travel on to be refused as "no base url" three layers away.</summary>
    Outcome<string> Resolve(string reference);
}

/// <summary>Turning a registry row into something a leg can actually be run against.
/// <para>
/// This is where "the reference does not resolve on THIS machine" is discovered — at test creation, by
/// name, in the same breath as a disabled model is refused. The alternative is discovering it three hours
/// into a sweep as a wall of identical transport failures.
/// </para></summary>
public static class ModelResolution
{
    public static Outcome<Subject> AsSubject(RegisteredModel model) =>
        model.AsRef().Match(
            reference => Outcome<Subject>.Success(new Subject(reference, model.Config.Sampling)),
            Outcome<Subject>.Failure);

    public static Outcome<ModelEndpoint> Endpoint(RegisteredModel model, ISecretSource secrets)
    {
        if (!model.Enabled)
        {
            return Outcome<ModelEndpoint>.Failure(
                $"model '{model.Key}' is disabled — a disabled model is refused when the test is created, by name");
        }

        return model.Runtime is ModelRuntimeKind.OpenAiEndpoint
            ? Resolve(model, secrets)
            : Outcome<ModelEndpoint>.Failure(
                $"model '{model.Key}' runs as {model.Runtime.ToString().ToLowerInvariant()}, and this build has no runtime for it "
                + "— the agent lane is owned by the tool benchmark plan");
    }

    /// <summary>The executable a CLI agent is launched as, resolved on THIS machine.
    /// <para>
    /// The counterpart of <see cref="Endpoint"/> for the other kind of runtime, and it refuses the same three
    /// things by name at the same moment: a disabled row, a runtime that is not a CLI at all, and a reference
    /// that resolves to nothing here. Discovering any of them at question forty of a batch is discovering it
    /// three hundred launches too late.
    /// </para>
    /// <para>
    /// This does NOT make a CLI model resolvable as a measurement SUBJECT — <see cref="AsSubject"/> and
    /// <see cref="Endpoint"/> still refuse it, because measuring an agent needs turn ceilings and telemetry
    /// that belong to the tool-benchmark plan. It makes it usable as a WORKER for the harness, which is a
    /// different role with a different port.
    /// </para></summary>
    public static Outcome<string> Executable(RegisteredModel model, ISecretSource secrets)
    {
        if (!model.Enabled)
        {
            return Outcome<string>.Failure(
                $"model '{model.Key}' is disabled — a disabled model is refused when the batch is created, by name");
        }

        if (model.Runtime is ModelRuntimeKind.OpenAiEndpoint or ModelRuntimeKind.BridgeLocal)
        {
            return Outcome<string>.Failure(
                $"model '{model.Key}' runs as {model.Runtime.ToString().ToLowerInvariant()} — it is answered over "
                + "HTTP, and launching it as a process would launch nothing");
        }

        return model.Config.ExecutableRef.Length == 0
            ? Outcome<string>.Failure(
                $"model '{model.Key}' names no executable reference — a CLI agent with no path is a refusal, not a "
                + "guess at whatever is on PATH")
            : secrets.Resolve(model.Config.ExecutableRef).Match(
                Outcome<string>.Success,
                reason => Outcome<string>.Failure($"model '{model.Key}': {reason}"));
    }

    private static Outcome<ModelEndpoint> Resolve(RegisteredModel model, ISecretSource secrets)
    {
        if (model.Config.BaseUrlRef.Length == 0)
        {
            return Outcome<ModelEndpoint>.Failure(
                $"model '{model.Key}' names no endpoint reference — an unset endpoint is a refusal, exactly like an unset model id");
        }

        return secrets.Resolve(model.Config.BaseUrlRef).Match(
            baseUrl => model.AsRef().Match(
                reference => ModelEndpoint.Parse(
                    reference, baseUrl, model.Config.InputCostPerMTok, model.Config.OutputCostPerMTok),
                Outcome<ModelEndpoint>.Failure),
            reason => Outcome<ModelEndpoint>.Failure($"model '{model.Key}': {reason}"));
    }
}
