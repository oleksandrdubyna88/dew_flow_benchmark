using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bench.Delivered;

/// <summary>Locating the JSON inside a model's reply.
///
/// <para>Models wrap JSON in prose or fences even when told not to, so the OUTERMOST object is located
/// rather than the whole reply being assumed to parse. That is a tolerance about packaging, not about
/// content: once the object is found, every field rule is strict.</para>
/// </summary>
public static partial class ReplyReader
{
    [GeneratedRegex(@"```(?:json)?\s*(?<body>[\s\S]*?)```")]
    private static partial Regex Fence { get; }

    /// <summary>The outermost JSON object in a reply. The caller owns the returned document.</summary>
    public static Reply<JsonDocument> ReadObject(string reply)
    {
        var raw = reply.Trim();
        var fence = Fence.Match(raw);

        if (fence.Success)
        {
            raw = fence.Groups["body"].Value.Trim();
        }

        var start = raw.IndexOf('{', StringComparison.Ordinal);
        var end = raw.LastIndexOf('}');

        if (start == -1 || end <= start)
        {
            return Reply<JsonDocument>.Refuse("no JSON object found in the reply");
        }

        try
        {
            return Reply<JsonDocument>.Read(JsonDocument.Parse(raw[start..(end + 1)]));
        }
        catch (JsonException e)
        {
            return Reply<JsonDocument>.Refuse($"not valid JSON: {e.Message}");
        }
    }

    public static string Text(JsonElement node, string property) =>
        node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    public static bool Flag(JsonElement node, string property) =>
        node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}

/// <summary>One step a decomposition claims, before anything prices it.</summary>
/// <param name="Anchor">Where in the diff it is. Required: a step with no anchor cannot be checked against
/// the change it claims to describe, and an unanchored step is the cheapest thing in the world to invent.</param>
public sealed record DecomposedStep(string Key, string What, string Anchor);

/// <param name="Capped">Whether the decomposition claims it could not account for more of the change.</param>
/// <param name="Reason">Why, when it is capped. Judged by <see cref="CoverageDecision.JudgeReason"/> —
/// which is why an empty one is carried rather than rejected here: the GATE decides what a missing reason
/// costs, and a parser that refused first would take that decision away from it.</param>
public sealed record Decomposition(IReadOnlyList<DecomposedStep> Steps, bool Capped, string Reason);

