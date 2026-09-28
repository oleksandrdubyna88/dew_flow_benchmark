using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Tests.Gate;

/// <summary>A read port over lists, for the query tests: what the database would answer, without the database. It also
/// records which gates each record read ASKED for, so a test can pin that a read is scoped to its gate.</summary>
internal sealed record ScriptedGateReads(
    IReadOnlyList<GateRunRecord> Records,
    IReadOnlyDictionary<string, IReadOnlyList<TaskSummary>> Tasks,
    IReadOnlyList<Rubric> Rubrics,
    IReadOnlyList<GateVerdict> Verdicts,
    IReadOnlyList<HandCheck> HandChecks) : IGateReads
{
    /// <summary>The gates of every <see cref="IGateReads.RecordsAsync"/> call, in order.</summary>
    public List<IReadOnlyCollection<GateKind>> RecordReads { get; } = [];

    Task<IReadOnlyList<GateRunRecord>> IGateReads.RecordsAsync(IReadOnlyCollection<GateKind> gates, CancellationToken cancellationToken)
    {
        RecordReads.Add(gates);
        return Task.FromResult<IReadOnlyList<GateRunRecord>>([.. Records.Where(r => gates.Contains(r.Scope.Gate))]);
    }

    Task<Outcome<GateKind>> IGateReads.GateOfRunAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(Records.FirstOrDefault(r => r.RunId == runId) is { } found
            ? Outcome<GateKind>.Success(found.Scope.Gate)
            : Outcome<GateKind>.Failure($"no gate run {runId} in this database"));

    Task<IReadOnlyList<TaskSummary>> IGateReads.TasksAsync(string suiteStamp, CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.TryGetValue(suiteStamp, out var tasks) ? tasks : (IReadOnlyList<TaskSummary>)[]);

    Task<IReadOnlySet<string>> IGateReads.RecordedStampsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(Tasks.Keys.ToHashSet(StringComparer.Ordinal));

    Task<IReadOnlyList<Rubric>> IGateReads.RubricsAsync(CancellationToken cancellationToken) => Task.FromResult(Rubrics);

    Task<IReadOnlyList<GateVerdict>> IGateReads.VerdictsAsync(IReadOnlyCollection<Guid> runIds, RubricCatalog catalog, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GateVerdict>>([.. Verdicts.Where(v => runIds.Contains(v.RunId) && catalog.Rubrics.Contains(v.Rubric))]);

    Task<IReadOnlyList<HandCheck>> IGateReads.HandChecksAsync(RubricCatalog catalog, CancellationToken cancellationToken) => Task.FromResult(HandChecks);

    Task<string> IGatePromptHashes.PromptHashAsync(Guid runId, CancellationToken cancellationToken) => Task.FromResult("p-" + runId.ToString("N")[..8]);
}
