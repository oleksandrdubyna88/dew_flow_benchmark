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

    /// <summary>Opens a recorder for one leg. Called before the leg runs; the leg writes into it.</summary>
    public LegRecorder Open(MeasurementTuple tuple) =>
        _byLeg.GetOrAdd(tuple.Canonical, _ => new LegRecorder());

    public Task<Outcome<LegTrace>> CaptureAsync(MeasurementTuple tuple, CancellationToken cancellationToken) =>
        Task.FromResult(_byLeg.TryGetValue(tuple.Canonical, out var recorder)
            ? Outcome<LegTrace>.Success(recorder.Assemble())
            // Not an empty trace. A leg nobody recorded and a leg that did nothing are different facts,
            // and returning LegTrace.Empty here would make the second indistinguishable from the first
            // in every report built afterwards.
            : Outcome<LegTrace>.Failure($"no leg was recorded for {tuple.Canonical}"));
}
