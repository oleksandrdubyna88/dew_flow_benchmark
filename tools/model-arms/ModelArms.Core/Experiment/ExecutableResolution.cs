using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>D17's executable rule, applied to a host: the executable of a command is a bare name found on PATH, or an absolute path, and the
/// file it resolves to is never under the target or the experiment root — as written, and at the end of a symbolic link. A harness never
/// runs a file an arm could write; <see cref="ExecutableRule"/> is the textual half, this adds what only a host can say. Pure over facts:
/// the console passes the file system's answers as functions.
/// <para>
/// What this does not do: it does not follow links between the check and the launch. A link changed after validation is a race this rule
/// does not close; the harness prints the resolved paths before the first launch so the operator can see what it saw.
/// </para></summary>
public static class ExecutableResolution
{
    /// <summary>The path to run, or why this executable is refused. <paramref name="finalTarget"/> answers where a path really ends once every
    /// symbolic link in it is followed (the path itself when there is none), or a failure when that cannot be settled — which refuses: an
    /// unresolvable link is never a pass. The result is the path as written, because that is the name the harness will run.</summary>
    public static Outcome<string> Resolve(
        string executable,
        IReadOnlyList<string> pathDirectories,
        IReadOnlyList<string> forbiddenRoots,
        Func<string, bool> isExecutableFile,
        Func<string, Outcome<string>> finalTarget,
        bool windows)
    {
        var spelled = ExecutableRule.Refusal(executable, forbiddenRoots);
        if (spelled.Length > 0)
        {
            return Outcome<string>.Failure(spelled);
        }

        var found = Locate(executable, pathDirectories, isExecutableFile, windows);
        return found.Match(path => Judge(path, forbiddenRoots, finalTarget), Outcome<string>.Failure);
    }

    private static Outcome<string> Locate(string executable, IReadOnlyList<string> pathDirectories, Func<string, bool> isExecutableFile, bool windows) =>
        Path.IsPathFullyQualified(executable)
            ? Existing(executable, isExecutableFile)
            : OnPath(executable, pathDirectories, isExecutableFile, windows);

    private static Outcome<string> Existing(string executable, Func<string, bool> isExecutableFile) =>
        isExecutableFile(executable)
            ? Outcome<string>.Success(executable)
            : Outcome<string>.Failure($"{executable} does not exist or is not executable");

    private static Outcome<string> OnPath(string name, IReadOnlyList<string> pathDirectories, Func<string, bool> isExecutableFile, bool windows)
    {
        var suffixes = windows ? new[] { string.Empty, ".exe" } : [string.Empty];
        var found = pathDirectories
            .Where(directory => directory.Length > 0)
            .SelectMany(directory => suffixes.Select(suffix => Path.Combine(directory, name + suffix)))
            .FirstOrDefault(isExecutableFile);
        return found is null ? Outcome<string>.Failure($"{name} is not on PATH on this host") : Outcome<string>.Success(found);
    }

    /// <summary>The resolved path through the textual rule (a PATH entry under the target is refused like a spelled one), then the end of its links.</summary>
    private static Outcome<string> Judge(string path, IReadOnlyList<string> forbiddenRoots, Func<string, Outcome<string>> finalTarget)
    {
        var refusal = ExecutableRule.Refusal(path, forbiddenRoots);
        return refusal.Length > 0
            ? Outcome<string>.Failure(refusal)
            : finalTarget(path).Match(target => FollowedLink(path, target, forbiddenRoots), reason => Outcome<string>.Failure(Unresolved(path, reason)));
    }

    private static Outcome<string> FollowedLink(string path, string target, IReadOnlyList<string> forbiddenRoots)
    {
        var refusal = ExecutableRule.Refusal(target, forbiddenRoots);
        return refusal.Length == 0
            ? Outcome<string>.Success(path)
            : Outcome<string>.Failure($"{path} is a symbolic link that ends at '{target}': {refusal}");
    }

    private static string Unresolved(string path, string reason) =>
        $"the symbolic links of '{path}' cannot be followed: {reason} — refused rather than assumed safe (D17)";
}
