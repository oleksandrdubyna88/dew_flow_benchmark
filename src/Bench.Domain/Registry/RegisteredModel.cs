using Bench.Domain.Runs;

namespace Bench.Domain.Registry;

/// <summary>A model's key in the registry — <c>claude-opus</c>, <c>local-qwen32</c>.
/// <para>
/// The identity every role draws on: a test's subjects, its ordered arbiters, and later its question
/// authors and reviewers all name a model by this key. One shape, shared with the variant and bank keys,
/// so a page listing all three cannot show two spellings of one thing.
/// </para></summary>
public sealed record ModelKey
{
    private ModelKey(string value) => Value = value;

    public string Value { get; }

    public static Outcome<ModelKey> Parse(string? value)
    {
        var trimmed = Slug.Clean(value);

        return Slug.IsValid(trimmed)
            ? Outcome<ModelKey>.Success(new ModelKey(trimmed))
            : Outcome<ModelKey>.Failure($"'{trimmed}' is not a usable model key — {Slug.Rule}");
    }

    public override string ToString() => Value;
}

/// <summary>One row of the model registry: an identity, how it is reached, and whether it may be chosen.
/// <para>
/// <b>Models are configuration, never constants.</b> A test picks its subjects and its arbiters from these
/// rows and stores the choice on itself, so the registry can change afterwards without rewriting history —
/// and a model that is disabled, or whose reference does not resolve on this machine, is refused when the
/// test is created rather than three hours into a sweep.
/// </para></summary>
public sealed record RegisteredModel(
    Guid Id,
    ModelKey Key,
    string DisplayName,
    ModelRuntimeKind Runtime,
    ModelHosting Hosting,
    ModelConfig Config,
    bool Enabled,
    DateTimeOffset CreatedAt)
{
    public static Outcome<RegisteredModel> Create(
        string? key,
        string? displayName,
        ModelRuntimeKind runtime,
        ModelHosting hosting,
        ModelConfig config,
        DateTimeOffset now) =>
        ModelKey.Parse(key).Match(
            parsed => Outcome<RegisteredModel>.Success(new RegisteredModel(
                Guid.CreateVersion7(), parsed, Named(displayName, parsed), runtime, hosting, config, true, now)),
            Outcome<RegisteredModel>.Failure);

    /// <summary>Rebuilds a stored row, re-parsing its key rather than trusting it: a row edited by hand is
    /// a real event, and the registry should refuse it by name instead of serving it into a run.</summary>
    public static Outcome<RegisteredModel> Rehydrate(
        Guid id,
        string? key,
        string? displayName,
        ModelRuntimeKind runtime,
        ModelHosting hosting,
        ModelConfig config,
        bool enabled,
        DateTimeOffset createdAt) =>
        ModelKey.Parse(key).Match(
            parsed => Outcome<RegisteredModel>.Success(new RegisteredModel(
                id, parsed, Named(displayName, parsed), runtime, hosting, config, enabled, createdAt)),
            Outcome<RegisteredModel>.Failure);

    /// <summary>The model as an axis value. Hosting travels with it because a billable subject must stay
    /// legible as one everywhere — a cloud row and a local row are not interchangeable numbers.</summary>
    public Outcome<ModelRef> AsRef() => ModelRef.Parse(Config.ModelId, Hosting);

    public RegisteredModel Enable(bool enabled) => this with { Enabled = enabled };

    public string Stamp => $"{Key.Value} · {Runtime.ToString().ToLowerInvariant()} · {Config.ModelId}";

    private static string Named(string? displayName, ModelKey key)
    {
        var trimmed = (displayName ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : key.Value;
    }
}

/// <summary>What a test decided: who answers, and who judges, in what order.
/// <para>
/// Stored on the RUN rather than read live from the registry, so a registry edit next month cannot change
/// what a finished test says it measured — the same reason the suite is frozen and the variant is never
/// edited.
/// </para></summary>
/// <param name="Ordinal">Arbiters are ordered: the first is the primary. A set with no order would make
/// "the primary arbiter disagreed" a sentence nobody could evaluate.</param>
public sealed record RunRole(Guid RunId, ModelKey Model, int Ordinal, DateTimeOffset AddedAt);
