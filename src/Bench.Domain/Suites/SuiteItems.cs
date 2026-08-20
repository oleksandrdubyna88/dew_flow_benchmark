namespace Bench.Domain.Suites;

/// <summary>What an expectation points at in the target tree, and the commit it was authored against.
/// <para>
/// The commit is not decoration. <c>Foo.cs:120</c> is true at exactly one tree; carrying the same
/// anchor forward to a newer commit without re-validating it is how a suite starts scoring an engine
/// against lines that moved. Re-targeting is therefore an explicit operation, never a silent reuse.
/// </para></summary>
public sealed record SourceAnchor(string FilePath, string MemberKey, LineSpan Lines, Targets.CommitSha AuthoredAt)
{
    public static SourceAnchor File(string filePath, Targets.CommitSha authoredAt) =>
        new(Normalise(filePath), string.Empty, LineSpan.Whole, authoredAt);

    public static SourceAnchor Member(string filePath, string memberKey, LineSpan lines, Targets.CommitSha authoredAt) =>
        new(Normalise(filePath), memberKey, lines, authoredAt);

    public bool IsWholeFile => MemberKey.Length == 0;

    public string Canonical => $"{FilePath}#{MemberKey}@{Lines.Canonical}";

    /// <summary>Forward slashes always: the same suite is authored on Windows and replayed on the CI
    /// runner, and a backslash would make an anchor machine-specific.</summary>
    private static string Normalise(string filePath) => filePath.Replace('\\', '/').TrimStart('/');
}

/// <summary>An inclusive 1-based line range, or <see cref="Whole"/> for "the file, no line claim".</summary>
public readonly record struct LineSpan(int Start, int End)
{
    public static LineSpan Whole => new(0, 0);

    public bool IsWhole => Start == 0 && End == 0;

    public string Canonical => IsWhole ? "*" : $"{Start}-{End}";
}

public enum ExpectationKind
{
    /// <summary>The engine must surface this file at all.</summary>
    File,

    /// <summary>The engine must surface this member — the strict form, and the one worth measuring.</summary>
    Member,

    /// <summary>The produced answer must contain this text.</summary>
    AnswerContains,

    /// <summary>The produced answer must NOT contain this text.</summary>
    AnswerExcludes,

    /// <summary>The subject must have CALLED this tool. The tool's name rides in
    /// <see cref="Expectation.Text"/> — no new field, because a name is exactly what the anchor of a tool
    /// expectation is.</summary>
    ToolUsed,

    /// <summary>The subject must NOT have called this tool.
    /// <para>The trap half, and it matters as much as the other: a description that makes a model reach for
    /// a tool where it should not have is a defect in the description, and it is invisible unless something
    /// asserts the negative.</para></summary>
    ToolNotUsed,
}

/// <summary>One thing that must be true of a leg's result. <see cref="Required"/> separates the
/// expectations a question is about from the ones that merely enrich its score.</summary>
public sealed record Expectation(ExpectationKind Kind, SourceAnchor Anchor, string Text, bool Required)
{
    public static Expectation Member(SourceAnchor anchor, bool required = true) =>
        new(ExpectationKind.Member, anchor, string.Empty, required);

    public static Expectation File(SourceAnchor anchor, bool required = true) =>
        new(ExpectationKind.File, anchor, string.Empty, required);

    public bool IsRetrieval => Kind is ExpectationKind.File or ExpectationKind.Member;

    /// <summary>Whether this expectation is about a TOOL rather than about text or an anchor. The tool's
    /// name is <see cref="Text"/>.</summary>
    public bool IsTool => Kind is ExpectationKind.ToolUsed or ExpectationKind.ToolNotUsed;

    public string Canonical => $"{Kind}:{Anchor.Canonical}:{Text}:{(Required ? "req" : "opt")}";
}

/// <summary>One question, its expectations, and the reference answer a judge may read.</summary>
public sealed record Question(string Id, string Prompt, IReadOnlyList<Expectation> Expectations, string ReferenceAnswer)
{
    public static Question Ask(string id, string prompt, params Expectation[] expectations) =>
        new(id, prompt, expectations, string.Empty);

    public IReadOnlyList<Expectation> RetrievalExpectations => [.. Expectations.Where(e => e.IsRetrieval)];

    public IReadOnlyList<Expectation> ToolExpectations => [.. Expectations.Where(e => e.IsTool)];

    /// <summary>What SHAPE of question this is — "a graph-shaped question", "a literal lookup". Empty by
    /// default and never inferred.
    /// <para>It is what turns "is the tool better on average" — where the measured answer is roughly a wash,
    /// one point of sixty-three — into "on which KIND of question is it better", which is where the one
    /// violent inversion in the record lived: 8/8 in 254 s against 0/8 in 1 058 s on a single task.</para>
    /// <para>An <c>init</c> property, so every question ever written keeps its meaning: no affinity is a
    /// question nobody has classified, not a question classified as nothing.</para></summary>
    public string ToolAffinity { get; init; } = string.Empty;

    public string Canonical =>
        $"{Id}|{Prompt}|{ReferenceAnswer}|{string.Join(';', Expectations.Select(e => e.Canonical))}";
}
