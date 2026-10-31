using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ModelArms.Core.Experiment;

/// <summary>The two kinds of path an experiment file holds: an absolute one (a place on the host) and a relative one (a place inside
/// the target or inside the root). Each reads through <see cref="StrictJson"/> and records what is wrong with it.</summary>
internal static partial class PathFields
{
    /// <summary>An absolute path, or the empty string after recording why not.</summary>
    public static string Absolute(StrictJson json, JsonObject obj, string path, string name)
    {
        var text = json.Text(obj, path, name);
        if (text.Length > 0 && !Path.IsPathFullyQualified(text))
        {
            json.Add($"{StrictJson.Join(path, name)} is '{text}' — it must be an absolute path");
        }

        return text;
    }

    /// <summary>A relative path inside <paramref name="inside"/> (not rooted, no <c>..</c> segment), or the empty string after recording why not.</summary>
    public static string Relative(StrictJson json, JsonObject obj, string path, string name, string inside) =>
        CheckRelative(json, json.Text(obj, path, name), StrictJson.Join(path, name), inside);

    /// <summary>One file name: not empty, no separator of either kind, not <c>.</c> or <c>..</c> — a name the harness hands to a tool as a file to write inside a folder of its own.</summary>
    public static string FileName(StrictJson json, JsonObject obj, string path, string name)
    {
        var text = json.Text(obj, path, name);
        if (text.Length > 0 && (text is "." or ".." || text.AsSpan().IndexOfAny('/', '\\') >= 0 || RootedPattern().IsMatch(text)))
        {
            json.Add($"{StrictJson.Join(path, name)} is '{text}' — it must be one file name: no separator, not '.' or '..'");
        }

        return text;
    }

    /// <summary>An array whose every element is a relative path inside <paramref name="inside"/>.</summary>
    public static IReadOnlyList<string> Relatives(StrictJson json, JsonObject obj, string path, string name, string inside)
    {
        var where = StrictJson.Join(path, name);
        return [.. json.Texts(obj, path, name).Select((text, index) => CheckRelative(json, text, $"{where}[{index}]", inside))];
    }

    private static string CheckRelative(StrictJson json, string text, string where, string inside)
    {
        if (text.Length > 0 && !IsInside(text))
        {
            json.Add($"{where} is '{text}' — it must be a relative path inside {inside}: not rooted, no '..' segment");
        }

        return text;
    }

    private static bool IsInside(string text) =>
        !Path.IsPathRooted(text) && !RootedPattern().IsMatch(text) && !text.Split('/', '\\').Contains("..");

    /// <summary>A path rooted in another host's way (<c>C:\x</c> on a Linux host is not "rooted" to the runtime) is refused too.</summary>
    [GeneratedRegex(@"^([/\\]|[A-Za-z]:)")]
    private static partial Regex RootedPattern();
}
