using System.Globalization;
using Bench.Domain;
using Bench.Infrastructure.Process;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The console's reads of the world that the root's pure deciders consume (<c>RootTrust</c>, <c>ExecutableResolution</c>,
/// <c>ExperimentFile.Placement</c>): which git checkout holds a path, which repository this binary belongs to, whether a file may be run, where a
/// symbolic link really ends, which user owns a file. It decides nothing; it reports what the host said.</summary>
public static class RootFacts
{
    private const string SolutionFile = "dew_flow_benchmark.slnx";
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static readonly TimeSpan Ask = TimeSpan.FromSeconds(10);

    /// <summary>The child's whole environment: the C locale, so nothing it prints is translated. The answers are numbers and bits, so this is belt and braces.</summary>
    private static readonly IReadOnlyDictionary<string, string> NeutralLocale = new Dictionary<string, string> { ["LC_ALL"] = "C" };

    /// <summary>The nearest ancestor-or-self of <paramref name="path"/> holding a <c>.git</c> file (a worktree) or folder (a clone); the empty string
    /// when there is none. The path need not exist yet: <c>init</c> asks about a root it has not made.</summary>
    public static string GitTopOf(string path) =>
        AncestorsOrSelf(path).FirstOrDefault(folder => Path.Exists(Path.Combine(folder, ".git"))) ?? string.Empty;

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

    /// <summary>The uid this process runs as, from <c>id -u</c>.</summary>
    public static async Task<Outcome<int>> CurrentUidAsync(CancellationToken ct) =>
        (await Said("id", ["-u"], ct)).Match(text => Uid(text, "id -u"), Outcome<int>.Failure);

    /// <summary>Who owns <paramref name="path"/>, its mode, and what kind of file it is — all three read by ONE <c>stat</c> call on ONE normalised path, so they
    /// are facts of one inode. The path is made absolute and trimmed once, here (a NUL is refused first): the owner used to come from the kernel's reading of
    /// the raw path and the mode from .NET's reading of the normalised one, and a <c>..</c> after a symbolic link climbs from the link's TARGET in the kernel's
    /// order but from the link's folder in .NET's, so the two could be two different files. A symbolic link at the path is refused; the facts of the file
    /// itself are what is judged. The path in the answer is the normalised one.</summary>
    public static async Task<Outcome<FileFacts>> FileFactsAsync(string path, CancellationToken ct) =>
        (await ReadFactsAsync(path, ct)).ToOutcome();

    /// <summary><see cref="ReadFactsAsync(string, CancellationToken)"/> for a path that must be of the <paramref name="expected"/> kind.</summary>
    public static async Task<FactsRead> ReadFactsAsync(string path, FileKind expected, CancellationToken ct) =>
        (await ReadFactsAsync(path, ct)).MustBe(expected);

    /// <summary><see cref="FileFactsAsync"/> with the cause of a failure kept: a path the check refuses to judge (a link, a NUL) is
    /// <see cref="FactsRead.Refused"/>, a <c>stat</c> that could not say is <see cref="FactsRead.Unread"/>. The console exits 4 for the first and 3 for the second.</summary>
    public static async Task<FactsRead> ReadFactsAsync(string path, CancellationToken ct)
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
        var said = await Said("stat", StatArguments(full), ct);
        return said.Match(text => Judge(full, text), reason => new FactsRead.Unread($"the owner of {full} cannot be read: {reason}"));
    }

    /// <summary>GNU <c>stat -c '%u %f %d %i'</c> (owner, the raw mode in hex, the device, the inode); BSD and macOS <c>stat -f '%u %p %d %i'</c> (owner, the raw mode in octal, the device, the inode). The raw mode carries the
    /// file type in its high bits, so the type is decided on bits and never on a word <c>stat</c> prints in the user's language. Without <c>-L</c>: the file itself.</summary>
    private static string[] StatArguments(string full) =>
        OperatingSystem.IsMacOS() ? ["-f", "%u %p %d %i", full] : ["-c", "%u %f %d %i", full];

    /// <summary>The facts in one line of <c>stat</c> output, or why the line cannot be used. A symbolic link is refused, naming where it points: the trust check
    /// judges the file itself, not a link that can be re-pointed. Public so the parser can be tested over the raw outputs of both <c>stat</c> flavours.</summary>
    public static Outcome<FileFacts> FactsOfStat(string full, string text, bool bsd) => ReadStat(full, text, bsd).ToOutcome();

    private static FactsRead ReadStat(string full, string text, bool bsd) =>
        StatLine.Read(text, bsd).Match(
            line => line.IsLink
                ? new FactsRead.Refused(
                    $"{full} is a symbolic link to '{LinkTargetOf(full) ?? "?"}' — the trust check judges the file itself, not a link that can be re-pointed; put the real file here or pass its real path (D17)")
                : new FactsRead.Read(new FileFacts(full, line.Uid, line.Permissions) { Identity = new FileIdentity(line.Device, line.Inode, line.Kind) }),
            reason => (FactsRead)new FactsRead.Unread($"stat answered '{text}' for {full}: {reason}"));

    private static FactsRead Judge(string full, string text) => ReadStat(full, text, OperatingSystem.IsMacOS());

    /// <summary>One line of <c>stat</c>: <c>uid rawmode device inode</c>. The raw mode is S_IFMT (<c>0xF000</c>) type bits over the permission bits (<c>0xFFF</c>, which includes set-id and sticky).</summary>
    private sealed record StatLine(int Uid, int RawMode, ulong Device, ulong Inode)
    {
        private const int TypeMask = 0xF000;
        private const int PermissionMask = 0xFFF;

        public bool IsLink => Kind == FileKind.Link;

        public UnixFileMode Permissions => (UnixFileMode)(RawMode & PermissionMask);

        public FileKind Kind =>
            (RawMode & TypeMask) switch
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

        public static Outcome<StatLine> Read(string text, bool bsd)
        {
            var parts = text.Split(' ');
            return parts.Length == 4 && TryOwnerAndMode(parts[0], parts[1], bsd, out var uid, out var raw) && TryIdentity(parts[2], parts[3], out var device, out var inode)
                ? Outcome<StatLine>.Success(new StatLine(uid, raw, device, inode))
                : Outcome<StatLine>.Failure("expected an owner, a raw mode, a device and an inode");
        }

        private static bool TryOwnerAndMode(string owner, string mode, bool bsd, out int uid, out int raw)
        {
            raw = 0;
            return int.TryParse(owner, NumberStyles.None, CultureInfo.InvariantCulture, out uid) && TryRaw(mode, bsd, out raw);
        }

        private static bool TryIdentity(string device, string inode, out ulong deviceNumber, out ulong inodeNumber)
        {
            inodeNumber = 0;
            return ulong.TryParse(device, NumberStyles.None, CultureInfo.InvariantCulture, out deviceNumber)
                && ulong.TryParse(inode, NumberStyles.None, CultureInfo.InvariantCulture, out inodeNumber);
        }

        private static bool TryRaw(string digits, bool octal, out int raw)
        {
            raw = 0;
            var parsed = digits.Length is > 0 and <= 8 && digits.All(c => octal ? c is >= '0' and <= '7' : char.IsAsciiHexDigit(c));
            if (parsed)
            {
                raw = Convert.ToInt32(digits, octal ? 8 : 16);
            }

            return parsed;
        }
    }

    private static Outcome<int> Uid(string text, string tool) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var uid)
            ? Outcome<int>.Success(uid)
            : Outcome<int>.Failure($"{tool} answered '{text}', not a user id");

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

    /// <summary>What a short-lived tool printed on stdout, or why it could not be asked.</summary>
    private static async Task<Outcome<string>> Said(string tool, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var attempt = await ProcessRunner.RunAsync(tool, arguments, Path.GetTempPath(), Ask, string.Empty, NeutralLocale, ct);
        return attempt is ProcessAttempt.Completed { Result.ExitCode: 0 } done
            ? Outcome<string>.Success(done.Result.StandardOutput.Trim())
            : Outcome<string>.Failure($"{tool} {string.Join(' ', arguments)}: {attempt.Describe}");
    }
}
