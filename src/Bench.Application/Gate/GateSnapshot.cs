using Bench.Contracts;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>One read of the gate's rows — every settled record, the rubrics the rows carry, every verdict and the recorded
/// suite stamps — and the decisions <see cref="GateReportQuery"/> takes over it. Read once per request: a campaign is
/// hundreds of rows, and a scope list that re-read the store per scope would be the N+1 the record reader avoids.</summary>
internal sealed class GateSnapshot
{
    private readonly IGateReads _reads;

    private GateSnapshot(IGateReads reads, IReadOnlyList<GateRunRecord> records, RubricCatalog catalog, IReadOnlyList<GateVerdict> verdicts, IReadOnlySet<string> recorded)
    {
        _reads = reads;
        Records = records;
        Catalog = catalog;
        Verdicts = verdicts;
        Recorded = recorded;
    }

    public IReadOnlyList<GateRunRecord> Records { get; }

    public RubricCatalog Catalog { get; }

    public IReadOnlyList<GateVerdict> Verdicts { get; }

    public IReadOnlySet<string> Recorded { get; }

    /// <summary>The rows of <paramref name="gates"/> only — a read is never over another gate's history.</summary>
    public static async Task<GateSnapshot> ReadAsync(IGateReads reads, IReadOnlyCollection<GateKind> gates, CancellationToken cancellationToken)
    {
        var records = await reads.RecordsAsync(gates, cancellationToken);
        var catalog = new RubricCatalog(await reads.RubricsAsync(cancellationToken));
        var verdicts = await reads.VerdictsAsync([.. records.Select(r => r.RunId)], catalog, cancellationToken);

        return new GateSnapshot(reads, records, catalog, verdicts, await reads.RecordedStampsAsync(cancellationToken));
    }

    public IReadOnlyList<GateScope> Scopes() => GateReport.Scopes(Records);

    public GateScopeDto ScopeDto(GateScope scope)
    {
        var records = Of(scope);
        var ids = records.Select(r => r.RunId).ToHashSet();

        return GateReportContract.Scope(scope, records, [.. Verdicts.Where(v => ids.Contains(v.RunId))], Recorded.Contains(scope.SuiteStamp));
    }

    /// <summary>The scope of <paramref name="gate"/> whose id is <paramref name="scopeId"/>, or a refusal naming the ids the
    /// gate does have — a wrong id should send its reader to the right one, not to a blank page.</summary>
    public Outcome<GateScope> Find(GateKind gate, string scopeId)
    {
        var scopes = Scopes().Where(s => s.Gate == gate).ToList();
        var found = scopes.FirstOrDefault(s => string.Equals(s.Id, scopeId, StringComparison.OrdinalIgnoreCase));

        return found is not null
            ? Outcome<GateScope>.Success(found)
            : Outcome<GateScope>.Failure(
                $"no {GateWord.Of(gate)}-gate scope '{scopeId}' in this database — "
                + (scopes.Count == 0 ? "the gate has no runs yet" : $"its scopes are {string.Join(", ", scopes.Select(s => s.Id))}"));
    }

