using System.Text;
using System.Text.RegularExpressions;

namespace Bench.Delivered;

/// <summary>A size that does not depend on the author's line-break taste — the same call costs the same
/// written on one 180-character line or wrapped across five.
///
/// <para>Ported from <c>scoreMeter · Metrics/LineNormalizer.cs</c>. The continuation joiner is unchanged:
/// the leading-<c>.</c> fluent-chain head it was built for (PHP <c>-&gt;</c> chains) already covers C# LINQ
/// and builder chains, which is why that half needed no adaptation at all. What DID change is the comment
/// and string syntax, and both changes are in <see cref="LanguageProfile"/> with their reasons.</para>
/// </summary>
public static partial class LineNormalizer
{
    /// <summary>Every logical line is hard-wrapped at this width and the count is physical lines. Inherited
    /// constant: the source measured its family at 100 and nothing here has re-measured it.</summary>
    private const int WrapWidth = 100;

    /// <summary>A backstop, not a rule: a literal that confuses the bracket counter must not swallow the
    /// rest of the block.</summary>
    private const int MaxJoinLines = 40;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun { get; }

    /// <summary>A pending line ending in one of these expects the next line.</summary>
    [GeneratedRegex(
        @"(?:,|\(|\[|\.|&&|\|\||=>|->|=|\?|\+|\*|/|\bAND\b|\bOR\b|\bimplements\b|\bextends\b)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ContinuationTail { get; }

    /// <summary>A line OPENING with one of these continues the previous one. <c>*</c> and <c>/</c> are
    /// deliberately absent: a leading <c>*</c> is a docblock prefix on every kept annotation line, and
    /// including it glued consecutive declarations into one line.</summary>
    [GeneratedRegex(@"^(?:->|\?->|::|\)|\]|,|;|:|\.|&&|\|\||=>|\?|\+|=)")]
    private static partial Regex BraceHead { get; }

    /// <summary>The stylesheet variant — see <see cref="LanguageProfile.IsStylesheet"/>.</summary>
    [GeneratedRegex(@"^(?:->|\)|\]|,|;|&&|\|\||=>|\?|\+|=)")]
    private static partial Regex StylesheetHead { get; }

    /// <summary>True when a line carries no logic: blank, a non-logic directive, or a comment that is not a
    /// configuring annotation.
    /// <para>
    /// <b>The C# adaptation lives here.</b> <c>#if</c>, <c>#else</c>, <c>#elif</c> and <c>#endif</c> are
    /// KEPT — they decide what compiles, and dropping them merges two mutually exclusive branches into one
    /// statement that never existed.
    /// </para></summary>
    public static bool IsDroppable(string line, LanguageProfile language)
    {
        var trimmed = line.Trim();

        if (trimmed.Length == 0)
        {
            return true;
        }

        return trimmed.StartsWith('#') ? Directive(trimmed, language) : Comment(trimmed, language);
    }

    /// <summary>A <c>#</c> line. In a hash-comment language it is a comment unless it is a PHP attribute
    /// (<c>#[</c>); otherwise it is the preprocessor, and only the non-logic directives drop.</summary>
    private static bool Directive(string trimmed, LanguageProfile language) =>
        language.HashOpensAComment
            ? !trimmed.StartsWith("#[", StringComparison.Ordinal)
            : language.NonLogicDirectives.Any(d => trimmed.StartsWith(d, StringComparison.Ordinal));

    private static bool Comment(string trimmed, LanguageProfile language)
    {
        var isComment = trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith('*')
            || trimmed.StartsWith("*/", StringComparison.Ordinal);

        if (!isComment)
        {
            return false;
        }

        // A comment survives only when it CONFIGURES something, never when it merely documents. For C# the
        // configuring set is empty, so every comment drops — its attributes were never comments.
        return LanguageProfile.DocOnlyTags.Any(tag => trimmed.Contains(tag, StringComparison.Ordinal))
            || !language.ConfiguringAnnotations.Any(a => trimmed.Contains(a, StringComparison.Ordinal));
    }

    /// <summary>Strips a trailing inline comment after masking string literals, so a URL's <c>//</c> inside
    /// a string is never read as a comment. The mask is length-preserving.</summary>
    public static string StripInlineComment(string line, LanguageProfile language)
    {
        var masked = MaskStrings(line, language);
        var cut = masked.IndexOf("//", StringComparison.Ordinal);

        // `#` only opens a comment where it is one. In C# a `#` past column zero is inside something the
        // mask already handled, and cutting there would truncate real code.
        var hash = language.HashOpensAComment ? masked.IndexOf('#') : -1;

        if (hash >= 0 && (cut < 0 || hash < cut))
        {
            cut = hash;
        }

        return cut < 0 ? line : line[..cut];
    }

    /// <summary>Blanks the contents of string literals, preserving length.
    /// <para>
    /// <b>C# verbatim and raw strings are why this is not the source's masker.</b> In <c>@"C:\path\"</c> a
    /// backslash is an ordinary character; applying C-style escaping treats the closing quote as escaped,
    /// leaves the mask open to the end of the line, and every <c>//</c> after it stops being seen. A raw
    /// string (<c>"""…"""</c>) is worse: its content may contain unescaped quotes by design.
    /// </para></summary>
    internal static string MaskStrings(string line, LanguageProfile language)
    {
        var masked = new StringBuilder(line.Length);
        var state = new MaskState();

        for (var i = 0; i < line.Length; i++)
        {
            i += Step(masked, line, i, state, language);
        }

        return masked.ToString();
    }

    private sealed class MaskState
    {
        public char Quote { get; set; }

        public bool Escaped { get; set; }

        /// <summary>Inside <c>@"…"</c> or <c>"""…"""</c>, where backslash escaping does not apply.</summary>
        public bool Verbatim { get; set; }
    }

    /// <summary>One character, returning how many EXTRA characters it consumed beyond the first.</summary>
    private static int Step(StringBuilder masked, string line, int i, MaskState state, LanguageProfile language)
    {
        if (state.Quote != '\0')
        {
            return Inside(masked, line, i, state);
        }

        var opener = Opener(line, i, language);

        if (opener > 0)
        {
            state.Quote = '"';
            state.Verbatim = true;
            masked.Append('x', opener);
            return opener - 1;
        }

        if (line[i] is '"' or '\'')
        {
            state.Quote = line[i];
            state.Verbatim = false;
            masked.Append('x');
            return 0;
        }

        masked.Append(line[i]);
        return 0;
    }

    /// <summary>The width of a verbatim opener at this position, or 0. <c>"""</c> first: <c>@"</c> would
    /// otherwise never see a raw string, and a raw string opened as an ordinary one closes on its second
    /// quote — one character in.</summary>
    private static int Opener(string line, int i, LanguageProfile language) =>
        !language.VerbatimStrings ? 0
        : line.AsSpan(i).StartsWith("\"\"\"") ? 3
        : line.AsSpan(i).StartsWith("@\"") ? 2
        : 0;

    private static int Inside(StringBuilder masked, string line, int i, MaskState state)
    {
        masked.Append('x');

        if (state.Escaped)
        {
            state.Escaped = false;
            return 0;
        }

        if (!state.Verbatim && line[i] == '\\')
        {
            state.Escaped = true;
            return 0;
        }

        if (line[i] == state.Quote)
        {
            state.Quote = '\0';
            state.Verbatim = false;
        }

        return 0;
    }

    public static string CollapseWhitespace(string line) => WhitespaceRun.Replace(line, " ").Trim();

    /// <summary>The physical-line count of one logical line, hard-wrapped at <see cref="WrapWidth"/>.</summary>
    public static int WrappedLineCount(string logicalLine) =>
        logicalLine.Length == 0 ? 0 : (logicalLine.Length + WrapWidth - 1) / WrapWidth;

    /// <summary>Drops blanks and comments, strips trailing comments, collapses whitespace. One entry per
    /// surviving PHYSICAL line — call <see cref="Join"/> to fold continuations into logical lines.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> lines, string path)
    {
        var language = LanguageProfile.For(path);
        var normalized = new List<string>();

        foreach (var line in lines)
        {
            if (IsDroppable(line, language))
            {
                continue;
            }

            var cleaned = CollapseWhitespace(StripInlineComment(line, language));

            if (cleaned.Length > 0)
            {
                normalized.Add(cleaned);
            }
        }

        return normalized;
    }

    /// <summary>Folds continuation lines into single logical lines, so the same call costs the same whether
    /// it was written on one long line or wrapped across five. Without this the metric measures the
    /// author's line-break taste: a fluent chain would count as one line per <c>.Method()</c>.</summary>
    public static IReadOnlyList<string> Join(IReadOnlyList<string> normalizedLines, string path)
    {
        var language = LanguageProfile.For(path);
        var stylesheet = LanguageProfile.IsStylesheet(path);
        var logical = new List<string>();
        var pending = string.Empty;
        var span = 0;

        foreach (var line in normalizedLines)
        {
            if (pending.Length > 0 && !(Continues(pending, language) || Head(line, stylesheet)))
            {
                logical.Add(pending);
                pending = string.Empty;
                span = 0;
            }

            pending = pending.Length > 0 ? $"{pending} {line}".Trim() : line;

            if (++span >= MaxJoinLines)
            {
                logical.Add(pending);
                pending = string.Empty;
                span = 0;
            }
        }

        if (pending.Length > 0)
        {
            logical.Add(pending);
        }

        return logical;
    }

    /// <summary>Normalize then join — the metric's definition of a logical line.</summary>
    public static IReadOnlyList<string> NormalizeAndJoin(IEnumerable<string> lines, string path) =>
        Join(Normalize(lines, path), path);

    /// <summary>True when <paramref name="pending"/> is an unfinished statement.</summary>
    private static bool Continues(string pending, LanguageProfile language)
    {
        if (OpenDepth(pending, language) > 0)
        {
            return true;
        }

        var tail = pending.TrimEnd();

        return tail.Length > 0 && tail[^1] is not (';' or '{' or '}' or ':') && ContinuationTail.IsMatch(tail);
    }

    /// <summary>Unbalanced <c>(</c>/<c>[</c> in string-blanked code. Braces are BLOCKS, not continuations,
    /// so they are not counted.</summary>
    private static int OpenDepth(string code, LanguageProfile language)
    {
        var depth = 0;

        foreach (var c in MaskStrings(code, language))
        {
            depth += c switch
            {
                '(' or '[' => 1,
                ')' or ']' => -1,
                _ => 0,
            };
        }

        return depth;
    }

    private static bool Head(string line, bool stylesheet) =>
        stylesheet ? StylesheetHead.IsMatch(line) : BraceHead.IsMatch(line);
}
