using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The trust facts of a path from the RUNTIME's own calls, for a host with no probed kernel reader (macOS, FreeBSD, any other POSIX, a Linux without <c>statx</c> — E3.S0): <see cref="FileInfo.Attributes"/> (what <c>lstat</c> says: a
/// link, a folder — the link is told by its <c>ReparsePoint</c> attribute, never by reading what it names), <see cref="File.GetUnixFileMode"/> (the mode, by <c>stat</c>), <see cref="FileInfo.Length"/> and <see cref="FileSystemInfo.LastWriteTimeUtc"/>. It is code the runtime ships and tests on those hosts, so what it says is TRUE for what it covers — and it cannot say the owner, the
/// inode, the device, the change time or whether a file is a FIFO: those are <c>null</c> or <see cref="FileKind.Unknown"/>, never a zero, and the road's printed line names them. A <c>lstat</c> offset written from headers for a Mac would read a WRONG owner or mode without an error, which is why none is written.
/// <para>
/// <b>Residual:</b> the link judgement and the mode are two calls; a link made in the window between them has its TARGET's mode read. The gate's re-read (<c>RootTrust.Changes</c> by kind, mode, length and write time) and the lock's own checks bound it; a swap that keeps the length and write time is invisible
/// to the witness, by design, and named as unverified.
/// </para></summary>
internal static class PortableFacts
{
    /// <summary>The facts of the file at the NORMALISED <paramref name="full"/> path itself (a link is reported as a link, its target not read). A host that will not say — an absent entry, a folder the user may not search — is <see cref="FactsRead.Unread"/> with its words, never an exception.</summary>
    public static FactsRead Read(string full)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FactsRead.Unread($"the mode of {full} is a POSIX fact; RootTrust declares the check not performed on Windows");
        }

        try
        {
            var info = new FileInfo(full);
            return IsLink(info) ? Facts(full, UnixFileMode.None, FileKind.Link, 0, 0) : Judged(full, info);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FactsRead.Unread($"{full}: {ex.Message}");
        }
    }

    /// <summary>True for a symbolic link, a dangling one included, by the entry's own attributes (<c>ReparsePoint</c>): what the link NAMES is never read here — the module reads a link's text in one place only (<c>RootFacts</c>, for the words of its refusal).</summary>
    private static bool IsLink(FileInfo info)
    {
        var attributes = info.Attributes;
        return (int)attributes != -1 && attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    /// <summary>The mode is read FIRST, so a folder the user may not search is the runtime's own denial (its attributes would read as "nothing there"); then the kind and the witness.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static FactsRead Judged(string full, FileInfo info)
    {
        var mode = File.GetUnixFileMode(full);
        info.Refresh();
        var attributes = info.Attributes;
        return (int)attributes == -1
            ? new FactsRead.Unread($"{full}: no such file or directory")
            : attributes.HasFlag(FileAttributes.Directory)
                ? Facts(full, mode, FileKind.Directory, 0, info.LastWriteTimeUtc.Ticks)
                : Facts(full, mode, FileKind.Unknown, info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private static FactsRead Facts(string full, UnixFileMode mode, FileKind kind, long length, long writeTicks) =>
        new FactsRead.Read(new FileFacts(full, null, mode)
        {
            Identity = new FileIdentity(0, 0, kind, 0m),
            Witness = new FileWitness(length, writeTicks),
            Source = FactsSource.Portable,
        });
}
