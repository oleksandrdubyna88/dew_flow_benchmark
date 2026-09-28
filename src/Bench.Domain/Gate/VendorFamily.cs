namespace Bench.Domain.Gate;

/// <summary>Which vendor FAMILY a reviewer-catalog row's model comes from — the question behind
/// <see cref="GateVerdict.AssessorFamilyMatches"/>: an assessor of the reviewer's own family is counted apart, never
/// refused (the <c>SelfJudged</c> discipline, <c>MEASURED_LESSONS.md</c> §4d).
/// <para>
/// The model id decides first, normalised — lower case, a <c>vendor/</c> route prefix dropped
/// (<c>OpenAI/gpt-4o</c>, <c>azure/gpt-4o</c>, <c>anthropic/claude-3.5</c> are the model after the slash) — because
/// a CLI can run another vendor's model and an api row names every vendor's. The runtime word decides only when no
/// prefix is known (a <c>codex</c> row running an id nobody listed is still OpenAI's CLI), and a model nobody can
/// place is its own family: the same unknown id on both sides is a match, two different ones are not.
/// </para></summary>
public static class VendorFamily
{
    /// <summary>Model-id prefixes, longest first where one is a prefix of another.</summary>
    private static readonly (string Prefix, string Family)[] Prefixes =
    [
        ("gpt", "openai"), ("chatgpt", "openai"), ("codex", "openai"), ("o1", "openai"), ("o3", "openai"), ("o4", "openai"),
        ("claude", "anthropic"),
        ("gemini", "google"), ("gemma", "google"),
        ("grok", "xai"),
        ("qwen", "alibaba"), ("qwq", "alibaba"),
        ("deepseek", "deepseek"),
        ("glm", "zhipu"),
        ("kimi", "moonshot"), ("moonshot", "moonshot"),
        ("mistral", "mistral"), ("codestral", "mistral"), ("devstral", "mistral"), ("magistral", "mistral"),
        ("llama", "meta"),
    ];

    public static string Of(ReviewerDefinition definition)
    {
        var model = Normalise(definition.Model);
        var byModel = Prefixes.Where(p => model.StartsWith(p.Prefix, StringComparison.Ordinal)).Select(p => p.Family).FirstOrDefault(string.Empty);
        var byRuntime = ByRuntime(definition.Runtime);

        return (byModel.Length, byRuntime.Length) switch
        {
            ( > 0, _) => byModel,
            (_, > 0) => byRuntime,
            _ => $"model:{model}",
        };
    }

    /// <summary>The family of a bare model id (an assessor named only by its model — the coai-bench judge's
    /// <c>claude-opus-5</c>): its prefix's family, or the model itself when nobody can place it.</summary>
    public static string OfModel(string model)
    {
        var normalised = Normalise(model);
        var family = Prefixes.Where(p => normalised.StartsWith(p.Prefix, StringComparison.Ordinal)).Select(p => p.Family).FirstOrDefault(string.Empty);

        return family.Length > 0 ? family : $"model:{normalised}";
    }

    /// <summary>Whether an assessor of <paramref name="assessorModel"/> judges a vendor SET whose members are named only by
    /// their runtime words (<c>codex,gemini,local</c>) — a match when any member's CLI is the assessor's family. A word no
    /// family is known for (<c>local</c>) never matches.</summary>
    public static bool MatchesAnyOf(string assessorModel, IEnumerable<string> runtimeWords) =>
        runtimeWords.Select(w => Enum.TryParse<ReviewerRuntime>(w.Trim(), ignoreCase: true, out var r) ? ByRuntime(r) : string.Empty)
            .Any(f => f.Length > 0 && string.Equals(f, OfModel(assessorModel), StringComparison.Ordinal));

    public static bool Matches(ReviewerDefinition assessor, ReviewerDefinition reviewer) =>
        string.Equals(Of(assessor), Of(reviewer), StringComparison.Ordinal);

    /// <summary>Lower case, and the model after the LAST <c>/</c>: a route prefix names who serves it, not who made it.</summary>
    public static string Normalise(string model)
    {
        var lower = model.Trim().ToLowerInvariant();
        var slash = lower.LastIndexOf('/');

        return slash >= 0 ? lower[(slash + 1)..] : lower;
    }

    private static string ByRuntime(ReviewerRuntime runtime) => runtime switch
    {
        ReviewerRuntime.Codex => "openai",
        ReviewerRuntime.Claude => "anthropic",
        ReviewerRuntime.Gemini or ReviewerRuntime.Antigravity => "google",
        _ => string.Empty,
    };
}
