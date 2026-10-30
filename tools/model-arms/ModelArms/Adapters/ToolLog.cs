using System.Text;
using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>A log the verb makes for one tool: created EXCLUSIVELY (<see cref="FileMode.CreateNew"/>, which never follows a link and fails on anything already at the name) before the tool starts, written line by line while it runs, and — for a log whose
/// text is read back — read through the SAME handle, so no second open of the path can meet a file swapped in since. The reading is bounded: a log past the limit is not read.</summary>
internal sealed class ToolLog : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly FileStream _stream;
    private readonly LogLines _lines;

    private ToolLog(string path, FileStream stream)
    {
        Path = path;
        _stream = stream;
        _lines = new LogLines(stream);
    }

    public string Path { get; }

    /// <summary>The stream a launcher writes into.</summary>
    public Stream Stream => _stream;

    /// <summary>The log at <paramref name="path"/>, or why it could not be made — an existing file or link at the name is the usual reason, and is left exactly as it was.</summary>
    public static Outcome<ToolLog> Create(string path)
    {
        try
        {
            return Outcome<ToolLog>.Success(new ToolLog(path, new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<ToolLog>.Failure($"{path} cannot be created: {ex.Message}");
        }
    }

    public void WriteLine(string line) => _lines.Write(line);

    /// <summary>Everything written so far, as UTF-8, read from the start of the handle this log holds; a failure when it is past <paramref name="limit"/> bytes.</summary>
    public Outcome<string> ReadBack(int limit)
    {
        _stream.Flush();
        if (_stream.Length > limit)
        {
            return Outcome<string>.Failure($"{Path} is bigger than {limit} bytes");
        }

        _stream.Position = 0;
        using var reader = new StreamReader(_stream, Utf8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var text = reader.ReadToEnd();
        _stream.Seek(0, SeekOrigin.End);
        return Outcome<string>.Success(text);
    }

    public void Dispose() => _stream.Dispose();
}
