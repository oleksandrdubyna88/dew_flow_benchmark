using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>A scout's <c>KEY FILES</c> block (§3.3): at most 10 paths, read as written — the recall matcher normalises them.</summary>
/// <param name="BlockFound">The block's header was there. A scout without one listed nothing, which is a fact, not an error.</param>
/// <param name="Paths">The parseable entries among the first <see cref="ScoutOutput.MaxLines"/> lines, cleaned of bullets and backticks.</param>
/// <param name="Unparseable">Lines among the first ten that are not one path — counted, never dropped silently.</param>
/// <param name="Extra">Lines beyond the tenth — counted, not read.</param>
public sealed record ScoutKeyFiles(bool BlockFound, IReadOnlyList<string> Paths, int Unparseable, int Extra);

/// <summary>One parsed claim: <c>path[:line] — statement</c>. The line is absent when the claim cites none.</summary>
public sealed record ScoutClaim(string Path, CapturedCount Line, string Statement);

/// <summary>A scout's <c>CLAIMS</c> block (§3.6, "claims, exactly").</summary>
/// <param name="BlockFound">False makes the scout SILENT: it contributes zero assessed claims and is counted as silent.</param>
/// <param name="Claims">The claims to assess — the parseable lines among the first ten.</param>
/// <param name="Unparseable">Lines among the first ten that do not parse as <c>path[:line] — statement</c>: counted and reported per arm,
/// never assessed, never dropped silently.</param>
/// <param name="Extra">Lines beyond the tenth: counted, not assessed.</param>
public sealed record ScoutClaims(bool BlockFound, IReadOnlyList<ScoutClaim> Claims, int Unparseable, int Extra)
{
    public bool Silent => !BlockFound;
}

/// <summary>The output contract's two parsers. A block starts at a line that reads <c>KEY FILES</c> or <c>CLAIMS</c> once markdown
/// decoration is stripped (<c>##</c>, <c>**</c>, a trailing colon), and runs to the next header or the end; blank lines inside are
/// skipped. Bullets (<c>-</c>, <c>*</c>, <c>1.</c>) and backticks are stripped from each line before it is read.</summary>
public static partial class ScoutOutput
{
    public const int MaxLines = 10;

    public const string KeyFilesHeader = "KEY FILES";

    public const string ClaimsHeader = "CLAIMS";

    /// <summary>The largest line number a claim may cite — a model's number is a client's input, so it is clamped (reliability rule).</summary>
    public const int MaxLine = 1_000_000;

    public static ScoutKeyFiles KeyFiles(string report)
    {
        var (found, lines) = Block(report, KeyFilesHeader);
        var read = lines.Take(MaxLines).ToList();
        var paths = read.Where(IsOnePath).ToList();

        return new ScoutKeyFiles(found, paths, read.Count - paths.Count, Math.Max(0, lines.Count - MaxLines));
    }

    public static ScoutClaims Claims(string report)
    {
        var (found, lines) = Block(report, ClaimsHeader);
        var read = lines.Take(MaxLines).ToList();
        var claims = read.SelectMany(Claim).ToList();

        return new ScoutClaims(found, claims, read.Count - claims.Count, Math.Max(0, lines.Count - MaxLines));
    }

    /// <summary>One claim line as one claim, or none when it does not parse — a path with no statement, a statement with no path, a
    /// line number that is not one. The caller counts the lines that gave none as unparseable.</summary>
    private static IReadOnlyList<ScoutClaim> Claim(string line)
    {
        var match = ClaimLine().Match(line);

        return match.Success
            ? CitedLine(match.Groups["line"]).Match<IReadOnlyList<ScoutClaim>>(
                cited => [new ScoutClaim(match.Groups["path"].Value, cited, match.Groups["statement"].Value.Trim())],
                _ => [])
            : [];
    }

    /// <summary>The cited line: absent when the claim cites none, a number in 1..<see cref="MaxLine"/>, or refused — a number a model made
    /// up past the cap is a client's input, clamped by refusal.</summary>
    private static Outcome<CapturedCount> CitedLine(Group cited)
    {
        if (!cited.Success)
        {
            return Outcome<CapturedCount>.Success(CapturedCount.Unavailable("the claim cites no line"));
        }

        return int.TryParse(cited.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= MaxLine
            ? Outcome<CapturedCount>.Success(CapturedCount.Number(number))
            : Outcome<CapturedCount>.Failure($"line {cited.Value} is not a line");
    }

    /// <summary><c>path[:line] — statement</c>: the path one token with a dot or a slash in it (a file), the separator an em dash, an en
    /// dash or a spaced hyphen, the statement non-empty.</summary>
    [GeneratedRegex(@"^(?<path>[^\s:]*[./\\][^\s:]*)(?::(?<line>\d+)(?:-\d+)?)?\s*(?:—|–|\s-\s|\s--\s)\s*(?<statement>\S.*)$")]
    private static partial Regex ClaimLine();

    /// <summary>A key-file line is ONE token that names a file (a dot or a slash in it) — anything with spaces is a sentence.</summary>
    private static bool IsOnePath(string line) => !line.Any(char.IsWhiteSpace) && line.IndexOfAny(['.', '/', '\\']) >= 0;

    private static (bool Found, IReadOnlyList<string> Lines) Block(string report, string header)
    {
        var lines = report.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => Header(l) == header);

        if (start < 0)
        {
            return (false, []);
        }

        return (true, [.. lines.Skip(start + 1).TakeWhile(l => Header(l) is not (KeyFilesHeader or ClaimsHeader)).Select(Clean).Where(l => l.Length > 0)]);
    }

    private static string Header(string line) => line.Trim().TrimStart('#', '*', ' ').TrimEnd('*', ':', ' ').ToUpperInvariant();

    private static string Clean(string line) => Bullet().Replace(line.Trim(), string.Empty).Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    [GeneratedRegex(@"^(?:[-*•]|\d+[.)])\s+")]
    private static partial Regex Bullet();
}
