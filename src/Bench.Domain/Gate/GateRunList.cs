namespace Bench.Domain.Gate;

/// <summary>The run list's one decision: which listed attempts are SUPERSEDED — an earlier attempt of a cell (campaign,
/// task, reviewer, repeat) whose later attempt is the run every figure reads (<see cref="GatePopulation"/>'s
/// latest-attempt rule). The list shows every attempt, and marks these, rather than hiding the ones the report does not
/// count.</summary>
public static class GateRunList
{
    public static IReadOnlySet<Guid> Superseded(IReadOnlyList<GateRunRecord> records) =>
        records.GroupBy(r => (r.CampaignId, r.Task, r.Reviewer, r.Repeat))
            .SelectMany(cell => cell.OrderByDescending(r => r.Attempt).Skip(1))
            .Select(r => r.RunId)
            .ToHashSet();
}
