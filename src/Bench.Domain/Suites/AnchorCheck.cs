namespace Bench.Domain.Suites;

/// <summary>A path resolved under a root, or a refusal.
/// <para>
/// Extracted from <c>FilesystemEngine</c>, which had the only copy of this arithmetic and needed it for the same
/// reason: a path that arrives as DATA — a tool argument, an authored question's anchor — can spell its way out
/// of the tree, and both callers read files with it.
/// </para></summary>
public static class RootedPath
{
    /// <summary>Resolve, then compare. Checking the STRING for <c>..</c> does not work, because
    /// <c>a/../../b</c> and a symlink both spell fine.</summary>
    public static Outcome<string> Under(string root, string relative)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var candidate = relative.Length == 0 ? full : Path.GetFullPath(Path.Combine(full, relative));

        return candidate == full || candidate.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? Outcome<string>.Success(candidate)
            : Outcome<string>.Failure($"'{relative}' resolves outside the tree");
    }
}

/// <summary>A file of the checked-out tree as the anchor check sees it. <see cref="Exists"/> is false for a
/// path that is absent OR outside the tree — both mean "this anchor points at nothing here".</summary>
public sealed record TreeFile(bool Exists, IReadOnlyList<string> Lines)
{
    public static TreeFile Absent => new(false, []);

    public static TreeFile Of(IReadOnlyList<string> lines) => new(true, lines);
}

public enum AnchorState
{
    /// <summary>The file is there and the claim about it is not obviously wrong.</summary>
    Resolved,

    /// <summary>No such file at this commit — or a path that escapes the tree.</summary>
    FileMissing,

    /// <summary>The line span runs past the end of the file.</summary>
    SpanBeyondFile,

    /// <summary>The file and the span exist, but the member's name appears nowhere in that span.</summary>
    MemberOutsideSpan,
}

public sealed record AnchorProof(SourceAnchor Anchor, AnchorState State, string Detail)
{
    public bool Resolved => State == AnchorState.Resolved;

    public string Describe => Resolved ? $"{Anchor.Canonical} resolves" : $"{Anchor.Canonical}: {Detail}";
}

/// <summary>Checking a question's ground truth against the tree it claims to describe — mechanically, before
/// anything is asked of a reviewer.
/// <para>
/// <b>Why it exists, measured 2026-08-18.</b> All three live reviewer notes on the first vetted pair LED with
/// exactly this check — <i>"Verified StoreNaming.KindOf exists at lines 45-49 of …"</i> — and the review
/// contract's first rejection reason is an expectation that points at nothing. So the most-cited half of a
/// review is arithmetic, and paying three agent launches for it is paying for the wrong thing.
/// </para>
/// <para>
/// <b>What it proves and what it does not.</b> That the file is present and the member's name occurs inside the
/// claimed span. It does NOT prove the member is DEFINED there — a name in a comment satisfies it — and it says
/// nothing about whether the question is any good. It catches the failure that actually happens: a line range
/// that is stale, off by a refactor, or invented.
/// </para></summary>
public static class AnchorCheck
{
    /// <summary>Every retrieval expectation's proof, in the question's own order. Answer expectations carry no
    /// anchor and are skipped — there is nothing about a tree to check in "the answer contains 'k + rank'".</summary>
    public static IReadOnlyList<AnchorProof> Verify(Question question, Func<string, TreeFile> read) =>
        [.. question.RetrievalExpectations.Select(expectation => Verify(expectation.Anchor, read))];

    public static AnchorProof Verify(SourceAnchor anchor, Func<string, TreeFile> read)
    {
        var file = read(anchor.FilePath);

        if (!file.Exists)
        {
            return new AnchorProof(anchor, AnchorState.FileMissing, "no such file in the tree at this commit");
        }

        if (anchor.IsWholeFile || anchor.Lines.IsWhole)
        {
            return new AnchorProof(anchor, AnchorState.Resolved, string.Empty);
        }

        if (anchor.Lines.End > file.Lines.Count)
        {
            return new AnchorProof(
                anchor,
                AnchorState.SpanBeyondFile,
                $"lines {anchor.Lines.Start}–{anchor.Lines.End} claimed, the file has {file.Lines.Count}");
        }

        return Named(anchor, file)
            ? new AnchorProof(anchor, AnchorState.Resolved, string.Empty)
            : new AnchorProof(
                anchor,
                AnchorState.MemberOutsideSpan,
                $"'{Member(anchor)}' appears nowhere in lines {anchor.Lines.Start}–{anchor.Lines.End}"
                + Elsewhere(anchor, file));
    }

    /// <summary>The member's own name, without the type that owns it: an anchor spells <c>Type.Member</c> while
    /// the declaration line spells only the member.</summary>
    private static string Member(SourceAnchor anchor) =>
        anchor.MemberKey.Split('.').Last();

    private static bool Named(SourceAnchor anchor, TreeFile file) =>
        file.Lines
            .Skip(Math.Max(0, anchor.Lines.Start - 1))
            .Take(Math.Max(0, anchor.Lines.End - anchor.Lines.Start + 1))
            .Any(line => line.Contains(Member(anchor), StringComparison.Ordinal));

    /// <summary>Where the name IS, when it is somewhere else — the difference between "this question is wrong"
    /// and "this line range is stale", which is the whole of what the operator does next.</summary>
    private static string Elsewhere(SourceAnchor anchor, TreeFile file)
    {
        var found = file.Lines
            .Index()
            .Where(line => line.Item.Contains(Member(anchor), StringComparison.Ordinal))
            .Select(line => line.Index + 1)
            .Take(4)
            .ToList();

        return found.Count == 0
            ? " and nowhere in the file"
            : $", but on line(s) {string.Join(", ", found)}";
    }
}
