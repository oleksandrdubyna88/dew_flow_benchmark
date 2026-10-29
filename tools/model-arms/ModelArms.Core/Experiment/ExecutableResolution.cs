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
        if (NulRootRefusal(forbiddenRoots) is { Length: > 0 } nulRoot)
        {
            return Outcome<string>.Failure(nulRoot);
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

    /// <summary>Why this entry of a PATH list is refused, or the empty string. The programs the harness starts look their own tools up in the PATH they
    /// inherit — a shebang interpreter (<c>#!/usr/bin/env node</c>), <c>dotnet</c> starting <c>git</c> — so an entry an arm could write to is as dangerous as a
    /// configured executable there (observed: an outside tool's <c>#!/usr/bin/env mynode</c> ran the target's <c>mynode</c>). Refused: an empty entry (the current
    /// directory), a relative one (resolved against wherever the child runs), one under a forbidden root as written, and one that really is under one — the
    /// kernel's answer for the entry, or for its nearest existing ancestor when it does not exist yet. An entry that does not exist and is outside every root is
    /// fine: every shell skips it.</summary>
    public static string PathEntryRefusal(string entry, IReadOnlyList<string> forbiddenRoots, Func<string, Outcome<string>> realPath)
    {
        var shape = EntryShape(entry);
        if (shape.Length > 0)
        {
            return shape;
        }

        var nulRoot = NulRootRefusal(forbiddenRoots);
        return nulRoot.Length > 0 ? nulRoot : UnderARoot(entry, AsWrittenAndReal(forbiddenRoots, realPath), realPath);
    }

    private static string EntryShape(string entry) =>
        entry switch
        {
            _ when ExecutableRule.HoldsNul(entry) => $"PATH holds an entry '{ExecutableRule.Printable(entry)}' with a NUL character — a path never does; refused (D17)",
            "" => "PATH holds an empty entry — it means the current directory, where an arm could write a file; remove it (D17)",
            _ when !Path.IsPathFullyQualified(entry) =>
                $"PATH entry '{entry}' is a relative path, resolved against whatever directory a program runs in — use an absolute path (D17)",
            _ => string.Empty,
        };

    private static string UnderARoot(string entry, IReadOnlyList<string> roots, Func<string, Outcome<string>> realPath)
    {
        var written = roots.FirstOrDefault(root => PathScope.IsEqualOrUnder(entry, root));
        if (written is not null)
        {
            return $"PATH entry '{entry}' is under '{ExecutableRule.Printable(written)}' — {Reach}";
        }

        var real = NearestExisting(entry, realPath);
        var really = roots.FirstOrDefault(root => PathScope.IsEqualOrUnder(real, root));
        return really is null ? string.Empty : $"PATH entry '{entry}' is really '{real}', which is under '{ExecutableRule.Printable(really)}' — {Reach}";
    }

    private const string Reach =
        "every program the harness starts looks its own tools up in PATH, and an arm could write a file there; remove it from PATH (D17)";

    private static string NulRootRefusal(IReadOnlyList<string> forbiddenRoots) =>
        forbiddenRoots.FirstOrDefault(ExecutableRule.HoldsNul) is { } nulRoot
            ? $"the forbidden root '{ExecutableRule.Printable(nulRoot)}' holds a NUL character — a path never does; refused (D17)"
            : string.Empty;

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
