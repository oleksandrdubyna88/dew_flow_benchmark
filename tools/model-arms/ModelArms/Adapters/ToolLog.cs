using System.Text;
using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>A log the verb makes for one tool: created EXCLUSIVELY (<see cref="FileMode.CreateNew"/>, which never follows a link and fails on anything already at the name) before the tool starts, written line by line while it runs, and — for a log whose
/// text is read back — read through the SAME handle, so no second open of the path can meet a file swapped in since. The reading is bounded by the limit itself (at most <c>limit + 1</c> bytes are taken, so a writer still appending cannot make it read more): a log past the limit is not read.</summary>
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
    public Outcome<string> ReadBack(int limit) => ReadBack(limit, () => { });

    /// <summary>The same with a call made between the length check and the read — the only way a test grows the file at the moment the old read was unbounded.</summary>
    internal Outcome<string> ReadBack(int limit, Action afterLengthChecked)
    {
        _stream.Flush();
        if (_stream.Length > limit)
        {
            return Outcome<string>.Failure($"{Path} is bigger than {limit} bytes");
        }

        afterLengthChecked();
        _stream.Position = 0;
        var bytes = ReadUpTo((long)limit + 1);
        _stream.Seek(0, SeekOrigin.End);
        return bytes.Length > limit ? Outcome<string>.Failure($"{Path} is bigger than {limit} bytes") : Outcome<string>.Success(Utf8.GetString(bytes));
    }

    /// <summary>At most <paramref name="count"/> bytes from where the handle is, in chunks: the read is bounded by the limit the caller states, never by a length looked at earlier — a writer that is still appending cannot make it take more.</summary>
    private byte[] ReadUpTo(long count)
    {
        using var taken = new MemoryStream();
        var chunk = new byte[81920];
        while (taken.Length < count && _stream.Read(chunk, 0, (int)Math.Min(chunk.Length, count - taken.Length)) is var n and > 0)
        {
            taken.Write(chunk, 0, n);
        }

        return taken.ToArray();
    }

    public void Dispose() => _stream.Dispose();
}
