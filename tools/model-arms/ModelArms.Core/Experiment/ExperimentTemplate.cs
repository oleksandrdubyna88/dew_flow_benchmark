using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ModelArms.Core.Experiment;

/// <summary>The <c>init</c> template (D18), derived from the worked example (D24) so the two cannot drift: the example's property names, structure and
/// comments are kept; every string value becomes <see cref="Blank"/> except the values of the top-level members <c>schema</c>, <c>scenarios</c> and
/// <c>baseline</c> (a fixed schema name and two file names the operator has no reason to change); every number becomes 0; array lengths are kept. Pure: the
/// example arrives as text, the template leaves as text, and Core opens nothing.
/// <para>
/// The comments are written back as <c>/* … */</c> (the writer's one comment form); the experiment reader skips both forms. The writer does not escape
/// <c>&lt;</c> and <c>&gt;</c>, so the placeholder reads <c>&lt;fill me&gt;</c> in the file.
/// </para></summary>
public static class ExperimentTemplate
{
    /// <summary>The placeholder every blanked string carries.</summary>
    public const string Blank = "<fill me>";

    private static readonly string[] KeptMembers = ["schema", "scenarios", "baseline"];

    private static readonly JsonReaderOptions ReadAllowingComments = new() { CommentHandling = JsonCommentHandling.Allow };

    private static readonly JsonWriterOptions WriteIndented = new() { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>What the template writes for each kind of token the reader yields (the reader never yields <c>None</c>): structure and names as they were, a
    /// comment as it was, a string blanked unless kept, a number as 0, the rest as they were.</summary>
    private static readonly Dictionary<JsonTokenType, Action<Utf8JsonWriter, Token>> Writes = new()
    {
        [JsonTokenType.StartObject] = (writer, _) => writer.WriteStartObject(),
        [JsonTokenType.EndObject] = (writer, _) => writer.WriteEndObject(),
        [JsonTokenType.StartArray] = (writer, _) => writer.WriteStartArray(),
        [JsonTokenType.EndArray] = (writer, _) => writer.WriteEndArray(),
        [JsonTokenType.PropertyName] = (writer, token) => writer.WritePropertyName(token.Text),
        [JsonTokenType.Comment] = (writer, token) => writer.WriteCommentValue(token.Text + " "),
        [JsonTokenType.String] = (writer, token) => writer.WriteStringValue(token.IsKept ? token.Text : Blank),
        [JsonTokenType.Number] = (writer, _) => writer.WriteNumberValue(0),
        [JsonTokenType.True] = (writer, _) => writer.WriteBooleanValue(true),
        [JsonTokenType.False] = (writer, _) => writer.WriteBooleanValue(false),
        [JsonTokenType.Null] = (writer, _) => writer.WriteNullValue(),
    };

    /// <summary>One token of the example and what the template does with it: <paramref name="Text"/> is the property name, the string value or the comment;
    /// <paramref name="IsKept"/> is true for a string that is the direct value of a kept top-level member.</summary>
    private sealed record Token(JsonTokenType Type, string Text, bool IsKept);

    /// <summary>The template for <paramref name="exampleText"/>. The text must be valid JSON with comments; a reader error is the caller's, the example being a file that ships.</summary>
    public static string From(string exampleText) =>
        JsonText.Write(
            WriteIndented,
            writer =>
            {
                foreach (var token in CommentsBehindTheirName(Tokens(Encoding.UTF8.GetBytes(exampleText))))
                {
                    Write(writer, token);
                }
            }) + "\n";

    private static List<Token> Tokens(byte[] example)
    {
        var reader = new Utf8JsonReader(example, ReadAllowingComments);
        var tokens = new List<Token>();
        var topMember = string.Empty;
        while (reader.Read())
        {
            var text = TextOf(ref reader);
            topMember = TopMemberAfter(topMember, reader.TokenType, reader.CurrentDepth, text);
            tokens.Add(new Token(reader.TokenType, text, IsKeptValue(reader.CurrentDepth, topMember)));
        }

        return tokens;
    }

    /// <summary>The writer puts a comment right after the value written before it, so the example's comment about <c>target</c> would end up after
    /// <c>schema</c>'s value, ahead of the comma. A comment is held back and written behind the property name that follows it (still ahead of that member's value),
    /// where it describes what it sits next to; a comment with no name after it (before a closing bracket, or before an array item) stays where it was.</summary>
    private static List<Token> CommentsBehindTheirName(IReadOnlyList<Token> tokens)
    {
        var ordered = new List<Token>();
        var held = new List<Token>();
        foreach (var token in tokens)
        {
            if (token.Type == JsonTokenType.Comment)
            {
                held.Add(token);
                continue;
            }

            ordered.AddRange(token.Type == JsonTokenType.PropertyName ? [token, .. held] : [.. held, token]);
            held.Clear();
        }

        ordered.AddRange(held);
        return ordered;
    }

    /// <summary>The top-level member the walk is inside: a property name at depth 1 starts a new one, anything else leaves it as it was.</summary>
    private static string TopMemberAfter(string current, JsonTokenType type, int depth, string text) =>
        type == JsonTokenType.PropertyName && depth == 1 ? text : current;

    /// <summary>A token at depth 1 is the direct value of the top-level member the walk is inside; a string nested deeper never is.</summary>
    private static bool IsKeptValue(int depth, string topMember) => depth == 1 && KeptMembers.Contains(topMember);

    private static string TextOf(ref Utf8JsonReader reader) =>
        reader.TokenType switch
        {
            JsonTokenType.PropertyName or JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Comment => reader.GetComment(),
            _ => string.Empty,
        };

    private static void Write(Utf8JsonWriter writer, Token token) => Writes[token.Type](writer, token);
}
