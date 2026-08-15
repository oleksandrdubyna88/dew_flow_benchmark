using Bench.Domain;
using Bench.Domain.Runs;
using Microsoft.Extensions.AI.Evaluation;

namespace Bench.Application;

/// <summary>The boundary between the adopted library's metric model and our stored shape.
/// <para>
/// It lives in the Application layer because it is the only place that knows both — the Domain must not
/// learn about a package, and the Infrastructure must not learn about scoring. Both directions are here
/// and both are tested: encoding is how a run gets stored, decoding is how a stored run becomes a report
/// without re-running anything.
/// </para></summary>
public static class MetricCodec
{
    public static StoredMetric Encode(EvaluationMetric metric)
    {
        var stored = metric switch
        {
            NumericMetric n => StoredMetric.Numeric(
                n.Name, n.Value ?? 0, metric.Reason ?? string.Empty, Failed(metric), Rating(metric)),
            BooleanMetric b => StoredMetric.Boolean(
                b.Name, b.Value ?? false, metric.Reason ?? string.Empty, Failed(metric), Rating(metric)),
            StringMetric s => StoredMetric.Text(
                s.Name, s.Value ?? string.Empty, metric.Reason ?? string.Empty, Failed(metric), Rating(metric)),
            _ => StoredMetric.Text(
                metric.Name, string.Empty, metric.Reason ?? string.Empty, Failed(metric), Rating(metric)),
        };

        return metric.Metadata is { Count: > 0 }
            ? stored.With(new Dictionary<string, string>(metric.Metadata, StringComparer.Ordinal))
            : stored;
    }

    public static IReadOnlyList<StoredMetric> Encode(EvaluationResult result) =>
        [.. result.Metrics.Values.Select(Encode)];

    /// <summary>Back into their model, so their report writer can render a run nobody is re-executing.</summary>
    public static Outcome<EvaluationMetric> Decode(StoredMetric stored)
    {
        var metric = Build(stored);

        return metric.Match(
            m =>
            {
                m.Reason = stored.Reason;
                m.Interpretation = new EvaluationMetricInterpretation(ParseRating(stored.Rating), stored.Failed, stored.Reason);

                foreach (var (key, value) in stored.Metadata)
                {
                    m.AddOrUpdateMetadata(key, value);
                }

                return Outcome<EvaluationMetric>.Success(m);
            },
            Outcome<EvaluationMetric>.Failure);
    }

    private static Outcome<EvaluationMetric> Build(StoredMetric stored) =>
        stored.Kind switch
        {
            MetricKind.Numeric => stored.AsNumber().Match(
                value => Outcome<EvaluationMetric>.Success(new NumericMetric(stored.Name, value)),
                Outcome<EvaluationMetric>.Failure),
            MetricKind.Boolean => Outcome<EvaluationMetric>.Success(new BooleanMetric(stored.Name, stored.Value == "true")),
            MetricKind.Text => Outcome<EvaluationMetric>.Success(new StringMetric(stored.Name, stored.Value)),
            _ => Outcome<EvaluationMetric>.Failure($"unknown metric kind {stored.Kind}"),
        };

    private static bool Failed(EvaluationMetric metric) => metric.Interpretation?.Failed ?? false;

    /// <summary>The rating as a NAME, not a number. An enum stored as its ordinal is a value that changes
    /// meaning when somebody inserts a member, and years-old results are exactly what this project exists
    /// to keep comparable.</summary>
    private static string Rating(EvaluationMetric metric) =>
        (metric.Interpretation?.Rating ?? EvaluationRating.Unknown).ToString();

    private static EvaluationRating ParseRating(string rating) =>
        Enum.TryParse<EvaluationRating>(rating, ignoreCase: true, out var parsed) ? parsed : EvaluationRating.Unknown;
}
