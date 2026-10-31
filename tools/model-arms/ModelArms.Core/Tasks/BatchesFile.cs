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
        var unknown = Unknown(root, TopMembers, "batches.json");
        var batches = Batches(JsText.Member(root, "batches"));
        var tasks = Tasks(JsText.Member(root, "tasks"), workFolder);
        var problems = new List<string>(unknown);
        problems.AddRange(batches.Match(_ => [], reason => (IReadOnlyList<string>)[reason]));
        problems.AddRange(tasks.Match(_ => [], reason => (IReadOnlyList<string>)[reason]));
        if (problems.Count == 0)
        {
            var ok = (batches as Outcome<IReadOnlyList<IReadOnlyList<string>>>.Ok)!.Value;
            var planned = (tasks as Outcome<IReadOnlyList<PlannedTask>>.Ok)!.Value;
            problems.AddRange(Crossed(ok, planned));
            return problems.Count == 0 ? Outcome<TaskBatches>.Success(new TaskBatches(ok, planned)) : Outcome<TaskBatches>.Failure(string.Join('\n', problems));
        }

        return Outcome<TaskBatches>.Failure(string.Join('\n', problems));
    }

    private static IEnumerable<string> Unknown(JsonElement obj, string[] known, string who) =>
        obj.EnumerateObject().Select(property => property.Name).Where(name => !known.Contains(name)).Select(name => $"{who} has an unknown member '{name}'");

    private static Outcome<IReadOnlyList<IReadOnlyList<string>>> Batches(JsonElement? member)
    {
        const string Shape = "batches is missing or not a list of lists of task ids";
        if (member is not { ValueKind: JsonValueKind.Array } lists)
        {
            return Outcome<IReadOnlyList<IReadOnlyList<string>>>.Failure(Shape);
        }

        var batches = lists.EnumerateArray().Select(list => Texts(list)).ToList();
        return batches.Any(batch => batch is null)
            ? Outcome<IReadOnlyList<IReadOnlyList<string>>>.Failure(Shape)
            : Outcome<IReadOnlyList<IReadOnlyList<string>>>.Success([.. batches.Select(batch => (IReadOnlyList<string>)batch!)]);
    }

    /// <summary>The texts of an array of texts; <c>null</c> for anything else.</summary>
    private static string[]? Texts(JsonElement? member)
    {
        if (member is not { ValueKind: JsonValueKind.Array } array)
        {
            return null;
        }

        var items = array.EnumerateArray().ToList();
        return items.All(item => item.ValueKind == JsonValueKind.String) ? [.. items.Select(item => item.GetString()!)] : null;
    }

    private static Outcome<IReadOnlyList<PlannedTask>> Tasks(JsonElement? member, string workFolder)
    {
        if (member is not { ValueKind: JsonValueKind.Object } tasks)
        {
            return Outcome<IReadOnlyList<PlannedTask>>.Failure("tasks is missing or not an object");
        }

        var names = tasks.EnumerateObject().Select(property => property.Name).ToList();
        var problems = new List<string>(names.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => $"tasks names {group.Key} twice"));
        var planned = new List<PlannedTask>();
        foreach (var property in tasks.EnumerateObject().DistinctBy(property => property.Name))
        {
            var task = Task(property.Name, property.Value, workFolder);
            problems.AddRange(task.Match(found => { planned.Add(found); return []; }, reason => (IReadOnlyList<string>)[reason]));
        }

        return problems.Count == 0
            ? Outcome<IReadOnlyList<PlannedTask>>.Success(planned)
            : Outcome<IReadOnlyList<PlannedTask>>.Failure(string.Join('\n', problems));
    }

    private static Outcome<PlannedTask> Task(string id, JsonElement value, string workFolder)
    {
        if (!TaskIds.IsTaskId(id) || value.ValueKind != JsonValueKind.Object)
        {
            return Outcome<PlannedTask>.Failure($"tasks.{id} is not an object named by a task id");
        }

        var files = Texts(JsText.Member(value, "files"));
        var depends = Texts(JsText.Member(value, "depends"));
        var scenarios = Texts(JsText.Member(value, "scenarios"));
        var brief = Brief(JsText.Member(value, "brief"), workFolder);
        var problems = new List<string>(Unknown(value, TaskMembers, id));
        problems.AddRange(new[] { ("files", files), ("depends", depends), ("scenarios", scenarios) }.Where(list => list.Item2 is null).Select(list => $"{id}'s {list.Item1} is missing or not a list of texts"));
        problems.AddRange(brief is null ? [$"{id}'s brief is not one file name under {workFolder}/"] : []);
        return problems.Count == 0
            ? Outcome<PlannedTask>.Success(new PlannedTask(id, files!, brief!, depends!, scenarios!))
            : Outcome<PlannedTask>.Failure(string.Join('\n', problems));
    }

    /// <summary>The file name of a brief written as <c>&lt;workFolder&gt;/&lt;name&gt;</c>; <c>null</c> when it is anything else.</summary>
    private static string? Brief(JsonElement? member, string workFolder)
    {
        var prefix = workFolder + "/";
        return member is { ValueKind: JsonValueKind.String } text && text.GetString()!.StartsWith(prefix, StringComparison.Ordinal)
            && text.GetString()![prefix.Length..] is { Length: > 0 } name && name is not ("." or "..") && name.IndexOfAny(['/', '\\', '\0']) < 0
            ? name
            : null;
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
