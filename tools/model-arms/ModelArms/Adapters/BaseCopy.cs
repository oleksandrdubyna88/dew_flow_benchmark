using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The copy of <c>base/</c> into an arm's clone (C15, E3.S3, E3 §8 items 16 and 22): the replacement of the shell's <c>cp -a</c>, so no new executable is resolved and nothing is looked up by name. <b>Folders</b> are made new, one by one, EXCLUSIVELY (libc <c>mkdir</c>: an existing name is never adopted) and given the source's mode and modification time once their
/// children are in them — a folder that cannot be written is writable only while it is filled; <b>files</b> are copied byte for byte with their mode and their modification time (the warm build that follows is incremental on those times, as <c>cp -a</c> kept them); <b>links are copied AS links</b>: their text kept as it is, never followed, a link to a folder never entered — so nothing the operator's checkout points at
/// is ever copied into an arm, and nothing outside the copy is ever read through it; and a ROOTED link that leads into the base itself or into the experiment root is REFUSED naming it (<see cref="Copy(string, string, PlatformRoad, IReadOnlyList{string})"/>), because an arm that wrote through it would write into the shared base that every later <c>make</c> copies and builds with the operator's rights; a <b>FIFO, a socket or a device</b> stops the copy naming its path under <c>base/</c> (the arm's clone would hold something a copy cannot make, and opening a FIFO waits for a writer for ever).
/// <para>
/// The KIND of every entry comes from the kernel where the host has a reader (<c>statx</c> with <c>AT_SYMLINK_NOFOLLOW</c>, D26 a). On a host with none (macOS, FreeBSD — E3.S0's portable road) a FIFO is not told from a file, so each file is opened once under the road's ceiling (<see cref="PlatformRoads.BoundedCeilingSeconds"/> s) before it is copied: an open that does not return is a FIFO, and
/// the copy stops naming it. The thread that open left is a background one: it ends when something opens the FIFO or the process ends.
/// </para>
/// <para>
/// What this does NOT do: copy a tree somebody is writing (the base is idle while <c>make</c> runs); keep the modification time of a LINK (the runtime has no no-follow setter; a link is made new), extended attributes, ACLs or ownership (<c>cp -a</c> kept what the user could); sparse holes (a hole is written as zeros); a ROOTED link that leads into somewhere else than the base or the experiment root (an absolute link to a system folder, to the operator's checkout) is copied as it is — as <c>cp -a</c> did — and so is a RELATIVE link whose text climbs out of the clone; the portable road opens each file under a ceiling but cannot tell a CHARACTER DEVICE from a file, so a copied device node (a <c>/dev/zero</c>) is copied as its bytes up to the end the device gives, with no ceiling on the copy itself.
/// The count it answers is the files and links copied, folders not counted.
/// </para></summary>
public static class BaseCopy
{
    private static readonly UnixFileMode OwnerFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>The copy of <paramref name="from"/> as the NEW folder <paramref name="to"/>, on this host's road.</summary>
    public static Outcome<long> Copy(string from, string to) => Copy(from, to, PlatformFactsReader.CurrentRoad);

    public static Outcome<long> Copy(string from, string to, PlatformRoad road) => Copy(from, to, road, []);

    /// <summary>The same where a ROOTED link may not lead into <paramref name="from"/> or into any of <paramref name="alsoKeepOut"/> (the experiment's root, for <c>make</c>): such a link, made new in an arm's clone, still names the SHARED folder, so an arm that wrote through it would write into the base that every later <c>make</c> copies and builds, or into another arm.</summary>
    public static Outcome<long> Copy(string from, string to, PlatformRoad road, IReadOnlyList<string> alsoKeepOut) =>
        Copy(from, to, road, TimeSpan.FromSeconds(PlatformRoads.BoundedCeilingSeconds), alsoKeepOut);

    /// <summary>The same with the ceiling of the portable road's open handed in: a test that plants a FIFO does not wait the production five seconds.</summary>
    internal static Outcome<long> Copy(string from, string to, PlatformRoad road, TimeSpan ceiling) => Copy(from, to, road, ceiling, []);

    private static Outcome<long> Copy(string from, string to, PlatformRoad road, TimeSpan ceiling, IReadOnlyList<string> alsoKeepOut)
    {
        try
        {
            return Folder(new DirectoryInfo(from), to, new Walk(from, road, ceiling, [.. new[] { from }.Concat(alsoKeepOut).Select(Normal)]));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<long>.Failure($"cannot copy {from} to {to}: {ex.Message}");
        }
    }

    private static string Normal(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>What one copy carries down every folder: where it started (for the names it prints), the road the kinds are told by and the ceiling of the portable open.</summary>
    private sealed record Walk(string Root, PlatformRoad Road, TimeSpan Ceiling, IReadOnlyList<string> KeepOut)
    {
        public string Named(FileSystemInfo entry) => Path.GetRelativePath(Root, entry.FullName).Replace('\\', '/');

        /// <summary>Whether a rooted link text, normalised as text, is one of the kept-out folders or inside one.</summary>
        public bool Leads(string text)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var full = Normal(text);
            return KeepOut.Any(folder => full.Equals(folder, comparison) || full.StartsWith(folder + Path.DirectorySeparatorChar, comparison));
        }
    }

