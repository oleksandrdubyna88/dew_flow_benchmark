using System.Text;

namespace ModelArms.Adapters;

/// <summary>Lines written into one stream, each as UTF-8 with a <c>"\n"</c>, under one lock and flushed — two readers (a tool's stdout and stderr) write into one log, so a line is never cut by the other's.</summary>
internal sealed class LogLines(Stream stream)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly object _gate = new();

    public void Write(string line)
    {
        var bytes = Utf8.GetBytes(line + "\n");
        lock (_gate)
        {
            stream.Write(bytes);
            stream.Flush();
        }
    }
}
