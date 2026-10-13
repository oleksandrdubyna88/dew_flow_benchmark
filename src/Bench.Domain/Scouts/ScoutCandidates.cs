using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>One candidate task as the operator lists it (§3.1) — a squash-merged change in a PUBLIC family repository: its id, the
/// repository slug, P (the parent) and S (the squash commit), the question kind, the frozen statement, and the PR's own plan or results
/// document (repository-relative; empty when it has none) — removed from the tree when it existed at P, and never part of G.</summary>
public sealed record ScoutCandidate(
    ScoutTaskId Id, string Repository, CommitSha Parent, CommitSha Fix, ScoutQuestionKind Kind, string Statement, string PlanFile);

/// <summary>A candidate with what the mirror says about it: P's commit date, and the ground truth G (§3.1) computed from the diff.</summary>
public sealed record ScoutCandidateFacts(ScoutCandidate Candidate, DateTimeOffset ParentDate, IReadOnlyList<string> GroundTruth);

/// <summary>A candidate the selection left out, and why — every exclusion is recorded, never silent.</summary>
public sealed record ScoutExclusion(ScoutTaskId Id, string Reason);

/// <summary>What the selection admitted and what it left out.</summary>
public sealed record ScoutSelection(IReadOnlyList<ScoutCandidateFacts> Admitted, IReadOnlyList<ScoutExclusion> Excluded);

/// <summary>The mechanical, pre-registered selection of §3.1:
/// <list type="bullet">
/// <item><b>after the later cut-off</b> — a candidate whose P was committed ON or before the later of the two models' published
/// cut-offs is excluded: a model that saw the code in training gains recall that says nothing about scouting;</item>
/// <item><b>never a pilot task</b> — the pilot's tasks (E2 S4) shaped the instrument and are excluded by id;</item>
/// <item><b>3–15 files of G</b>, not docs-only — a release, a version bump or a dependency bump leaves too few files once lockfiles,
/// manifests and changelogs are out;</item>
/// <item><b>a seeded draw per repository</b> — the candidates of each repository ordered by <see cref="StableHash"/> of the seed and the
/// id (reproducible on any machine, as the schedule is), the first <c>k</c> taken.</item>
/// </list></summary>
public static class ScoutCandidates
{
    public const int MinFiles = 3;

    public const int MaxFiles = 15;

    public static ScoutSelection Admit(IReadOnlyList<ScoutCandidateFacts> candidates, DateOnly laterCutoff, IReadOnlySet<string> pilotTasks)
    {
        var decided = candidates.Select(c => (Facts: c, Reason: Exclusion(c, laterCutoff, pilotTasks))).ToList();

        return new ScoutSelection(
            [.. decided.Where(d => d.Reason.Length == 0).Select(d => d.Facts)],
            [.. decided.Where(d => d.Reason.Length > 0).Select(d => new ScoutExclusion(d.Facts.Candidate.Id, d.Reason))]);
    }

    private static string Exclusion(ScoutCandidateFacts c, DateOnly laterCutoff, IReadOnlySet<string> pilot) =>
        (pilot.Contains(c.Candidate.Id.Value), DateOnly.FromDateTime(c.ParentDate.UtcDateTime) > laterCutoff) switch
        {
            (true, _) => "a pilot task — it shaped the instrument and is never a candidate",
            (_, false) => $"P was committed {c.ParentDate.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}, not after the later cut-off {laterCutoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            _ => ScoutGroundTruth.Refusal(c.GroundTruth),
        };

    /// <summary>Per repository, the first <paramref name="perRepository"/> of its admitted candidates in the seeded order; repositories and
    /// the result in ordinal order of their slug and the drawn order.</summary>
    public static IReadOnlyList<ScoutCandidateFacts> Draw(IReadOnlyList<ScoutCandidateFacts> admitted, int perRepository, int seed) =>
        [.. admitted.GroupBy(c => c.Candidate.Repository, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.OrderBy(c => StableHash.Of($"{seed.ToString(CultureInfo.InvariantCulture)}\u001f{c.Candidate.Id.Value}"), StringComparer.Ordinal).Take(perRepository))];
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
