using Bench.Application.Gate;
using Bench.Domain.Gate;

namespace Bench.Tests.Gate;

/// <summary>A read port over lists, for the query tests: what the database would answer, without the database.</summary>
internal sealed record ScriptedGateReads(
    IReadOnlyList<GateRunRecord> Records,
    IReadOnlyDictionary<string, IReadOnlyList<TaskSummary>> Tasks,
    IReadOnlyList<Rubric> Rubrics,
    IReadOnlyList<GateVerdict> Verdicts,
    IReadOnlyList<HandCheck> HandChecks) : IGateReads
{
    Task<IReadOnlyList<GateRunRecord>> IGateReads.RecordsAsync(CancellationToken cancellationToken) => Task.FromResult(Records);

    Task<IReadOnlyList<TaskSummary>> IGateReads.TasksAsync(string suiteStamp, CancellationToken cancellationToken) =>
        Task.FromResult(Tasks.TryGetValue(suiteStamp, out var tasks) ? tasks : (IReadOnlyList<TaskSummary>)[]);

    Task<IReadOnlySet<string>> IGateReads.RecordedStampsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(Tasks.Keys.ToHashSet(StringComparer.Ordinal));

    Task<IReadOnlyList<Rubric>> IGateReads.RubricsAsync(CancellationToken cancellationToken) => Task.FromResult(Rubrics);

    Task<IReadOnlyList<GateVerdict>> IGateReads.VerdictsAsync(IReadOnlyCollection<Guid> runIds, RubricCatalog catalog, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GateVerdict>>([.. Verdicts.Where(v => runIds.Contains(v.RunId) && catalog.Rubrics.Contains(v.Rubric))]);

    Task<IReadOnlyList<HandCheck>> IGateReads.HandChecksAsync(RubricCatalog catalog, CancellationToken cancellationToken) => Task.FromResult(HandChecks);

    Task<string> IGateReads.PromptHashAsync(Guid runId, CancellationToken cancellationToken) => Task.FromResult("p-" + runId.ToString("N")[..8]);
}
