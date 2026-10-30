using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What one bounded read of a model-written file came to. Three states, never two: the file is <see cref="Absent"/> (nothing by that name — the model wrote none), it is <see cref="Bytes"/>, or it is there and <see cref="Refused"/> — a link, a
/// FIFO, a folder, a file past the limit, a read that did not finish in time — which is NOT "absent": a caller that took a refusal for a missing file would let a blocked answer pass.</summary>
internal abstract record BoundedRead
{
    private BoundedRead() { }

    public sealed record Absent : BoundedRead;

    public sealed record Bytes(byte[] Value) : BoundedRead;

    /// <summary>The file is there and cannot be read as a small regular file; the reason names it and what is wrong.</summary>
    public sealed record Refused(string Reason) : BoundedRead;
}

/// <summary>C16, the one road every reader of a model-written file takes (D17): <b>no link followed, no FIFO waited on past a ceiling, never more than <c>limit</c> bytes</b>. A file the model under test can write — the arm's clone, <c>notes/</c>,
/// <c>work/</c>, <c>out/</c>, the suite's CTRF — is read through here, and so are the root's own files the operator owns, because under the lock an arm can still create a file by name in the root. Extracted from <c>GateStatusFile</c>; it and the
/// gate lock's holder note decode on top.
/// <para>
/// <b>Both roads</b> refuse a link by the path's own attributes (<c>ReparsePoint</c>, a dangling link included, nothing it names read), read a path that names nothing as <see cref="BoundedRead.Absent"/>, and refuse a folder.
/// <see cref="BoundedRoad.Kernel"/> (Linux x86-64 with a working <c>statx</c>) is <see cref="KernelFacts.ReadSmall"/>: <c>O_NOFOLLOW | O_NONBLOCK</c>, the descriptor judged a regular file and the same inode as the path, at most <c>limit + 1</c> bytes.
/// <see cref="BoundedRoad.Portable"/> is the runtime's <c>FileStream(Open, Read, ReadWrite | Delete)</c> and the <c>limit + 1</c> read, the WHOLE open-and-read run on a thread under <see cref="PlatformRoads.BoundedCeilingSeconds"/> seconds: a FIFO with no writer blocks the open and one with a silent writer
/// blocks the read, and neither is told by its kind there — the ceiling answers. The thread is left to end when its open returns (a background thread: a verb's process ends it); the alternative is a hang no wall covers, because this read is the harness's own, not a child's.
/// </para>
/// <para>
/// <b>Residuals, said:</b> on the portable road the link check PRECEDES the open, so a link swapped in between them is followed (the runtime has no no-follow open); a device file is read as its bytes up to the limit or refused past it (the runtime never sets
/// <c>FileAttributes.Device</c> on Unix, so a test for it was an inert measure); a link in a PARENT folder is not looked at.
/// </para></summary>
internal static class BoundedFile
{
    private static readonly TimeSpan OpenCeiling = TimeSpan.FromSeconds(PlatformRoads.BoundedCeilingSeconds);

    public static BoundedRead Read(string path, int limit, PlatformRoad road) => Read(path, limit, road.Bounded);

    public static BoundedRead Read(string path, int limit, BoundedRoad road) => Read(path, limit, road, OpenCeiling);

    /// <summary>The same with the ceiling handed in: a test that plants a FIFO does not wait the production five seconds for each.</summary>
    internal static BoundedRead Read(string path, int limit, BoundedRoad road, TimeSpan ceiling) => Read(path, limit, road, ceiling, Judged);

    /// <summary>The same with the path's own judgement handed in: the only way a test can make the link check stall, to see the ceiling cover it.</summary>
    internal static BoundedRead Read(string path, int limit, BoundedRoad road, TimeSpan ceiling, Func<string, BoundedRead?> judge) =>
        road == BoundedRoad.Kernel ? judge(path) ?? Kernel(path, limit) : Portable(path, limit, ceiling, judge);

    /// <summary>The answer the path alone decides, or <c>null</c> when the file must be opened: a link is refused (nothing it names is read), a path that names nothing is absent, and a path the host cannot look at (a parent folder the user may not search) is
    /// <see cref="BoundedRead.Refused"/> — never absent, never a throw: C16 answers for every input.</summary>
    private static BoundedRead? Judged(string path)
    {
        try
        {
            return IsLink(path) ? new BoundedRead.Refused($"{path} is a link — what it names is not read (D17)")
                : !Path.Exists(path) ? new BoundedRead.Absent()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BoundedRead.Refused($"{path} cannot be looked at: {ex.Message}");
        }
    }

    /// <summary>True for a symbolic link, a dangling one included: the file's own attributes say so (<c>ReparsePoint</c>) without reading what it names. A path that names nothing reads as <c>-1</c>, which is no link; a folder the user may not search makes the runtime throw, which <see cref="Judged"/> turns into a refusal.</summary>
    private static bool IsLink(string path)
    {
        var attributes = new FileInfo(path).Attributes;
        return attributes != (FileAttributes)(-1) && attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static BoundedRead Kernel(string path, int limit) =>
        KernelFacts.ReadSmall(path, limit).Match<BoundedRead>(bytes => new BoundedRead.Bytes(bytes), reason => new BoundedRead.Refused(reason));

    /// <summary>The WHOLE read on a background thread under the ceiling — the path's own check, the folder test and the open and read alike, because a stalled file system stalls any of them and the refusal's words say so.</summary>
    private static BoundedRead Portable(string path, int limit, TimeSpan ceiling, Func<string, BoundedRead?> judge)
    {
        var done = new TaskCompletionSource<BoundedRead>(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() => done.SetResult(Whole(path, limit, judge))) { IsBackground = true, Name = "bounded-file-read" }.Start();
        return done.Task.Wait(ceiling)
            ? done.Task.Result
            : new BoundedRead.Refused($"{path} was not read within {ceiling.TotalSeconds:0.##} s — a FIFO with no writer, or a stalled file system; not read (D17)");
    }

    /// <summary>The thread's whole work. A detached edge: nothing here may escape as an exception — an unhandled one on this thread would end the process — so every failure is a refusal with its words.</summary>
    private static BoundedRead Whole(string path, int limit, Func<string, BoundedRead?> judge)
    {
        try
        {
            return judge(path) ?? (Directory.Exists(path) ? new BoundedRead.Refused($"{path} is not a regular file (D17)") : OpenedAndRead(path, limit));
        }
        catch (Exception ex)
        {
            return new BoundedRead.Refused($"{path} cannot be read: {ex.Message}");
        }
    }

    private static BoundedRead OpenedAndRead(string path, int limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var read = ReadUpTo(stream, limit + 1);
        return read.Length > limit ? new BoundedRead.Refused($"{path} is bigger than {limit} bytes") : new BoundedRead.Bytes(read);
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
}
