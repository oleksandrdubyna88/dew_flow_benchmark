using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The gate's READ side — everything a report, the API and the page are computed from, and nothing that writes.
/// A read host (the bench API, the qln console) registers this and no write port, so no route it maps can reach one.
/// <para>
/// <b>The rubric catalog of a read is built from the ROWS</b> (<see cref="RubricsAsync"/>): a stored verdict is the record
/// of the wording it was judged under, while the prompt files are what an INGEST is checked against — and a read host
/// carries no prompt folder.
/// </para></summary>
public interface IGateReads
{
    /// <summary>Every settled cell of every campaign, as the report reads it — imported and native alike.</summary>
    Task<IReadOnlyList<GateRunRecord>> RecordsAsync(CancellationToken cancellationToken);

    /// <summary>The recorded tasks of one suite stamp, or none when that suite was never recorded. A suite has at least one
    /// task, so an empty answer means <i>not recorded</i> and never <i>a suite of nothing</i>.</summary>
    Task<IReadOnlyList<TaskSummary>> TasksAsync(string suiteStamp, CancellationToken cancellationToken);

    /// <summary>The suite stamps whose tasks are recorded.</summary>
    Task<IReadOnlySet<string>> RecordedStampsAsync(CancellationToken cancellationToken);

    /// <summary>The distinct (id, kind, hash) the stored verdicts and hand-checks carry.</summary>
    Task<IReadOnlyList<Rubric>> RubricsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<GateVerdict>> VerdictsAsync(IReadOnlyCollection<Guid> runIds, RubricCatalog catalog, CancellationToken cancellationToken);

    Task<IReadOnlyList<HandCheck>> HandChecksAsync(RubricCatalog catalog, CancellationToken cancellationToken);

    /// <summary>The prompt hash of a settled cell — the one fact the run detail shows that the record does not carry.
    /// Empty when the cell is not there or recorded none.</summary>
    Task<string> PromptHashAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>Records what a report needs of a SUITE — its tasks' summaries, no text and no path — so a read host can put the
/// calibration tasks apart without the suite file (which stays outside git and outside the database).</summary>
public interface IGateSuiteTasks
{
    /// <summary>Writes every task of <paramref name="suite"/> in ONE transaction: an interrupted record leaves the stamp
    /// unrecorded, never half-recorded. The same set again is a no-op (0); a DIFFERENT row under a stamp already held is
    /// refused naming the task, since a stamp is its tasks' hash and a second reading of it is a defect, not an update.</summary>
    Task<Outcome<int>> RecordAsync(GateSuite suite, CancellationToken cancellationToken);
}
