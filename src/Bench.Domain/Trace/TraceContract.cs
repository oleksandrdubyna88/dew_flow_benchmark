namespace Bench.Domain.Trace;

/// <summary>The white-box trace contract: which stages a retrieval funnel may name, and what happens
/// when an engine claims a version this build does not know.
/// <para>
/// <b>v0 is PROVISIONAL, and its first reconciliation has already happened.</b> The stage names here
/// were originally authored from the stage list of an engine that went out of scope — a description of
/// a system rather than a payload from one, which is the footing this project has retracted claims
/// from before. On 2026-08-15 a real engine (<c>dew_flow_rag_qln</c>) emitted its first funnel, and the
/// difference was recorded rather than smoothed away: <b>five of the seven drafted names were wrong</b>.
/// See <see cref="Deviation"/> for what changed and why the emitter won every disagreement.
/// </para>
/// <para>
/// Generality lives in the CAPABILITY DECLARATION, never in the payload shape. The characteristic
/// failure of an interface with no implementation is that it becomes a bag of key-value pairs "so any
/// engine can fill it" — carrying no schema, no guarantees, and nothing a report can compute on. So
/// the stages are named, and an engine declares which of them it does not perform.
/// </para></summary>
public static class TraceContract
{
    public const string V0 = "trace/v0";

    /// <summary>Separates a stage from an optional breakdown of it: <c>retrieve:dense</c>.
    /// <para>
    /// The base name is the contract; the qualifier is data. This exists because the first real emitter
    /// wanted to report a stage per channel as well as in total, and neither alternative was acceptable:
    /// inventing two new top-level stages would put the channel list into the contract (so a third
    /// channel becomes a contract change), and refusing the breakdown would lose the answer to the
    /// question the funnel exists for — a pool that admitted nothing cannot say WHICH head went quiet.
    /// </para></summary>
    public const char Qualifier = ':';

    /// <summary>The stages a funnel may report, in pipeline order.
    /// <para>
    /// These are the names a working engine actually emits, not names chosen here for a reader's
    /// comfort. Where the draft and the emitter disagreed, the emitter won — a contract whose names no
    /// producer uses is a contract that produces empty columns.
    /// </para></summary>
    public static IReadOnlyList<string> Stages =>
    [
        // collection: deriving the corpus name and confirming it exists at all. Its presence separates
        // "this project was never indexed" from "the query matched nothing" — opposite facts that look
        // identical in a result count.
        "collection",
        "embed-query",
        "retrieve",
        "fuse",
        "rerank",
        "cut",
        "graph-enrich",
    ];

    /// <summary>What changed when the first real payload arrived, kept as text because the reason is
    /// the valuable part and a diff does not carry it.</summary>
    public const string Deviation =
        "trace/v0 drafted: embed, dense, sparse, semantic, fuse, rerank, graph, assemble. "
        + "The first emitting engine produced: collection, embed-query, retrieve (+ per-channel "
        + "qualifiers), fuse, rerank, cut, and declared graph-enrich absent. Two names survived. "
        + "'dense' and 'sparse' collapsed into one 'retrieve' because both channels ride one batched "
        + "request, so splitting the STAGE would have invented a time split that does not exist; the "
        + "per-channel counts arrive as qualifiers instead. 'semantic' was dropped: no such channel "
        + "exists in a shipping engine. 'assemble' became 'cut' — the last step is a truncation to the "
        + "requested count, not an assembly. 'collection' was added, and it earns its place.";

    public static bool Knows(string version) => version == V0;

    /// <summary>Which mode an engine's declaration earns it.
    /// <para>
    /// An engine claiming a version we do not know DEGRADES to black-box rather than failing the run.
    /// The alternative — refusing to measure an engine because its trace is too new — would make the
    /// benchmark unable to measure exactly the engines most worth measuring.
    /// </para></summary>
    public static bool IsWhiteBox(string declaredVersion) => Knows(declaredVersion);

    /// <summary>Why a funnel was not taken, in words a report can print. Empty when it was.</summary>
    public static string DegradationReason(string declaredVersion) =>
        declaredVersion.Length == 0
            ? "the engine emits no trace contract"
            : Knows(declaredVersion)
                ? string.Empty
                : $"the engine emits '{declaredVersion}' and this build reads {V0}";

    /// <summary>The contract stage a name belongs to — <c>retrieve</c> for <c>retrieve:dense</c>.</summary>
    public static string BaseStage(string name) =>
        name.Split(Qualifier, 2)[0];

    public static bool Defines(string name) => Stages.Contains(BaseStage(name));

    /// <summary>Refuses a funnel that names a stage the contract does not define, in its stages or in
    /// its absent list. A stage nobody declared is a column no report can compute on, and silently
    /// keeping it is how a named contract turns back into a bag of key-value pairs.</summary>
    public static Outcome<RetrievalFunnel> Validate(RetrievalFunnel funnel)
    {
        if (funnel.ContractVersion != V0)
        {
            return Outcome<RetrievalFunnel>.Failure(
                $"funnel declares '{funnel.ContractVersion}', this build reads {V0}");
        }

        var unknown = funnel.Stages.Select(s => s.Name)
            .Concat(funnel.Absent)
            .Where(name => !Defines(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return unknown.Count == 0
            ? Outcome<RetrievalFunnel>.Success(funnel)
            : Outcome<RetrievalFunnel>.Failure(
                $"funnel names stage(s) the contract does not define: {string.Join(", ", unknown)}");
    }
}
