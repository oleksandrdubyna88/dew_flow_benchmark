using System.Text;

namespace Bench.Domain.Retrieval;

/// <summary>Why a hit's own source text is or is not here.
/// <para>
/// Three states rather than a string that may be empty, and the third is the reason this type exists.
/// The snippet is the bulk of what a retrieval lane stores — at a limit of 20 it is most of a cell — and
/// it is redundant, because the corpus at the pinned commit reproduces it. So it is kept for a window and
/// dropped after, leaving the row and every metric computed from it intact
/// (<c>todo/PLAN_variant_matrix.md</c> §3.5, *What grows, and who owns it*).
/// </para>
/// <para>
/// <b>"The engine sent none" and "we deleted it" must never render alike.</b> Both are an empty string, and
/// merging them would turn a retention decision into a claim about the engine — the same mislabelled-gap
/// defect <see cref="Trace.Captured"/> exists to prevent, one layer down.
/// </para></summary>
public enum HitTextState
{
    /// <summary>The text is here.</summary>
    Present,

    /// <summary>The engine returned no text with this hit.</summary>
    NotReported,

    /// <summary>It was here and retention dropped it. The byte count survives; the text does not.</summary>
    Pruned,
}

/// <summary>One hit's source text, with the state that says how to read an empty one.</summary>
/// <param name="Bytes">What the text WEIGHED, kept in every state. It is the payload accounting a
/// retention listing prints, so dropping the text must not drop the number that justified dropping it.</param>
public sealed record HitSnippet(HitTextState State, string Value, long Bytes, string Reason)
{
    public static HitSnippet Text(string value) =>
        new(HitTextState.Present, value, Encoding.UTF8.GetByteCount(value), string.Empty);

    public static HitSnippet NotReported(string reason) => new(HitTextState.NotReported, string.Empty, 0, reason);

    /// <summary>Rebuilds a stored snippet whose text retention has dropped. The count is what was stored
    /// before the drop, so a report can still say what the run cost.</summary>
    public static HitSnippet Pruned(long bytes, DateTimeOffset at) =>
        new(HitTextState.Pruned, string.Empty, bytes, $"kept until retention dropped it on {at:yyyy-MM-dd}");

    public bool HasText => State == HitTextState.Present;
}

/// <summary>One retrieved result, as this system stores it.
/// <para>
/// Every field here answers a question a retrieval metric asks. The span is what decides whether a hit
/// reached an anchor when the names do not line up; the channels and per-channel ranks are what separate
/// "the sparse head found it and fusion buried it" from "nothing found it at all" — the distinction a
/// funnel gives per stage and this gives per result.
/// </para></summary>
/// <param name="Rank">1-based position in the list the engine returned. The ORDER is the measurement, so
/// it is stored rather than inferred from a row order nobody guaranteed.</param>
/// <param name="Member">The readable identity — <c>Type.Member</c> — which is the form a suite's anchors
/// are authored in, so it is the form a retrieval metric can compare. Composed by the adapter from
/// whatever its engine reports; empty for an engine that reports no member structure at all.</param>
/// <param name="MemberKey">The engine's OWN identity for this member, stored verbatim and never matched
/// on: its format belongs to that engine (<c>csharp|Ns|Type|Member`0|(args)</c> for the QLN index) and an
/// anchor authored here does not share it. Kept so a later join is possible without re-running anything.
/// Two identities, and the difference is the point — one is comparable here, one is comparable there.</param>
/// <param name="Score">The score that ORDERED this hit, with <paramref name="Ordering"/> saying which
/// score that was. A score whose origin is unstated cannot be compared to anything.</param>
public sealed record RetrievedHit(
    int Rank,
    string RelativePath,
    int StartLine,
    int EndLine,
    string Member,
    string MemberKey,
    string Signature,
    double Score,
    string Ordering,
    IReadOnlyList<string> Channels,
    IReadOnlyList<int> Ranks,
    HitSnippet Snippet)
{
    /// <summary>What a prompt and a report head this hit with.</summary>
    public string Where => $"{RelativePath}:{StartLine}-{EndLine}";

    /// <summary>Whether this hit's lines overlap a span. Inclusive on both ends, and false for a hit with
    /// no span at all — a result that never said where it was cannot be said to cover anything.</summary>
    public bool Covers(int start, int end) =>
        StartLine > 0 && EndLine >= StartLine && StartLine <= end && EndLine >= start;
}
