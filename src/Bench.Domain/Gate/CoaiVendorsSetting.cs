using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>The vendor list as the product reads it — ONE JSON string under one environment variable — and
/// the ONE place a reviewer row becomes that string (the <c>QlnRequest.From</c> precedent: a recipe becomes a
/// request in exactly one place).
/// <para>
/// The factory lives INSIDE the type, and that is the guarantee rather than a convention. C# gives an enclosing
/// type no access to a nested type's private members, so a setting nested in a separate builder class would have
/// needed an <c>internal</c> door the builder could use — and every other type in the domain could use it too
/// (the first cut had exactly that: an <c>internal static Sealed</c>). Here the constructor is private and
/// <see cref="From"/> is the only member that calls it; an architecture test proves that nothing in any
/// production assembly returns a setting but <see cref="From"/>.
/// </para>
/// <para>
/// The variable's NAME is private too. Public, it let a host write <c>env[name] = anything</c> beside the value
/// the one producer made; now the only way to put a vendor list into an environment is
/// <see cref="ApplyTo"/>, which writes this value and removes any inherited spelling of the variable.
/// </para></summary>
public sealed record CoaiVendorsSetting
{
    /// <summary>The one place the variable's name is spelled in production code.</summary>
    private const string VariableName = "COAI_VENDORS";

    private CoaiVendorsSetting(string json, IReadOnlyList<GateReviewerId> reviewers)
    {
        Json = json;
        Reviewers = reviewers;
    }

    public string Json { get; }

    /// <summary>Whether <paramref name="name"/> is the vendors variable (any case) — so a caller can leave it out of a hash
    /// or refuse it as a run setting without the name ever being spelled, or exposed, outside this type.</summary>
    public static bool IsVariable(string name) => string.Equals(name, VariableName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Which reviewer rows the string was made from, in order — so a run record can name them
    /// without parsing the string back.</summary>
    public IReadOnlyList<GateReviewerId> Reviewers { get; }

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
        foreach (var row in reviewers.Select(reviewer => Row(reviewer, gate, resolved)))
        {
            if (row is Outcome<JsonObject>.Fail fail)
            {
                return Outcome<CoaiVendorsSetting>.Failure(fail.Reason);
            }

            rows.Add(((Outcome<JsonObject>.Ok)row).Value);
        }

        return Outcome<CoaiVendorsSetting>.Success(new CoaiVendorsSetting(rows.ToJsonString(), [.. reviewers.Select(r => r.Id)]));
    }

    /// <summary>A NEW environment: <paramref name="environment"/> with this vendor list set, and any inherited
    /// spelling of the variable (whatever its case — Windows reads names case-insensitively) removed, so the
    /// product reads one list and it is this one. The caller's environment is not touched.</summary>
    public IReadOnlyDictionary<string, string> ApplyTo(IReadOnlyDictionary<string, string> environment) =>
        new Dictionary<string, string>(
            environment.Where(e => !string.Equals(e.Key, VariableName, StringComparison.OrdinalIgnoreCase)),
            StringComparer.Ordinal)
        {
            [VariableName] = Json,
        };

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

    /// <summary>The row. The runtime is the product's own WORD for how to reach the reviewer — a CLI row names
    /// its CLI (<c>claude</c>, <c>gemini</c>, …), never <c>cli</c>, which the product would run on Codex. The
    /// effort is written only when it is a real vendor value: <see cref="ReviewerTransport.ModuleDefault"/> is
    /// the harness's "send nothing", and on an OpenAI-style dialect the same word would turn reasoning off.</summary>
    private static JsonObject Build(GateReviewer reviewer, GateKind gate, (string BaseUrl, string Executable) addresses)
    {
        var d = reviewer.Definition;
        var row = new JsonObject
        {
            ["id"] = reviewer.Id.Value,
            ["runtime"] = d.Runtime.Word(),
            ["model"] = d.Model,
            ["baseUrl"] = addresses.BaseUrl,
            ["executablePath"] = addresses.Executable,
            ["dialect"] = d.Transport.Dialect,
            ["key"] = d.KeyName,
            ["thinking"] = d.Transport.Thinking,
            ["reviewMinutes"] = d.Transport.ReviewMinutesCap,
            ["plan"] = gate == GateKind.Plan,
            ["code"] = gate == GateKind.Code,
            ["feature"] = gate == GateKind.Feature,
            ["document"] = false,
        };

        return Optional(row, d);
    }

    private static JsonObject Optional(JsonObject row, ReviewerDefinition d)
    {
        if (!d.Transport.AsksModuleDefault)
        {
            row["effort"] = d.Transport.ReasoningEffort;
        }

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
}

/// <summary>The values resolved on THIS machine for the references a reviewer row carries — a referenced
/// endpoint's url, a referenced CLI's path. Resolved by the caller through the secret source, handed in as
/// plain values, and never stored: the row keeps the names.</summary>
public sealed record ResolvedReferences(IReadOnlyDictionary<string, string> Values)
{
    public static ResolvedReferences Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    public bool TryGet(string name, out string value) => Values.TryGetValue(name, out value!);
}
