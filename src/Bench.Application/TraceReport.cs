using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Renders one leg's trace, identically for both modes.
/// <para>
/// This is where the two implementations of <see cref="IRunTrace"/> have to agree, so it is where the
/// guarantee is asserted: a black-box trace renders with its funnel columns ABSENT, never as zeroes.
/// The distinction is the whole reason <see cref="Captured"/> exists — "the target survived 0 of 145
/// candidates" and "nobody watched the candidates" are opposite readings, and only one of them is a
/// finding about the engine.
/// </para></summary>
public static class TraceReport
{
    /// <summary>What a field reads as when nothing measured it. Deliberately not "0", not "-", and not
    /// blank: a reader scanning a column has to be able to tell instrumentation from measurement at a
    /// glance, and every blank cell in a table looks like a small number.</summary>
    public const string NotCaptured = "not captured";

    public static IReadOnlyList<string> Render(LegTrace trace) =>
    [
        $"prompt     {Show(trace.Prompt)}",
        $"response   {Show(trace.Response)}",
        $"tools      {trace.ToolCalls.Count} call(s), {Refused(trace)} refused",
        $"time       tools {Ms(trace.Time.Tools)} · thinking {Ms(trace.Time.Thinking)} · waiting {Ms(trace.Time.InfrastructureWait)}",
        $"tokens     fresh {trace.Tokens.Fresh} · cache-read {trace.Tokens.CacheRead} · cache-write {trace.Tokens.CacheWrite}",
        $"cost       ${trace.CostUsd:F4}",
        $"funnel     {Funnel(trace)}",
        $"accounted  {Accounting(trace.Funnel)}",
        $"absent     {Absent(trace.Funnel)}",
    ];

    /// <summary>The funnel as one line. Absent for a black-box engine, and it SAYS absent — a report
    /// that printed an empty funnel would let a reader conclude that retrieval admitted nothing.</summary>
    private static string Funnel(LegTrace trace) =>
        trace.Funnel.IsPresent
            ? $"[{trace.Funnel.ContractVersion}] "
              + string.Join(" → ", trace.Funnel.Stages.Select(s => $"{s.Name} {s.In}/{s.Out}"))
            : $"{NotCaptured} ({Why(trace)})";

    /// <summary>Why there is no funnel. An engine that never claimed a contract and one that claimed
    /// <c>trace/v0</c> and then sent something unreadable are different events, and they render
    /// identically the moment the reason is dropped — at which point a contract mismatch looks like a
    /// design decision and nobody goes and fixes it.</summary>
    private static string Why(LegTrace trace) =>
        trace.FunnelNote.Length > 0 ? trace.FunnelNote : "black-box engine";

    /// <summary>Where the funnel's own time went, and what it could not account for.
    /// <para>
    /// The unattributed remainder is printed even when it is zero, and that is the point: the failure
    /// this line exists to catch is an instrument that sums the stages it knows about and calls the
    /// result a total. A remainder that is silently absent is indistinguishable from one that is
    /// silently large.
    /// </para></summary>
    private static string Accounting(RetrievalFunnel funnel) =>
        funnel.IsPresent
            ? $"{funnel.TotalMs} ms total, {funnel.AccountedMs} ms in stages, {funnel.UnattributedMs} ms unattributed"
            : NotCaptured + " (black-box engine)";

    /// <summary>Contract stages the engine says it does not perform. Named rather than shown as zeroes:
    /// "did not run" is a different claim from "ran and found nothing", and only the second is a
    /// finding about retrieval.</summary>
    private static string Absent(RetrievalFunnel funnel) =>
        funnel.Absent.Count > 0 ? string.Join(", ", funnel.Absent) : "none declared";

    /// <summary>A captured value, or the reason it is missing. The reason is printed rather than
    /// swallowed: "not captured" alone invites the reader to assume it was an oversight.</summary>
    private static string Show(Captured value) =>
        value.WasCaptured
            ? $"{value.Value.Length} char(s)"
            : $"{NotCaptured} — {value.Reason}";

    private static int Refused(LegTrace trace) => trace.ToolCalls.Count(c => c.Refused);

    private static string Ms(TimeSpan span) => $"{span.TotalMilliseconds:F0} ms";
}
