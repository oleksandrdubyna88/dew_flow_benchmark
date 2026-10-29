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
    private const int MaxLinkRounds = 40;
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static readonly TimeSpan Ask = TimeSpan.FromSeconds(10);

    /// <summary>The nearest ancestor-or-self of <paramref name="path"/> holding a <c>.git</c> file (a worktree) or folder (a clone); the empty string
    /// when there is none. The path need not exist yet: <c>init</c> asks about a root it has not made.</summary>
    public static string GitTopOf(string path) =>
        AncestorsOrSelf(path).FirstOrDefault(folder => Path.Exists(Path.Combine(folder, ".git"))) ?? string.Empty;

    /// <summary>The nearest ancestor-or-self of the binary's folder that holds the benchmark solution file; the empty string for a binary installed elsewhere.</summary>
    public static string ThisRepository(string baseDirectory) =>
        AncestorsOrSelf(baseDirectory).FirstOrDefault(folder => File.Exists(Path.Combine(folder, SolutionFile))) ?? string.Empty;

    /// <summary>A file that exists and, off Windows, has an execute bit for someone: the question the PATH search asks of each candidate.</summary>
    public static bool IsExecutableFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & AnyExecute) != 0;
    }

    /// <summary>Where <paramref name="path"/> really ends once every symbolic link in it is followed — the link itself and every folder above it — or
    /// why that cannot be settled (a loop, an unreadable link). A path with no link is its own final target. Nothing here is a pass by default: the
    /// caller refuses what this could not resolve.</summary>
    public static Outcome<string> FinalTargetOf(string path)
    {
        try
        {
            return Settle(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Outcome<string>.Failure($"'{path}' cannot be resolved: {ex.Message}");
        }
    }

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

        var owner = (await Said("stat", OperatingSystem.IsMacOS() ? ["-f", "%u", path] : ["-c", "%u", path], ct)).Match(text => Uid(text, "stat"), Outcome<int>.Failure);
        return owner switch
        {
            Outcome<int>.Ok ok => Outcome<FileFacts>.Success(new FileFacts(path, ok.Value, File.GetUnixFileMode(path))),
            Outcome<int>.Fail fail => Outcome<FileFacts>.Failure($"the owner of {path} cannot be read: {fail.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };
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

    /// <summary>One link is followed per round, the first met from the root down, until a round meets none. Bounded, so a loop ends in a refusal.</summary>
    private static Outcome<string> Settle(string full)
    {
        var current = full;
        for (var round = 0; round < MaxLinkRounds; round++)
        {
            var next = FollowFirstLink(current);
            if (string.Equals(next, current, StringComparison.Ordinal))
            {
                return Outcome<string>.Success(current);
            }

            current = next;
        }

        return Outcome<string>.Failure($"'{full}' passes through more than {MaxLinkRounds} symbolic links — a loop is assumed");
    }

    private static string FollowFirstLink(string full)
    {
        var prefix = Path.GetPathRoot(full) ?? string.Empty;
        foreach (var segment in full[prefix.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            prefix = Path.Combine(prefix, segment);
            var info = new FileInfo(prefix);
            if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                return Path.GetFullPath(Path.TrimEndingDirectorySeparator(target.FullName) + full[prefix.Length..]);
            }
        }

        return full;
    }

    /// <summary>What a short-lived tool printed on stdout, or why it could not be asked.</summary>
    private static async Task<Outcome<string>> Said(string tool, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var attempt = await ProcessRunner.RunAsync(tool, arguments, Path.GetTempPath(), Ask, ct);
        return attempt is ProcessAttempt.Completed { Result.ExitCode: 0 } done
            ? Outcome<string>.Success(done.Result.StandardOutput.Trim())
            : Outcome<string>.Failure($"{tool} {string.Join(' ', arguments)}: {attempt.Describe}");
    }
}
