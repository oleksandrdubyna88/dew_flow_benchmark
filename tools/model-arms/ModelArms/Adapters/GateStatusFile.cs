using System.Text;
using Bench.Domain;

namespace ModelArms.Adapters;

/// <summary>The gate's status file, <c>notes/gate.json</c>, read as what it is: a file the gate MODEL controls (D17). It is read without waiting, without following a link and without reading more than <see cref="LimitBytes"/> — through
/// <see cref="KernelFacts.ReadSmall"/> (the road the gate lock's holder note takes: <c>O_NONBLOCK | O_NOFOLLOW | O_NOCTTY | O_CLOEXEC</c>, the descriptor judged a regular file and compared with the path) where that exists (Linux x86-64), and through
/// <see cref="ReadFallback"/> elsewhere. Three answers, never two: the file is <see cref="Answer.Absent"/> (the model wrote none — the shell's "missing"), it is <see cref="Answer.Text"/>, or it is there and <see cref="Answer.Refused"/> — a link, a FIFO,
/// a folder, a file past the limit — which is NOT "missing": finishing a gate whose answer could not be read would let a blocked gate pass, so the caller stops resumable with the reason.
/// <para>
/// <b>What the bytes may be</b> (E2.S7 re-check R6): UTF-8 text. A leading UTF-8 byte order mark (<c>EF BB BF</c>) is taken off before decoding, on both roads — kept, it is the character U+FEFF in front of <c>{</c>, the JSON does not parse and a BLOCKED gate would read as
/// <c>missing</c> and pass. A UTF-16 byte order mark (<c>FF FE</c>, <c>FE FF</c>) and any NUL byte (UTF-16 text without a mark, a file padded with zeros by a crashed write) are REFUSED with the reason named, never decoded into garbage and read as missing. CRLF is JSON whitespace and needs nothing.
/// </para>
/// <para>
/// <b>Residual:</b> the fallback cannot tell a FIFO or a device from a file (.NET names no such attribute on Unix), so off Linux x86-64 a FIFO at <c>gate.json</c> is opened as any file and waits for a writer; the limit and the link check hold there.
/// A link in a PARENT folder of <c>notes/</c> is not looked at (the arm folder is the operator's).
/// </para></summary>
internal static class GateStatusFile
{
    /// <summary>The most a <c>gate.json</c> may hold: a status word and a reason are far below it, and what the model wrote past it is never read.</summary>
    public const int LimitBytes = 64 * 1024;

    public abstract record Answer
    {
        private Answer()
        {
        }

        /// <summary>No file: the gate model wrote no status.</summary>
        public sealed record Absent : Answer;

        public sealed record Text(string Value) : Answer;

        /// <summary>The file is there and cannot be read as a status; the reason names the file and what is wrong with it.</summary>
        public sealed record Refused(string Reason) : Answer;
    }

    public static Answer Read(string path) => Judged(path) ?? Answered(path, KernelFacts.CanReadSmall ? KernelFacts.ReadSmall(path, LimitBytes) : Portable(path, LimitBytes));

    /// <summary>The portable road: the same three answers from what <see cref="FileInfo"/> can see — a link refuses, a folder or a device is not a regular file, and at most <paramref name="limit"/> + 1 bytes are read, shared with a writer.</summary>
    internal static Answer ReadFallback(string path, int limit) => Judged(path) ?? Answered(path, Portable(path, limit));

    /// <summary>The answer the path alone decides, or <c>null</c> when the file must be opened: a link is refused (nothing it names is read), a path that names nothing is absent.</summary>
    private static Answer? Judged(string path) =>
        IsLink(path) ? new Answer.Refused($"{path} is a link — what it names is not read (D17)")
        : !Path.Exists(path) ? new Answer.Absent()
        : null;

    /// <summary>True for a symbolic link, a dangling one included: the file's own attributes say so (<c>ReparsePoint</c>) without reading what it names — the module reads a link's text in one place only (<c>RootFacts</c>). A path that names nothing reads as <c>-1</c>, which is no link.</summary>
    private static bool IsLink(string path)
    {
        var attributes = new FileInfo(path).Attributes;
        return attributes != (FileAttributes)(-1) && attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    /// <summary>The ONE place the bytes of either road become text: a refusal stays a refusal, UTF-8 text loses a leading byte order mark, and bytes that are not UTF-8 text are refused by name.</summary>
    private static Answer Answered(string path, Outcome<byte[]> bytes) => bytes.Match(read => Decoded(path, read), reason => new Answer.Refused(reason));

    private static Answer Decoded(string path, byte[] bytes) =>
        Unreadable(path, bytes) is { Length: > 0 } why ? new Answer.Refused(why) : new Answer.Text(Encoding.UTF8.GetString(WithoutMark(bytes)));

    /// <summary>Why these bytes are not UTF-8 text, or <c>""</c>.</summary>
    private static string Unreadable(string path, byte[] bytes) =>
        IsUtf16Mark(bytes) ? $"{path} is UTF-16 text (it starts with a UTF-16 byte order mark) — the status is read as UTF-8"
        : Array.IndexOf(bytes, (byte)0) >= 0 ? $"{path} holds a NUL byte — it is not UTF-8 text"
        : string.Empty;

    private static bool IsUtf16Mark(byte[] bytes) => bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..];

    private static ReadOnlySpan<byte> WithoutMark(byte[] bytes) => bytes is [0xEF, 0xBB, 0xBF, ..] ? bytes.AsSpan(3) : bytes;

    private static Outcome<byte[]> Portable(string path, int limit) =>
        NotRegular(path) ? Outcome<byte[]>.Failure($"{path} is not a regular file (D17)") : Bounded(path, limit);

    private static bool NotRegular(string path) => Directory.Exists(path) || new FileInfo(path).Attributes.HasFlag(FileAttributes.Device);

    /// <summary>At most <paramref name="limit"/> bytes: the read asks for one more, and a file that gives it is refused.</summary>
    private static Outcome<byte[]> Bounded(string path, int limit)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Counted(path, limit, ReadUpTo(stream, limit + 1));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<byte[]>.Failure($"{path} cannot be read: {ex.Message}");
        }
    }

    private static byte[] ReadUpTo(Stream stream, int count)
    {
        var buffer = new byte[count];
        var total = 0;
        while (total < count && stream.Read(buffer, total, count - total) is var n and > 0)
        {
            total += n;
        }

        return buffer[..total];
    }

    private static Outcome<byte[]> Counted(string path, int limit, byte[] read) =>
        read.Length > limit ? Outcome<byte[]>.Failure($"{path} is bigger than {limit} bytes") : Outcome<byte[]>.Success(read);
}
