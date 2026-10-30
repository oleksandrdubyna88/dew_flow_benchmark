using System.Text;

namespace ModelArms.Adapters;

/// <summary>The gate's status file, <c>notes/gate.json</c>, read as what it is: a file the gate MODEL controls (D17). It is read without waiting, without following a link and without reading more than <see cref="LimitBytes"/> — through
/// <see cref="KernelFacts.ReadSmall"/> (the road the gate lock's holder note takes: <c>O_NONBLOCK | O_NOFOLLOW | O_NOCTTY | O_CLOEXEC</c>, the descriptor judged a regular file and compared with the path) where that exists (Linux x86-64), and through
/// <see cref="ReadFallback"/> elsewhere. Three answers, never two: the file is <see cref="Answer.Absent"/> (the model wrote none — the shell's "missing"), it is <see cref="Answer.Text"/>, or it is there and <see cref="Answer.Refused"/> — a link, a FIFO,
/// a folder, a file past the limit — which is NOT "missing": finishing a gate whose answer could not be read would let a blocked gate pass, so the caller stops resumable with the reason.
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

    public static Answer Read(string path) => Judged(path) ?? (KernelFacts.CanReadSmall ? Decoded(KernelFacts.ReadSmall(path, LimitBytes)) : Opened(path, LimitBytes));

    /// <summary>The portable road: the same three answers from what <see cref="FileInfo"/> can see — a link target refuses, a folder or a reparse point is not a regular file, and at most <paramref name="limit"/> + 1 bytes are read, shared with a writer.</summary>
    internal static Answer ReadFallback(string path, int limit) => Judged(path) ?? Opened(path, limit);

    /// <summary>The answer the path alone decides, or <c>null</c> when the file must be opened: a link is refused (nothing it names is read), a path that names nothing is absent.</summary>
    private static Answer? Judged(string path) =>
        new FileInfo(path).LinkTarget is not null ? new Answer.Refused($"{path} is a link — its target is not read (D17)")
        : !Path.Exists(path) ? new Answer.Absent()
        : null;

    private static Answer Decoded(Bench.Domain.Outcome<byte[]> bytes) => bytes.Match(read => (Answer)new Answer.Text(Encoding.UTF8.GetString(read)), reason => new Answer.Refused(reason));

    private static Answer Opened(string path, int limit)
    {
        if (Directory.Exists(path) || new FileInfo(path).Attributes.HasFlag(FileAttributes.Device))
        {
            return new Answer.Refused($"{path} is not a regular file (D17)");
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[limit + 1];
            var total = 0;
            while (total < buffer.Length && stream.Read(buffer, total, buffer.Length - total) is var n and > 0)
            {
                total += n;
            }

            return total > limit ? new Answer.Refused($"{path} is bigger than {limit} bytes") : new Answer.Text(Encoding.UTF8.GetString(buffer, 0, total));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Answer.Refused($"{path} cannot be read: {ex.Message}");
        }
    }
}
