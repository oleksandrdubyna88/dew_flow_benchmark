using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>How a candidate's fix landed (plan §3.1, D10, E2 S7) — what decides which commit P must be, and so what the parent check
/// (<see cref="ScoutParentCheck"/>) verifies against the mirror.</summary>
public enum ScoutMergeStyle
{
    /// <summary>A GitHub PR squash-merged: ONE parent, and P is it.</summary>
    PrSquash,

    /// <summary>A GitHub PR rebase-merged: the fix's first parent is a commit INSIDE the PR, so P is the PR's recorded <c>baseRefOid</c>.</summary>
    PrRebase,

    /// <summary>A merge commit: TWO parents, and P is the first.</summary>
    MergeCommit,

    /// <summary>A local squash commit (a private repository's clone, D10): one parent, and P is it; keyed by its sha, since it has no PR number.</summary>
    LocalSquash,
}

/// <summary>The merge styles' words, spelled once.</summary>
public static class ScoutMergeStyles
{
    public const string Forms = "pr-squash, pr-rebase, merge-commit or local-squash";

    public static string Word(ScoutMergeStyle style) => style switch
    {
        ScoutMergeStyle.PrSquash => "pr-squash",
        ScoutMergeStyle.PrRebase => "pr-rebase",
        ScoutMergeStyle.MergeCommit => "merge-commit",
        _ => "local-squash",
    };

    public static Outcome<ScoutMergeStyle> Parse(string? word) =>
        Enum.GetValues<ScoutMergeStyle>().Where(s => Word(s) == (word ?? string.Empty).Trim().ToLowerInvariant()).Select(Outcome<ScoutMergeStyle>.Success).DefaultIfEmpty(
            Outcome<ScoutMergeStyle>.Failure($"'{(word ?? string.Empty).Trim()}' is not a merge style — {Forms}")).First();

    /// <summary>A style whose candidate IS a GitHub pull request and carries its number.</summary>
    public static bool IsPullRequest(ScoutMergeStyle style) => style is ScoutMergeStyle.PrSquash or ScoutMergeStyle.PrRebase;
}

/// <summary>A rebase-merged PR's base — the commit P must be (§3.1: "P is the PR's <c>baseRefOid</c>") — and where the operator read it
/// (<c>gh pr view --json baseRefOid</c>, the GitHub API, …), so the evidence names its source.</summary>
public sealed record ScoutBaseRef(string Oid, string Source)
{
    public static ScoutBaseRef None { get; } = new(string.Empty, string.Empty);

    public bool IsRecorded => Oid.Length > 0;
}

/// <summary>One candidate task as the operator lists it (§3.1): its id, the repository (a public slug or a private clone's url), P (the
/// parent) and the fix commit, the question kind, the frozen statement, the PR's own plan or results document (repository-relative; empty
/// when it has none) — removed from the tree when it existed at P, and never part of G — and, since S7, HOW the fix landed
/// (<see cref="MergeStyle"/>), its PR <see cref="Number"/> when it is one, and a rebase-merged PR's <see cref="BaseRef"/>.</summary>
public sealed record ScoutCandidate(
    ScoutTaskId Id, string Repository, CommitSha Parent, CommitSha Fix, ScoutQuestionKind Kind, string Statement, string PlanFile)
{
    /// <summary>D10: a PRIVATE candidate's id is its label, its repository a local url, and nothing it holds but the label, the language and
    /// the sizes reaches a public writer (<see cref="ScoutPublication"/>).</summary>
    public ScoutTaskPrivacy Privacy { get; init; } = ScoutTaskPrivacy.Public;

    public string Language { get; init; } = string.Empty;

    public ScoutMergeStyle MergeStyle { get; init; } = ScoutMergeStyle.LocalSquash;

    /// <summary>The GitHub PR number — zero for a local commit.</summary>
    public int Number { get; init; }

    public ScoutBaseRef BaseRef { get; init; } = ScoutBaseRef.None;

    public bool IsPrivate => Privacy == ScoutTaskPrivacy.Private;

    /// <summary>The candidate's public label — its id (S4: a private task's id IS its label).</summary>
    public string Label => Id.Value;

    public bool IsPullRequest => Number > 0;

    /// <summary>The key the draw orders a candidate by (D10): <c>repo#number</c> for a GitHub PR, <c>repo@fixsha</c> (the full sha) for a
    /// local commit — <c>repo</c> being the SHORT repository name (<see cref="ScoutRepository.ShortName"/>).</summary>
    public string Key => IsPullRequest
        ? $"{ScoutRepository.ShortName(Repository)}#{Number.ToString(CultureInfo.InvariantCulture)}"
        : $"{ScoutRepository.ShortName(Repository)}@{Fix.Value}";
}

