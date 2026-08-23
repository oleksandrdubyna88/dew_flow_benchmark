using System.Text;
using System.Text.RegularExpressions;

namespace Bench.Delivered;

/// <summary>What a changed file counts AS.</summary>
public enum PathFate
{
    /// <summary>The direct implementation of logic — the thing being measured.</summary>
    Counted,

    /// <summary>Excluded from size, still shown to a judge as proof.</summary>
    Evidence,

    /// <summary>No size and no evidence value. The path is still RECORDED — never silently gone.</summary>
    Dropped,
}

/// <summary>One row of the table: a name, what its files count as, and the globs that claim them.</summary>
/// <param name="Inherited">True for a row carried over from <c>scoreMeter</c> unchanged. Marked rather
/// than merged, so a reader can tell a rule this project measured from one it accepted on another
/// stack's evidence — the same *inherited calibration* discipline the weighting protocol uses.</param>
public sealed record PathCategory(string Name, PathFate Fate, IReadOnlyList<string> Globs, bool Inherited);

/// <summary>Path → fate, and the two properties that must never be "tidied".
///
/// <para><b>Ported mechanism, replaced table.</b> The rules below are <c>scoreMeter</c>'s
/// <c>PathCategories</c> exactly — case sensitivity, first-match-wins order, the glob translation, the
/// three fates, dropped-is-recorded. The TABLE is this project's, because the source's is PHP/JS-tuned
/// (<c>**/*Test.php</c>, <c>**/behat/**</c>, <c>**/Version20*.php</c>) and would misprice a C# diff.</para>
///
/// <para><b>1. Matching is CASE-SENSITIVE, and that is measurement-pinned.</b> Both source repositories
/// kept hand-written data-fix commands under <c>Command/Migration/IssueNNNN/</c> while schema migrations
/// lived in <c>doctrine_migrations/</c>. Case-insensitive matching read the former as the latter and
/// measured a whole 9.42-hour ticket as <b>0 lines</b>. The same trap is live here: C#'s EF folder is
/// <c>Migrations/</c> and a lowercase <c>migrations/</c> row must not claim it by accident, which is why
/// both spellings appear and are separately named.</para>
///
/// <para><b>2. Order is significant — first match wins</b>, so the no-value trees come first and a
/// lockfile under <c>vendor/</c> is never read as evidence.</para>
/// </summary>
public static class PathCategoryTable
{
    /// <summary>The seeded table. Data rather than code branches, so a project can extend it without
    /// editing a matcher — and so the C# band can be proven to change nothing for the inherited rows.</summary>
    public static IReadOnlyList<PathCategory> Seed { get; } =
    [
        new("vendored", PathFate.Dropped, [
            "**/vendor/**", "**/node_modules/**", "**/bower_components/**",
            "**/.venv/**", "**/site-packages/**",
        ], Inherited: true),

        // The C# band's dropped half. Build output carries no logic and no proof, and it is the one tree
        // a stray `git add -A` sweeps in by the thousand.
        new("build-output", PathFate.Dropped, [
            "**/obj/**", "**/bin/**",
        ], Inherited: false),

        new("lockfile", PathFate.Dropped, [
            "**/package-lock.json", "**/pnpm-lock.yaml", "**/yarn.lock",
            "**/composer.lock", "**/Gemfile.lock", "**/poetry.lock", "**/*.lock",
        ], Inherited: true),

        new("prompt_skill", PathFate.Dropped, [
            ".claude/**", "**/.claude/**", "**/skills/**", "**/prompts/**",
            "**/*.prompt.*", "**/*Prompts.*", "**/*Prompt.md",
        ], Inherited: true),

        new("test", PathFate.Evidence, [
            "**/*.test.*", "**/*.spec.*", "**/__tests__/**",
            "**/tests/**", "**/test/**", "**/Tests/**", "**/Test/**",
            "**/*Test.php", "**/*TestCase.php", "**/*.feature", "**/behat/**",
            "**/fixtures/**", "**/Fixtures/**", "**/__snapshots__/**", "**/*.snap",
            "**/phpunit.xml*", "**/behat.yml*", "**/*.dataset.*",
        ], Inherited: true),

        new("migration", PathFate.Evidence, [
            "**/migrations/**", "**/doctrine_migrations/**",
            "**/old_doctrine_migrations/**", "**/Version20*.php",
            "**/*_migration.sql", "**/*.migration.sql",
        ], Inherited: true),

        // EF's folder, capitalised — a SEPARATE row from the inherited lowercase one on purpose. Folding
        // them together would need case-insensitive matching, which is the thing that cost a 9.42-hour
        // ticket its entire size.
        new("migration-ef", PathFate.Evidence, [
            "**/Migrations/**",
        ], Inherited: false),

        new("generated", PathFate.Evidence, [
            "**/*.generated.*", "**/Generated/**", "**/generated/**",
            "**/*.min.js", "**/*.min.css", "**/dist/**", "**/build/**",
            "**/*.g.php", "**/*_pb2.py", "**/*.pb.go", "**/*.d.ts", "**/*.gen.go",
            "**/schema.graphql", "**/*.map", "**/*.min.map",
            "**/openapi*.json", "**/openapi*.yaml", "**/openapi*.yml",
            "**/swagger*.json", "**/swagger*.yaml", "**/swagger*.yml",
            "**/api-docs/**", "**/apidoc/**", "**/*ApiClient.php",
            "**/Client/Generated/**", "**/generated-client/**",
        ], Inherited: true),

        // The C# band's generated half. `*.generated.cs` is already claimed by the inherited row above;
        // these two spellings are not, and both are written by tooling rather than by an author.
        new("generated-csharp", PathFate.Evidence, [
            "**/*.Designer.cs", "**/*.g.cs",
        ], Inherited: false),

        // Build SHAPE is proof, not priced logic: a csproj change is real evidence that a project moved,
        // and pricing it as implementation would pay for a one-line package bump like a feature.
        new("build-shape", PathFate.Evidence, [
            "**/*.csproj", "**/*.props", "**/*.targets", "**/*.slnx", "**/*.sln",
        ], Inherited: false),

        new("docs", PathFate.Evidence, [
            "**/*.md", "**/*.rst", "**/*.adoc", "**/docs/**", "**/doc/**",
            "**/Documentation/**", "**/CHANGELOG*", "**/LICENSE*",
        ], Inherited: true),

        new("translation", PathFate.Evidence, [
            "**/translations/**", "**/Resources/translations/**", "**/*.po",
            "**/*.mo", "**/*.xlf", "**/messages.*.yml", "**/messages.*.yaml",
        ], Inherited: true),
    ];

