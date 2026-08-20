using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>The sampler of a run that takes no readings.
/// <para>
/// A null object rather than a nullable dependency, the shape <c>NoRetriever</c> and <c>NoFunnelSink</c>
/// already set here: a run configured without sampling is a legitimate configuration — on a host with no
/// accelerator, or when an operator does not want a background loop beside a campaign — and it must produce
/// legs that say <em>nobody watched</em> rather than legs whose runner had to check for a missing service.
/// </para>
/// <para>
/// Its answer is empty, which <c>LegSampling</c> turns into <em>not sampled</em> with a reason. That is a
/// different fact from a machine that was watched and found idle, and the whole reason the summaries carry a
/// flag rather than a bare number.
/// </para></summary>
public sealed class NoHardwareSampler : IHardwareSampler
{
    public Task<(IReadOnlyList<LoadSample> Load, IReadOnlyList<VramSample> Vram)> ReadAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult<(IReadOnlyList<LoadSample>, IReadOnlyList<VramSample>)>(([], []));
}
