namespace Bench.Delivered;

/// <summary>What the two asks say.
///
/// <para><b>In the module rather than in <c>prompts/</c>, deliberately.</b> This project keeps agent briefs
/// as files a person can edit, and these are not that: the scale's wording IS the measured artefact — models
/// quote the matching line back and land on its score reproducibly — so a text an operator could edit
/// without bumping the protocol would silently make two runs incomparable. The catalog's editability is a
/// feature for briefs whose wording is a preference; here it would be a hole.</para>
///
/// <para>Every string below is covered by <see cref="WeightingProtocol.Protocol"/>. Change one and the
/// protocol id has to move with it, which is the whole reason a score carries that string.</para>
/// </summary>
public static class DeliveredWorkPrompts
{
    /// <summary>The decomposition ask: what the change DID, step by step.</summary>
    public const string DecomposeSystem =
        "You read a unified diff and account for what it changed, step by step.\n\n"
        + "Answer with JSON only: {\"steps\":[{\"key\":\"s1\",\"what\":\"…\",\"anchor\":\"path/File.cs#Member\"}],"
        + "\"capped\":false,\"reason\":\"\"}\n\n"
        + "Rules:\n"
        + "- Every step needs an anchor naming a file the diff actually touched. A step nobody can locate in "
        + "the diff cannot be checked, and is the cheapest thing in the world to invent.\n"
        + "- Keys are s1, s2, s3 … and each appears once.\n"
        + "- One step is one unit of work, not one hunk and not one file. Five distinct gates added to one "
        + "class are five steps; the same rename applied across twenty files is one.\n"
        + "- Account for as much of the change as the steps honestly can. If some of it genuinely does not "
        + "decompose — generated code, a mechanical rename, boilerplate — set \"capped\": true and say in "
        + "\"reason\" WHICH part and WHY. Naming a cause is the point; restating that you could not "
        + "decompose it further is not a cause.";

    /// <summary>The weighing ask: what each step is WORTH.</summary>
    public static string WeighSystem =>
        "You price each step of a change on a fixed scale.\n\n"
        + "Answer with JSON only: {\"scores\":[{\"key\":\"s1\",\"score\":3,\"why\":\"…\"}]}\n\n"
        + "Score every key you are given, exactly once, and invent none.\n\n"
        + "The scale:\n\n"
        + WeightingProtocol.Scale
        + "\n\nGive \"why\" in one sentence naming what the step does, not how large it is.";

    /// <summary>The diff, and the steps when there are steps to price.</summary>
    public static string Diff(string cleanedDiff) => $"The diff:\n\n```diff\n{cleanedDiff}\n```";

    public static string Steps(IReadOnlyList<DecomposedStep> steps) =>
        "Price these steps:\n\n"
        + string.Join('\n', steps.Select(s => $"- {s.Key}: {s.What} (at {s.Anchor})"));

    /// <summary>The ONE re-ask the gate allows, naming the shortfall in the numbers it was judged by.
    /// <para>
    /// It names the figures rather than saying "do better", because the first is actionable and the second
    /// is how a re-ask produces a differently-worded version of the same answer.
    /// </para></summary>
    public static string ReAsk(CoverageVerdict verdict) =>
        $"That decomposition accounts for {verdict.Coverage:P0} of the change, and {verdict.Threshold:P0} is "
        + "what a change this size is held to.\n\n"
        + "Go through the diff again and add the steps you left out. Do not split existing steps to raise the "
        + "number — a decomposition padded into smaller pieces scores no better and is the thing this check "
        + "exists to catch.\n\n"
        + "If the remainder genuinely does not decompose, set \"capped\": true and name the cause.";
}
