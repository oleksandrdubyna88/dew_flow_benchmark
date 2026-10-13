using Bench.Domain.Probes;

namespace Bench.Domain.Scouts;

/// <summary>What the path audit found for one child call.</summary>
public enum PathFinding
{
    /// <summary>Every target, and every path a Glob listed, is inside the tree.</summary>
    Inside,

    /// <summary>An outside target, and the CLI refused it (a <c>permission_denied</c> event or an <c>is_error</c> result) — RECORDED.</summary>
    OutsideDenied,

    /// <summary>An outside target that returned an explicitly EMPTY success ("No files found") — recorded, and kept apart from a denial.</summary>
    OutsideEmpty,

    /// <summary>An outside target that returned content — STOP.</summary>
    OutsideContent,

    /// <summary>An outside target whose result is missing — never a safe read: STOP.</summary>
    OutsideNotCaptured,

    /// <summary>A Glob (or a Grep) whose result lists a path outside the tree, wherever it pointed — STOP.</summary>
    ListedOutside,
}

/// <summary>One audited child call. The resolved target stays in memory — it is a path on this machine, and no row stores it.</summary>
public sealed record AuditedCall(string CallId, string Tool, string Target, PathFinding Finding);

/// <summary>Resolves a junction or a symlink to its REAL target — the port the audit reads links through, so a test can fake one.
/// Given a resolved absolute path, answers the path every link on it resolved; a path with no link answers itself.</summary>
public sealed record LinkTargets(Func<string, string> Resolve)
{
    /// <summary>No link anywhere — what a test of the pure rules passes.</summary>
    public static LinkTargets None { get; } = new(path => path);
}

/// <summary>The path audit of §3.2, fail-stop: every child <c>Read</c>, <c>Glob</c>, <c>Grep</c> and <c>LS</c> target is resolved —
/// a relative path against the session's working directory, an omitted Grep/Glob root as the working directory itself, an absolute
/// glob pattern by its head, <c>..</c> folded, a junction or symlink by its resolved target — and held to the tree.
/// <para>
/// An inside target is fine. An outside attempt that was denied is RECORDED, and an explicitly empty success is recorded apart from
/// it. An outside call that returned content, a Glob result that listed any outside path, and an outside call whose result is missing,
/// truncated or unrecognised STOP the campaign — a void-and-retry would keep the broken boundary. A target that cannot be resolved
/// (a missing input, a <c>..</c> above the drive) is treated as outside.
/// </para></summary>
public static class PathAudit
{
    public static readonly IReadOnlySet<string> AuditedTools = new HashSet<string>(StringComparer.Ordinal) { "Read", "Glob", "Grep", "LS" };

    /// <summary>The results the CLI prints for a search that found nothing — the only results read as "empty"; any other text is content.</summary>
    private static readonly IReadOnlySet<string> EmptyResults = new HashSet<string>(StringComparer.Ordinal) { "No files found", "No matches found" };

    public static IReadOnlyList<AuditedCall> Audit(SubagentTranscript transcript, string treeRoot, LinkTargets links)
    {
        // The root is judged by its real location too: a tree under a linked temp folder must not read every target as outside.
        var root = links.Resolve(TreePaths.Resolve(treeRoot, string.Empty));
        var cwd = transcript.WorkingDirectory.Length > 0 ? transcript.WorkingDirectory : treeRoot;

        return [.. transcript.Agents.SelectMany(a => a.Child.Tools).Where(c => AuditedTools.Contains(c.Name)).Select(c => Call(c, root, cwd, links))];
    }

    /// <summary>The resolved target of one child call, as the audit resolves it — what the access check (S5) matches to its planted targets.</summary>
    public static string TargetOf(ChildToolCall call, string cwd) => Target(call, cwd).Target;

    /// <summary>Whether a result's text is one of the CLI's explicitly empty answers.</summary>
    public static bool IsEmptyResult(string result) => EmptyResults.Contains(result.Trim());