    private static Outcome<long> Folder(DirectoryInfo source, string destination, Walk walk) =>
        ExclusiveFolder.Make(destination, walk.Road) switch
        {
            FolderMade.Made => Filled(source, destination, walk),
            FolderMade.AlreadyThere => Outcome<long>.Failure($"{destination} already exists — a copy never adopts a folder"),
            FolderMade.Failed failed => Outcome<long>.Failure($"cannot create {destination}: {failed.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>The folder's children, then its mode and its time: the folder is owner-writable while it is filled (the umask may have narrowed the <c>0700</c> the kernel was asked for), and a time set before the children are made would be moved by them.</summary>
    private static Outcome<long> Filled(DirectoryInfo source, string destination, Walk walk)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination, OwnerFolder);
        }

        var copied = Children(source, destination, walk);
        if (copied is Outcome<long>.Ok)
        {
            Stamped(source, destination);
        }

        return copied;
    }

    private static Outcome<long> Children(DirectoryInfo source, string destination, Walk walk)
    {
        long copied = 0;
        foreach (var entry in source.EnumerateFileSystemInfos())
        {
            var one = One(entry, Path.Combine(destination, entry.Name), walk);
            if (one is not Outcome<long>.Ok ok)
            {
                return one;
            }

            copied += ok.Value;
        }

        return Outcome<long>.Success(copied);
    }

    private static Outcome<long> One(FileSystemInfo entry, string target, Walk walk) =>
        KindOf(entry, walk.Road).Match(
            kind => kind switch
            {
                FileKind.Link => Link(entry, target, walk),
                FileKind.Directory => Folder((DirectoryInfo)entry, target, walk),
                FileKind.Regular or FileKind.Unknown => Regular((FileInfo)entry, target, kind, walk),
                _ => Outcome<long>.Failure($"{walk.Named(entry)} is {Describe(kind)} — a copy of the base carries files, folders and links only; remove it from base/ and run make again"),
            },
            Outcome<long>.Failure);

    /// <summary>A link is told by the entry's own attributes on every road (<c>ReparsePoint</c>, a dangling one included — what it names is never read). Anything else is the kernel's answer where the host gives one; elsewhere a folder is told by the runtime and a file is <see cref="FileKind.Unknown"/>.</summary>
    private static Outcome<FileKind> KindOf(FileSystemInfo entry, PlatformRoad road)
    {
        if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return Outcome<FileKind>.Success(FileKind.Link);
        }

        if (road.Trust != TrustRoad.Kernel)
        {
            return Outcome<FileKind>.Success(entry is DirectoryInfo ? FileKind.Directory : FileKind.Unknown);
        }

        return KernelFacts.Read(entry.FullName) switch
        {
            FactsRead.Read read => Outcome<FileKind>.Success(read.Facts.Identity.Kind),
            FactsRead.Unread unread => Outcome<FileKind>.Failure(unread.Reason),
            FactsRead.Refused refused => Outcome<FileKind>.Failure(refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>The link made new with the SAME text: <see cref="FileSystemInfo.LinkTarget"/> is the text the link holds, not where it leads.</summary>
    private static Outcome<long> Link(FileSystemInfo entry, string target, Walk walk)
    {
        if (entry.LinkTarget is not { } text)
        {
            return Outcome<long>.Failure($"{walk.Named(entry)} is a reparse point that is not a symbolic link — it cannot be copied as one");
        }

        if (Path.IsPathRooted(text) && walk.Leads(text))
        {
            return Outcome<long>.Failure($"{walk.Named(entry)} is a link to {text}, inside the base or the experiment root — an arm that wrote through it would write into the shared base or another arm; make it a relative link or remove it from base/");
        }

        new FileInfo(target).CreateAsSymbolicLink(text);
        return Outcome<long>.Success(1);
    }

    private static Outcome<long> Regular(FileInfo source, string target, FileKind kind, Walk walk)
    {
        if (kind == FileKind.Unknown && !OpensWithin(source.FullName, walk.Ceiling))
        {
            return Outcome<long>.Failure($"{walk.Named(source)} did not open within {walk.Ceiling.TotalSeconds:0.##} s — a FIFO, which this host cannot tell from a file; remove it from base/ and run make again");
        }

        File.Copy(source.FullName, target, overwrite: false);
        Stamped(source, target);
        return Outcome<long>.Success(1);
    }

    /// <summary>The source's mode and modification time on the copy, exactly: the mode is set, not left to the umask.</summary>
    private static void Stamped(FileSystemInfo source, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target, source.UnixFileMode);
        }

        File.SetLastWriteTimeUtc(target, source.LastWriteTimeUtc);
    }

    /// <summary>Whether the file opens within the ceiling, on a thread of its own: an open that fails PROMPTLY is an answer too (the copy that follows reports it with the runtime's words); one that does not return is a FIFO with no writer.</summary>
    private static bool OpensWithin(string path, TimeSpan ceiling)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() => Opened(path, done)) { IsBackground = true, Name = "base-copy-open" }.Start();
        return done.Task.Wait(ceiling);
    }

    /// <summary>A detached edge: nothing may escape as an exception — an unhandled one on this thread would end the process.</summary>
    private static void Opened(string path, TaskCompletionSource<bool> done)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception)
        {
            // Opened or failed: either way it returned. The copy reports a failure with its own words.
        }
        finally
        {
            done.TrySetResult(true);
        }
    }

    private static string Describe(FileKind kind) =>
        kind switch
        {
            FileKind.Fifo => "a FIFO",
            FileKind.Socket => "a socket",
            FileKind.CharacterDevice or FileKind.BlockDevice => "a device",
            _ => "a file of a kind a copy cannot make",
        };
}
