using System.Text.Json.Nodes;

namespace ModelArms.Core.Experiment;

/// <summary>The one rule that keeps the <c>init</c> template from reading as a filled file: a string still equal to <see cref="ExperimentTemplate.Blank"/>, at any path.
/// Each field's own validator rejects the placeholder only when it happens to dislike the text (an absolute path, a sha, a choice); a free text such as <c>known</c>,
/// <c>steps.code.model</c> or <c>target.planBranch</c> would take it. The rule is on the reader's one road in, not on the fields, so a field added later is covered.</summary>
internal static class UnfilledValues
{
    /// <summary>The dotted paths (the reader's own spelling: <c>target.suite.command[0]</c>) of every string under <paramref name="node"/> that is still the placeholder.</summary>
    public static IEnumerable<string> Paths(JsonNode? node, string path) =>
        node switch
        {
            JsonObject members => members.SelectMany(member => Paths(member.Value, StrictJson.Join(path, member.Key))),
            JsonArray items => items.SelectMany((item, index) => Paths(item, $"{path}[{index}]")),
            JsonValue value when IsBlank(value) => [path],
            _ => [],
        };

    private static bool IsBlank(JsonValue value) => value.TryGetValue(out string? text) && text == ExperimentTemplate.Blank;

    /// <summary>Records a problem for each unfilled value that no earlier problem already names: a line about the path starts with it, then a space or a colon.</summary>
    public static void Refuse(StrictJson reader, JsonNode root)
    {
        foreach (var path in Paths(root, string.Empty).Where(path => !reader.Problems.Any(problem => Names(problem, path))).ToList())
        {
            reader.Add($"{path} is still the template's {ExperimentTemplate.Blank} — replace it");
        }
    }

    private static bool Names(string problem, string path) =>
        problem.StartsWith(path, StringComparison.Ordinal) && problem.Length > path.Length && problem[path.Length] is ' ' or ':';
}
