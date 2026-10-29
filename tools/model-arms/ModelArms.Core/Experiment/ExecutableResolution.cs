using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>D17's executable rule, applied to a host: the executable of a command is a bare name found on PATH, or an absolute path, and the file it
/// really is — the path the kernel would open, every link and every <c>..</c> applied in the kernel's order — is never under the target or the
/// experiment root. A harness never runs a file an arm could write; <see cref="ExecutableRule"/> is the textual half, this adds what only a host can
/// say. Pure over facts: the console passes the file system's answers as <see cref="HostPaths"/>.
/// <para>
/// The roots are resolved through the same seam, so a root spelled through a link (<c>~/git</c> to <c>/mnt/d/git</c>) is compared by where it really is.
/// Both spellings of the executable are judged against both spellings of the roots: a link INSIDE the target that points out of it is still refused by
/// its spelling. A root that does not exist yet (the root <c>init</c> is about to make) is resolved through its nearest existing ancestor — climbing over
/// components that are ABSENT only: a component that exists and cannot be resolved (a dangling link, a loop, a folder the user cannot search) refuses, because
/// the answer above it is not where it will lead once something is made there.
/// </para>
/// <para>
/// What this does not do: it cannot see a hard link, a second name for the same file that is under no root by name; and a path changed between the
/// check and the launch is a race this closes only by returning the REAL path, which the harness launches and prints.
/// </para></summary>
public static class ExecutableResolution
{
    private const string Assumed = "refused rather than assumed safe (D17)";

    private const string Reach =
        "every program the harness starts looks its own tools up in PATH, and an arm could write a file there; remove it from PATH (D17)";

    /// <summary>The REAL path to run, or why there is none. A PATH candidate that cannot be resolved is skipped, as <c>execvp</c> does. An executable the
    /// operator SPELLED is <see cref="ExecutableAnswer.Absent"/> when there is no directory entry by that name, and <see cref="ExecutableAnswer.Refused"/> when
    /// there is one the kernel cannot resolve or that is not an executable file — "not found" would be a lie about a dangling link.</summary>
    public static ExecutableAnswer Resolve(
        string executable, IReadOnlyList<string> pathDirectories, IReadOnlyList<string> forbiddenRoots, HostPaths host, bool windows)
    {
        if (NulRootRefusal(forbiddenRoots) is { Length: > 0 } nulRoot)
        {
            return new ExecutableAnswer.Refused(nulRoot);
        }

        return RealRoots(forbiddenRoots, host).Match(
            roots => ExecutableRule.Refusal(executable, roots) is { Length: > 0 } spelled ? new ExecutableAnswer.Refused(spelled) : Located(executable, pathDirectories, roots, host, windows),
            reason => new ExecutableAnswer.Refused(reason));
    }

    /// <summary>Why this entry of a PATH list is refused, or the empty string. The programs the harness starts look their own tools up in the PATH they
    /// inherit — a shebang interpreter (<c>#!/usr/bin/env node</c>), <c>dotnet</c> starting <c>git</c>, <c>git</c> starting its helpers — so an entry an arm could
    /// write to is as dangerous as a configured executable there (observed: an outside tool's <c>#!/usr/bin/env mynode</c> ran the target's <c>mynode</c>).
    /// Refused: an empty entry (the current directory), a relative one (resolved against wherever the child runs), one under a forbidden root as written, one
    /// that really is under one, and one whose existing component the kernel cannot resolve. An entry that does not exist and is outside every root is fine:
    /// every shell skips it.</summary>
    public static string PathEntryRefusal(string entry, IReadOnlyList<string> forbiddenRoots, HostPaths host)
    {
        var shape = EntryShape(entry);
        if (shape.Length > 0)
        {
            return shape;
        }

        var nulRoot = NulRootRefusal(forbiddenRoots);
        return nulRoot.Length > 0 ? nulRoot : RealRoots(forbiddenRoots, host).Match(roots => UnderARoot(entry, roots, host), reason => reason);
    }

    private static ExecutableAnswer Located(
        string executable, IReadOnlyList<string> pathDirectories, IReadOnlyList<string> roots, HostPaths host, bool windows) =>
        Path.IsPathFullyQualified(executable)
            ? Spelled(executable, roots, host)
            : OnPath(executable, pathDirectories, host, windows) is { } found ? Judge(found, roots, host) : new ExecutableAnswer.Absent($"{executable} is not on PATH on this host");

    /// <summary>A spelled executable: the operator named this file. No directory entry is the host's lack; an entry the kernel cannot resolve, or that is not an
    /// executable file, is the operator's configuration — "not on PATH" would be a lie.</summary>
    private static ExecutableAnswer Spelled(string executable, IReadOnlyList<string> roots, HostPaths host) =>
        host.Present(executable) switch
        {
            Outcome<bool>.Ok { Value: false } => new ExecutableAnswer.Absent($"{executable} does not exist"),
            Outcome<bool>.Fail fail => new ExecutableAnswer.Refused(Unresolved(executable, fail.Reason)),
            _ => host.RealPath(executable).Match(
                _ => host.IsExecutableFile(executable) ? Judge(executable, roots, host) : new ExecutableAnswer.Refused(NotExecutable(executable)),
                reason => new ExecutableAnswer.Refused(Unresolved(executable, reason))),
        };

