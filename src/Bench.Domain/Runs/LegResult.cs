using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Trace;

namespace Bench.Domain.Runs;

public enum MetricKind
{
    Numeric,
    Boolean,
    Text,
}

/// <summary>One scored metric, in OUR shape.
/// <para>
/// The adopted library's own serialisation is internal, so its stored object graph cannot be persisted
/// by anyone but its own disk store. That turned out to be the better outcome: we keep the metric MODEL
/// and the report writer, and own the storage — which is what makes "group by engine" a query instead of
/// a full scan with string parsing.
/// </para>
/// <para>
/// One value slot, not three, with <see cref="Kind"/> saying how to read it. The same shape as
/// <see cref="LegOutcomeCodec"/>: a discriminator plus a canonical text, so adding a metric kind breaks
/// at a compiler error rather than silently storing a new thing as an old one.
/// </para></summary>
public sealed record StoredMetric(
    string Name,
    MetricKind Kind,
    string Value,
    string Reason,
    bool Failed,
    string Rating,
    IReadOnlyDictionary<string, string> Metadata)
{
    public static StoredMetric Numeric(string name, double value, string reason, bool failed, string rating) =>
        new(name, MetricKind.Numeric, value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            reason, failed, rating, new Dictionary<string, string>());

    public static StoredMetric Boolean(string name, bool value, string reason, bool failed, string rating) =>
        new(name, MetricKind.Boolean, value ? "true" : "false", reason, failed, rating, new Dictionary<string, string>());

    public static StoredMetric Text(string name, string value, string reason, bool failed, string rating) =>
        new(name, MetricKind.Text, value, reason, failed, rating, new Dictionary<string, string>());

    public StoredMetric With(IReadOnlyDictionary<string, string> metadata) => this with { Metadata = metadata };

    /// <summary>The numeric reading, for a metric that has one. A boolean counts as 1 or 0 because that
    /// is what an aggregate over a mixed set has to do; anything else reads as "no number here", which is
    /// a different fact from zero.</summary>
    public Outcome<double> AsNumber() =>
        Kind switch
        {
            MetricKind.Numeric when double.TryParse(Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) => Outcome<double>.Success(parsed),
            MetricKind.Boolean => Outcome<double>.Success(Value == "true" ? 1 : 0),
            MetricKind.Numeric => Outcome<double>.Failure($"metric '{Name}' claims to be numeric but holds '{Value}'"),
            _ => Outcome<double>.Failure($"metric '{Name}' is {Kind} and has no numeric reading"),
        };
}

/// <summary>What one leg produced: what it was asked, what it answered, and how that scored.
/// <para>
/// The answer is stored because re-judging must never require re-running the leg — the expensive artefact
/// is the subject's output, and a second arbiter should cost nothing but its own inference.
/// </para></summary>
public sealed record LegResult(
    Guid Id,
    Guid CellId,
    string Prompt,
    string Answer,
    IReadOnlyList<StoredMetric> Metrics,
    DateTimeOffset CreatedAt)
{
    public static LegResult Of(Guid cellId, string prompt, string answer, IReadOnlyList<StoredMetric> metrics, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), cellId, prompt, answer, metrics, now);

    /// <summary>The model's own reasoning, when its runtime returned any. Kept forever with the prompt and
    /// the answer: this trio IS the artefact, and a published number that cannot be re-read against the text
    /// that produced it is a number nobody can check.</summary>
    public Captured Thinking { get; init; } = Captured.Unavailable("the runtime returned no separate reasoning");

    /// <summary>Tokens, latency, stop reason and the sampling as SENT. Unrecoverable after the fact, since a
    /// re-run is a different call.</summary>
    public ResponseMeta Meta { get; init; } = ResponseMeta.None;

    /// <summary>What retrieval surfaced for this leg — the funnel, the hits and the axes that served it.
    /// <see cref="RetrievedContext.NotPerformed"/> for the control arm, which is a statement about the arm
    /// rather than an empty result.</summary>
    public RetrievedContext Retrieval { get; init; } = RetrievedContext.NotPerformed;

    /// <summary>What the machine was doing while this leg ran.
    /// <para>
    /// <c>NotSampled</c> by default, which is the truth for every leg measured before a sampler existed and
    /// for every host with nothing to read. It is deliberately not zeroes: a leg on a machine nobody watched
    /// and a leg on an idle one are opposite readings of the same digits.
    /// </para></summary>
    public LegLoad Load { get; init; } = LegLoad.NotSampled("no sampler was running for this leg");

    public bool Passed => Metrics.Count > 0 && Metrics.All(m => !m.Failed);
}
