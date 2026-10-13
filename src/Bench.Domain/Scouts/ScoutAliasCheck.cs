using Bench.Domain.Probes;

namespace Bench.Domain.Scouts;

/// <summary>How an alias check came out (§3.14).</summary>
public abstract record AliasVerdict
{
    private AliasVerdict()
    {
    }

    /// <summary>Every scout answered with the id the verdict recorded for its alias.</summary>
    public sealed record Same(IReadOnlyList<string> Ids) : AliasVerdict;

    /// <summary>At least one alias now resolves to another id: what moved, from what to what.</summary>
    public sealed record Moved(IReadOnlyList<(string Alias, string Was, string Now)> Changes) : AliasVerdict;

    /// <summary>The check measured nothing — a scout with no <c>resolvedModel</c>, or not both aliases answered.</summary>
    public sealed record NotMeasured(string Reason) : AliasVerdict;
}

/// <summary>The alias watch of §3.14: ONE tiny cell — a two-scout probe like §2's, one <c>sonnet</c> and one <c>opus</c> code explorer listing
/// a one-file directory — budget-capped at $1, and each scout's <c>resolvedModel</c> compared with the id the verdict recorded. Detection lives
/// here because a session cannot see the move: the Agent result a caller receives carries the text and an agent id, not the model.</summary>
public static class ScoutAliasCheck
{
    public const decimal CapUsd = 1m;

    /// <summary>What the run is, said first — S5 saw a driver shaped as a blind verbatim relay refused by the API's safeguards (E2 S8).</summary>
    public const string Purpose =
        "This is a benchmark check. It confirms which model versions answer the \"sonnet\" and \"opus\" aliases when this session launches "
        + "read-only code-exploring subagents. Nothing is modified: each subagent only lists one directory of this snapshot.";

    public const string ScoutPrompt = "List the files in your working directory. Read nothing else. Reply with KEY FILES then CLAIMS, one line each.";

    public static string Driver() =>
        Purpose + "\n\n"
        + "Launch exactly 2 Agent calls in ONE message, all at once, then reply DONE and nothing else.\n"
        + $"Both calls use subagent_type \"{ScoutArms.CodeExplorerType}\". The first uses model \"sonnet\", the second model \"opus\".\n"
        + "Both calls get the prompt between the markers as it is, because the check compares the two answers to the same question. "
        + "Read nothing yourself and call no other tool.\n\n"
        + "=== PROMPT BEGIN ===\n" + ScoutPrompt + "\n=== PROMPT END ===\n";

    public static AliasVerdict Compare(SubagentTranscript transcript, string opusId, string sonnetId)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal) { ["opus"] = opusId, ["sonnet"] = sonnetId };
        var answered = transcript.Agents.Where(a => expected.ContainsKey(a.InputModel) && a.Result.ResolvedModel.Length > 0)
            .GroupBy(a => a.InputModel, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Result.ResolvedModel, StringComparer.Ordinal);

        if (answered.Count < expected.Count)
        {
            return new AliasVerdict.NotMeasured($"{expected.Count - answered.Count} alias(es) came back with no resolvedModel — the check measured nothing; nothing is concluded");
        }

        var changes = expected.Where(e => !string.Equals(answered[e.Key], e.Value, StringComparison.Ordinal))
            .Select(e => (e.Key, e.Value, answered[e.Key])).OrderBy(c => c.Key, StringComparer.Ordinal).ToList();

        return changes.Count == 0 ? new AliasVerdict.Same([.. answered.Values.Order(StringComparer.Ordinal)]) : new AliasVerdict.Moved(changes);
    }
}
