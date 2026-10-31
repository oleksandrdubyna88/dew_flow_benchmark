using System.Text;
using System.Text.Json;
using Bench.Domain;

namespace ModelArms.Core.Tasks;

/// <summary>A list member of a task as the shell reads it: <c>t.files ?? []</c> and its kin. <see cref="NotAList"/> is a member that is there and is not an array (the shell iterated a string's characters and threw on the rest); its items read as none.</summary>
internal sealed record ListMember(IReadOnlyList<JsValue> Items, bool NotAList);

/// <summary>One element of <c>tasks</c>, every member as JavaScript would see it — a member the task lacks is <c>undefined</c>, and so is every member of an element that is not an object.</summary>
internal sealed record TaskDraft(JsValue Id, JsValue Brief, ListMember Files, ListMember Depends, ListMember Scenarios);

/// <summary>The one reading of a <c>tasks.json</c>: the elements of its <c>tasks</c> array (none when <c>tasks</c> is not an array, as <c>Array.isArray(doc?.tasks) ? … : []</c>), or the reader's reason when the text is not JSON.</summary>
internal static class TaskDrafts
{
    /// <summary>The text is decoded as UTF-8 first, so a lone surrogate becomes U+FFFD instead of an exception: a model-written file is data.</summary>
    public static Outcome<IReadOnlyList<TaskDraft>> Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
            var identities = new Identities();
            JsonElement? tasks = JsText.Member(document.RootElement, "tasks");
            IReadOnlyList<TaskDraft> drafts = tasks is { ValueKind: JsonValueKind.Array } array
                ? [.. array.EnumerateArray().Select(task => Draft(task, identities))]
                : [];
            return Outcome<IReadOnlyList<TaskDraft>>.Success(drafts);
        }
        catch (JsonException exception)
        {
            return Outcome<IReadOnlyList<TaskDraft>>.Failure(exception.Message);
        }
    }

    private static TaskDraft Draft(JsonElement task, Identities identities) =>
        new(
            identities.Value(JsText.Member(task, "id")),
            identities.Value(JsText.Member(task, "brief")),
            identities.List(JsText.Member(task, "files")),
            identities.List(JsText.Member(task, "depends")),
            identities.List(JsText.Member(task, "scenarios")));

    /// <summary>Numbers the values of one reading, so each object and array is its own (<see cref="JsValue.Identity"/>).</summary>
    private sealed class Identities
    {
        private int _last;

        public JsValue Value(JsonElement? element) => new(element?.Clone(), ++_last);

        public ListMember List(JsonElement? member) =>
            member switch
            {
                null or { ValueKind: JsonValueKind.Null } => new ListMember([], false),
                { ValueKind: JsonValueKind.Array } array => new ListMember([.. array.EnumerateArray().Select(item => Value(item))], false),
                _ => new ListMember([], true),
            };
    }
}
