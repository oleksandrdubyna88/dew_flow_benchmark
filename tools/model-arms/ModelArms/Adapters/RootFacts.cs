using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The console's reads of the world that the root's pure deciders consume (<c>RootTrust</c>, <c>ExecutableResolution</c>,
/// <c>ExperimentFile.Placement</c>): which git checkout holds a path, which repository this binary belongs to, whether a file may be run, where a
/// symbolic link really ends, which user owns a file. It decides nothing; it reports what the host said.</summary>
public static class RootFacts
{
    private const string SolutionFile = "dew_flow_benchmark.slnx";
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>The nearest ancestor-or-self of <paramref name="path"/> holding a <c>.git</c> file (a worktree) or folder (a clone); the empty string
    /// when there is none. The path need not exist yet: <c>init</c> asks about a root it has not made. The question is asked of where the path REALLY is — the kernel's
    /// answer for its nearest existing ancestor, with the absent segments appended (<c>ExecutableResolution.RealPathClimbing</c>) — because a root spelled through a link
    /// into a checkout has no checkout above its text. The spelling is asked second, and the fallback cannot hide a checkout: on POSIX a path the kernel cannot place is stopped earlier with exit 3
    /// (<c>init</c> at <c>Present(root)</c>, <c>validate</c> at the trust <c>stat</c>), so the fallback only adds refusals; a platform without a resolver keeps the textual check that ran before.</summary>
    public static string GitTopOf(string path) =>
        TopOf(RealAncestry(path)) is { Length: > 0 } real ? real : TopOf(AncestorsOrSelf(path));

    private static string TopOf(IEnumerable<string> folders) =>
        folders.FirstOrDefault(folder => Path.Exists(Path.Combine(folder, ".git"))) ?? string.Empty;

    private static IEnumerable<string> RealAncestry(string path) =>
        ExecutableResolution.RealPathClimbing(Path.GetFullPath(path), Host).Match<IEnumerable<string>>(AncestorsOrSelf, _ => []);

    /// <summary>The nearest ancestor-or-self of the binary's folder that holds the benchmark solution file; the empty string for a binary installed elsewhere.</summary>
    public static string ThisRepository(string baseDirectory) =>
        AncestorsOrSelf(baseDirectory).FirstOrDefault(folder => File.Exists(Path.Combine(folder, SolutionFile))) ?? string.Empty;

    /// <summary>A file the kernel can resolve, that exists and, off Windows, has an execute bit for someone: the question the PATH search asks of each
    /// candidate. A dangling link, a loop, a folder and a file that vanishes mid-question all answer false — a predicate answers for every input and
    /// never throws what the library below it validates.</summary>
    public static bool IsExecutableFile(string path) => RealPathOf(path).Match(IsRunnable, _ => false);

    /// <summary>The path the kernel would open for <paramref name="path"/>, every link and every <c>..</c> applied in the kernel's own order (see
    /// <c>RealPath</c>), or the operating system's reason it cannot say: a missing component, a dangling link, a loop, a platform no resolver was probed on.
    /// Nothing is a pass by default: the caller refuses what this could not resolve.</summary>
    public static Outcome<string> RealPathOf(string path) => RealPath.Of(path);

    /// <summary>The uid this process runs as: <c>geteuid(2)</c> through libc, no process.</summary>
    public static int CurrentUid() => KernelFacts.EffectiveUid();

    /// <summary>The host's answers about paths, for the deciders that judge them: where a path really is, whether a directory entry is there, whether it is an executable file.</summary>
    public static HostPaths Host { get; } = new(RealPathOf, KernelFacts.Present, IsExecutableFile);

    /// <summary>Who owns <paramref name="path"/>, its mode, and what kind of file it is — all read by ONE <c>statx</c> call on ONE normalised path, so they
    /// are facts of one inode. The path is made absolute and trimmed once, here (a NUL is refused first): the owner used to come from the kernel's reading of
    /// the raw path and the mode from .NET's reading of the normalised one, and a <c>..</c> after a symbolic link climbs from the link's TARGET in the kernel's
    /// order but from the link's folder in .NET's, so the two could be two different files. A symbolic link at the path is refused; the facts of the file
    /// itself are what is judged. The path in the answer is the normalised one.</summary>
    public static Outcome<FileFacts> FileFacts(string path) => ReadFacts(path).ToOutcome();

    /// <summary><see cref="ReadFacts(string)"/> for a path that must be of the <paramref name="expected"/> kind.</summary>
    public static FactsRead ReadFacts(string path, FileKind expected) => ReadFacts(path).MustBe(expected);

    /// <summary>The facts of a trust path, with the cause of a failure kept: a path the check refuses to judge (a link, a NUL) is
    /// <see cref="FactsRead.Refused"/>, a read the host could not make is <see cref="FactsRead.Unread"/>. The console exits 4 for the first and 3 for the second.</summary>
    public static FactsRead ReadFacts(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FactsRead.Unread($"the owner and mode of {path} are POSIX facts; RootTrust declares the check not performed on Windows");
        }

        if (ExecutableRule.HoldsNul(path))
        {
            return new FactsRead.Refused($"the trust path '{ExecutableRule.Printable(path)}' holds a NUL character — refused (D17)");
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var read = KernelFacts.Read(full);
        return read is FactsRead.Read { Facts.Identity.Kind: FileKind.Link }
            ? new FactsRead.Refused(
                $"{full} is a symbolic link to '{LinkTargetOf(full) ?? "?"}' — the trust check judges the file itself, not a link that can be re-pointed; put the real file here or pass its real path (D17)")
            : read;
    }

    /// <summary>The type bits and the permission bits of a raw mode, as the kernel reports it: the file type in the high bits (<c>S_IFMT</c>, <c>0xF000</c>) over the
    /// permission bits (<c>0xFFF</c>, set-id and sticky included).</summary>
    internal sealed record RawMode(int Raw)
    {
        private const int TypeMask = 0xF000;
        private const int PermissionMask = 0xFFF;

        public UnixFileMode Permissions => (UnixFileMode)(Raw & PermissionMask);

        public FileKind Kind =>
            (Raw & TypeMask) switch
            {
                0x8000 => FileKind.Regular,
                0x4000 => FileKind.Directory,
                0xA000 => FileKind.Link,
                0x1000 => FileKind.Fifo,
                0xC000 => FileKind.Socket,
                0x2000 => FileKind.CharacterDevice,
                0x6000 => FileKind.BlockDevice,
                _ => FileKind.Unknown,
            };
    }

    private static IEnumerable<string> AncestorsOrSelf(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            yield return current;
        }
    }

    private static bool IsRunnable(string real)
    {
        try
        {
            return File.Exists(real) && (OperatingSystem.IsWindows() || (File.GetUnixFileMode(real) & AnyExecute) != 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Where a trust path points when it is itself a symbolic link (made absolute and the trailing separators trimmed — <c>GetFullPath</c> collapses <c>link//</c> and <c>link/.</c>: <c>readlink("link/")</c> follows the link and
    /// reports none), or null for a path that is not one. An ancestor may be a link; the last segment may not.</summary>
    private static string? LinkTargetOf(string path) => new FileInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))).LinkTarget;
}
