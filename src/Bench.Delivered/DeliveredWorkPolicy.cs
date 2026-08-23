using System.Text.RegularExpressions;

namespace Bench.Delivered;

/// <summary>One weighed unit of delivered work.</summary>
/// <param name="Why">The weigher's own justification. The policy READS it — a unit that declares itself a
/// repeat is treated differently from one that merely sits beside a sibling — which is why the text is part
/// of the value rather than a display field.</param>
public sealed record UnitScore(string Key, int Score, string Why);

/// <summary>One unit the policy changed. The raw score is never overwritten: the trail shows both what the
/// model said and what the policy did, and a report that could only show the second would be unable to
/// answer whether the correction or the model produced a number.</summary>
/// <param name="AppliedScore">0 for a unit the allowance dropped.</param>
public sealed record Adjustment(string Key, int RawScore, int AppliedScore, string Rule);

/// <param name="Total">The sum over the surviving units at their APPLIED scores.
/// <para>
/// A plain sum. The source multiplied SQL units by a configured factor; that multiplier is a claim about
/// what kind of work is worth more, fitted on a stack whose output was largely schema migrations, and this
/// project has measured nothing of the sort. Porting it would import a value judgement wearing a constant's
/// clothes — the same reason the layer weights stayed behind.
/// </para></param>
public sealed record PolicyResult(IReadOnlyList<UnitScore> Applied, int Total, IReadOnlyList<Adjustment> Adjustments)
{
    public static PolicyResult Empty { get; } = new([], 0, []);

    /// <summary>Whether any correction fired. A run where the policy changed nothing and one where it was
    /// never consulted must not read the same.</summary>
    public bool Corrected => Adjustments.Count > 0;

    public string Describe =>
        Adjustments.Count == 0
            ? $"{Total} over {Applied.Count} unit(s), uncorrected"
            : $"{Total} over {Applied.Count} unit(s) · {Adjustments.Count} adjustment(s): "
                + string.Join(", ", Adjustments.GroupBy(a => a.Rule).Select(g => $"{g.Count()}× {g.Key}"));
}

/// <param name="AnchorFileByKey">Unit key → the file its anchor names. A <c>#symbol</c> suffix names a
/// place INSIDE a file, not a different file, so the policy groups by the file part. A key with no anchor
/// never groups with anything.</param>
/// <param name="RescuedKeys">Keys admitted by a reviewing pass rather than found in the diff itself.</param>
/// <param name="DiffUnitCount">How many units the diff-side stage actually found, or <c>null</c> when that
/// reading is unavailable — the policy then keeps every rescue rather than inventing a denominator.
/// Renamed from the source's <c>PrPointCount</c>: there is no pull request here, and the number's meaning
/// is "evidence the diff itself carries".</param>
public sealed record PolicyInput(
    IReadOnlyList<UnitScore> Scores,
    IReadOnlyDictionary<string, string> AnchorFileByKey,
    IReadOnlySet<string> RescuedKeys,
    int? DiffUnitCount);

/// <summary>The deterministic corrections applied to weighed unit scores before they are summed.
///
/// <para><b>Policy lives in code, after the model, on purpose.</b> A model cannot change it, and it can be
/// recomputed over historical runs from their persisted stages without paying for a single call. That
/// recompute property is why this type takes values and returns values: nothing here reads a store.</para>
///
/// <para>Both rules answer defects MEASURED on the source corpus, and both constants are declared in
/// <see cref="Inherited"/> with the measurement that produced them — see that file before trusting a
/// number here.</para>
///
/// <para><b>The rescue allowance.</b> Matched units are never touched: each is tied to work the matcher saw
/// in the diff. Rescues are admitted strongest-first only until the scored units reach the allowance. The
/// leak it closes concentrates in SMALL changes, which is exactly where a delivered-work score is easiest
/// to inflate — a one-line diff scored 79.0 on 22 rescued points.</para>
///
/// <para><b>The near-duplicate cap.</b> A unit that <em>declares itself</em> a repeat of a sibling on the
/// same anchor file is capped. Co-location alone NEVER caps — five distinct gates in one fat manager class
/// are five real units — and a cross-file "mirrors X" is following a pattern rather than repeating work, so
/// it is exempt too. Both exemptions are the difference between a cap and a penalty on large files.</para>
/// </summary>
public static partial class DeliveredWorkPolicy
{
    public const string NearDuplicateRule = "near-duplicate-cap";

