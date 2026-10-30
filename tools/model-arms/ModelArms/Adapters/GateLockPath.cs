using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What the gate lock's path is, read WITHOUT following a link (D17): the one question every road into <see cref="FileGateLock"/> asks before it opens anything. The facts come from the module's own trust reader
/// (<see cref="RootFacts"/>: <c>statx</c> or the runtime's facts, by the road), the one that judges <c>experiment.json</c> and <c>scenarios.json</c>, so a link is refused by the same sentence there and here. Off POSIX nothing was read and nothing is judged: the
/// gate has already said the trust check was not performed.</summary>
internal abstract record GateLockPath
{
    private GateLockPath() { }

    /// <summary>Nothing is at the path (not even a dangling link).</summary>
    public sealed record Missing : GateLockPath;

    /// <summary>A regular file; <see cref="FileFacts.Identity"/> says which inode.</summary>
    public sealed record Regular(FileFacts Facts) : GateLockPath;

    /// <summary>No POSIX facts exist on this platform.</summary>
    public sealed record Unjudged : GateLockPath;

    public sealed record Refused(string Reason) : GateLockPath;

    public sealed record Unread(string Reason) : GateLockPath;

    /// <summary>The path as it is now, on this host's road: absent, a regular file, or why neither.</summary>
    public static GateLockPath Of(string path) => Of(path, PlatformFactsReader.CurrentRoad);

    /// <summary>The path as it is now, on the road handed in: <see cref="TrustRoad.NotPerformed"/> (Windows) judges nothing; the others ask whether an entry is there and read its facts as <c>RootFacts</c> does — a link and a folder are refused on the portable road too, a FIFO is not told (the road's line says so).</summary>
    public static GateLockPath Of(string path, PlatformRoad road) =>
        road.Trust == TrustRoad.NotPerformed ? new Unjudged()
        : KernelFacts.Present(path).Match(there => there ? Judged(path, road) : new Missing(), reason => new Unread(reason));

    private static GateLockPath Judged(string path, PlatformRoad road) =>
        RootFacts.ReadFacts(path, FileKind.Regular, road) switch
        {
            FactsRead.Read read => new Regular(read.Facts),
            FactsRead.Refused refused => new Refused(refused.Reason),
            FactsRead.Unread unread => new Unread(unread.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
