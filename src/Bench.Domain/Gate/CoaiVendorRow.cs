using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>The vendor list as the product reads it — ONE JSON string under one environment variable.
/// <para>
/// Its constructor is private and the only thing that produces one is <see cref="CoaiVendorRow.From"/>, so no
/// other code can hand the server a vendors string: a second producer is how a bench ends up measuring a
/// vendor nobody configured. The environment builder takes THIS value, and the architecture guard scans the
/// tree for the literal to prove there is no other spelling.
/// </para></summary>
public sealed record CoaiVendorsSetting
{
    /// <summary>The one place the variable's name is spelled in production code.</summary>
    public const string VariableName = "COAI_VENDORS";

    private CoaiVendorsSetting(string json, IReadOnlyList<GateReviewerId> reviewers)
    {
        Json = json;
        Reviewers = reviewers;
    }

    public string Json { get; }

    /// <summary>Which reviewer rows the string was made from, in order — so a run record can name them
    /// without parsing the string back.</summary>
    public IReadOnlyList<GateReviewerId> Reviewers { get; }

    internal static CoaiVendorsSetting Sealed(string json, IReadOnlyList<GateReviewerId> reviewers) => new(json, reviewers);
}

/// <summary>The values resolved on THIS machine for the references a reviewer row carries — a referenced
/// endpoint's url, a referenced CLI's path. Resolved by the caller through the secret source, handed in as
/// plain values, and never stored: the row keeps the names.</summary>
public sealed record ResolvedReferences(IReadOnlyDictionary<string, string> Values)
{
    public static ResolvedReferences Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    public bool TryGet(string name, out string value) => Values.TryGetValue(name, out value!);
}

/// <summary>One vendor row as the panel's settings file carries it — the shape <c>--from-coai-settings</c>
/// imports. Strings as read; the reviewer row is built from them one step later.</summary>
public sealed record CoaiVendorFields(
    string Id,
    string Runtime,
    string Model,
    string BaseUrl,
    string ExecutablePath,
    string Dialect,
    string Key,
    string Effort,
    string RemoteVendor,
    int ReviewMinutes,
    bool Thinking,
    bool Plan,
    bool Code,
    bool Feature,
    bool Document);

/// <summary>A reviewer row becomes the vendor row the product reads in exactly one place — the
/// <c>QlnRequest.From</c> precedent — and a vendor field this build does not know is refused by name.</summary>
public static class CoaiVendorRow
{
    /// <summary>Every field the product's vendor row knows, in the product's own spelling. The writer emits
    /// only these and the reader refuses anything else: one list, enumerated by both, so a field the product
    /// grows is a red test here rather than a silently-dropped setting.</summary>
    public static IReadOnlyList<string> KnownFields { get; } =
    [
        "id", "runtime", "model", "baseUrl", "executablePath", "plan", "code", "document", "remoteVendor",
        "dialect", "feature", "key", "price", "effort", "thinking", "reviewMinutes",
    ];

    /// <summary>The vendors string for a run of <paramref name="gate"/> over <paramref name="reviewers"/>.
    /// <para>
    /// Only the gate under measurement is ticked on every row, whatever else the row could review — the
    /// measurement rule: pin everything you are not varying. A retired row, a row that does not host the gate,
    /// and a reference nothing resolved on this machine are each refused by name before any process starts.
    /// </para></summary>
    public static Outcome<CoaiVendorsSetting> From(IReadOnlyList<GateReviewer> reviewers, GateKind gate, ResolvedReferences resolved)
    {
        if (reviewers.Count == 0)
        {
            return Outcome<CoaiVendorsSetting>.Failure("a run names at least one reviewer — an empty vendor list measures nobody");
        }

        var rows = new JsonArray();
        foreach (var reviewer in reviewers)
        {
            var row = Row(reviewer, gate, resolved);
            if (row is Outcome<JsonObject>.Fail fail)
            {
                return Outcome<CoaiVendorsSetting>.Failure(fail.Reason);
            }

            rows.Add(((Outcome<JsonObject>.Ok)row).Value);
        }

        return Outcome<CoaiVendorsSetting>.Success(
            CoaiVendorsSetting.Sealed(rows.ToJsonString(), [.. reviewers.Select(r => r.Id)]));
    }

    /// <summary>The panel's own rows, read back for import. A row carrying a field this build does not know is
    /// refused naming the field — dropping it would import a reviewer that is not the one the operator runs.</summary>
    public static Outcome<IReadOnlyList<CoaiVendorFields>> Read(string? json)
    {
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(json ?? string.Empty);
        }
        catch (JsonException e)
        {
            return Outcome<IReadOnlyList<CoaiVendorFields>>.Failure($"{CoaiVendorsSetting.VariableName} is not JSON: {e.Message}");
        }

        if (parsed is not JsonArray array)
        {
            return Outcome<IReadOnlyList<CoaiVendorFields>>.Failure($"{CoaiVendorsSetting.VariableName} is a JSON ARRAY of vendor rows");
        }

