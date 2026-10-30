using System.Globalization;
using System.Text.RegularExpressions;

namespace ModelArms.Core.Checks;

/// <summary>One line of <c>git diff --numstat</c>: the path and the lines added and deleted. A binary file's counts are <c>-</c> in git's text and 0 here (the shell's <c>+a || 0</c>).</summary>
public sealed record NumstatRow(string File, int Added, int Deleted);

/// <summary>The lines of the diff split as the judges read them: those in test files, those in source files that are not tests, those in documents. The three are NOT a partition — a document under a <c>tests/</c> folder counts in two.</summary>
public sealed record ChangedLines(int Tests, int Src, int Docs);

/// <summary>The shell's diff arithmetic (<c>objective-checks.sh:51</c>, <c>:62-63</c>) over the text of <c>git diff --numstat &lt;base&gt;..HEAD</c>.</summary>
public static class DiffStats
{
    private static readonly Regex TestFolder = new(@"tests?\/", RegexOptions.ECMAScript);

    /// <summary>The rows of the numstat text, in order. A line is <c>added TAB deleted TAB path</c>; an empty line and a line of fewer than three fields are not rows.</summary>
    public static IReadOnlyList<NumstatRow> Rows(string numstat) =>
        [.. numstat.Split('\n').Where(line => line.Length > 0).Select(line => line.Split('\t')).Where(fields => fields.Length >= 3).Select(fields => new NumstatRow(fields[2], Lines(fields[0]), Lines(fields[1])))];

    /// <summary>The shell's three rules, verbatim: a path holding <c>test/</c> or <c>tests/</c> is a test file; one that starts <c>src_</c> and is not a test file is source; one that ends <c>.md</c> is a document. They are the coai
    /// target's layout, so another target's source reads 0 (E3 §8 item 12).</summary>
    public static ChangedLines Split(IReadOnlyList<NumstatRow> rows) =>
        new(
            Sum(rows, file => TestFolder.IsMatch(file)),
            Sum(rows, file => file.StartsWith("src_", StringComparison.Ordinal) && !TestFolder.IsMatch(file)),
            Sum(rows, file => file.EndsWith(".md", StringComparison.Ordinal)));

    private static int Sum(IReadOnlyList<NumstatRow> rows, Func<string, bool> wanted) =>
        rows.Where(row => wanted(row.File)).Sum(row => row.Added + row.Deleted);

    private static int Lines(string field) =>
        int.TryParse(field.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var lines) ? lines : 0;
}