    private static readonly (PathCategory Row, Regex[] Patterns)[] Compiled =
    [
        .. Seed.Select(row => (row, (Regex[])[.. row.Globs.Select(Compile)])),
    ];

    /// <summary>The category name for a path, or <c>logic</c> when nothing claims it.</summary>
    public static string Categorize(string path) => Match(path)?.Name ?? "logic";

    /// <summary>What a path counts as. Unclaimed is <see cref="PathFate.Counted"/> — the default is to
    /// PRICE a file, so a tree this table has never seen is measured rather than silently ignored.</summary>
    public static PathFate Fate(string path) => Match(path)?.Fate ?? PathFate.Counted;

    private static PathCategory? Match(string path)
    {
        foreach (var (row, patterns) in Compiled)
        {
            if (Array.Exists(patterns, p => p.IsMatch(path)))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>Whether one glob claims one path, in this table's dialect.
    /// <para>
    /// Public because the dialect is part of the module's contract, not an internal detail: callers write
    /// globs of their own (<see cref="DiffCleaner.Clean"/>'s extra exclusions), and a caller guessing at
    /// whether <c>*</c> crosses a directory boundary would write an exclusion that silently matches nothing.
    /// </para></summary>
    public static bool MatchesGlob(string glob, string path) => Compile(glob).IsMatch(path);

    private static Regex Compile(string glob) =>
        new(GlobToPattern(glob), RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Glob → regex: <c>**/</c> matches any leading directories including none, <c>**</c> any
    /// depth, <c>*</c> one segment, <c>?</c> one character, <c>{a,b}</c> alternation. Ported verbatim —
    /// the table's meaning is the translation's meaning, and a subtly different one re-fates files.</summary>
    internal static string GlobToPattern(string glob)
    {
        var pattern = new StringBuilder("^");
        var i = 0;

        while (i < glob.Length)
        {
            i += Append(pattern, glob, i);
        }

        return pattern.Append('$').ToString();
    }

    /// <summary>One token of the glob, returning how many characters it consumed.</summary>
    private static int Append(StringBuilder pattern, string glob, int i) =>
        glob[i] switch
        {
            '*' => Star(pattern, glob, i),
            '?' => Literal(pattern, "[^/]"),
            '{' when glob.IndexOf('}', i) > i => Alternation(pattern, glob, i),
            _ => Literal(pattern, Regex.Escape(glob[i].ToString())),
        };

    private static int Star(StringBuilder pattern, string glob, int i)
    {
        var doubled = i + 1 < glob.Length && glob[i + 1] == '*';

        return (doubled, doubled && i + 2 < glob.Length && glob[i + 2] == '/') switch
        {
            (true, true) => Consume(pattern, "(?:.*/)?", 3),
            (true, false) => Consume(pattern, ".*", 2),
            _ => Consume(pattern, "[^/]*", 1),
        };
    }

    private static int Alternation(StringBuilder pattern, string glob, int i)
    {
        var end = glob.IndexOf('}', i);
        var alternatives = glob[(i + 1)..end].Split(',');

        pattern.Append("(?:").Append(string.Join('|', alternatives.Select(Regex.Escape))).Append(')');

        return end + 1 - i;
    }

    private static int Literal(StringBuilder pattern, string emit) => Consume(pattern, emit, 1);

    private static int Consume(StringBuilder pattern, string emit, int width)
    {
        pattern.Append(emit);
        return width;
    }
}