/// <summary>Reading the two replies the delivered-work stage asks for.
///
/// <para><b>Every rule here refuses rather than repairs.</b> A duplicate key, a score off the scale, a
/// step scored that nobody asked about, a key silently dropped — each is a reply the protocol cannot
/// record, and the stage's answer to all of them is the one re-ask the gate allows. Repairing any of them
/// would put a number into a published score that no model produced.</para>
/// </summary>
public static class DeliveredWorkReplies
{
    /// <summary>The decomposition: what the change did, step by step, each anchored in the diff.</summary>
    public static Reply<Decomposition> ReadDecomposition(string reply)
    {
        var read = ReplyReader.ReadObject(reply);

        if (read is not Reply<JsonDocument>.Ok(var document))
        {
            return Reply<Decomposition>.Refuse(read.Reason);
        }

        using (document)
        {
            var root = document.RootElement;

            if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            {
                return Reply<Decomposition>.Refuse("the reply has no \"steps\" array");
            }

            var parsed = new List<DecomposedStep>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var step in steps.EnumerateArray())
            {
                // Read ONCE: this call records the key, so asking twice would report the reply's own
                // second step as a duplicate of itself.
                var readStep = ReadStep(step, seen);

                if (readStep is not Reply<DecomposedStep>.Ok(var value))
                {
                    return Reply<Decomposition>.Refuse(readStep.Reason);
                }

                parsed.Add(value);
            }

            return Reply<Decomposition>.Read(
                new Decomposition(parsed, ReplyReader.Flag(root, "capped"), ReplyReader.Text(root, "reason")));
        }
    }

    private static Reply<DecomposedStep> ReadStep(JsonElement step, HashSet<string> seen)
    {
        var key = ReplyReader.Text(step, "key");

        if (key.Length == 0)
        {
            return Reply<DecomposedStep>.Refuse("a step is missing its \"key\"");
        }

        if (!seen.Add(key))
        {
            return Reply<DecomposedStep>.Refuse($"duplicate step key {key}");
        }

        var anchor = ReplyReader.Text(step, "anchor");

        return anchor.Length == 0
            ? Reply<DecomposedStep>.Refuse($"{key}: every step needs an anchor from the diff")
            : Reply<DecomposedStep>.Read(new DecomposedStep(key, ReplyReader.Text(step, "what"), anchor));
    }

    /// <summary>The weighing: one score per step, on the scale, for exactly the steps that were asked
    /// about.</summary>
    /// <param name="expectedKeys">The decomposition's keys. The reply must cover them EXACTLY — every one
    /// scored once and none invented. A key silently dropped here would silently drop work from the score,
    /// and an invented one would price a step the diff never contained.</param>
    public static Reply<IReadOnlyList<UnitScore>> ReadScores(string reply, IReadOnlyList<string> expectedKeys)
    {
        var read = ReplyReader.ReadObject(reply);

        if (read is not Reply<JsonDocument>.Ok(var document))
        {
            return Reply<IReadOnlyList<UnitScore>>.Refuse(read.Reason);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("scores", out var scores)
                || scores.ValueKind != JsonValueKind.Array)
            {
                return Reply<IReadOnlyList<UnitScore>>.Refuse("the reply has no \"scores\" array");
            }

            var byKey = new Dictionary<string, UnitScore>(StringComparer.Ordinal);

            foreach (var entry in scores.EnumerateArray())
            {
                if (ReadScore(entry, byKey) is { Length: > 0 } refusal)
                {
                    return Reply<IReadOnlyList<UnitScore>>.Refuse(refusal);
                }
            }

            return Uncovered(expectedKeys, byKey.Keys) is { Length: > 0 } gap
                ? Reply<IReadOnlyList<UnitScore>>.Refuse(
                    $"score every key exactly once and invent no others — {gap}")
                // Returned in the order they were ASKED, never the order the model answered: a caller
                // zipping scores against steps must not depend on a model's ordering.
                : Reply<IReadOnlyList<UnitScore>>.Read([.. expectedKeys.Select(key => byKey[key])]);
        }
    }

    /// <summary>Reads one score into <paramref name="byKey"/>, or names why it cannot.</summary>
    private static string ReadScore(JsonElement entry, Dictionary<string, UnitScore> byKey)
    {
        var key = ReplyReader.Text(entry, "key");

        if (key.Length == 0)
        {
            return "a score entry is missing its \"key\"";
        }

        if (byKey.ContainsKey(key))
        {
            return $"duplicate score for {key}";
        }

        // The ValueKind check is not redundant: TryGetInt32 THROWS on a non-number token rather than
        // answering false, so a model that wrote "score": "high" would crash the stage instead of being
        // refused by it — the one outcome this module promises never to produce.
        if (!entry.TryGetProperty("score", out var node)
            || node.ValueKind != JsonValueKind.Number
            || !node.TryGetInt32(out var score))
        {
            return $"{key}: \"score\" must be an integer";
        }

        if (!WeightingProtocol.IsOnScale(score))
        {
            return $"{key}: score {score} is outside "
                + $"{WeightingProtocol.MinScore}-{WeightingProtocol.MaxScore}";
        }

        byKey[key] = new UnitScore(key, score, ReplyReader.Text(entry, "why"));

        return string.Empty;
    }

    private static string Uncovered(IReadOnlyList<string> expected, IEnumerable<string> present)
    {
        var seen = present.ToList();
        var missing = expected.Where(k => !seen.Contains(k, StringComparer.Ordinal)).ToList();
        var unknown = seen.Where(k => !expected.Contains(k, StringComparer.Ordinal)).ToList();

        return missing.Count == 0 && unknown.Count == 0
            ? string.Empty
            : $"missing: [{string.Join(", ", missing)}], unknown: [{string.Join(", ", unknown)}]";
    }
}
