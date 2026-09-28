using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>A turn-1 prompt with the two things the product writes PER RUN replaced — the A/A's unit of comparison (E7,
/// S7.2a). Measured on the calibration's own records: two runs of one task, one reviewer, one product sha differ in the
/// product's 8-hex SESSION id, which fences every section (<c>--- the plan — … (fb0f5dd6) ---</c>), and in the run's data
/// directory, named on the gate-history line — and in nothing else. So a raw prompt hash can never match across runs.
/// <para>
/// The tokens are LEARNED from the lines that carry them, never pattern-replaced anywhere: the session id only on whole
/// fence lines, every fence carrying the same one; the data directory only on the one whole gate-history line. An 8-hex id
/// inside the task's own source, a quoted copy of the history sentence, a line ending — all stay, so a difference there is
/// a difference. Two ids across the fences, or two history lines, make the shape <see cref="AmbiguousLines"/>: neither can
/// be told apart as the product's, and the comparison fails naming them rather than guessing.
/// </para></summary>
public sealed partial record PromptShape
{
    private const string Session = "<session>";

    private const string DataDir = "<data-dir>";

    private PromptShape(string text, IReadOnlyList<int> ambiguousLines)
    {
        Text = text;
        AmbiguousLines = ambiguousLines;
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>The normalised prompt. PRIVATE, not merely unprinted: a prompt carries a repository's source, and a public
    /// property would ride along wherever a shape is logged, serialised or turned into a string. Only its hash leaves.</summary>
    private string Text { get; }

    /// <summary>Lower-case hex SHA-256 of the normalised text's UTF-8 bytes.</summary>
    public string Sha256 { get; }

    /// <summary>1-based lines that made the shape ambiguous; empty for a well-formed prompt.</summary>
    public IReadOnlyList<int> AmbiguousLines { get; }

    public static PromptShape Of(string prompt)
    {
        var lines = prompt.Split('\n');
        var fences = Matches(lines, FenceLine());
        var history = Matches(lines, HistoryLine());
        var sessions = fences.Select(f => f.Match.Groups["id"].Value).Distinct(StringComparer.Ordinal).Count();
        var normalised = lines.Select((line, i) => Normalise(line, i, fences, history)).ToArray();

        return new PromptShape(string.Join('\n', normalised), Ambiguous(fences, history, sessions));
    }

    /// <summary><see cref="PromptComparison.Same"/> when the shapes hash alike; otherwise the differing line POSITIONS (a line
    /// present on one side only counts), the first <see cref="PromptComparison.ListedLines"/> of them listed.</summary>
    public static PromptComparison Compare(PromptShape first, PromptShape second) =>
        (first.AmbiguousLines.Count + second.AmbiguousLines.Count, first.Sha256 == second.Sha256) switch
        {
            ( > 0, _) => new PromptComparison.Ambiguous([.. first.AmbiguousLines.Union(second.AmbiguousLines).Order()]),
            (_, true) => new PromptComparison.Same(),
            _ => Differences(first.Text.Split('\n'), second.Text.Split('\n')),
        };

    private static PromptComparison.Differs Differences(string[] first, string[] second)
    {
        var positions = Enumerable.Range(0, Math.Max(first.Length, second.Length))
            .Where(i => DiffersAt(first, second, i))
            .Select(i => i + 1)
            .ToList();

        return new PromptComparison.Differs(positions.Count, [.. positions.Take(PromptComparison.ListedLines)]);
    }

    /// <summary>A position past either side's end always differs: one side has a line there and the other has none.</summary>
    private static bool DiffersAt(string[] first, string[] second, int index) =>
        index >= first.Length || index >= second.Length || !string.Equals(first[index], second[index], StringComparison.Ordinal);

    private static IReadOnlyList<int> Ambiguous(IReadOnlyList<LineMatch> fences, IReadOnlyList<LineMatch> history, int sessions) =>
        [.. (sessions > 1 ? MinorityFences(fences) : []).Concat(history.Count > 1 ? history.Select(h => h.Index + 1) : []).Order()];

    /// <summary>The fences whose id is not the one most fences carry — the lines a reader should look at. When no id is the
    /// clear majority every fence is named: picking one would call the other side the product's on a coin toss.</summary>
    private static IEnumerable<int> MinorityFences(IReadOnlyList<LineMatch> fences)
    {
        var counts = fences.GroupBy(f => f.Match.Groups["id"].Value, StringComparer.Ordinal).Select(g => (Id: g.Key, Count: g.Count())).OrderByDescending(g => g.Count).ToList();
        var majority = counts[0].Count > counts[1].Count ? counts[0].Id : string.Empty;

        return fences.Where(f => f.Match.Groups["id"].Value != majority).Select(f => f.Index + 1);
    }

    private static string Normalise(string line, int index, IReadOnlyList<LineMatch> fences, IReadOnlyList<LineMatch> history) =>
        (fences.Any(f => f.Index == index), history.Count == 1 && history[0].Index == index) switch
        {
            (true, _) => Replace(line, fences.First(f => f.Index == index).Match.Groups["id"], Session),
            (_, true) => Replace(line, history[0].Match.Groups["dir"], DataDir),
            _ => line,
        };

    private static string Replace(string line, Group group, string token) => string.Concat(line.AsSpan(0, group.Index), token, line.AsSpan(group.Index + group.Length));

    private static List<LineMatch> Matches(string[] lines, Regex pattern) =>
        [.. lines.Select((line, i) => new LineMatch(i, pattern.Match(line))).Where(m => m.Match.Success)];

    private sealed record LineMatch(int Index, Match Match);

    /// <summary>A whole section fence: <c>--- &lt;title&gt; (&lt;8 hex&gt;) ---</c>, an optional <c>\r</c> kept as part of the line.</summary>
    [GeneratedRegex(@"^--- .+ \((?<id>[0-9a-f]{8})\) ---\r?$", RegexOptions.CultureInvariant)]
    private static partial Regex FenceLine();

    /// <summary>The product's gate-history line when the run's data directory holds no rounds database.</summary>
    [GeneratedRegex(@"^Gate history unavailable: there is no rounds database at (?<dir>.+)[\\/]coai\.db\.\r?$", RegexOptions.CultureInvariant)]
    private static partial Regex HistoryLine();
}

/// <summary>What <see cref="PromptShape.Compare"/> found. Never carries a line's text — only its 1-based position.</summary>
public abstract record PromptComparison
{
    /// <summary>How many differing positions are listed; <see cref="Differs.Count"/> says how many there were.</summary>
    public const int ListedLines = 20;

    private PromptComparison()
    {
    }

    public sealed record Same : PromptComparison;

    public sealed record Differs(int Count, IReadOnlyList<int> Lines) : PromptComparison;

    public sealed record Ambiguous(IReadOnlyList<int> Lines) : PromptComparison;
}
