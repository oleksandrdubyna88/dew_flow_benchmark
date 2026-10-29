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

    /// <summary>Who owns <paramref name="path"/> (from <c>stat</c>) and its mode (from the file system), file or folder alike.</summary>
    public static async Task<Outcome<FileFacts>> FileFactsAsync(string path, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            return Outcome<FileFacts>.Failure($"the owner and mode of {path} are POSIX facts; RootTrust declares the check not performed on Windows");
        }

        if (LinkTargetOf(path) is { } target)
        {
            return Outcome<FileFacts>.Failure(
                $"{path} is a symbolic link to '{target}' — the trust check judges the file itself, not a link that can be re-pointed; put the real file here or pass its real path (D17)");
        }

        var owner = (await Said("stat", StatOwnerArguments(path), ct)).Match(text => Uid(text, "stat"), Outcome<int>.Failure);
        return owner switch
        {
            Outcome<int>.Ok ok => Outcome<FileFacts>.Success(new FileFacts(path, ok.Value, File.GetUnixFileMode(path))),
            Outcome<int>.Fail fail => Outcome<FileFacts>.Failure($"the owner of {path} cannot be read: {fail.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>GNU <c>stat -c %u</c>; the BSD spelling on macOS.</summary>
    private static string[] StatOwnerArguments(string path) => OperatingSystem.IsMacOS() ? ["-f", "%u", path] : ["-c", "%u", path];

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

    /// <summary>Where a trust path points when it is itself a symbolic link (the trailing separator trimmed: <c>readlink("link/")</c> follows the link and
    /// reports none), or null for a path that is not one. An ancestor may be a link; the last segment may not.</summary>
    private static string? LinkTargetOf(string path) => new FileInfo(Path.TrimEndingDirectorySeparator(path)).LinkTarget;

    /// <summary>What a short-lived tool printed on stdout, or why it could not be asked.</summary>
    private static async Task<Outcome<string>> Said(string tool, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var attempt = await ProcessRunner.RunAsync(tool, arguments, Path.GetTempPath(), Ask, ct);
        return attempt is ProcessAttempt.Completed { Result.ExitCode: 0 } done
            ? Outcome<string>.Success(done.Result.StandardOutput.Trim())
            : Outcome<string>.Failure($"{tool} {string.Join(' ', arguments)}: {attempt.Describe}");
    }
}
