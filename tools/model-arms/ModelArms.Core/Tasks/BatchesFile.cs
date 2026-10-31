using System.Text;
using System.Text.Json;
using Bench.Domain;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Tasks;

/// <summary><c>work/batches.json</c>: the bytes of <c>tasks-plan.mjs:50</c> (<c>JSON.stringify(…, null, 2)</c>, no trailing newline) and their inverse. The harness writes the file once and compares it on re-entry (E4 §8 item 8); the reader refuses what the writer cannot produce.</summary>
public static class BatchesFile
{
    private static readonly string[] TopMembers = ["batches", "tasks"];

    private static readonly string[] TaskMembers = ["files", "brief", "depends", "scenarios"];

    /// <summary>The object <c>{"batches": [[…]…], "tasks": {"&lt;id&gt;": {"files", "brief": "&lt;workFolder&gt;/&lt;name&gt;", "depends", "scenarios"}}}</c> in the order the plan holds them.</summary>
    public static string Write(TaskBatches batches, string workFolder) =>
        ObjectiveJson.Indented(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("batches");
            foreach (var batch in batches.Batches)
            {
                Strings(writer, batch);
            }

            writer.WriteEndArray();
            writer.WriteStartObject("tasks");
            foreach (var task in batches.Tasks)
            {
                writer.WriteStartObject(task.Id);
                writer.WritePropertyName("files");
                Strings(writer, task.Files);
                writer.WriteString("brief", workFolder + "/" + task.Brief);
                writer.WritePropertyName("depends");
                Strings(writer, task.Depends);
                writer.WritePropertyName("scenarios");
                Strings(writer, task.Scenarios);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });

    /// <summary>The plan <paramref name="json"/> holds, or why it is not a <see cref="Write"/> of one: a member missing or unknown, a batch naming a task the file does not hold, a task in no batch or in two, an empty batch, a brief that is not one file name under <paramref name="workFolder"/>.</summary>
    public static Outcome<TaskBatches> Read(string json, string workFolder)
    {
        try
        {
            using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Shaped(document.RootElement, workFolder)
                : Outcome<TaskBatches>.Failure("batches.json is not an object");
        }
        catch (JsonException exception)
        {
            return Outcome<TaskBatches>.Failure("batches.json is not JSON: " + exception.Message);
        }
    }

    private static void Strings(Utf8JsonWriter writer, IEnumerable<string> items)
    {
        writer.WriteStartArray();
        foreach (var item in items)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }

    private static Outcome<TaskBatches> Shaped(JsonElement root, string workFolder)
    {
        var batches = Batches(JsText.Member(root, "batches"));
        var tasks = Tasks(JsText.Member(root, "tasks"), workFolder);
        var listed = batches.Match(found => found, _ => []);
        var planned = tasks.Match(found => found, _ => []);
        List<string> problems = [.. Unknown(root, TopMembers, "batches.json"), .. Reasons(batches), .. Reasons(tasks)];
        if (problems.Count == 0)
        {
            problems.AddRange(Crossed(listed, planned));
        }

        return problems.Count == 0 ? Outcome<TaskBatches>.Success(new TaskBatches(listed, planned)) : Outcome<TaskBatches>.Failure(string.Join('\n', problems));
    }

    private static IEnumerable<string> Reasons<T>(Outcome<T> outcome) => outcome.Match(_ => (IEnumerable<string>)[], reason => [reason]);

    private static IEnumerable<string> Unknown(JsonElement obj, string[] known, string who) =>
        obj.EnumerateObject().Select(property => property.Name).Where(name => !known.Contains(name)).Select(name => $"{who} has an unknown member '{name}'");

    private static Outcome<IReadOnlyList<IReadOnlyList<string>>> Batches(JsonElement? member)
    {
        const string Shape = "batches is missing or not a list of lists of task ids";
        if (member is not { ValueKind: JsonValueKind.Array } lists)
        {
            return Outcome<IReadOnlyList<IReadOnlyList<string>>>.Failure(Shape);
        }

        var batches = lists.EnumerateArray().Select(list => TextList(list)).ToList();
        return batches.Any(batch => batch is Outcome<IReadOnlyList<string>>.Fail)
            ? Outcome<IReadOnlyList<IReadOnlyList<string>>>.Failure(Shape)
            : Outcome<IReadOnlyList<IReadOnlyList<string>>>.Success([.. batches.Select(batch => batch.Match(found => found, _ => []))]);
    }

