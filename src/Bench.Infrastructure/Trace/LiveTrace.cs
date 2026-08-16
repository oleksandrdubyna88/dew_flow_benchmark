using System.Collections.Concurrent;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Infrastructure.Trace;

/// <summary>The black-box trace: what a leg was observed doing, from outside whatever served it.
/// <para>
/// Works against ANY engine and any runtime, which is the point — an engine that emits no funnel is
/// still fully measurable, and most of them will be. What it sees is the prompt, the answer, every
/// tool call with its outcome, where the wall-clock went, the token split and the cost.
/// </para>
/// <para>
/// A leg registers its recorder before it runs and the trace is read back afterwards, rather than the
/// port going off to fetch one: a black-box observation is produced by RUNNING the thing, and a
/// capture method that pretended otherwise would have to reconstruct the leg from logs — which is how
/// a trace comes to disagree with what happened.
/// </para></summary>
public sealed class LiveTrace : IRunTrace
{
    private readonly ConcurrentDictionary<string, LegRecorder> _byLeg = new(StringComparer.Ordinal);

    public TraceMode Mode => TraceMode.BlackBox;

    /// <summary>How many legs are being recorded right now. A campaign settles tens of thousands of legs
    /// through one worker, so this number staying near the count of legs IN FLIGHT — rather than near the
    /// count of legs ever run — is the whole guarantee.</summary>
    public int Recording => _byLeg.Count;

    /// <summary>Opens a recorder for one leg. Called before the leg runs; the leg writes into it.</summary>
    public LegRecorder Open(MeasurementTuple tuple) =>
        _byLeg.GetOrAdd(tuple.Canonical, _ => new LegRecorder());

    /// <summary>Reads a leg's trace, and RETIRES it in the same step.
    /// <para>
    /// Capture is the handover: after it, the trace lives in the result store and this recorder — its tool
    /// calls, its captured prompt and its response text — is only a copy nobody will read again. Keeping
    /// it meant one dictionary entry per leg for the life of the process, which for the long-running
    /// worker this is being wired into is the life of a three-week campaign.
    /// </para></summary>
    public Task<Outcome<LegTrace>> CaptureAsync(MeasurementTuple tuple, CancellationToken cancellationToken) =>
        Task.FromResult(_byLeg.TryRemove(tuple.Canonical, out var recorder)
            ? Outcome<LegTrace>.Success(recorder.Assemble())
            // Not an empty trace. A leg nobody recorded and a leg that did nothing are different facts,
            // and returning LegTrace.Empty here would make the second indistinguishable from the first
            // in every report built afterwards. The reason names BOTH ways there is nothing to hand over,
            // because since capture retires the recording, "already taken" is now one of them.
            : Outcome<LegTrace>.Failure(
                $"no leg was recorded for {tuple.Canonical} — it was never opened, or its trace was already captured"));

    /// <summary>Retires a recording nobody will capture — the leg crashed, was abandoned, or its cell was
    /// handed back. Returns whether there was one, so a caller can tell a cleanup from a no-op.</summary>
    public bool Close(MeasurementTuple tuple) => _byLeg.TryRemove(tuple.Canonical, out _);
}
