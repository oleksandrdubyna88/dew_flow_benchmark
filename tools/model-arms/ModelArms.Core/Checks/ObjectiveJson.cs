using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bench.Domain.Trace;

namespace ModelArms.Core.Checks;

/// <summary>The writer of <c>objective.json</c> and of the summary line: <c>JSON.stringify(out, null, 2)</c> and <c>JSON.stringify(out)</c> of the shell (<c>objective-checks.sh:65-67</c>), made by <c>Utf8JsonWriter</c> so a string is ESCAPED and never
/// concatenated (an arm names files and tests; one holding a quote must not be able to end its own string). The relaxed encoder is the one <c>McpConfig</c> uses, with the residual its doc names: the PARSED value equals the input for every
/// well-formed string, and the BYTES equal <c>JSON.stringify</c>'s for printable BMP text — a character outside the BMP, DEL and a C1 control are written as escapes where <c>JSON.stringify</c> writes them raw.</summary>
internal static class ObjectiveJson
{
    private static readonly JsonWriterOptions Pretty = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonWriterOptions Compact = Pretty with { Indented = false };

    /// <summary>Two-space indented text, <c>\n</c> line ends and no trailing newline.</summary>
    public static string Indented(Action<Utf8JsonWriter> write) => Written(Pretty, write);

    public static string OneLine(Action<Utf8JsonWriter> write) => Written(Compact, write);

    /// <summary>A count as a JSON number, or <c>null</c> when it was not captured — the shell's <c>NaN</c>, never a zero.</summary>
    public static void Count(this Utf8JsonWriter writer, string name, CapturedCount count)
    {
        if (count.WasCaptured)
        {
            writer.WriteNumber(name, count.Value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    public static void Strings(this Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string Written(JsonWriterOptions options, Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
