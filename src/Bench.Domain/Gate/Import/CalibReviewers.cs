using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>One model row of the calibration harness's <c>models.py</c> (its <c>MODELS</c> dict, as JSON) — what a
/// <c>runs.jsonl</c> line does NOT record: the vendor's public endpoint, the vault entry NAME, the list prices.</summary>
public sealed record CalibModel(string Model, string Endpoint, string KeyName, ReviewerPrices Prices);

public static class CalibModels
{
    /// <summary><c>{"grok-4.7": {"endpoint": …, "key": …, "price": {"in", "cached", "out", "tierFrom"?, …}}, …}</c>.</summary>
    public static Outcome<IReadOnlyDictionary<string, CalibModel>> Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root
                ? Models(root)
                : Outcome<IReadOnlyDictionary<string, CalibModel>>.Failure("the calibration models file is not a JSON object of model → row");
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyDictionary<string, CalibModel>>.Failure($"the calibration models file is not JSON — {ex.Message}");
        }
    }

    private static Outcome<IReadOnlyDictionary<string, CalibModel>> Models(JsonObject root)
    {
        var models = new Dictionary<string, CalibModel>(StringComparer.Ordinal);

        foreach (var (model, node) in root)
        {
            var row = node as JsonObject ?? [];
            var prices = Prices(row["price"] as JsonObject ?? []);
            if (prices is Outcome<ReviewerPrices>.Fail fail)
            {
                return Outcome<IReadOnlyDictionary<string, CalibModel>>.Failure($"model '{model}': {fail.Reason}");
            }

            models[model] = new CalibModel(model, JsonRead.Text(row, "endpoint"), JsonRead.Text(row, "key"), ((Outcome<ReviewerPrices>.Ok)prices).Value);
        }

        return Outcome<IReadOnlyDictionary<string, CalibModel>>.Success(models);
    }

    private static Outcome<ReviewerPrices> Prices(JsonObject p) =>
        p.Count == 0
            ? Outcome<ReviewerPrices>.Success(ReviewerPrices.Unknown)
            : ReviewerPrices.Of(Money(p, "in"), Money(p, "cached"), Money(p, "out"), JsonRead.Long(p["tierFrom"]) ?? 0,
                Money(p, "tierIn"), Money(p, "tierCached"), Money(p, "tierOut"));

    private static decimal Money(JsonObject p, string name) => (decimal)JsonRead.Double(p[name]);
}

/// <summary>The reviewer row a calibration line was measured WITH: its model, its calibrated transport preset, the
/// model's endpoint, key name and prices — hashed like any other row. Created as <c>&lt;model&gt;-&lt;hash8&gt;</c>, or
/// matched by definition hash to a row that already exists under any name.</summary>
public static class CalibReviewers
{
    public static Outcome<ReviewerDefinition> Definition(CalibModel model, CalibPreset preset) =>
        (ReviewerEndpoint.Parse(model.Endpoint), preset.Transport, HostedGates.Of([GateKind.Feature])) switch
        {
            (Outcome<ReviewerEndpoint>.Fail f, _, _) => Outcome<ReviewerDefinition>.Failure($"model '{model.Model}' endpoint: {f.Reason}"),
            (_, Outcome<ReviewerTransport>.Fail f, _) => Outcome<ReviewerDefinition>.Failure($"model '{model.Model}' transport: {f.Reason}"),
            (Outcome<ReviewerEndpoint>.Ok e, Outcome<ReviewerTransport>.Ok t, Outcome<HostedGates>.Ok g) => ReviewerDefinition.Parse(
                ReviewerRuntime.Api, model.Model, e.Value, model.KeyName, credsKeyRef: string.Empty, executableRef: string.Empty,
                remoteVendor: string.Empty, t.Value, model.Prices, g.Value),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>The id of a NEW row: the model as a slug plus eight characters of the definition's hash, so two presets of
    /// one model are two rows and the name says which configuration it is.</summary>
    public static Outcome<GateReviewerId> NewId(string model, ReviewerDefinition definition) =>
        GateReviewerId.Parse($"{ImportSlug.Of(model, 55)}-{definition.Hash[..8]}");

    /// <summary>The existing rows with this definition, under whatever names they were added, first by id — an import never
    /// adds a row twice for one configuration, and none is an empty list, not a null.</summary>
    public static IReadOnlyList<GateReviewer> Existing(IReadOnlyList<GateReviewer> catalog, ReviewerDefinition definition) =>
        [.. catalog.Where(r => string.Equals(r.Hash, definition.Hash, StringComparison.Ordinal)).OrderBy(r => r.Id.Value, StringComparer.Ordinal)];
}