    public const string RescueAllowanceRule = "rescue-allowance";

    [GeneratedRegex(Inherited.DeclaredMirrorVocabulary, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeclaredMirror { get; }

    public static PolicyResult Apply(PolicyInput input)
    {
        var adjustments = new List<Adjustment>();
        var capped = CapDeclaredMirrors(input, adjustments);
        var admitted = AdmitRescues(input, capped, adjustments);

        return new PolicyResult(admitted, admitted.Sum(unit => unit.Score), adjustments);
    }

    /// <summary>Caps every declared mirror of a same-file sibling, keeping the STRONGEST unit on each file
    /// at full price. Order is deterministic — score descending, then key — so the same input always elects
    /// the same survivor, which is what lets a rescore reproduce a published number exactly.</summary>
    private static List<UnitScore> CapDeclaredMirrors(PolicyInput input, List<Adjustment> adjustments)
    {
        var seenPerFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var byKey = new Dictionary<string, UnitScore>(StringComparer.Ordinal);

        foreach (var unit in input.Scores
                     .OrderByDescending(u => u.Score)
                     .ThenBy(u => u.Key, StringComparer.Ordinal))
        {
            byKey[unit.Key] = Cap(unit, RepeatsOnItsFile(input, unit, seenPerFile), adjustments);
        }

        // Original order preserved: the policy adjusts VALUES, never reorders the trail.
        return [.. input.Scores.Select(unit => byKey[unit.Key])];
    }

    private static UnitScore Cap(UnitScore unit, bool repeatOnFile, List<Adjustment> adjustments)
    {
        if (!repeatOnFile || unit.Score <= Inherited.NearDuplicateCap || !DeclaredMirror.IsMatch(unit.Why))
        {
            return unit;
        }

        adjustments.Add(new Adjustment(unit.Key, unit.Score, Inherited.NearDuplicateCap, NearDuplicateRule));

        return unit with { Score = Inherited.NearDuplicateCap };
    }

    /// <summary>Whether a stronger sibling has already been seen on this unit's anchor file. Mutates the
    /// tally as it goes, which is why it is called exactly once per unit in the ordered walk.</summary>
    private static bool RepeatsOnItsFile(
        PolicyInput input, UnitScore unit, Dictionary<string, int> seenPerFile)
    {
        if (AnchorFileOf(input, unit.Key) is not { } file)
        {
            return false;
        }

        seenPerFile.TryGetValue(file, out var seen);
        seenPerFile[file] = seen + 1;

        return seen > 0;
    }

    /// <summary>Keeps every matched unit, then admits rescues strongest-first until the scored units reach
    /// the allowance. A dropped rescue is RECORDED at applied score 0 rather than silently vanishing — the
    /// difference between a correction a reader can audit and a number that simply came out lower.</summary>
    private static List<UnitScore> AdmitRescues(
        PolicyInput input, List<UnitScore> capped, List<Adjustment> adjustments)
    {
        if (input.DiffUnitCount is not { } evidence)
        {
            return capped;
        }

        var matched = capped.Count(u => !input.RescuedKeys.Contains(u.Key));
        var allowance = Math.Max(0, (Inherited.RescueAllowancePerEvidenceUnit * evidence) - matched);

        var admitted = capped
            .Where(u => input.RescuedKeys.Contains(u.Key))
            .OrderByDescending(u => u.Score)
            .ThenBy(u => u.Key, StringComparer.Ordinal)
            .Take(allowance)
            .Select(u => u.Key)
            .ToHashSet(StringComparer.Ordinal);

        var kept = new List<UnitScore>(capped.Count);

        foreach (var unit in capped)
        {
            if (!input.RescuedKeys.Contains(unit.Key) || admitted.Contains(unit.Key))
            {
                kept.Add(unit);
                continue;
            }

            // The model's OWN score for the trail, even where the cap already lowered it.
            adjustments.Add(new Adjustment(
                unit.Key, RawScoreOf(input, unit.Key), AppliedScore: 0, RescueAllowanceRule));
        }

        return kept;
    }

    private static string? AnchorFileOf(PolicyInput input, string key) =>
        input.AnchorFileByKey.TryGetValue(key, out var anchor) && !string.IsNullOrWhiteSpace(anchor)
            ? anchor.Split('#')[0]
            : null;

    private static int RawScoreOf(PolicyInput input, string key) =>
        input.Scores.First(u => string.Equals(u.Key, key, StringComparison.Ordinal)).Score;
}
