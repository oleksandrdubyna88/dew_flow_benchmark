using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>The scouts' operator files, read and written in ONE place, each a refusal by name when it is malformed:
/// <list type="bullet">
/// <item><b>candidates</b> (<c>bench scouts tasks --candidates</c>) — <c>{ "candidates": [ { "id", "repository", "parent", "fix", "kind",
/// "statement", "planFile" } ] }</c>;</item>
/// <item><b>tasks</b> (<c>tasks.json</c>, what <c>tasks</c> writes and <c>run</c> reads) — the frozen tasks, each with its added identifiers
/// so the leak check runs again on the way in;</item>
/// <item><b>prices</b> (<c>--prices</c>) — <c>{ "source": "https://…", "readOn": "yyyy-MM-dd", "models": [ { "modelId", "input", "output",
/// "cacheRead", "cacheWrite5m", "cacheWrite1h" } ] }</c>, USD per million tokens.</item>
/// </list></summary>
public static class ScoutFiles
{
    public static string KindWord(ScoutQuestionKind kind) => kind switch
    {
        ScoutQuestionKind.WhereDecided => "where-decided",
        ScoutQuestionKind.HowEndToEnd => "how-end-to-end",
        _ => "what-must-change",
    };

    public static Outcome<ScoutQuestionKind> Kind(string word) =>
        Enum.GetValues<ScoutQuestionKind>().Where(k => KindWord(k) == word.Trim()).Select(Outcome<ScoutQuestionKind>.Success).DefaultIfEmpty(
            Outcome<ScoutQuestionKind>.Failure($"'{word}' is not a question kind — where-decided, how-end-to-end or what-must-change")).First();

    public static Outcome<IReadOnlyList<ScoutCandidate>> Candidates(string json) =>
        Array(json, "candidates").Match(items => Collect(items.Select(Candidate)), Outcome<IReadOnlyList<ScoutCandidate>>.Failure);

    private static Outcome<ScoutCandidate> Candidate(JsonNode? node)
    {
        var id = ScoutTaskId.Parse(Text(node, "id"));
        var label = Text(node, "id");

        return (id, CommitSha.Parse(Text(node, "parent")), CommitSha.Parse(Text(node, "fix")), Kind(Text(node, "kind"))) switch
        {
            (Outcome<ScoutTaskId>.Fail f, _, _, _) => Outcome<ScoutCandidate>.Failure(f.Reason),
            (_, Outcome<CommitSha>.Fail f, _, _) => Outcome<ScoutCandidate>.Failure($"candidate {label}: parent — {f.Reason}"),
            (_, _, Outcome<CommitSha>.Fail f, _) => Outcome<ScoutCandidate>.Failure($"candidate {label}: fix — {f.Reason}"),
            (_, _, _, Outcome<ScoutQuestionKind>.Fail f) => Outcome<ScoutCandidate>.Failure($"candidate {label}: {f.Reason}"),
            (Outcome<ScoutTaskId>.Ok i, Outcome<CommitSha>.Ok p, Outcome<CommitSha>.Ok s, Outcome<ScoutQuestionKind>.Ok k) =>
                Outcome<ScoutCandidate>.Success(new ScoutCandidate(i.Value, Text(node, "repository"), p.Value, s.Value, k.Value, Text(node, "statement"), Text(node, "planFile"))),
            _ => Outcome<ScoutCandidate>.Failure("unreachable"),
        };
    }

    /// <summary>One frozen task and the identifiers its diff added — what <c>tasks.json</c> carries per task.</summary>
    public sealed record FrozenTask(ScoutTask Task, IReadOnlyList<string> AddedIdentifiers);

    public static Outcome<IReadOnlyList<ScoutTask>> Tasks(string json) =>
        Array(json, "tasks").Match(items => Collect(items.Select(TaskOf)), Outcome<IReadOnlyList<ScoutTask>>.Failure);