        var rows = new List<CoaiVendorFields>();
        foreach (var node in array)
        {
            var read = Fields(node);
            if (read is Outcome<CoaiVendorFields>.Fail fail)
            {
                return Outcome<IReadOnlyList<CoaiVendorFields>>.Failure(fail.Reason);
            }

            rows.Add(((Outcome<CoaiVendorFields>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<CoaiVendorFields>>.Success(rows);
    }

    private static Outcome<JsonObject> Row(GateReviewer reviewer, GateKind gate, ResolvedReferences resolved)
    {
        var definition = reviewer.Definition;

        var refusal = (reviewer.IsActive, definition.Gates.Hosts(gate)) switch
        {
            (false, _) => $"reviewer '{reviewer.Id}' is retired (since {reviewer.RetiredAt:u}) — a retired row is readable, not runnable",
            (_, false) => $"reviewer '{reviewer.Id}' is not ticked for the {gate.ToString().ToLowerInvariant()} gate — it hosts {definition.Gates.Canonical}",
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<JsonObject>.Failure(refusal);
        }

        return Addresses(reviewer, resolved).Match(
            addresses => Outcome<JsonObject>.Success(Build(reviewer, gate, addresses)),
            Outcome<JsonObject>.Failure);
    }

    private static JsonObject Build(GateReviewer reviewer, GateKind gate, (string BaseUrl, string Executable) addresses)
    {
        var d = reviewer.Definition;
        var row = new JsonObject
        {
            ["id"] = reviewer.Id.Value,
            ["runtime"] = d.Runtime.ToString().ToLowerInvariant(),
            ["model"] = d.Model,
            ["baseUrl"] = addresses.BaseUrl,
            ["executablePath"] = addresses.Executable,
            ["dialect"] = d.Transport.Dialect,
            ["key"] = d.KeyName,
            ["effort"] = d.Transport.ReasoningEffort,
            ["thinking"] = d.Transport.Thinking,
            ["reviewMinutes"] = d.Transport.ReviewMinutesCap,
            ["plan"] = gate == GateKind.Plan,
            ["code"] = gate == GateKind.Code,
            ["feature"] = gate == GateKind.Feature,
            ["document"] = false,
        };

        if (d.Runtime == ReviewerRuntime.Remote && d.RemoteVendor.Length > 0)
        {
            row["remoteVendor"] = d.RemoteVendor;
        }

        if (d.Prices.Known)
        {
            row["price"] = Price(d.Prices);
        }

        return row;
    }

    private static JsonObject Price(ReviewerPrices prices)
    {
        var price = new JsonObject
        {
            ["in"] = (double)prices.InPerMTok,
            ["cached"] = (double)prices.CachedPerMTok,
            ["out"] = (double)prices.OutPerMTok,
        };

        if (prices.HasTier)
        {
            price["tierFrom"] = prices.TierFromTokens;
            price["tierIn"] = (double)prices.TierIn;
            price["tierCached"] = (double)prices.TierCached;
            price["tierOut"] = (double)prices.TierOut;
        }

        return price;
    }

    /// <summary>The two address fields as VALUES for this machine: a value endpoint as it is, a referenced one
    /// through what the caller resolved, and a refusal naming the reference when nothing did.</summary>
    private static Outcome<(string BaseUrl, string Executable)> Addresses(GateReviewer reviewer, ResolvedReferences resolved)
    {
        var d = reviewer.Definition;

        var baseUrl = d.Endpoint switch
        {
            ReviewerEndpoint.Value v => Outcome<string>.Success(v.Url),
            ReviewerEndpoint.Reference r => Resolve(reviewer.Id, "endpoint", r.Name, resolved),
            _ => Outcome<string>.Success(string.Empty),
        };

        var executable = d.ExecutableRef.Length == 0
            ? Outcome<string>.Success(string.Empty)
            : Resolve(reviewer.Id, "executable", d.ExecutableRef, resolved);

        return baseUrl.Match(
            url => executable.Match(
                exe => Outcome<(string, string)>.Success((url, exe)),
                Outcome<(string, string)>.Failure),
            Outcome<(string, string)>.Failure);
    }

    private static Outcome<string> Resolve(GateReviewerId id, string what, string name, ResolvedReferences resolved) =>
        resolved.TryGet(name, out var value) && value.Length > 0
            ? Outcome<string>.Success(value)
            : Outcome<string>.Failure(
                $"reviewer '{id}' names its {what} by reference ({name}) and nothing resolved it on this machine — set the variable, "
                + "or the run would hand the product an empty address");

    private static Outcome<CoaiVendorFields> Fields(JsonNode? node)
    {
        if (node is not JsonObject row)
        {
            return Outcome<CoaiVendorFields>.Failure("a vendor row is a JSON object");
        }

        var id = Text(row, "id");
        var unknown = row.Select(p => p.Key).Where(k => !KnownFields.Contains(k, StringComparer.Ordinal)).ToList();

        if (unknown.Count > 0)
        {
            return Outcome<CoaiVendorFields>.Failure(
                $"vendor row '{id}' carries a field this build does not know: {string.Join(", ", unknown.Select(u => $"'{u}'"))} — "
                + $"the fields it knows are {string.Join(", ", KnownFields)}; a field dropped on import is a reviewer that is not the one the operator runs");
        }

        return id.Length == 0
            ? Outcome<CoaiVendorFields>.Failure("a vendor row without an id is not a vendor")
            : Outcome<CoaiVendorFields>.Success(new CoaiVendorFields(
                id, Text(row, "runtime"), Text(row, "model"), Text(row, "baseUrl"), Text(row, "executablePath"),
                Text(row, "dialect"), Text(row, "key"), Text(row, "effort"), Text(row, "remoteVendor"),
                Number(row, "reviewMinutes"), Flag(row, "thinking", true),
                Flag(row, "plan", true), Flag(row, "code", true), Flag(row, "feature", false), Flag(row, "document", false)));
    }

    private static string Text(JsonObject row, string name) =>
        row[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;

    private static int Number(JsonObject row, string name) =>
        row[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;

    /// <summary>Absent folds to the product's own default for that flag — plan and code absent mean yes,
    /// feature and document absent mean no.</summary>
    private static bool Flag(JsonObject row, string name, bool whenAbsent) =>
        row[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : whenAbsent;
}
