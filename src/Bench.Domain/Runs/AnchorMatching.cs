namespace Bench.Domain.Runs;

/// <summary>The one rule for "does a named place reach an authored anchor", shared by
/// <see cref="RetrievalScoring"/> (hits against a question's anchors) and
/// <see cref="DiagnosisScoring"/> (a diagnosis's anchors against the reference fix's) — one place, so a
/// report can never print two definitions of "found".</summary>
public static class AnchorMatching
{
    /// <summary>Identity, when both sides spell a member the same way. Ordinal and whole-string: a
    /// suffix rule would let <c>Retry</c> answer for <c>NoRetry</c>, and a recall figure inflated that
    /// way is indistinguishable from a real one. An empty name matches nothing.</summary>
    public static bool SameMember(string named, string authored) =>
        named.Length > 0 && string.Equals(named, authored, StringComparison.Ordinal);

    /// <summary>Paths compare case-insensitively with separators normalised: the same suite is authored
    /// on Windows and replayed on a Linux runner, and a case or slash difference there is a fact about
    /// a filesystem rather than about the subject.</summary>
    public static bool SamePath(string named, string authored) =>
        named.Length > 0
        && string.Equals(
            named.Replace('\\', '/').TrimStart('/'),
            authored.Replace('\\', '/').TrimStart('/'),
            StringComparison.OrdinalIgnoreCase);
}
