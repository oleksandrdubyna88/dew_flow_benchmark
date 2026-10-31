using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What the gate lock's path is, read WITHOUT following a link (D17): the one question every road into <see cref="FileGateLock"/> asks before it opens anything. The facts come from the module's own <c>statx</c> reader
/// (<see cref="RootFacts"/>, <see cref="KernelFacts"/>), the one that judges <c>experiment.json</c> and <c>scenarios.json</c>, so a link is refused by the same sentence there and here. Off POSIX nothing was read and nothing is judged: the
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

    /// <summary>The path as it is now: absent, a regular file, or why neither.</summary>
    public static GateLockPath Of(string path) =>
        OperatingSystem.IsWindows() ? new Unjudged()
        : KernelFacts.Present(path).Match(there => there ? Judged(path) : new Missing(), reason => new Unread(reason));

    private static GateLockPath Judged(string path) =>
        RootFacts.ReadFacts(path, FileKind.Regular) switch
        {
            FactsRead.Read read => new Regular(read.Facts),
            FactsRead.Refused refused => new Refused(refused.Reason),
            FactsRead.Unread unread => new Unread(unread.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
}