/// <summary>What the mirror says about the fix's lineage: the fix's parents in order (<c>git rev-list --parents -n 1 fix</c>), and — for a
/// rebase-merged PR — whether the recorded base is an ancestor of the fix.</summary>
public sealed record ScoutParentFacts(IReadOnlyList<CommitSha> FixParents, bool BaseRefIsAncestorOfFix)
{
    /// <summary>Not read — every parent check fails on it, so a candidate is never admitted on lineage nobody looked at.</summary>
    public static ScoutParentFacts Unread { get; } = new([], false);

    /// <summary>The facts from git's words: the <c>rev-list --parents -n 1</c> line (<c>fix p1 [p2 …]</c>) and the ancestor answer.</summary>
    public static Outcome<ScoutParentFacts> Parse(string revListParents, bool baseRefIsAncestorOfFix)
    {
        var shas = revListParents.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(CommitSha.Parse).ToList();

        return shas.OfType<Outcome<CommitSha>.Fail>().FirstOrDefault() is { } bad
            ? Outcome<ScoutParentFacts>.Failure($"the fix's parents could not be read — {bad.Reason}")
            : Outcome<ScoutParentFacts>.Success(new ScoutParentFacts([.. shas.OfType<Outcome<CommitSha>.Ok>().Select(o => o.Value)], baseRefIsAncestorOfFix));
    }
}

/// <summary>A candidate with what the mirror says about it: P's commit date, the ground truth G (§3.1) computed from the diff, and the fix's
/// lineage for the parent check.</summary>
public sealed record ScoutCandidateFacts(ScoutCandidate Candidate, DateTimeOffset ParentDate, IReadOnlyList<string> GroundTruth, ScoutParentFacts Parents);

/// <summary>A candidate the selection left out, and why — every exclusion is recorded, never silent.</summary>
public sealed record ScoutExclusion(ScoutTaskId Id, string Reason);

/// <summary>What the selection admitted and what it left out.</summary>
public sealed record ScoutSelection(IReadOnlyList<ScoutCandidateFacts> Admitted, IReadOnlyList<ScoutExclusion> Excluded);

/// <summary>The mechanical, pre-registered admission of §3.1 (the draw over the admitted is <see cref="ScoutSelectionDraw"/>):
/// <list type="bullet">
/// <item><b>after the later cut-off</b> — a candidate whose P was committed ON or before the later of the two models' published
/// cut-offs is excluded: a model that saw the code in training gains recall that says nothing about scouting;</item>
/// <item><b>P verified per merge style</b> (S7, <see cref="ScoutParentCheck"/>) — a mismatch excludes the candidate by id with a reason CODE,
/// never a value;</item>
/// <item><b>3–15 files of G</b>, not docs-only — a release, a version bump or a dependency bump leaves too few files once lockfiles,
/// manifests and changelogs are out.</item>
/// </list>
/// The pilot's tasks are no longer excluded by id: under D10 the pilot is drawn from repositories 9 and 10, so it cannot overlap the main
/// tasks by construction.</summary>
public static class ScoutCandidates
{
    public const int MinFiles = 3;

    public const int MaxFiles = 15;

    public static ScoutSelection Admit(IReadOnlyList<ScoutCandidateFacts> candidates, DateOnly laterCutoff)
    {
        var decided = candidates.Select(c => (Facts: c, Reason: Exclusion(c, laterCutoff))).ToList();

        return new ScoutSelection(
            [.. decided.Where(d => d.Reason.Length == 0).Select(d => d.Facts)],
            [.. decided.Where(d => d.Reason.Length > 0).Select(d => new ScoutExclusion(d.Facts.Candidate.Id, d.Reason))]);
    }

    private static string Exclusion(ScoutCandidateFacts c, DateOnly laterCutoff) =>
        (DateOnly.FromDateTime(c.ParentDate.UtcDateTime) > laterCutoff, ScoutParentCheck.Refusal(c.Candidate, c.Parents)) switch
        {
            (false, _) => $"P was committed {c.ParentDate.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}, not after the later cut-off {laterCutoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            (_, { Length: > 0 } code) => code,
            _ => ScoutGroundTruth.Refusal(c.GroundTruth),
        };
}

/// <summary>The ground truth G of §3.1 — <c>git diff --name-only P S</c> restricted to files that exist at P, minus submodule gitlinks (mode 160000 at P), lockfiles, generated
/// files, changelogs, version manifests, and the PR's own plan or results document — and the identifiers a diff ADDED, which the leak
/// check refuses in a statement.</summary>
/// <summary>One entry of P's tree: its git mode (<c>100644</c>, <c>100755</c>, <c>120000</c> a symlink, <c>160000</c> a submodule's GITLINK) and
/// its repository-relative path.</summary>
public sealed record ScoutTreeEntry(string Mode, string Path)
{
    /// <summary>The mode of a gitlink — a submodule's pinned commit, recorded in the tree as one entry.</summary>
    public const string GitlinkMode = "160000";

