using Bench.Domain.Trace;

namespace Bench.Domain.Retrieval;

/// <summary>One query-time knob as a name and its canonical text.</summary>
public readonly record struct Axis(string Name, string Value);

/// <summary>The query-time knobs of one search — as ASKED, or as the engine said it APPLIED them.
/// <para>
/// Named values rather than a typed mirror of one engine's contract, because this side stores axes from
/// engines it does not compile against. The typed half lives in each adapter, which projects its own
/// request DTO into this shape — so what is recorded is literally what went out, not a second mapping that
/// can drift from it.
/// </para>
/// <para>
/// <b>Stored here, asserted later.</b> An engine that ignores an axis it does not know echoes back a
/// well-formed set that simply lacks the field, which is how a variant records <c>wsum</c> in its
/// definition and RRF in its numbers. Comparing the two sets is the enforcement of
/// <c>todo/PLAN_variant_matrix.md</c> §3.4 step 3 and belongs to build-order step 5, with the
/// index preparations that give a blocked cell somewhere honest to go. This type is what makes that
/// comparison possible without re-running anything; it deliberately does not perform it yet.
/// </para></summary>
public sealed record EngineAxes(IReadOnlyList<Axis> Values)
{
    public static EngineAxes None { get; } = new([]);

    /// <summary>A fixed order, so two sets of the same axes produce the same text.</summary>
    public string Canonical =>
        string.Join(',', Values.OrderBy(v => v.Name, StringComparer.Ordinal).Select(v => $"{v.Name}={v.Value}"));
}

/// <summary>What one retrieval produced: the hits, the funnel, and what actually served the query.
/// <para>
/// <see cref="NotPerformed"/> is a first-class state, not an empty list. A baseline leg surfaces nothing
/// because that is the ARM, and a retrieval that returned zero hits is a fact about the QUERY — the two
/// must never render alike, or the no-retrieval control starts looking like a broken index.
/// </para></summary>
public sealed record RetrievedContext
{
    private RetrievedContext(
        bool performed,
        string collection,
        IReadOnlyList<RetrievedHit> hits,
        RetrievalFunnel funnel,
        string funnelNote,
        EngineAxes requested,
        EngineAxes applied,
        long payloadBytes,
        long elapsedMs)
    {
        WasPerformed = performed;
        Collection = collection;
        Hits = hits;
        Funnel = funnel;
        FunnelNote = funnelNote;
        Requested = requested;
        Applied = applied;
        PayloadBytes = payloadBytes;
        ElapsedMs = elapsedMs;
    }

    /// <summary>Whether a retrieval happened at all.</summary>
    public bool WasPerformed { get; }

    /// <summary>Which corpus answered. Two variants are two collections, so a number compared across them
    /// is a number compared across a rebuild.</summary>
    public string Collection { get; }

    /// <summary>In the order the engine returned them.</summary>
    public IReadOnlyList<RetrievedHit> Hits { get; }

    public RetrievalFunnel Funnel { get; }

    /// <summary>Why the funnel is absent when the engine CLAIMED one. Empty when it arrived, and empty for
    /// an engine that never claimed a contract — that case is legible from the funnel itself.</summary>
    public string FunnelNote { get; }

    /// <summary>The axes this run asked for.</summary>
    public EngineAxes Requested { get; }

    /// <summary>The axes the engine said it applied — after its own clamping, never as requested.</summary>
    public EngineAxes Applied { get; }

    /// <summary>Bytes of the engine's response. Part of the per-cell log volume the plan records from what
    /// the harness itself observes, rather than from a daemon log nobody can attribute per leg.</summary>
    public long PayloadBytes { get; }

    public long ElapsedMs { get; }

    /// <summary>The state of a leg whose arm surfaces nothing.</summary>
    public static RetrievedContext NotPerformed { get; } = new(
        performed: false, string.Empty, [], RetrievalFunnel.None, string.Empty,
        EngineAxes.None, EngineAxes.None, 0, 0);

    public static RetrievedContext Of(
        string collection,
        IReadOnlyList<RetrievedHit> hits,
        RetrievalFunnel funnel,
        string funnelNote,
        EngineAxes requested,
        EngineAxes applied,
        long payloadBytes,
        long elapsedMs) =>
        new(true, collection, hits, funnel, funnelNote, requested, applied, payloadBytes, elapsedMs);

    /// <summary>Whether the funnel arrived and this build could read it. A degraded funnel is still stored,
    /// with its reason: both vantage points are data.</summary>
    public bool IsWhiteBox => Funnel.IsPresent;
}
