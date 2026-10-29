using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>D17's executable rule, applied to a host: the executable of a command is a bare name found on PATH, or an absolute path, and the file it
/// really is — the path the kernel would open, every link and every <c>..</c> applied in the kernel's order — is never under the target or the
/// experiment root. A harness never runs a file an arm could write; <see cref="ExecutableRule"/> is the textual half, this adds what only a host can
/// say. Pure over facts: the console passes the file system's answers as functions.
/// <para>
/// The roots are resolved through the same seam, so a root spelled through a link (<c>~/git</c> to <c>/mnt/d/git</c>) is compared by where it really is.
/// Both spellings of the executable are judged against both spellings of the roots: a link INSIDE the target that points out of it is still refused by
/// its spelling.
/// </para>
/// <para>
/// What this does not do: it cannot see a hard link, a second name for the same file that is under no root by name; and a path changed between the
/// check and the launch is a race this closes only by returning the REAL path, which the harness launches and prints.
/// </para></summary>
public static class ExecutableResolution
{
    /// <summary>The REAL path to run, or why this executable is refused. <paramref name="realPath"/> answers where a path really ends, or a failure
    /// carrying the operating system's reason (a component is missing, a link dangles or loops, the platform has no probed resolver). A PATH candidate
    /// that cannot be resolved is skipped, as <c>execvp</c> does; an executable the operator SPELLED that cannot be resolved is refused naming the reason.</summary>
    public static Outcome<string> Resolve(
        string executable,
        IReadOnlyList<string> pathDirectories,
        IReadOnlyList<string> forbiddenRoots,
        Func<string, bool> isExecutableFile,
        Func<string, Outcome<string>> realPath,
        bool windows)
    {
        if (forbiddenRoots.FirstOrDefault(ExecutableRule.HoldsNul) is { } nulRoot)
        {
            return Outcome<string>.Failure($"the forbidden root '{ExecutableRule.Printable(nulRoot)}' holds a NUL character — a path never does; refused (D17)");
        }

        var roots = AsWrittenAndReal(forbiddenRoots, realPath);
        var spelled = ExecutableRule.Refusal(executable, roots);
        if (spelled.Length > 0)
        {
            return Outcome<string>.Failure(spelled);
        }

        var found = Path.IsPathFullyQualified(executable)
            ? Existing(executable, isExecutableFile, realPath)
            : OnPath(executable, pathDirectories, isExecutableFile, windows);
        return found.Match(path => Judge(path, roots, realPath), Outcome<string>.Failure);
    }

    /// <summary>A spelled executable: the operator named this file, so one the kernel cannot resolve is refused naming the reason — "not on PATH" would be a lie.</summary>
    private static Outcome<string> Existing(string executable, Func<string, bool> isExecutableFile, Func<string, Outcome<string>> realPath) =>
        realPath(executable).Match(
            _ => isExecutableFile(executable)
                ? Outcome<string>.Success(executable)
                : Outcome<string>.Failure($"{executable} does not exist or is not executable"),
            reason => Outcome<string>.Failure(Unresolved(executable, reason)));

    private static Outcome<string> OnPath(string name, IReadOnlyList<string> pathDirectories, Func<string, bool> isExecutableFile, bool windows)
    {
        var suffixes = windows ? new[] { string.Empty, ".exe" } : [string.Empty];
        var found = pathDirectories
            .Where(directory => directory.Length > 0)
            .SelectMany(directory => suffixes.Select(suffix => Path.Combine(directory, name + suffix)))
            .FirstOrDefault(isExecutableFile);
        return found is null ? Outcome<string>.Failure($"{name} is not on PATH on this host") : Outcome<string>.Success(found);
    }

    /// <summary>The written path through the textual rule (a PATH entry under the target is refused like a spelled one), then the real path through the
    /// same roots. The real path is the answer.</summary>
    private static Outcome<string> Judge(string written, IReadOnlyList<string> roots, Func<string, Outcome<string>> realPath)
    {
        var refusal = ExecutableRule.Refusal(written, roots);
        return refusal.Length > 0
            ? Outcome<string>.Failure(refusal)
            : realPath(written).Match(real => Real(written, real, roots), reason => Outcome<string>.Failure(Unresolved(written, reason)));
    }

    private static Outcome<string> Real(string written, string real, IReadOnlyList<string> roots)
    {
        var refusal = ExecutableRule.RealRefusal(written, real, roots);
        return refusal.Length == 0 ? Outcome<string>.Success(real) : Outcome<string>.Failure(refusal);
    }

    private static string Unresolved(string path, string reason) =>
        $"the real path of '{path}' cannot be resolved: {reason} — refused rather than assumed safe (D17)";

    /// <summary>The roots as written followed by where each really is.</summary>
    private static IReadOnlyList<string> AsWrittenAndReal(IReadOnlyList<string> roots, Func<string, Outcome<string>> realPath) =>
        [.. roots.Concat(roots.Select(root => RealRoot(root, realPath))).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>A root that does not exist yet (the root <c>init</c> is about to make, a target not cloned) is resolved through its nearest existing
    /// ancestor, the remaining segments appended; if no ancestor answers, the root stays as written.</summary>
    private static string RealRoot(string root, Func<string, Outcome<string>> realPath) =>
        Path.IsPathFullyQualified(root) ? NearestExisting(root, realPath) : root;

    private static string NearestExisting(string root, Func<string, Outcome<string>> realPath)
    {
        for (var ancestor = root; ancestor is not null; ancestor = Path.GetDirectoryName(ancestor))
        {
            if (realPath(ancestor) is Outcome<string>.Ok real)
            {
                return ancestor == root ? real.Value : Path.Combine(real.Value, Path.GetRelativePath(ancestor, root));
            }
        }

        return root;
    }
}