    public bool IsGitlink => Mode == GitlinkMode;
}

public static partial class ScoutGroundTruth
{
    private static readonly string[] LockFiles =
        ["package-lock.json", "yarn.lock", "pnpm-lock.yaml", "packages.lock.json", "cargo.lock", "poetry.lock", "gemfile.lock", "composer.lock", "go.sum"];

    private static readonly string[] VersionManifests = ["version.json", ".release-please-manifest.json", "version", "version.txt"];

    private static readonly string[] DocExtensions = [".md", ".txt", ".rst", ".adoc"];

    public static IReadOnlyList<string> Of(IReadOnlyList<string> changed, IReadOnlySet<string> atParent, string planFile) =>
        [.. changed.Select(Slash).Where(atParent.Contains).Where(path => !Excluded(path, Slash(planFile))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>G from P's tree as <c>git ls-tree -r</c> lists it, entry by entry with its mode — a submodule's GITLINK (mode 160000) is not a tree
    /// file a scout can name, so it never enters G (§3.1 as amended 2026-10-12: found in a candidate whose G held one).</summary>
    public static IReadOnlyList<string> Of(IReadOnlyList<string> changed, IReadOnlyList<ScoutTreeEntry> atParent, string planFile) =>
        Of(changed, atParent.Where(e => !e.IsGitlink).Select(e => e.Path).ToHashSet(StringComparer.Ordinal), planFile);

    /// <summary>The entries of <c>git ls-tree -r &lt;P&gt;</c> — <c>&lt;mode&gt; &lt;type&gt; &lt;object&gt;\t&lt;path&gt;</c> per line; a line that is not one is skipped.</summary>
    public static IReadOnlyList<ScoutTreeEntry> TreeEntries(string lsTree) =>
        [.. lsTree.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[0].Split(' ').Length == 3)
            .Select(parts => new ScoutTreeEntry(parts[0].Split(' ')[0], parts[1].Trim()))];

    /// <summary>Why a G cannot be a task — too few or too many files, or nothing but documents; empty when it can.</summary>
    public static string Refusal(IReadOnlyList<string> groundTruth) =>
        (groundTruth.Count is >= ScoutCandidates.MinFiles and <= ScoutCandidates.MaxFiles, groundTruth.All(IsDocument)) switch
        {
            (false, _) => $"G has {groundTruth.Count} file(s) that existed at P — a task needs {ScoutCandidates.MinFiles}–{ScoutCandidates.MaxFiles}",
            (_, true) => "every file of G is a document — a docs-only change is not a scouting task",
            _ => string.Empty,
        };

    /// <summary>The identifiers on the diff's ADDED lines that occur nowhere in the tree at P — an identifier the change introduced. Words
    /// shorter than <see cref="LeakCheck.MinimumToken"/> are not identifiers here, as the leak check does not check them.</summary>
    public static IReadOnlyList<string> AddedIdentifiers(IEnumerable<string> addedLines, IReadOnlySet<string> identifiersAtParent) =>
        [.. addedLines.SelectMany(Identifiers).Where(id => !identifiersAtParent.Contains(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    /// <summary>Every identifier-shaped word of a text: a letter or <c>_</c>, then letters, digits or <c>_</c>, at least three long.</summary>
    public static IEnumerable<string> Identifiers(string text) => Identifier().Matches(text).Select(m => m.Value);

    private static bool Excluded(string path, string planFile)
    {
        var name = path.Split('/')[^1].ToLowerInvariant();

        return string.Equals(path, planFile, StringComparison.Ordinal)
            || LockFiles.Contains(name) || name.EndsWith(".lock", StringComparison.Ordinal)
            || VersionManifests.Contains(name) || name.StartsWith("changelog", StringComparison.Ordinal)
            || Generated(name) || PlanOrResults(name);
    }

    private static bool Generated(string name) =>
        name.EndsWith(".designer.cs", StringComparison.Ordinal) || name.EndsWith(".g.cs", StringComparison.Ordinal)
        || name.EndsWith("modelsnapshot.cs", StringComparison.Ordinal) || name.EndsWith(".min.js", StringComparison.Ordinal);

    private static bool PlanOrResults(string name) =>
        (name.StartsWith("plan_", StringComparison.Ordinal) || name.StartsWith("results_", StringComparison.Ordinal)) && name.EndsWith(".md", StringComparison.Ordinal);

    private static bool IsDocument(string path) => DocExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static string Slash(string path) => path.Replace('\\', '/').Trim();

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]{2,}")]
    private static partial Regex Identifier();
}