    public async Task<GateAnswer<GateModelTableDto>> TableAsync(GateScope scope, string rubricAsked, CancellationToken cancellationToken)
    {
        var tasks = await _reads.TasksAsync(scope.SuiteStamp, cancellationToken);
        var dto = ScopeDto(scope);

        var missing = MissingTasks(Of(scope), tasks);

        if (tasks.Count == 0 || missing.Count > 0)
        {
            return GateAnswer<GateModelTableDto>.Refuse(GateRefusalKind.Conflict, TasksNotRecorded(scope.SuiteStamp, missing));
        }

        return GateRubricChoice.Of(dto.Rubrics, rubricAsked) switch
        {
            GateAnswer<GateRubricDto>.Answered chosen => GateAnswer<GateModelTableDto>.Of(await ComputeAsync(scope, dto, tasks, chosen.Value, cancellationToken)),
            GateAnswer<GateRubricDto>.Refused refused => GateAnswer<GateModelTableDto>.Refuse(refused.Kind, refused.Reason),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    public async Task<IReadOnlyList<GateRunSummaryDto>> RunListAsync(GateScope scope, CancellationToken cancellationToken)
    {
        var records = Of(scope);
        var tasks = (await _reads.TasksAsync(scope.SuiteStamp, cancellationToken)).ToDictionary(t => t.Id.Value, StringComparer.Ordinal);
        var superseded = GateRunList.Superseded(records);

        return [.. records
            .OrderBy(r => r.Task.Value, StringComparer.Ordinal).ThenBy(r => r.Reviewer.Value, StringComparer.Ordinal)
            .ThenBy(r => r.Repeat).ThenBy(r => r.CampaignId).ThenBy(r => r.Attempt)
            .Select(r => GateReportContract.Summary(r, TaskOrNot.From(tasks, r.Task), superseded.Contains(r.RunId)))];
    }

    public async Task<GateRunDetailDto> DetailAsync(GateRunRecord record, CancellationToken cancellationToken)
    {
        var tasks = (await _reads.TasksAsync(record.Scope.SuiteStamp, cancellationToken)).ToDictionary(t => t.Id.Value, StringComparer.Ordinal);
        var superseded = GateRunList.Superseded(Of(record.Scope)).Contains(record.RunId);
        var summary = GateReportContract.Summary(record, TaskOrNot.From(tasks, record.Task), superseded);

        return GateReportContract.Detail(
            summary, ScopeDto(record.Scope), record, await _reads.PromptHashAsync(record.RunId, cancellationToken),
            [.. Verdicts.Where(v => v.RunId == record.RunId)]);
    }

    public static string TasksNotRecorded(string suiteStamp, IReadOnlyList<string> missing) =>
        $"the tasks of suite {suiteStamp} are not {(missing.Count > 0 ? $"all recorded in this database ({string.Join(", ", missing)} missing)" : "recorded in this database")}, "
        + "so its calibration tasks cannot be put apart from the measured ones — record them once with "
        + "`bench gate suite record --suite-file <that suite's file> --db …` (or import the suite again); the run list is still readable";

    /// <summary>The scope's task ids the recorded set does not hold. A task that is not in the set would be read as a MEASURED
    /// task by the report — the calibration split folded silently — so one missing task refuses the table like none recorded.</summary>
    private static IReadOnlyList<string> MissingTasks(IReadOnlyList<GateRunRecord> records, IReadOnlyList<TaskSummary> tasks) =>
        tasks.Count == 0
            ? []
            : [.. records.Select(r => r.Task.Value).Distinct(StringComparer.Ordinal)
                .Where(id => tasks.All(t => t.Id.Value != id)).Order(StringComparer.Ordinal)];

    private async Task<GateModelTableDto> ComputeAsync(
        GateScope scope, GateScopeDto dto, IReadOnlyList<TaskSummary> tasks, GateRubricDto chosen, CancellationToken cancellationToken)
    {
        var rubric = Catalog.Resolve(chosen.Hash).Match(r => r, reason => throw new InvalidOperationException(reason));
        var records = Of(scope);
        var ids = records.Select(r => r.RunId).ToHashSet();
        var input = new GateReportInput(tasks, records, [.. Verdicts.Where(v => ids.Contains(v.RunId))])
        {
            HandChecks = await _reads.HandChecksAsync(Catalog, cancellationToken),
        };

        return GateReportContract.Table(GateReport.PerModel(scope, rubric, input), dto);
    }

    private IReadOnlyList<GateRunRecord> Of(GateScope scope) => [.. Records.Where(r => r.Scope == scope)];
}

/// <summary>Which of a scope's rubrics was asked for: by its stamp (<c>id#hash12</c>) or by its id when only one wording
/// of that id is in the scope. Only a rubric the scope's verdicts CARRY can be chosen — a table under a rubric nobody
/// read this scope with would be a column of dashes that looks like a measurement.</summary>
internal static class GateRubricChoice
{
    public static GateAnswer<GateRubricDto> Of(IReadOnlyList<GateRubricDto> carried, string asked)
    {
        var matches = carried.Where(r => Matches(r, asked)).ToList();

        return (carried.Count, matches.Count) switch
        {
            (0, _) => GateAnswer<GateRubricDto>.Refuse(
                GateRefusalKind.NotFound, "nothing in this scope was assessed — no verdict carries a rubric to read it under; the run list is still readable"),
            (_, 1) => GateAnswer<GateRubricDto>.Of(matches[0]),
            (_, 0) => GateAnswer<GateRubricDto>.Refuse(
                GateRefusalKind.NotFound, $"this scope's verdicts carry no rubric '{asked}' — they carry {string.Join(", ", carried.Select(r => r.Stamp))}"),
            _ => GateAnswer<GateRubricDto>.Refuse(
                GateRefusalKind.BadRequest, $"'{asked}' names {matches.Count} wordings in this scope — choose one: {string.Join(", ", matches.Select(r => r.Stamp))}"),
        };
    }

    private static bool Matches(GateRubricDto rubric, string asked) =>
        string.Equals(rubric.Stamp, asked, StringComparison.OrdinalIgnoreCase) || string.Equals(rubric.Id, asked, StringComparison.OrdinalIgnoreCase);
}
