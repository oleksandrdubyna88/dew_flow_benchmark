namespace Bench.Domain.Scouts;

/// <summary>What leaked into a task statement.</summary>
public enum LeakKind
{
    /// <summary>A ground-truth file's whole name — <c>ProbeCampaign.cs</c>.</summary>
    Basename,

    /// <summary>A ground-truth file's name without its extension — <c>ProbeCampaign</c>.</summary>
    Stem,

    /// <summary>An identifier the change itself added — it did not exist at P, so naming it hands the scout the answer.</summary>
    AddedIdentifier,
}

/// <summary>One leak, with the token that leaked — the operator rewrites the statement, so it is named.</summary>
public sealed record LeakFinding(LeakKind Kind, string Token);

/// <summary>The automatic leak check of §3.1: a task statement is refused when it contains a basename or a stem of a
/// ground-truth file, or an identifier the diff added. A statement that names the answer measures reading, not scouting.
/// <para>
/// A basename is matched as a substring (ignoring case) — <c>probecampaign.cs</c> in any casing is the file. A stem and an
/// identifier are matched as a WHOLE WORD (ignoring case): <c>Run</c> as a stem must not refuse "running". Tokens shorter
/// than <see cref="MinimumToken"/> are not checked: a stem like <c>a</c> or <c>io</c> would refuse every sentence.
/// </para></summary>
public static class LeakCheck
{
    public const int MinimumToken = 3;

    public static IReadOnlyList<LeakFinding> Find(string statement, IReadOnlyList<string> groundTruth, IReadOnlyList<string> addedIdentifiers)
    {
        var basenames = groundTruth.Select(Basename).Where(Checked).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var stems = basenames.Select(Stem).Where(Checked).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return
        [
            .. basenames.Where(b => statement.Contains(b, StringComparison.OrdinalIgnoreCase)).Select(b => new LeakFinding(LeakKind.Basename, b)),
            .. stems.Where(s => ContainsWord(statement, s)).Select(s => new LeakFinding(LeakKind.Stem, s)),
            .. addedIdentifiers.Where(Checked).Distinct(StringComparer.Ordinal).Where(i => ContainsWord(statement, i)).Select(i => new LeakFinding(LeakKind.AddedIdentifier, i)),
        ];
    }

    private static bool Checked(string token) => token.Length >= MinimumToken;

    private static string Basename(string path) => path.Replace('\\', '/').TrimEnd('/').Split('/')[^1];

    private static string Stem(string basename) => basename.LastIndexOf('.') is var dot and > 0 ? basename[..dot] : basename;

    /// <summary>The token as a whole word: no letter, digit or underscore right before it or right after it.</summary>
    public static bool ContainsWord(string text, string token)
    {
        var from = 0;

        while (text.IndexOf(token, from, StringComparison.OrdinalIgnoreCase) is var at and >= 0)
        {
            if (!IsWordChar(text, at - 1) && !IsWordChar(text, at + token.Length))
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }

    private static bool IsWordChar(string text, int index) =>
        index >= 0 && index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_');
}
