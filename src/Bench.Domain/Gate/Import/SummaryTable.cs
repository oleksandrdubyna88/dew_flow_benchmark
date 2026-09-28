using System.Globalization;
using System.Text.RegularExpressions;

namespace Bench.Domain.Gate;

/// <summary>One number of a published table: a metric (a slug of its column header, <c>-part1</c> / <c>-part2</c> for an
/// <c>a / b</c> cell) and its value — or <i>not captured</i> when the cell held no number (<c>—</c>, <c>electricity</c>).</summary>
public sealed record SummaryFigure(string Metric, bool Captured, double Value);

/// <summary>One row of a published table: its position, its label (a slug of the first column) and its numbers — no text.</summary>
public sealed record SummaryRow(int Ordinal, string Label, IReadOnlyList<SummaryFigure> Figures);

/// <summary>The per-model numbers of a results document whose raw data is gone, as SUMMARY-ONLY rows: they carry a
/// citation (the document, the section, the document's hash) and no findings, and no report reads them into a run figure —
/// they are shown, never averaged with runs.</summary>
public sealed record SummaryTable(string Section, IReadOnlyList<SummaryRow> Rows);

public static partial class SummaryTables
{
    /// <summary>The first markdown table under the heading named <paramref name="heading"/> (any level, compared without
    /// case), read cell by cell. Refused when the heading or its table is not there, and when two columns slug alike —
    /// two metrics under one name would be one number overwriting another.</summary>
    public static Outcome<SummaryTable> Parse(string markdown, string heading)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, l => IsHeading(l, heading));
        var table = start < 0 ? [] : TableAfter(lines, start + 1);

        return (start, table.Count) switch
        {
            ( < 0, _) => Outcome<SummaryTable>.Failure($"no heading '{heading}' in the document"),
            (_, < 3) => Outcome<SummaryTable>.Failure($"no table under '{heading}' — a header, a separator and at least one row"),
            _ => Read(ImportSlug.Of(heading), Cells(table[0]), [.. table.Skip(2).Select(Cells)]),
        };
    }

    /// <summary>A cell's NUMBER: markdown emphasis and code marks, <c>$</c>, <c>~</c>, <c>%</c> and thousands separators
    /// ignored, a <c>k</c> or <c>M</c> suffix applied; the first number the cell carries, or none.</summary>
    public static double? Number(string cell)
    {
        var text = cell.Replace(",", string.Empty, StringComparison.Ordinal);
        var match = NumberPattern.Match(text);

        return match.Success
            ? double.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) * Scale(match.Groups["s"].Value)
            : null;
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9.])(?<n>-?\d+(?:\.\d+)?)(?:\s*(?<s>[kM]))?(?![A-Za-z0-9])")]
    private static partial Regex NumberPattern { get; }

    private static double Scale(string suffix) => suffix switch
    {
        "k" => 1_000,
        "M" => 1_000_000,
        _ => 1,
    };

    private static bool IsHeading(string line, string heading) =>
        line.StartsWith('#') && string.Equals(line.TrimStart('#').Trim(), heading.Trim(), StringComparison.OrdinalIgnoreCase);

    private static List<string> TableAfter(string[] lines, int from) =>
        [.. lines.Skip(from).SkipWhile(l => !l.TrimStart().StartsWith('|') && !l.StartsWith('#'))
            .TakeWhile(l => l.TrimStart().StartsWith('|'))];

    private static IReadOnlyList<string> Cells(string row) =>
        [.. row.Trim().Trim('|').Split('|').Select(c => c.Trim())];

    private static Outcome<SummaryTable> Read(string section, IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var metrics = header.Skip(1).Select((h, i) => ImportSlug.Of(h) is { Length: > 0 } slug ? slug : $"column-{(i + 2).ToString(CultureInfo.InvariantCulture)}").ToList();
        var twice = metrics.Select((m, i) => (m, i)).GroupBy(p => p.m, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        return twice is not null
            ? Outcome<SummaryTable>.Failure(
                $"columns '{header[twice.First().i + 1]}' and '{header[twice.Skip(1).First().i + 1]}' both read as metric '{twice.Key}' — refused rather than one overwriting the other")
            : Outcome<SummaryTable>.Success(new SummaryTable(section, [.. rows.Select((cells, i) => Row(i + 1, cells, metrics))]));
    }

    private static SummaryRow Row(int ordinal, IReadOnlyList<string> cells, IReadOnlyList<string> metrics) =>
        new(ordinal, ImportSlug.Of(cells.Count > 0 ? cells[0] : string.Empty),
            [.. metrics.SelectMany((metric, i) => Figures(metric, i + 1 < cells.Count ? cells[i + 1] : string.Empty))]);

    private static IEnumerable<SummaryFigure> Figures(string metric, string cell)
    {
        var parts = cell.Split(" / ");

        return parts.Length == 1
            ? [Figure(metric, parts[0])]
            : parts.Select((part, i) => Figure($"{metric}-part{(i + 1).ToString(CultureInfo.InvariantCulture)}", part));
    }

    private static SummaryFigure Figure(string metric, string cell) =>
        Number(cell) is { } value ? new SummaryFigure(metric, true, value) : new SummaryFigure(metric, false, 0);
}
