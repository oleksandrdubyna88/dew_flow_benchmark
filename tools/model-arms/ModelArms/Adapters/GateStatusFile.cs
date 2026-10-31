using System.Text;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The gate's status file, <c>notes/gate.json</c>, read as what it is: a file the gate MODEL controls (D17). It is read without following a link, without waiting past a ceiling and without reading more than <see cref="LimitBytes"/> — through
/// <see cref="BoundedFile"/> (C16, E3.S0), which owns the two roads (the kernel's <c>O_NOFOLLOW | O_NONBLOCK</c> open on Linux x86-64, the runtime's under a ceiling elsewhere). Three answers, never two: the file is <see cref="Answer.Absent"/> (the model wrote none —
/// the shell's "missing"), it is <see cref="Answer.Text"/>, or it is there and <see cref="Answer.Refused"/> — a link, a FIFO, a folder, a file past the limit — which is NOT "missing": finishing a gate whose answer could not be read would let a blocked gate pass, so the caller stops
/// resumable with the reason.
/// <para>
/// <b>What the bytes may be</b> (E2.S7 re-check R6): UTF-8 text. A leading UTF-8 byte order mark (<c>EF BB BF</c>) is taken off before decoding, on both roads — kept, it is the character U+FEFF in front of <c>{</c>, the JSON does not parse and a BLOCKED gate would read as
/// <c>missing</c> and pass. A UTF-16 byte order mark (<c>FF FE</c>, <c>FE FF</c>) and any NUL byte (UTF-16 text without a mark, a file padded with zeros by a crashed write) are REFUSED with the reason named, never decoded into garbage and read as missing. CRLF is JSON whitespace and needs nothing.
/// </para>
/// <para>
/// <b>Residual:</b> the portable road tells a FIFO or a device by its ceiling, not by its kind (<see cref="BoundedFile"/>). A link in a PARENT folder of <c>notes/</c> is not looked at (the arm folder is the operator's).
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

    /// <summary>The status file's answer on this host's bounded road.</summary>
    public static Answer Read(string path) => Read(path, PlatformFactsReader.CurrentRoad.Bounded);

    /// <summary>The same with the road handed in (<see cref="BoundedFile"/>, C16).</summary>
    public static Answer Read(string path, BoundedRoad road) => Answered(path, BoundedFile.Read(path, LimitBytes, road));

    /// <summary>The portable road: the same three answers from what <see cref="FileInfo"/> can see, whatever the host.</summary>
    internal static Answer ReadFallback(string path, int limit) => Answered(path, BoundedFile.Read(path, limit, BoundedRoad.Portable));

    /// <summary>The ONE place the bytes of either road become text: a refusal stays a refusal, UTF-8 text loses a leading byte order mark, and bytes that are not UTF-8 text are refused by name.</summary>
    private static Answer Answered(string path, BoundedRead read) =>
        read switch
        {
            BoundedRead.Bytes bytes => Decoded(path, bytes.Value),
            BoundedRead.Refused refused => new Answer.Refused(refused.Reason),
            _ => new Answer.Absent(),
        };

    private static Answer Decoded(string path, byte[] bytes) =>
        Unreadable(path, bytes) is { Length: > 0 } why ? new Answer.Refused(why) : new Answer.Text(Encoding.UTF8.GetString(WithoutMark(bytes)));

    /// <summary>Why these bytes are not UTF-8 text, or <c>""</c>.</summary>
    private static string Unreadable(string path, byte[] bytes) =>
        IsUtf16Mark(bytes) ? $"{path} is UTF-16 text (it starts with a UTF-16 byte order mark) — the status is read as UTF-8"
        : Array.IndexOf(bytes, (byte)0) >= 0 ? $"{path} holds a NUL byte — it is not UTF-8 text"
        : string.Empty;

    private static bool IsUtf16Mark(byte[] bytes) => bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..];

    private static ReadOnlySpan<byte> WithoutMark(byte[] bytes) => bytes is [0xEF, 0xBB, 0xBF, ..] ? bytes.AsSpan(3) : bytes;
}
