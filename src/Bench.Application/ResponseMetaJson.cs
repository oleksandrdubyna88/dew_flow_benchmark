using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Storing <see cref="ResponseMeta"/> as JSON, through an explicit wire record.
/// <para>
/// Explicit rather than serialising the domain type directly, for the same reason
/// <c>VariantJson</c> and <c>ModelConfigJson</c> are: the domain type is free to change shape, while a
/// stored row must stay readable for years. Every wire member is nullable, so a row written before this
/// column existed — <c>{}</c>, the default the migration gave it — reads as
/// <see cref="ResponseMeta.None"/>: nobody counted, rather than a leg that reported zeroes.
/// </para></summary>
public static class ResponseMetaJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(ResponseMeta meta) =>
        JsonSerializer.Serialize(
            new MetaFile
            {
                PromptTokens = Count(meta.PromptTokens),
                CompletionTokens = Count(meta.CompletionTokens),
                LatencyMs = (long)meta.Latency.TotalMilliseconds,
                Sampling = new SamplingFile
                {
                    Captured = meta.Sampling.Captured,
                    Temperature = meta.Sampling.Temperature,
                    Seed = meta.Sampling.Seed,
                    Source = meta.Sampling.Source,
                },
                Stop = meta.Stop,
                StopDetail = meta.StopDetail,
                ResponseBytes = meta.ResponseBytes,
            },
            Options);

    public static ResponseMeta Read(string json)
    {
        var file = Parse(json);

        return file?.Sampling is null || file.PromptTokens is null || file.CompletionTokens is null
            ? ResponseMeta.None
            : new ResponseMeta(
                Count(file.PromptTokens),
                Count(file.CompletionTokens),
                TimeSpan.FromMilliseconds(file.LatencyMs ?? 0),
                new SamplingAsSent(
                    file.Sampling.Captured ?? false,
                    file.Sampling.Temperature ?? 0,
                    file.Sampling.Seed ?? 0,
                    file.Sampling.Source ?? string.Empty),
                file.Stop ?? StopReason.Unknown,
                file.StopDetail ?? string.Empty,
                file.ResponseBytes ?? 0);
    }

    private static MetaFile? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<MetaFile>(json, Options);
        }
        catch (JsonException)
        {
            // A row a human edited, or one written by a build that is not this one. None rather than a
            // throw: a whole run's report must not be unreadable because one row is.
            return null;
        }
    }

    private static CountFile Count(CapturedCount count) =>
        new() { WasCaptured = count.WasCaptured, Value = count.Value, Reason = count.Reason };

    private static CapturedCount Count(CountFile file) =>
        file.WasCaptured == true
            ? CapturedCount.Number(file.Value ?? 0)
            : CapturedCount.Unavailable(file.Reason ?? "the stored row said nothing about why");

    private sealed record MetaFile
    {
        public CountFile? PromptTokens { get; init; }

        public CountFile? CompletionTokens { get; init; }

        public long? LatencyMs { get; init; }

        public SamplingFile? Sampling { get; init; }

        public StopReason? Stop { get; init; }

        public string? StopDetail { get; init; }

        public long? ResponseBytes { get; init; }
    }

    private sealed record CountFile
    {
        public bool? WasCaptured { get; init; }

        public long? Value { get; init; }

        public string? Reason { get; init; }
    }

    private sealed record SamplingFile
    {
        public bool? Captured { get; init; }

        public double? Temperature { get; init; }

        public int? Seed { get; init; }

        public string? Source { get; init; }
    }
}
