namespace Bench.Domain.Trace;

/// <summary>Which ask of the delivered-work pipeline a payload came from.</summary>
public enum DeliveredStage
{
    /// <summary>What the change did, step by step, each anchored in the diff.</summary>
    Decompose,

    /// <summary>What each step is worth on the scale.</summary>
    Weigh,

    /// <summary>The gate's verdict on the decomposition's coverage, and the re-ask it may ask for.</summary>
    Coverage,
}

/// <summary>One raw model exchange from the delivered-work pipeline, kept so the score can be recomputed.
///
/// <para><b>This table is PERMANENT, deliberately, and it is the one place where that is the right
/// answer.</b> The property the whole delivered-work port exists to preserve is that policy and figures
/// recompute over historical runs <em>without one model call</em> — and a payload rolled up or aged out is
/// a run that can no longer be rescored, which is that property gone. So its projected size is a budget
/// line a retention listing PRINTS rather than a cleanup target: the decompose payload is a step-by-step
/// account of a whole diff, there are up to three per result, and at this project's stated target of tens
/// of thousands of cells that is tens of gigabytes for this table alone. Stated here rather than
/// discovered later, because the shared rule is that anything which grows names its owner before the first
/// write.</para>
///
/// <para>What MAY be dropped without losing the property is the prompt text, which is reconstructible from
/// <see cref="Protocol"/> plus <see cref="PromptHash"/> — which is why both are fields rather than one.</para>
/// </summary>
/// <param name="Ordinal">Which attempt this was, from 0. The gate allows exactly one re-ask, so a stage
/// with two payloads is a re-asked one — and that is readable from the data rather than only from a log.</param>
/// <param name="PayloadJson">The reply AS IT ARRIVED, before any parsing. A stored parse is a stored
/// interpretation: it could not be re-read under a fixed parser, which is half of what a rescore is for.</param>
/// <param name="PromptHash">What was asked, as a hash. Two runs whose payloads differ under one hash are
/// the model being non-deterministic; under two hashes they are not comparable at all.</param>
/// <param name="Protocol">The scale and rules in force, source acknowledged inside it. A score is
/// comparable only with scores produced under the same string.</param>
public sealed record StagePayload(
    Guid Id,
    Guid ResultId,
    DeliveredStage Stage,
    int Ordinal,
    string PayloadJson,
    string PromptHash,
    string Protocol,
    DateTimeOffset CreatedAt)
{
    public static StagePayload Of(
        Guid resultId,
        DeliveredStage stage,
        int ordinal,
        string payloadJson,
        string promptHash,
        string protocol,
        DateTimeOffset now) =>
        new(Guid.CreateVersion7(), resultId, stage, ordinal, payloadJson, promptHash, protocol, now);

    /// <summary>Whether this payload came from the gate's one re-ask rather than the first attempt.</summary>
    public bool IsReAsk => Ordinal > 0;

    public string Describe => $"{Stage}#{Ordinal} · {PayloadJson.Length} chars · {Protocol}";
}
