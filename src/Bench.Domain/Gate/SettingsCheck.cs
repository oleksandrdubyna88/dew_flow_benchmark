using System.Globalization;
using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>Whether the settings a run ASKED for are the settings its session actually got — a port of
/// <c>coai-bench</c>'s <c>SettingsCheck</c>. Three lists, because they are three different facts: a mismatch (asked, and
/// the session says otherwise), checked (asked, and the session agrees), and notChecked (asked, and nothing on disk can
/// show its effect — never reported as passing).</summary>
public sealed record SettingsApplied(IReadOnlyList<string> Mismatches, IReadOnlyList<string> Checked, IReadOnlyList<string> Unchecked)
{
    public bool Ok => Mismatches.Count == 0;

    public string ToJson() => new JsonObject
    {
        ["mismatches"] = new JsonArray([.. Mismatches.Select(m => (JsonNode)m)]),
        ["checked"] = new JsonArray([.. Checked.Select(m => (JsonNode)m)]),
        ["notChecked"] = new JsonArray([.. Unchecked.Select(m => (JsonNode)m)]),
    }.ToJsonString();
}

public static class SettingsCheck
{
    private const string RoundsPrefix = "COAI_ROUNDS_";
    private const string ThresholdPrefix = "COAI_THRESHOLD_";
    private const string EnabledPrefix = "COAI_ENABLED_";
    private const string OnExhausted = "COAI_ON_EXHAUSTED";

    /// <param name="asked">Every <c>COAI_*</c> the product was sent (the snapshot).</param>
    /// <param name="sessionConfigJson"><c>state.config</c> of THIS run's session file, or empty when there was none.</param>
    public static SettingsApplied Compare(IReadOnlyDictionary<string, string> asked, string sessionConfigJson)
    {
        var config = GateReplyParser.Json(sessionConfigJson) as JsonObject;
        var mismatches = new List<string>();
        var examined = new List<string>();
        var notChecked = new List<string>();

        foreach (var (name, value) in asked.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            var verdict = config is null ? Verdict.Unobservable : Judge(name, value, config);
            Record(verdict, name, mismatches, examined, notChecked);
        }

        return new SettingsApplied(mismatches, examined, notChecked);
    }

    private sealed record Verdict(string What, string Mismatch, bool Observable)
    {
        public static Verdict Unobservable { get; } = new(string.Empty, string.Empty, false);
    }

    private static void Record(Verdict verdict, string name, List<string> mismatches, List<string> examined, List<string> notChecked)
    {
        if (!verdict.Observable)
        {
            notChecked.Add(name);
            return;
        }

        examined.Add(verdict.What);
        if (verdict.Mismatch.Length > 0)
        {
            mismatches.Add(verdict.Mismatch);
        }
    }

    private static Verdict Judge(string name, string value, JsonObject config) =>
        name switch
        {
            OnExhausted => Policy(value, config),
            _ when name.StartsWith(RoundsPrefix, StringComparison.Ordinal) => Role(config, name[RoundsPrefix.Length..], "maxRounds", "rounds", value),
            _ when name.StartsWith(ThresholdPrefix, StringComparison.Ordinal) => Role(config, name[ThresholdPrefix.Length..], "threshold", "threshold", value),
            _ when name.StartsWith(EnabledPrefix, StringComparison.Ordinal) => Role(config, name[EnabledPrefix.Length..], "enabled", "enabled", value),
            _ => Verdict.Unobservable,
        };

    /// <summary><c>good_enough</c> in the environment, <c>GoodEnough</c> in the state: one decision, spelled twice.</summary>
    private static Verdict Policy(string asked, JsonObject config)
    {
        var seen = config["onExhausted"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

        return new Verdict("on-exhausted", Same(asked, seen) ? string.Empty : $"on-exhausted: asked '{asked}', the session says '{seen}'", true);
    }

    private static Verdict Role(JsonObject config, string roleUpper, string field, string what, string asked)
    {
        var roles = config["roles"] as JsonObject ?? [];
        var (role, gate) = roles.Where(r => string.Equals(r.Key, roleUpper, StringComparison.OrdinalIgnoreCase)).Select(r => (r.Key, r.Value as JsonObject)).FirstOrDefault();

        if (role is null || gate is null)
        {
            return new Verdict($"{roleUpper} {what}", $"{roleUpper} {what}: asked {asked}, the session has no such role", true);
        }

        var seen = gate[field] is JsonValue v ? v.ToJsonString().Trim('"') : "nothing";

        return new Verdict($"{role} {what}", Same(asked, seen) ? string.Empty : $"{role} {what}: asked {asked}, the session says {seen}", true);
    }

    private static bool Same(string asked, string seen) =>
        string.Equals(Normal(asked), Normal(seen), StringComparison.OrdinalIgnoreCase);

    private static string Normal(string word) => word.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLower(CultureInfo.InvariantCulture);
}