    /// <summary>An array of texts, or the failure that says it is not one.</summary>
    private static Outcome<IReadOnlyList<string>> TextList(JsonElement? member)
    {
        if (member is not { ValueKind: JsonValueKind.Array } array)
        {
            return Outcome<IReadOnlyList<string>>.Failure("not a list of texts");
        }

        var items = array.EnumerateArray().ToList();
        return items.All(item => item.ValueKind == JsonValueKind.String)
            ? Outcome<IReadOnlyList<string>>.Success([.. items.Select(item => item.GetString() ?? string.Empty)])
            : Outcome<IReadOnlyList<string>>.Failure("not a list of texts");
    }

    private static Outcome<IReadOnlyList<PlannedTask>> Tasks(JsonElement? member, string workFolder)
    {
        if (member is not { ValueKind: JsonValueKind.Object } tasks)
        {
            return Outcome<IReadOnlyList<PlannedTask>>.Failure("tasks is missing or not an object");
        }

        var twice = tasks.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"tasks names {group.Key} twice");
        var results = tasks.EnumerateObject().DistinctBy(property => property.Name).Select(property => Task(property.Name, property.Value, workFolder)).ToList();
        List<string> problems = [.. twice, .. results.SelectMany(Reasons)];
        return problems.Count == 0
            ? Outcome<IReadOnlyList<PlannedTask>>.Success([.. results.SelectMany(result => result.Match(found => (IEnumerable<PlannedTask>)[found], _ => []))])
            : Outcome<IReadOnlyList<PlannedTask>>.Failure(string.Join('\n', problems));
    }

    private static Outcome<PlannedTask> Task(string id, JsonElement value, string workFolder)
    {
        if (!TaskIds.IsTaskId(id) || value.ValueKind != JsonValueKind.Object)
        {
            return Outcome<PlannedTask>.Failure($"tasks.{id} is not an object named by a task id");
        }

        var files = TextList(JsText.Member(value, "files"));
        var depends = TextList(JsText.Member(value, "depends"));
        var scenarios = TextList(JsText.Member(value, "scenarios"));
        var brief = Brief(id, JsText.Member(value, "brief"), workFolder);
        List<string> problems =
        [
            .. Unknown(value, TaskMembers, id),
            .. Reasons(files).Select(_ => $"{id}'s files is missing or not a list of texts"),
            .. Reasons(depends).Select(_ => $"{id}'s depends is missing or not a list of texts"),
            .. Reasons(scenarios).Select(_ => $"{id}'s scenarios is missing or not a list of texts"),
            .. Reasons(brief),
        ];
        return problems.Count == 0
            ? Outcome<PlannedTask>.Success(new PlannedTask(id, files.Match(found => found, _ => []), brief.Match(found => found, _ => string.Empty), depends.Match(found => found, _ => []), scenarios.Match(found => found, _ => [])))
            : Outcome<PlannedTask>.Failure(string.Join('\n', problems));
    }

    /// <summary>The file name of a brief written as <c>&lt;workFolder&gt;/&lt;name&gt;</c>; a failure when it is anything else.</summary>
    private static Outcome<string> Brief(string id, JsonElement? member, string workFolder)
    {
        var prefix = workFolder + "/";
        var text = member is { ValueKind: JsonValueKind.String } written ? written.GetString() ?? string.Empty : string.Empty;
        var name = text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : string.Empty;
        return TasksPlan.IsFileName(name) ? Outcome<string>.Success(name) : Outcome<string>.Failure($"{id}'s brief is not one file name under {prefix}");
    }

    /// <summary>Every task in exactly one batch, no empty batch, and every task a batch or a dependency names is one the file holds.</summary>
    private static IEnumerable<string> Crossed(IReadOnlyList<IReadOnlyList<string>> batches, IReadOnlyList<PlannedTask> tasks)
    {
        var known = tasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        var named = batches.SelectMany(batch => batch).ToList();
        return
        [
            .. batches.Select((batch, index) => (batch, index)).Where(entry => entry.batch.Count == 0).Select(entry => $"batches holds an empty batch (batch {entry.index + 1})"),
            .. batches.SelectMany((batch, index) => batch.Where(id => !known.Contains(id)).Select(id => $"batch {index + 1} names {id}, which is not under tasks")),
            .. named.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1 && known.Contains(group.Key)).Select(group => $"{group.Key} is in batches twice"),
            .. tasks.Where(task => !named.Contains(task.Id)).Select(task => $"{task.Id} is in no batch"),
            .. tasks.SelectMany(task => task.Depends.Where(dependency => !known.Contains(dependency)).Select(dependency => $"{task.Id} depends on {dependency}, which is not under tasks")),
        ];
    }
}
