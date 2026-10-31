using System.Buffers;
using System.Text;
using System.Text.Json;

namespace ModelArms.Core;

/// <summary>The one place the module turns a <see cref="Utf8JsonWriter"/>'s output into text: the writer is made over the options, handed to <paramref name="write"/>, disposed (which flushes it) and the bytes decoded as UTF-8.
/// <c>McpConfig</c>, <c>ExperimentTemplate</c>, <c>ArmMap</c> and <c>ObjectiveReport</c> each had their own copy of these five lines (<c>common.reuse-first</c>).</summary>
internal static class JsonText
{
    public static string Write(JsonWriterOptions options, Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
