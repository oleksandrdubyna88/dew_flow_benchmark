namespace Bench.Domain.Runs;

public enum ModelHosting
{
    Local,
    Cloud,
}

/// <summary>The model under test. There is no default and there cannot be one.
/// <para>
/// This type exists because of a specific near-miss upstream: a reranker row whose model id was left
/// empty did not fall back to "the local model" — it resolved to the installation's SYSTEM DEFAULT,
/// a paid cloud model, and an arm labelled "local, $0" was one run away from sending ~100 requests to
/// a billed API. An unset id is therefore a refusal here, enforced by the type rather than by a
/// convention somebody has to remember.
/// </para></summary>
public sealed record ModelRef
{
    private ModelRef(string id, ModelHosting hosting)
    {
        Id = id;
        Hosting = hosting;
    }

    public string Id { get; }

    public ModelHosting Hosting { get; }

    public bool IsBillable => Hosting == ModelHosting.Cloud;

    public static Outcome<ModelRef> Parse(string? id, ModelHosting hosting)
    {
        var trimmed = (id ?? string.Empty).Trim();

        return trimmed.Length == 0
            ? Outcome<ModelRef>.Failure(
                "the model id is unset — that is a refusal, never a fallback to a default. Name the model explicitly")
            : Outcome<ModelRef>.Success(new ModelRef(trimmed, hosting));
    }

    public override string ToString() => $"{Id}({Hosting.ToString().ToLowerInvariant()})";
}

/// <summary>The sampling a leg asks for. Determinism is a precondition of repeats meaning anything.</summary>
public sealed record Sampling(double Temperature, int Seed)
{
    public static Sampling Deterministic(int seed) => new(0.0, seed);

    public string Canonical => $"t={Temperature.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},s={Seed}";
}

/// <summary>What the request actually carried — a separate type from <see cref="Sampling"/> on purpose.
/// <para>
/// A temperature read back from a settings page is not evidence that it was applied: at least one
/// runtime's OpenAI-compatible route substitutes its own defaults over the model file, which makes
/// every "deterministic" measurement through it unreproducible while looking configured. Only a value
/// captured from the outgoing request counts, and <see cref="NotCaptured"/> is deliberately
/// distinguishable from a captured zero.
/// </para></summary>
public sealed record SamplingAsSent(bool Captured, double Temperature, int Seed, string Source)
{
    public static SamplingAsSent NotCaptured(string why) => new(false, 0, 0, why);

    public static SamplingAsSent From(Sampling sampling, string source) =>
        new(true, sampling.Temperature, sampling.Seed, source);

    public bool Matches(Sampling requested) =>
        Captured && Temperature == requested.Temperature && Seed == requested.Seed;
}

/// <summary>One model under test, with the sampling its legs will request.</summary>
public sealed record Subject(ModelRef Model, Sampling Sampling)
{
    public string Canonical => $"{Model.Id}|{Sampling.Canonical}";
}

/// <summary>A tool surface and the preamble that describes it to the model. Lanes are data, not flags:
/// the upstream system encoded four of them as four boolean columns, so a fifth was a migration.</summary>
public sealed record Lane(string Name, string Preamble)
{
    public static Lane Named(string name) => new(name, string.Empty);

    public string Canonical => Name;
}

public enum EngineKind
{
    /// <summary>Our own retrieval stack. The only kind that can be white-box.</summary>
    Qln,

    Mindex,

    /// <summary>Any third-party retrieval service reachable over HTTP.</summary>
    Http,

    /// <summary>No retrieval at all — plain filesystem tools over the checkout. A first-class engine,
    /// because "tools against no tools" is the comparison the whole exercise exists to keep asking,
    /// and the measured answer has been uncomfortably close more than once.</summary>
    NoRetrieval,
}

/// <summary>The engine under measurement, and the fingerprint of whatever index it is serving from.
/// An empty fingerprint means unknown — never "current", and never a default.</summary>
public sealed record EngineRef(EngineKind Kind, string Endpoint, string Version, string IndexFingerprint)
{
    public static EngineRef Filesystem() => new(EngineKind.NoRetrieval, string.Empty, string.Empty, string.Empty);

    public bool MayBeWhiteBox => Kind == EngineKind.Qln;

    public string Canonical => $"{Kind}|{Endpoint}|{Version}|{IndexFingerprint}";
}
