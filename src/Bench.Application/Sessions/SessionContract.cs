using Bench.Contracts;
using Bench.Domain.Sessions;
using Bench.Domain.Trace;

namespace Bench.Application.Sessions;

/// <summary>The stored session, flattened for the wire.
/// <para>
/// A flattening rather than the domain object itself, for the reason <c>RunReportContract</c> already
/// states: this contract depends on nothing, so <see cref="SessionPhase"/> would leave with it. Every enum
/// travels as its NAME — an ordinal changes meaning the day somebody inserts a member, and these objects
/// are published beside the results.
/// </para>
/// <para>
/// <b>The detectors run HERE, on the way out.</b> Findings are derived rather than stored, deliberately:
/// a better detector must reach every session already measured, not only the ones measured after it
/// shipped. The inputs are all persisted — tool name, arguments, target, the working-tree evidence — which
/// is what makes the derivation free.
/// </para></summary>
public static class SessionContract
{
    public static SessionSummaryDto From(SessionSummary summary) => new(
        summary.Id,
        summary.Source.ToString(),
        summary.SessionKey,
        summary.Runtime,
        new SessionTaskDto(summary.Task.Id, summary.Task.Name, summary.Task.PlanPath),
        summary.WorkspacePath,
        summary.Branch,
        summary.StartedAt,
        summary.LastEventAt,
        summary.Phase.ToString(),
        summary.Calls,
        summary.Research,
        summary.Execution,
        summary.Verification,
        summary.Unfinished,
        summary.CompileFailures,
        summary.Disagreements);

    /// <summary>One session, whole. The detectors run HERE and not on the list, because they read the call
    /// sequence in order — a list endpoint that ran them on every poll would load every call of every
    /// session to render a summary.</summary>
    public static SessionDetailDto From(SessionRun run, Guid id, ToolTaxonomy taxonomy)
    {
        var economics = SessionAnalysis.Of(run.Calls, taxonomy);

        return new SessionDetailDto(
            Summarise(run, id),
            [.. economics.Phases.Select(From)],
            economics.Switches,
            economics.UnknownTools,
            [.. economics.Findings.Select(From)],
            [.. run.Calls.Select(From)]);
    }

    private static SessionSummaryDto Summarise(SessionRun run, Guid id) => new(
        id,
        run.Source.ToString(),
        run.SessionKey,
        run.Runtime,
        new SessionTaskDto(run.Task.Id, run.Task.Name, run.Task.PlanPath),
        run.WorkspacePath,
        run.Branch,
        run.StartedAt,
        run.LastEventAt,
        (run.Calls.Count > 0 ? run.Calls[^1].Phase : SessionPhase.Unknown).ToString(),
        run.Calls.Count,
        run.Calls.Count(c => c.Phase == SessionPhase.Research),
        run.Calls.Count(c => c.Phase == SessionPhase.Execution),
        run.Calls.Count(c => c.Phase == SessionPhase.Verification),
        run.Calls.Count(c => c.State == CallState.Started),
        run.Calls.Count(c => c.CompileFailure),
        run.Calls.Count(c => c.Disagrees));

    private static SessionToolCallDto From(SessionToolCall call) => new(
        call.Ordinal,
        call.ToolName,
        call.Class.ToString(),
        call.Phase.ToString(),
        call.Target,
        call.ArgumentsJson,
        call.At,
        call.State.ToString(),
        call.Outcome.ToString(),
        From(call.Response),
        call.ResponseChars,
        From(call.DurationMs),
        call.Mutation.ToString(),
        call.Disagrees,
        call.CompileFailure);

    private static SessionFindingDto From(SessionFinding finding) => new(
        finding.Kind.ToString(), finding.Subject, finding.Occurrences, finding.Ordinals, finding.Detail);

    private static PhaseTotalsDto From(PhaseTotals totals) =>
        new(totals.Phase.ToString(), totals.Calls, totals.Elapsed.TotalSeconds);

    private static CapturedTextDto From(Captured value) => new(value.WasCaptured, value.Value, value.Reason);

    private static CapturedCountDto From(CapturedCount value) => new(value.WasCaptured, value.Value, value.Reason);
}
