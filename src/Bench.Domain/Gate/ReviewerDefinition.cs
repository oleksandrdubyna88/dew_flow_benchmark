using System.Globalization;
using Bench.Domain.Registry;

namespace Bench.Domain.Gate;

/// <summary>The calibrated transport a reviewer is measured WITH — part of the subject, not a run setting.
/// <para>
/// The calibration found that one model at its vendor default never answered inside any deadline and
/// answered in 5.4 minutes at <c>medium</c>; another at <c>high</c> took 20–26 minutes and at <c>medium</c>
/// 9–12. A row that named the model alone would put those under one label, so every knob here changes the
/// reviewer's hash.
/// </para></summary>
/// <param name="Dialect">The request dialect the product spells the call in — a row of its own dialect table.</param>
/// <param name="ReasoningEffort">In the vendor's spelling; <c>none</c> when the module's default is asked for.</param>
/// <param name="MaxTokens">The completion ceiling asked for; the product may floor it per dialect.</param>
/// <param name="TimeoutMinutes">The per-turn deadline.</param>
/// <param name="FollowUps">How many source follow-ups a feature review may ask for.</param>
/// <param name="ReviewMinutesCap">The whole-review limit — every turn of one reviewer's conversation.</param>
/// <param name="Thinking">The thinking switch, where the dialect has one — in the product's three states.</param>
public sealed record ReviewerTransport(
    string Dialect,
    string ReasoningEffort,
    int MaxTokens,
    int TimeoutMinutes,
    int FollowUps,
    int ReviewMinutesCap,
    ThinkingSetting Thinking)
{
    /// <summary>The two-state spelling every row stored before three states existed was made with: <c>true</c> is on,
    /// <c>false</c> is off — never the vendor default, which no stored row asked for.</summary>
    public static Outcome<ReviewerTransport> Parse(
        string? dialect, string? reasoningEffort, int maxTokens, int timeoutMinutes, int followUps, int reviewMinutesCap, bool thinking) =>
        Parse(dialect, reasoningEffort, maxTokens, timeoutMinutes, followUps, reviewMinutesCap, thinking ? ThinkingSetting.On : ThinkingSetting.Off);

    public static Outcome<ReviewerTransport> Parse(
        string? dialect, string? reasoningEffort, int maxTokens, int timeoutMinutes, int followUps, int reviewMinutesCap, ThinkingSetting thinking)
    {
        var d = (dialect ?? string.Empty).Trim().ToLowerInvariant();
        var effort = (reasoningEffort ?? string.Empty).Trim().ToLowerInvariant();

        var refusal = (d.Length, effort.Length, maxTokens, timeoutMinutes, followUps, reviewMinutesCap) switch
        {
            (0, _, _, _, _, _) => "a transport names its dialect — the product spells every request in one, and 'unset' is not one of them",
            (_, 0, _, _, _, _) => "a transport names its reasoning effort — say 'none' when the module's default is meant, so that is a decision rather than an omission",
            (_, _, < 1, _, _, _) => $"maxTokens must be at least 1, got {maxTokens}",
            (_, _, _, < 1, _, _) => $"timeoutMinutes must be at least 1, got {timeoutMinutes}",
            (_, _, _, _, < 0, _) => $"followUps cannot be negative, got {followUps}",
            (_, _, _, _, _, < 1) => $"reviewMinutesCap must be at least 1, got {reviewMinutesCap}",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ReviewerTransport>.Failure(refusal)
            : Outcome<ReviewerTransport>.Success(new ReviewerTransport(d, effort, maxTokens, timeoutMinutes, followUps, reviewMinutesCap, thinking));
    }

    /// <summary>The harness's word for "send no effort; let the product's dialect module choose". It is a
    /// HARNESS word and never reaches the product: on an OpenAI-style dialect <c>none</c> is a real value that
    /// turns reasoning OFF, so passing it through would measure a different subject than the row names.</summary>
    public const string ModuleDefault = "none";

    public bool AsksModuleDefault => string.Equals(ReasoningEffort, ModuleDefault, StringComparison.Ordinal);

    /// <summary>Length-prefixed: the dialect and the effort are free text side by side, and a <c>,</c>-joined form
    /// let dialect <c>"xai,effort=high"</c> with effort <c>medium</c> read like dialect <c>xai</c> with effort
    /// <c>"high,effort=medium"</c>.</summary>
    public string Canonical =>
        CanonicalFields.Of(
            "transport", Dialect, ReasoningEffort, Invariant(MaxTokens), Invariant(TimeoutMinutes), Invariant(FollowUps),
            Invariant(ReviewMinutesCap), ThinkingWord(Thinking));

    /// <summary>The two words rows were stored under are kept exactly, so every stored row still hashes to its stored
    /// hash; the vendor default is the new third word.</summary>
    private static string ThinkingWord(ThinkingSetting thinking) => thinking switch
    {
        ThinkingSetting.On => "thinking-on",
        ThinkingSetting.Off => "thinking-off",
        _ => "thinking-default",
    };

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>The product's thinking switch in its own three states (<c>coai · src_mcp/core/Api/ApiRowSettings.cs</c>):
/// the field absent is the vendor's default, <c>true</c> is on, <c>false</c> is off — and off is refused for a family
/// with no switch. E7's first A/A: a two-state row that could only say on or off asked grok and glm for OFF, and the
/// product skipped them.</summary>
public enum ThinkingSetting
{
    /// <summary>Send no field; the vendor's default applies.</summary>
    VendorDefault,

    On,

    Off,
}

/// <summary>What a reviewer charges per million tokens, or the honest statement that nobody knows.
/// <para>
/// <see cref="Unknown"/> is a state, not a zero: a CLI reviewer reports no cost and a row that priced it at
/// nothing would make the cheapest reviewer the one nobody metered. A tier is optional — one vendor doubles
/// every rate past a prompt size — and carried as values because a price is neither secret nor machine-specific.
/// </para></summary>
public sealed record ReviewerPrices
{
    private ReviewerPrices(
        bool known, decimal inPerMTok, decimal cachedPerMTok, decimal outPerMTok,
        long tierFromTokens, decimal tierIn, decimal tierCached, decimal tierOut)
    {
        Known = known;
        InPerMTok = inPerMTok;
        CachedPerMTok = cachedPerMTok;
        OutPerMTok = outPerMTok;
        TierFromTokens = tierFromTokens;
        TierIn = tierIn;
        TierCached = tierCached;
        TierOut = tierOut;
    }

    public bool Known { get; }

    public decimal InPerMTok { get; }

    public decimal CachedPerMTok { get; }

    public decimal OutPerMTok { get; }

    /// <summary>Zero when there is no tier.</summary>
    public long TierFromTokens { get; }

    public decimal TierIn { get; }

    public decimal TierCached { get; }

    public decimal TierOut { get; }

    public bool HasTier => TierFromTokens > 0;

    public static ReviewerPrices Unknown { get; } = new(false, 0, 0, 0, 0, 0, 0, 0);

    public static Outcome<ReviewerPrices> Of(
        decimal inPerMTok, decimal cachedPerMTok, decimal outPerMTok,
        long tierFromTokens = 0, decimal tierIn = 0, decimal tierCached = 0, decimal tierOut = 0)
    {
        var negative = new[] { inPerMTok, cachedPerMTok, outPerMTok, tierIn, tierCached, tierOut }.Any(p => p < 0);

        var refusal = (negative, tierFromTokens) switch
        {
            (true, _) => "a negative price is not a price",
            (_, < 0) => $"a tier starts at a token count, got {tierFromTokens}",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<ReviewerPrices>.Failure(refusal)
            : Outcome<ReviewerPrices>.Success(new ReviewerPrices(
                true, inPerMTok, cachedPerMTok, outPerMTok, tierFromTokens, tierIn, tierCached, tierOut));
    }

    public string Canonical => Known
        ? $"in={Money(InPerMTok)},cached={Money(CachedPerMTok)},out={Money(OutPerMTok)}"
          + (HasTier ? $",tierFrom={TierFromTokens},tierIn={Money(TierIn)},tierCached={Money(TierCached)},tierOut={Money(TierOut)}" : string.Empty)
        : "unknown";

    private static string Money(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}

/// <summary>A reviewer row's definition — everything that makes it the subject it is, hashed.
/// <para>
/// References, never values, wherever the value is a property of this machine or this account: the vault
/// ENTRY name (<see cref="KeyName"/>), the NAME of the variable holding the vault's access key
/// (<see cref="CredsKeyRef"/>), the NAME of the variable holding a CLI's path (<see cref="ExecutableRef"/>).
/// The endpoint is the one field that may be a value, and only when it is a vendor's public url —
/// <see cref="ReviewerEndpoint"/> says why.
/// </para></summary>
public sealed record ReviewerDefinition
{
    private ReviewerDefinition(
        ReviewerRuntime runtime, string model, ReviewerEndpoint endpoint, string keyName, string credsKeyRef,
        string executableRef, string remoteVendor, ReviewerTransport transport, ReviewerPrices prices, HostedGates gates)
    {
        Runtime = runtime;
        Model = model;
        Endpoint = endpoint;
        KeyName = keyName;
        CredsKeyRef = credsKeyRef;
        ExecutableRef = executableRef;
        RemoteVendor = remoteVendor;
        Transport = transport;
        Prices = prices;
        Gates = gates;
    }

    public ReviewerRuntime Runtime { get; }

    public string Model { get; }

    public ReviewerEndpoint Endpoint { get; }

    /// <summary>The vault entry the product reads the vendor key from. A name — the key itself never reaches
    /// this harness. Empty for a runtime that needs none.</summary>
    public string KeyName { get; }

    /// <summary>The NAME of the environment variable holding <c>COAI_CREDS_KEY</c> on this machine. Empty
    /// for a runtime that needs no vault.</summary>
    public string CredsKeyRef { get; }

    /// <summary>The NAME of the variable holding a CLI's path. Empty for anything but a <c>cli</c> row.</summary>
    public string ExecutableRef { get; }

    /// <summary>For a <c>remote</c> row: the vendor id the Team server knows, which is not this row's id.</summary>
    public string RemoteVendor { get; }

    public ReviewerTransport Transport { get; }

    public ReviewerPrices Prices { get; }

    /// <summary>Which gates this reviewer is ticked for. A run of one gate asks the product for that gate ONLY,
    /// whatever else the row could review — <c>CoaiVendorRow</c> pins the other ticks off.</summary>
    public HostedGates Gates { get; }

    public static Outcome<ReviewerDefinition> Parse(
        ReviewerRuntime runtime, string? model, ReviewerEndpoint endpoint, string? keyName, string? credsKeyRef,
        string? executableRef, string? remoteVendor, ReviewerTransport transport, ReviewerPrices prices, HostedGates gates)
    {
        var id = (model ?? string.Empty).Trim();
        var key = (keyName ?? string.Empty).Trim();
        var creds = (credsKeyRef ?? string.Empty).Trim();
        var exe = (executableRef ?? string.Empty).Trim();

        var refusal = Refuse(runtime, id, endpoint, key, creds, exe);

        return refusal.Length > 0
            ? Outcome<ReviewerDefinition>.Failure(refusal)
            : Outcome<ReviewerDefinition>.Success(new ReviewerDefinition(
                runtime, id, endpoint, key, creds, exe, (remoteVendor ?? string.Empty).Trim(), transport, prices, gates));
    }

    /// <summary>Length-prefixed (<see cref="CanonicalFields"/>), like the suite's forms: the model and the url are
    /// free text side by side, and a <c>|</c>-joined form let a model <c>"m|endpoint=url:A"</c> at url <c>B</c> hash
    /// like model <c>"m"</c> at url <c>"A|endpoint=url:B"</c> — two subjects under one hash.</summary>
    public string Canonical =>
        CanonicalFields.Of(
            "reviewer",
            Runtime.Word(),
            Model,
            Endpoint.Canonical,
            KeyName,
            CredsKeyRef,
            ExecutableRef,
            RemoteVendor,
            Transport.Canonical,
            Prices.Canonical,
            Gates.Canonical);

    public string Hash => StableHash.Of(Canonical);

    private static string Refuse(ReviewerRuntime runtime, string model, ReviewerEndpoint endpoint, string key, string creds, string exe)
    {
        if (model.Length == 0)
        {
            return "a reviewer row must name its model — an unset id is a refusal, never a fallback to a default";
        }

        if (runtime == ReviewerRuntime.Api && endpoint is ReviewerEndpoint.None)
        {
            return "an api reviewer needs an endpoint — a public vendor url as a value, or the NAME of the variable holding a machine-local one";
        }

        return RefuseNames([("keyName", key), ("credsKeyRef", creds), ("executableRef", exe)]);
    }

    /// <summary>Every name field is a NAME. The two shapes people paste are named in the refusal, because
    /// "invalid reference" teaches nobody what the rule is for.</summary>
    private static string RefuseNames(IReadOnlyList<(string Field, string Value)> names)
    {
        foreach (var (field, value) in names.Where(n => n.Value.Length > 0 && !ModelConfig.IsReference(n.Value)))
        {
            return ModelConfig.LooksLikeAValue(value)
                ? $"{field} was given a VALUE — store the NAME of the environment variable or vault entry that holds it; a "
                  + "reviewer row is published with the results"
                : $"'{Short(value)}' is not a usable {field} — a vault entry name or an environment variable name (letters, "
                  + "digits, '_', ':' and '.'); a key that starts with a vendor prefix is the SECRET, and it never enters a row";
        }

        return string.Empty;
    }

    private static string Short(string value) => value.Length <= 12 ? value : value[..12] + "…";
}
