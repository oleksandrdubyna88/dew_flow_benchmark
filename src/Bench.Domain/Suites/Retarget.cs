namespace Bench.Domain.Suites;

/// <summary>What the target tree says about one anchor, read by an adapter and handed to the domain.
/// The domain does no IO; it decides, which is what makes the decision testable without a checkout.</summary>
/// <param name="FileExists">Whether the anchor's path resolves at the new commit.</param>
/// <param name="MemberFound">Whether the member key was located in that file.</param>
/// <param name="MemberStart">Where it was found, 1-based; 0 when nothing was located.</param>
public readonly record struct AnchorProbe(bool FileExists, bool MemberFound, int MemberStart)
{
    public static AnchorProbe Missing => new(false, false, 0);

    public static AnchorProbe FileOnly => new(true, false, 0);

    public static AnchorProbe FoundAt(int line) => new(true, true, line);
}

/// <summary>The verdict on carrying one anchor to a new commit. A closed union, so a caller cannot
/// forget the case that matters — an anchor that still resolves but at a different line.</summary>
public abstract record RetargetVerdict
{
    private RetargetVerdict() { }

    /// <summary>Resolves exactly where it did. Safe to reuse.</summary>
    public sealed record Unchanged : RetargetVerdict;

    /// <summary>Still resolves, at a different line. The anchor is rewritten and the change recorded —
    /// never applied silently, because a moved anchor is also how a wrong one starts scoring.</summary>
    public sealed record Moved(int From, int To) : RetargetVerdict;

    /// <summary>No longer resolves. The question needs a human before it is measured again.</summary>
    public sealed record Vanished(string Reason) : RetargetVerdict;
}

/// <summary>Carrying a frozen suite from the commit it was authored against to a newer one.
/// <para>
/// Re-running the same questions at a new commit is the point of a benchmark — it is how a regression
/// becomes visible. What is forbidden is doing it <em>silently</em>: an expectation is a claim about a
/// tree, and a tree that moved underneath it turns a scored miss into a fact about the suite rather
/// than about the engine.
/// </para></summary>
public static class Retarget
{
    public static RetargetVerdict Verdict(SourceAnchor anchor, AnchorProbe probe)
    {
        if (!probe.FileExists)
        {
            return new RetargetVerdict.Vanished($"{anchor.FilePath} does not exist at the new commit");
        }

        if (anchor.IsWholeFile)
        {
            return new RetargetVerdict.Unchanged();
        }

        return probe.MemberFound
            ? LineVerdict(anchor, probe.MemberStart)
            : new RetargetVerdict.Vanished($"{anchor.MemberKey} was not found in {anchor.FilePath} at the new commit");
    }

    /// <summary>Applies a verdict, returning the anchor to store and whether a human must look at it.
    /// A <see cref="RetargetVerdict.Vanished"/> anchor is returned unchanged: it is not repaired here,
    /// it is reported.</summary>
    public static (SourceAnchor Anchor, bool NeedsReview) Apply(
        SourceAnchor anchor, RetargetVerdict verdict, Targets.CommitSha newCommit) =>
        verdict switch
        {
            RetargetVerdict.Unchanged => (anchor with { AuthoredAt = newCommit }, false),
            RetargetVerdict.Moved m => (Shift(anchor, m, newCommit), true),
            _ => (anchor, true),
        };

    private static RetargetVerdict LineVerdict(SourceAnchor anchor, int foundAt) =>
        anchor.Lines.IsWhole || anchor.Lines.Start == foundAt
            ? new RetargetVerdict.Unchanged()
            : new RetargetVerdict.Moved(anchor.Lines.Start, foundAt);

    private static SourceAnchor Shift(SourceAnchor anchor, RetargetVerdict.Moved moved, Targets.CommitSha newCommit)
    {
        var length = anchor.Lines.End - anchor.Lines.Start;
        return anchor with
        {
            Lines = new LineSpan(moved.To, moved.To + length),
            AuthoredAt = newCommit,
        };
    }
}
