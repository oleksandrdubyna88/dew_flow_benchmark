using System.Text.RegularExpressions;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One binding of a step: a brief FIELD name and the file it stands for, relative to the arm folder (<c>HA_JSON</c> → <c>notes/hA.json</c>). The path is kept as
/// written and is the destination: two destinations are compared <see cref="StringComparison.OrdinalIgnoreCase"/> wherever they are compared, so the comparison fails closed
/// on a host whose file system ignores case. The folder it stands under is not compared that way: it is exactly <c>notes</c> or <c>work</c>, the two a launch grants.</summary>
public sealed partial record OutputBinding(string Field, string RelativePath)
{
    private static readonly string[] Folders = ["notes", "work"];

    /// <summary>The binding the two texts spell, or why they are none. The field is capitals and underscores — the filler's own <c>{{NAME}}</c>, so a name the filler cannot
    /// match is a binding no template can use. The path is relative to the arm folder, written with forward slashes, under exactly <c>notes</c> or <c>work</c> (the two folders a launch
    /// grants — <c>Notes</c> is another folder on a case-sensitive host, one no launch grants), with no empty, <c>.</c> or <c>..</c> segment, no control, format, line-separator or non-ASCII-space character (NUL, a
    /// newline, U+200B, U+2028, U+00A0) and no space at either end of a segment.</summary>
    public static Outcome<OutputBinding> Parse(string field, string path) =>
        !FieldPattern().IsMatch(field)
            ? Outcome<OutputBinding>.Failure($"{ExecutableRule.Printable(field)} is not a field name — capitals and underscores, as a template's {{{{NAME}}}}")
            : !IsOutputPath(path)
                ? Outcome<OutputBinding>.Failure($"'{ExecutableRule.Printable(path)}' is not an output path — relative, under notes/ or work/, forward slashes, no '..' (notes/hA.json)")
                : Outcome<OutputBinding>.Success(new OutputBinding(field, path));

    private static bool IsOutputPath(string path) => IsPlain(path) && UnderAFolder(path.Split('/'));

    private static bool IsPlain(string path) => path.IndexOfAny(['\\', '\0']) < 0 && !Path.IsPathRooted(path);

    private static bool UnderAFolder(string[] segments) =>
        segments.Length >= 2 && Folders.Contains(segments[0], StringComparer.Ordinal) && segments.All(IsUsable);

    private static bool IsUsable(string segment) => segment.Length > 0 && segment is not ("." or "..") && IsClean(segment);

    private static bool IsClean(string segment) =>
        segment[0] != ' ' && segment[^1] != ' ' && !segment.Any(ExecutableRule.IsInvisible);

    [GeneratedRegex(@"^[A-Z_]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex FieldPattern();
}
