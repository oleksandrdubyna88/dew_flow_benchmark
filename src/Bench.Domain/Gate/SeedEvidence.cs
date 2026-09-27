namespace Bench.Domain.Gate;

/// <summary>Where a planted seed's evidence sat for the reviewer. Seed recall is reported APART from evidence
/// availability: a seed the product never showed the reviewer is not a seed the reviewer missed.</summary>
public enum EvidenceWhere
{
    /// <summary>Its changed line is in the turn-1 prompt the product built.</summary>
    Pack,

    /// <summary>A seed that REMOVES a line: it is in the pack when its file's hunks are.</summary>
    PackByFile,

    /// <summary>In the repository at head and servable through a source request, but not in the pack.</summary>
    OnRequest,

    /// <summary>Its file is credential-shaped and the product never serves it.</summary>
    Withheld,

    /// <summary>No turn-1 prompt has been recorded for the task yet.</summary>
    Unknown,
}

/// <summary>Reads where each seed's evidence sat off a real run's FIRST prompt per task — so it describes the
/// product's pack, not any harness's. A port of <c>report.py: seed_evidence</c>.</summary>
public static class SeedEvidence
{
    private static readonly string[] CredentialShapes = ["credential", "secret", ".env", "token"];

    /// <summary>The first line of the planted text, capped as the Python does, is what is looked for.</summary>
    public const int LineProbeChars = 60;

    public static EvidenceWhere Classify(SeedSpec seed, string turnOnePrompt)
    {
        if (CredentialShapes.Any(shape => seed.File.Contains(shape, StringComparison.OrdinalIgnoreCase)))
        {
            return EvidenceWhere.Withheld;
        }

        if (turnOnePrompt.Length == 0)
        {
            return EvidenceWhere.Unknown;
        }

        return seed.IsRemoval ? ByFile(seed, turnOnePrompt) : ByLine(seed, turnOnePrompt);
    }

    private static EvidenceWhere ByFile(SeedSpec seed, string prompt) =>
        prompt.Contains(seed.File, StringComparison.Ordinal) ? EvidenceWhere.PackByFile : EvidenceWhere.OnRequest;

    private static EvidenceWhere ByLine(SeedSpec seed, string prompt)
    {
        var probe = FirstLine(seed.New);

        return probe.Length > 0 && prompt.Contains(probe, StringComparison.Ordinal) ? EvidenceWhere.Pack : EvidenceWhere.OnRequest;
    }

    private static string FirstLine(string text)
    {
        var first = text.Trim().Split('\n')[0].TrimEnd('\r');
        return first.Length <= LineProbeChars ? first : first[..LineProbeChars];
    }
}