    public static ScoutReason StopReason(PathFinding finding) => finding switch
    {
        PathFinding.OutsideContent => ScoutReason.OutsideReadReturnedContent,
        PathFinding.OutsideNotCaptured => ScoutReason.OutsideResultNotCaptured,
        PathFinding.ListedOutside => ScoutReason.OutsideGlobListedPath,
        _ => ScoutReason.None,
    };

    private static AuditedCall Call(ChildToolCall call, string root, string cwd, LinkTargets links)
    {
        var (target, listBase) = Target(call, cwd);
        var real = target.Length > 0 ? links.Resolve(target) : string.Empty;
        var finding = TreePaths.IsInside(root, real) ? InsideFinding(call, root, listBase, links) : OutsideFinding(call);

        return new AuditedCall(call.Id, call.Name, real, finding);
    }

    private static PathFinding OutsideFinding(ChildToolCall call) =>
        call.Fate switch
        {
            ChildToolFate.Denied or ChildToolFate.Errored => PathFinding.OutsideDenied,
            ChildToolFate.Returned when EmptyResults.Contains(call.ResultText.Trim()) => PathFinding.OutsideEmpty,
            ChildToolFate.Returned => PathFinding.OutsideContent,
            _ => PathFinding.OutsideNotCaptured,
        };

    /// <summary>An inside Glob's result is still read: each listed line is a path under the glob's base, and one that resolves outside —
    /// through a junction, say — is a stop. Every other inside call is fine whatever it returned.</summary>
    private static PathFinding InsideFinding(ChildToolCall call, string root, string listBase, LinkTargets links) =>
        call.Name == "Glob" && call.Fate == ChildToolFate.Returned && Listed(call.ResultText).Any(line => !TreePaths.IsInside(root, Real(listBase, line, links)))
            ? PathFinding.ListedOutside
            : PathFinding.Inside;

    private static string Real(string listBase, string line, LinkTargets links)
    {
        var resolved = TreePaths.Resolve(listBase, line);
        return resolved.Length > 0 ? links.Resolve(resolved) : string.Empty;
    }

    private static IEnumerable<string> Listed(string result) =>
        result.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !EmptyResults.Contains(l) && !l.StartsWith('('));

    /// <summary>The call's target and, for a listing, the directory its result lines are relative to.</summary>
    private static (string Target, string ListBase) Target(ChildToolCall call, string cwd)
    {
        // An input that is not JSON names no target: every lookup below reads empty, and an empty target is outside (fail-closed).
        using var input = ProbeJson.Parse(call.Input);
        var arguments = input is null ? default : input.RootElement;

        return call.Name switch
        {
            "Read" => (Resolved(cwd, ProbeJson.Text(arguments, "file_path")), cwd),
            "LS" => (Resolved(cwd, ProbeJson.Text(arguments, "path")), cwd),
            "Glob" => GlobTarget(cwd, ProbeJson.Text(arguments, "path"), ProbeJson.Text(arguments, "pattern")),
            _ => GrepTarget(cwd, ProbeJson.Text(arguments, "path"), ProbeJson.Text(arguments, "glob")),
        };
    }

    /// <summary>A Read or an LS with no path named reads nothing nameable — unresolvable, so outside.</summary>
    private static string Resolved(string cwd, string path) => path.Length > 0 ? TreePaths.Resolve(cwd, path) : string.Empty;

    /// <summary>A Glob reaches its base (the <c>path</c>, or the working directory when omitted) joined with its pattern's head; an
    /// absolute pattern stands alone.</summary>
    private static (string Target, string ListBase) GlobTarget(string cwd, string path, string pattern)
    {
        var listBase = TreePaths.Resolve(cwd, path.Length > 0 ? path : ".");
        return (TreePaths.Resolve(listBase, TreePaths.GlobHead(pattern)), listBase);
    }

    /// <summary>A Grep reaches its <c>path</c> (or the working directory) narrowed by its <c>glob</c> filter's head.</summary>
    private static (string Target, string ListBase) GrepTarget(string cwd, string path, string glob)
    {
        var searchBase = TreePaths.Resolve(cwd, path.Length > 0 ? path : ".");
        return (TreePaths.Resolve(searchBase, TreePaths.GlobHead(glob)), searchBase);
    }
}
