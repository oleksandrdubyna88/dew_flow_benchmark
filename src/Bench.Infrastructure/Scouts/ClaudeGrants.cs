using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Infrastructure.Scouts;

/// <summary>The directories a Claude settings file GRANTS (<c>permissions.additionalDirectories</c>) — measured 2026-10-12 on claude 2.1.289:
/// a scout launched with the allow-list from a one-commit tree was DENIED its parent directory, and still READ and GLOBBED inside a directory
/// the operator's user settings granted (<c>tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-confine-allowlist-grant.ndjson</c>). So a granted
/// directory is readable from ANY tree, and the boundary of §3.2 is every readable location — not where the tree is placed.
/// <para>
/// A grant is resolved as the CLI would: <c>~</c> to the home directory, a relative path against the directory the session runs in.
/// </para></summary>
public static class ClaudeGrants
{
    /// <summary>The user's own settings file — the one the operator's grants live in.</summary>
    public static string UserSettings => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    /// <summary>A project's settings files relative to its root, as git names them.</summary>
    public static IReadOnlyList<string> ProjectSettingsPaths { get; } = [".claude/settings.json", ".claude/settings.local.json"];

    /// <summary>A project's settings files, as the CLI reads them from the project root.</summary>
    public static IReadOnlyList<string> ProjectSettings(string projectRoot) =>
        [.. ProjectSettingsPaths.Select(path => Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)))];

    /// <summary>Every directory <paramref name="settingsFiles"/> grant, resolved against <paramref name="sessionDirectory"/>; a file that is
    /// absent grants nothing; one that cannot be read is a refusal — a grant nobody could read is a grant nobody checked.</summary>
    public static Outcome<IReadOnlyList<string>> Read(IReadOnlyList<string> settingsFiles, string sessionDirectory)
    {
        var grants = new List<string>();

        foreach (var file in settingsFiles.Where(File.Exists))
        {
            var read = Parse(File.ReadAllText(file), sessionDirectory);

            if (read is Outcome<IReadOnlyList<string>>.Fail fail)
            {
                return Outcome<IReadOnlyList<string>>.Failure($"{file}: {fail.Reason}");
            }

            grants.AddRange(((Outcome<IReadOnlyList<string>>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<string>>.Success(grants);
    }

    public static Outcome<IReadOnlyList<string>> Parse(string json, string sessionDirectory)
    {
        try
        {
            return Outcome<IReadOnlyList<string>>.Success(
                [.. (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })?["permissions"]?["additionalDirectories"] as JsonArray ?? [])
                    .Select(node => node is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty)
                    .Where(text => text.Length > 0)
                    .Select(text => Resolve(text, sessionDirectory))]);
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<string>>.Failure($"not JSON — {ex.Message}");
        }
    }

    public static string Resolve(string grant, string sessionDirectory)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expanded = grant == "~" || grant.StartsWith("~/", StringComparison.Ordinal) || grant.StartsWith("~\\", StringComparison.Ordinal)
            ? Path.Combine(home, grant.Length > 2 ? grant[2..] : string.Empty)
            : grant;

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded, sessionDirectory));
    }

    /// <summary>Whether two directories overlap — one is the other or lies inside it, compared as the filesystem does on this platform.</summary>
    public static bool Overlap(string a, string b) => Within(a, b) || Within(b, a);

    public static bool Within(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        return string.Equals(full, parent, comparison) || full.StartsWith(parent + Path.DirectorySeparatorChar, comparison);
    }
}

/// <summary>The trees' STARTUP GUARD (§3.2): every scout verb that builds or launches refuses — exit 4, a sentence naming both paths — when
/// the trees root (or the mirror root) overlaps a directory the user's Claude settings grant, since a granted directory is readable from any tree and a sibling
/// task's tree (or the mirror beside it) would then be within a scout's reach. A relative grant is resolved as if from a tree inside the
/// root, because that is where the session runs (<c>..</c> from a tree IS the trees root). A tree's OWN project settings are checked when the
/// tree is prepared (<see cref="ProjectRefusal"/>).</summary>
public static class ScoutGrantGuard
{
    /// <param name="mirrorRoot">The checkout root the bare mirrors live under — they hold every task's FUTURE, so a grant over them is the
    /// same leak as a grant over the trees.</param>
    public static string Refusal(string treesRoot, string mirrorRoot, IReadOnlyList<string> userSettingsFiles)
    {
        var grants = ClaudeGrants.Read(userSettingsFiles, Path.Combine(treesRoot, "tree"));

        if (grants is Outcome<IReadOnlyList<string>>.Fail fail)
        {
            return $"the Claude user settings could not be read — {fail.Reason}; a grant nobody could read is a grant nobody checked";
        }

        var granted = ((Outcome<IReadOnlyList<string>>.Ok)grants).Value;

        return new[] { ("trees root", treesRoot), ("mirror root", mirrorRoot) }
            .SelectMany(root => granted.Where(g => ClaudeGrants.Overlap(root.Item2, g)).Select(g => Sentence(root.Item1, root.Item2, g)))
            .FirstOrDefault(string.Empty);
    }

    private static string Sentence(string name, string root, string granted) =>
        $"the {name} {Path.GetFullPath(root)} overlaps {granted}, a directory the Claude user settings grant (permissions.additionalDirectories) — "
        + "a granted directory is readable from any tree, so a scout could read another task's tree or its own future; choose a root outside every granted directory";

    /// <summary>A tree whose OWN project settings grant a directory outside the tree is refused: the CLI would hand every scout launched in it
    /// that directory, and nothing says what history it holds.</summary>
    public static string ProjectRefusal(string treeRoot) =>
        ClaudeGrants.Read(ClaudeGrants.ProjectSettings(treeRoot), treeRoot) switch
        {
            Outcome<IReadOnlyList<string>>.Fail fail => $"the tree's own Claude settings could not be read — {fail.Reason}",
            Outcome<IReadOnlyList<string>>.Ok ok when ok.Value.FirstOrDefault(g => !ClaudeGrants.Within(g, treeRoot)) is { } outside =>
                $"the tree's own .claude settings grant {outside}, outside the tree — a scout launched there could read it; the task is excluded",
            _ => string.Empty,
        };

    /// <summary>The admission's version of <see cref="ProjectRefusal"/> (§3.1 as amended 2026-10-13), over the settings texts git read at P
    /// before any tree exists: <see cref="ScoutCandidates.ProjectSettingsUnreadable"/> when one could not be read or parsed,
    /// <see cref="ScoutCandidates.ProjectGrantsOutside"/> when one grants a directory outside <paramref name="treeRoot"/> (where the tree
    /// will be built, so a relative grant resolves as the CLI would resolve it there), otherwise empty. A CODE, never the path.</summary>
    public static string ProjectCode(IReadOnlyList<Outcome<string>> settingsTexts, string treeRoot)
    {
        var read = settingsTexts.Select(text => text.Match(json => ClaudeGrants.Parse(json, treeRoot), Outcome<IReadOnlyList<string>>.Failure)).ToList();

        return read.Any(r => r is Outcome<IReadOnlyList<string>>.Fail) ? ScoutCandidates.ProjectSettingsUnreadable
            : read.OfType<Outcome<IReadOnlyList<string>>.Ok>().SelectMany(r => r.Value).Any(g => !ClaudeGrants.Within(g, treeRoot)) ? ScoutCandidates.ProjectGrantsOutside
            : string.Empty;
    }
}