    private static string? OnPath(string name, IReadOnlyList<string> pathDirectories, HostPaths host, bool windows)
    {
        var suffixes = windows ? new[] { string.Empty, ".exe" } : [string.Empty];
        return pathDirectories
            .Where(directory => directory.Length > 0)
            .SelectMany(directory => suffixes.Select(suffix => Path.Combine(directory, name + suffix)))
            .FirstOrDefault(host.IsExecutableFile);
    }

    /// <summary>The written path through the textual rule (a PATH entry under the target is refused like a spelled one), then the real path through the
    /// same roots. The real path is the answer.</summary>
    private static ExecutableAnswer Judge(string written, IReadOnlyList<string> roots, HostPaths host)
    {
        var refusal = ExecutableRule.Refusal(written, roots);
        return refusal.Length > 0
            ? new ExecutableAnswer.Refused(refusal)
            : host.RealPath(written).Match(real => Real(written, real, roots), reason => new ExecutableAnswer.Refused(Unresolved(written, reason)));
    }

    private static ExecutableAnswer Real(string written, string real, IReadOnlyList<string> roots) =>
        ExecutableRule.RealRefusal(written, real, roots) is { Length: > 0 } refusal ? new ExecutableAnswer.Refused(refusal) : new ExecutableAnswer.Found(real);

    private static string Unresolved(string path, string reason) => $"the real path of '{path}' cannot be resolved: {reason} — {Assumed}";

    private static string NotExecutable(string path) =>
        $"{path} is not an executable file — a folder, a FIFO or a file without an execute bit; make it executable or name another (D17)";

    private static string EntryShape(string entry) =>
        entry switch
        {
            _ when ExecutableRule.HoldsNul(entry) => $"PATH holds an entry '{ExecutableRule.Printable(entry)}' with a NUL character — a path never does; refused (D17)",
            "" => "PATH holds an empty entry — it means the current directory, where an arm could write a file; remove it (D17)",
            _ when !Path.IsPathFullyQualified(entry) =>
                $"PATH entry '{entry}' is a relative path, resolved against whatever directory a program runs in — use an absolute path (D17)",
            _ => string.Empty,
        };

    private static string UnderARoot(string entry, IReadOnlyList<string> roots, HostPaths host)
    {
        var written = roots.FirstOrDefault(root => PathScope.IsEqualOrUnder(entry, root));
        return written is not null
            ? $"PATH entry '{entry}' is under '{ExecutableRule.Printable(written)}' — {Reach}"
            : Climb(entry, host).Match(real => UnderRealRoot(entry, real, roots), reason => $"PATH entry '{entry}' cannot be resolved: {reason} — {Assumed}");
    }

    private static string UnderRealRoot(string entry, string real, IReadOnlyList<string> roots) =>
        roots.FirstOrDefault(root => PathScope.IsEqualOrUnder(real, root)) is { } really
            ? $"PATH entry '{entry}' is really '{real}', which is under '{ExecutableRule.Printable(really)}' — {Reach}"
            : string.Empty;

    private static string NulRootRefusal(IReadOnlyList<string> forbiddenRoots) =>
        forbiddenRoots.FirstOrDefault(ExecutableRule.HoldsNul) is { } nulRoot
            ? $"the forbidden root '{ExecutableRule.Printable(nulRoot)}' holds a NUL character — a path never does; refused (D17)"
            : string.Empty;

    /// <summary>The roots as written followed by where each really is; a root whose real path cannot be established refuses everything (fail closed, as a NUL root).</summary>
    private static Outcome<IReadOnlyList<string>> RealRoots(IReadOnlyList<string> roots, HostPaths host)
    {
        var reals = roots.Select(root => (Root: root, Real: RealRoot(root, host))).ToList();
        return reals.FirstOrDefault(pair => pair.Real is Outcome<string>.Fail) is { Real: Outcome<string>.Fail fail } failed
            ? Outcome<IReadOnlyList<string>>.Failure($"the forbidden root '{ExecutableRule.Printable(failed.Root)}' cannot be resolved: {fail.Reason} — {Assumed}")
            : Outcome<IReadOnlyList<string>>.Success([.. roots.Concat(reals.Select(pair => pair.Real.Match(real => real, _ => string.Empty))).Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    private static Outcome<string> RealRoot(string root, HostPaths host) =>
        Path.IsPathFullyQualified(root) ? Climb(root, host) : Outcome<string>.Success(root);

    /// <summary>Where a path really is: its own real path, else its nearest ancestor's with the remaining segments appended — climbing over ABSENT components only.
    /// A component that exists and cannot be resolved, or that the kernel cannot say anything about, is a failure carrying why.</summary>
    private static Outcome<string> Climb(string path, HostPaths host) => ClimbFrom(path, path, host);

    private static Outcome<string> ClimbFrom(string ancestor, string path, HostPaths host) =>
        host.RealPath(ancestor).Match(
            real => Outcome<string>.Success(ancestor == path ? real : Path.Combine(real, Path.GetRelativePath(ancestor, path))),
            reason => host.Present(ancestor) switch
            {
                Outcome<bool>.Ok { Value: false } => Path.GetDirectoryName(ancestor) is { } parent
                    ? ClimbFrom(parent, path, host)
                    : Outcome<string>.Failure($"no ancestor of '{path}' can be resolved"),
                Outcome<bool>.Ok => Outcome<string>.Failure($"'{ancestor}' exists but its real path cannot be resolved: {reason}"),
                Outcome<bool>.Fail fail => Outcome<string>.Failure($"'{ancestor}': {fail.Reason}"),
                _ => throw new InvalidOperationException("unreachable"),
            });
}
