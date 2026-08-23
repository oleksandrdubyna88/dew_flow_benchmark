namespace Bench.Delivered;

/// <summary>What "a comment" and "a string" mean in one language.
///
/// <para><b>Why this type exists at all.</b> The source normalizer drops any line starting with <c>#</c> —
/// correct for PHP and Python, and wrong for C#, where <c>#</c> opens the preprocessor. Dropping
/// <c>#if</c>/<c>#else</c>/<c>#endif</c> does not remove a comment; it MERGES two mutually exclusive
/// branches into one nonsense statement, and the joiner then folds the wreckage into a logical line. So the
/// comment syntax became per-language, keyed by extension — which is a widening of what the source already
/// did for stylesheets rather than a new idea.</para>
/// </summary>
/// <param name="HashOpensAComment">PHP and Python: yes. C#: no — see the type note.</param>
/// <param name="NonLogicDirectives">Preprocessor lines that carry no logic and drop like comments.
/// <c>#region</c> is presentation and <c>#pragma</c>/<c>#nullable</c> are compiler settings; none of them
/// changes which statements compile, which is exactly what separates them from <c>#if</c>.</param>
/// <param name="ConfiguringAnnotations">Comment lines that CONFIGURE behaviour and therefore survive.
/// Empty for C#: its attributes are real <c>[…]</c> statements that were never comments to begin with, so
/// nothing has to be rescued from the comment rule — while <c>///</c> doc comments drop like any other.</param>
/// <param name="VerbatimStrings">Whether <c>@"…"</c> and <c>"""…"""</c> exist. The masker must know: in a
/// verbatim string a backslash is an ordinary character, and applying C-style escaping to one leaves the
/// mask open to the end of the line — after which every <c>//</c> in it stops being seen.</param>
public sealed record LanguageProfile(
    bool HashOpensAComment,
    IReadOnlyList<string> NonLogicDirectives,
    IReadOnlyList<string> ConfiguringAnnotations,
    bool VerbatimStrings)
{
    /// <summary>C#. The profile this project actually measures with.</summary>
    public static LanguageProfile CSharp { get; } = new(
        HashOpensAComment: false,
        NonLogicDirectives: ["#region", "#endregion", "#pragma", "#nullable", "#line", "#warning", "#error"],
        ConfiguringAnnotations: [],
        VerbatimStrings: true);

    /// <summary>The source's own behaviour, kept so its fixtures still measure identically. Changing this
    /// would silently invalidate the parity evidence the port rests on.</summary>
    public static LanguageProfile Curly { get; } = new(
        HashOpensAComment: true,
        NonLogicDirectives: [],
        ConfiguringAnnotations:
            ["@ORM\\", "@Route", "@Assert\\", "@Serializer\\", "@JMS\\", "@Groups", "@Security"],
        VerbatimStrings: false);

    /// <summary>Doc-only tags. Shared: a <c>@param</c> is documentation in every language that has one, and
    /// the source measured this list against its own corpus.</summary>
    public static IReadOnlyList<string> DocOnlyTags { get; } =
    [
        "@param", "@return", "@returns", "@var", "@throws", "@throw", "@author", "@since",
        "@deprecated", "@see", "@link", "@inheritdoc", "@package", "@license", "@copyright",
        "@example", "@todo", "@internal", "@covers", "@dataProvider", "@test",
    ];

    /// <summary>The profile for a path, by extension. Anything unrecognised gets the source's behaviour,
    /// which is the conservative choice: it is the one whose measurements exist.</summary>
    public static LanguageProfile For(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? CSharp : Curly;

    /// <summary>In a stylesheet <c>.</c>, <c>:</c> and <c>::</c> OPEN a rule (<c>.btn {</c>, <c>:root {</c>)
    /// rather than continuing an expression, so they must not attach to the pending line.</summary>
    public static bool IsStylesheet(string path) =>
        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".scss", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".less", StringComparison.OrdinalIgnoreCase);
}