    private static Outcome<ScoutTask> TaskOf(JsonNode? node)
    {
        var id = ScoutTaskId.Parse(Text(node, "id"));

        return (id, CommitSha.Parse(Text(node, "parent")), CommitSha.Parse(Text(node, "fix")), Kind(Text(node, "kind"))) switch
        {
            (Outcome<ScoutTaskId>.Ok i, Outcome<CommitSha>.Ok p, Outcome<CommitSha>.Ok s, Outcome<ScoutQuestionKind>.Ok k) => ScoutTask.Frozen(
                i.Value, Text(node, "repository"), p.Value, s.Value, k.Value, Text(node, "statement"),
                Texts(node, "groundTruth"), Texts(node, "addedIdentifiers"), Texts(node, "removals"), Text(node, "treeHash")),
            _ => Outcome<ScoutTask>.Failure($"task '{Text(node, "id")}' has a malformed id, parent, fix or kind"),
        };
    }

    public static string WriteTasks(IReadOnlyList<FrozenTask> tasks, int seed, string cutoffsNote) =>
        new JsonObject
        {
            ["seed"] = seed,
            ["cutoffs"] = cutoffsNote,
            ["tasks"] = new JsonArray([.. tasks.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.Task.Id.Value,
                ["repository"] = t.Task.Repository,
                ["parent"] = t.Task.Parent.Value,
                ["fix"] = t.Task.Fix.Value,
                ["kind"] = KindWord(t.Task.Kind),
                ["statement"] = t.Task.Statement,
                ["groundTruth"] = Array(t.Task.GroundTruth),
                ["addedIdentifiers"] = Array(t.AddedIdentifiers),
                ["removals"] = Array(t.Task.Removals),
                ["treeHash"] = t.Task.TreeHash,
            })]),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

    public static Outcome<PriceTable> Prices(string json)
    {
        var models = Array(json, "models");
        var root = Root(json);
        var readOn = DateOnly.TryParseExact(Text(root, "readOn"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);

        return (models, readOn) switch
        {
            (Outcome<IReadOnlyList<JsonNode?>>.Fail f, _) => Outcome<PriceTable>.Failure(f.Reason),
            (_, false) => Outcome<PriceTable>.Failure("the price table has no \"readOn\" date (yyyy-MM-dd) — the date it was read is part of every cost"),
            (Outcome<IReadOnlyList<JsonNode?>>.Ok ok, _) => Collect(ok.Value.Select(Price)).Match(prices => PriceTable.Of(Text(root, "source"), date, prices), Outcome<PriceTable>.Failure),
            _ => Outcome<PriceTable>.Failure("unreachable"),
        };
    }

    private static Outcome<ModelPrice> Price(JsonNode? node) =>
        ModelPrice.Of(Text(node, "modelId"), Number(node, "input"), Number(node, "output"), Number(node, "cacheRead"), Number(node, "cacheWrite5m"), Number(node, "cacheWrite1h"));

    private static Outcome<IReadOnlyList<T>> Collect<T>(IEnumerable<Outcome<T>> items)
    {
        var all = items.ToList();

        return all.OfType<Outcome<T>.Fail>().FirstOrDefault() is { } fail
            ? Outcome<IReadOnlyList<T>>.Failure(fail.Reason)
            : Outcome<IReadOnlyList<T>>.Success([.. all.OfType<Outcome<T>.Ok>().Select(o => o.Value)]);
    }

    private static JsonNode? Root(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Outcome<IReadOnlyList<JsonNode?>> Array(string json, string name) =>
        Root(json)?[name] is JsonArray items
            ? Outcome<IReadOnlyList<JsonNode?>>.Success([.. items])
            : Outcome<IReadOnlyList<JsonNode?>>.Failure($"the file is not JSON with a \"{name}\" array");

    private static JsonArray Array(IReadOnlyList<string> values) => new([.. values.Select(v => (JsonNode)JsonValue.Create(v))]);

    private static string Text(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;

    private static IReadOnlyList<string> Texts(JsonNode? node, string name) =>
        node?[name] is JsonArray items ? [.. items.Select(i => i is JsonValue v && v.TryGetValue<string>(out var t) ? t.Trim() : string.Empty).Where(t => t.Length > 0)] : [];

    private static decimal Number(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : -1m;
}
