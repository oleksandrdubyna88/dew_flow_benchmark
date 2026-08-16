using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Domain.Runs;

namespace Bench.Application.Registry;

/// <summary>A registry row's configuration, on the wire.
/// <para>
/// Read back through <see cref="ModelConfig.Parse"/> rather than materialised straight into the record —
/// which is what makes the publication rule a GUARD rather than a comment: a row edited by hand to carry a
/// url or an absolute path is refused when it is read, not served into a run and then published.
/// </para>
/// <para>
/// camelCase and unknown-member-disallowed for the same reasons the variant definition is: this string is
/// stored verbatim and goes out with the results, and a field this build does not know would otherwise run
/// a model under a configuration nobody asked for.
/// </para></summary>
public static class ModelConfigJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Write(ModelConfig config) =>
        JsonSerializer.Serialize(
            new ConfigWire
            {
                ModelId = config.ModelId,
                BaseUrlRef = Empty(config.BaseUrlRef),
                ApiKeyRef = Empty(config.ApiKeyRef),
                ExecutableRef = Empty(config.ExecutableRef),
                Temperature = config.Sampling.Temperature,
                Seed = config.Sampling.Seed,
                InputCostPerMTok = config.InputCostPerMTok,
                OutputCostPerMTok = config.OutputCostPerMTok,
            },
            Options);

    public static Outcome<ModelConfig> Read(string json)
    {
        try
        {
            var wire = JsonSerializer.Deserialize<ConfigWire>(json, Options);

            return wire is null
                ? Outcome<ModelConfig>.Failure("the model configuration is empty")
                : ModelConfig.Parse(
                    wire.ModelId,
                    wire.BaseUrlRef,
                    wire.ApiKeyRef,
                    wire.ExecutableRef,
                    new Sampling(wire.Temperature, wire.Seed),
                    wire.InputCostPerMTok,
                    wire.OutputCostPerMTok);
        }
        catch (JsonException ex)
        {
            return Outcome<ModelConfig>.Failure($"the model configuration could not be read — {ex.Message}");
        }
    }

    private static string? Empty(string value) => value.Length > 0 ? value : null;

    private sealed record ConfigWire
    {
        public string ModelId { get; init; } = string.Empty;

        public string? BaseUrlRef { get; init; }

        public string? ApiKeyRef { get; init; }

        public string? ExecutableRef { get; init; }

        public double Temperature { get; init; }

        public int Seed { get; init; } = 1;

        public decimal InputCostPerMTok { get; init; }

        public decimal OutputCostPerMTok { get; init; }
    }
}
