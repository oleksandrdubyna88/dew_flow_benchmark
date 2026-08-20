using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Durable run state. The port exists so the rules live in the domain and the database is left
/// with the single job only it can do: making a claim atomic.
/// <para>
/// Three guarantees are the reason this is not just a table:
/// <b>persist before enqueue</b> — a run and every one of its cells are written in one transaction
/// before any work starts, so an interrupted run is resumable rather than invisible;
/// <b>claim and settle</b> — exactly one worker may own a cell, enforced where concurrency actually
/// happens rather than by a check-then-act in application code;
/// <b>sweep</b> — cells stranded by a dead host come back, but only <see cref="CellLifecycle.MaxAttempts"/>
/// times, because a cell that kills its host will kill the next one too.
/// </para>
/// <para>
/// There is no "current run" and no "newest run" anywhere in this interface, deliberately. Every method
/// names the run it operates on. The system this replaces resolved an absent run id to the project's most
/// recent row whatever its status, and ~14 evaluations overwrote one another in a single session before
/// anybody noticed.
/// </para></summary>
public interface IRunStore
{
    /// <summary>Writes the run and all its cells in ONE transaction. Fails whole; never writes a run
    /// whose cells are missing, which would be a queue that looks started and can never finish.</summary>
    Task<Outcome<BenchRun>> CreateAsync(
        BenchRun run, IReadOnlyList<RunCell> cells, CancellationToken cancellationToken);

    Task<Outcome<BenchRun>> LoadAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Takes ownership of one pending cell, atomically. A failure here is the ordinary answer
    /// "there is nothing to claim", not an error — several workers racing to drain a queue is the
    /// expected shape, and the losers must not treat losing as a fault.</summary>
    Task<Outcome<RunCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, CancellationToken cancellationToken);

    /// <summary>Records how a claimed cell ended. Refuses a cell this owner does not hold — a settle from
    /// a worker whose claim was already swept away must not overwrite the retry that replaced it.</summary>
    Task<Outcome<RunCell>> SettleAsync(
        Guid cellId, WorkerIdentity owner, LegOutcome outcome, CancellationToken cancellationToken);

    /// <summary>Hands back the cells claimed longer ago than <paramref name="staleAfter"/> <b>whose owner
    /// is provably gone</b>, abandoning those past the attempt cap. Returns what it did. Run this at
    /// startup: the cells a crash stranded are the ones nobody is coming back for.
    /// <para>
    /// <b>Time alone is not sufficient and never was.</b> A second process running the same command is a
    /// second worker, so "claimed longer than the window" also describes a colleague having a slow leg;
    /// requeuing it puts two workers on one measurement and refuses the honest one's settle. Ownership is
    /// checked against the recorded host and pid — see <see cref="WorkerIdentity"/>.
    /// </para></summary>
    Task<SweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken);

    Task<RunProgress> ProgressAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The distinct questions this run PLANNED, whether or not any of them was ever answered.
    /// <para>
    /// Planned rather than measured, and the difference is the point: a report splits these into the half
    /// that selects and the half that confirms, and a half whose legs all crashed must read as
    /// <em>measured nothing</em> rather than as a half that never existed. Reading the set off the results
    /// would make those two indistinguishable — and would quietly shrink the denominator of every claim
    /// about coverage.
    /// </para>
    /// <para>
    /// From the cells rather than from the bank snapshot, because both doors have to answer: a run frozen
    /// from a suite FILE writes no <c>run_questions</c> rows at all — a file has no groups — while every
    /// run has cells.
    /// </para></summary>
    Task<IReadOnlyList<string>> QuestionIdsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>The most recently created runs, newest first, capped by <paramref name="limit"/>.
    /// <para>
    /// A listing, and emphatically not a "current run": every other method on this port names the run it
    /// operates on, because the system this replaces resolved an absent run id to the project's most recent
    /// row whatever its status, and ~14 evaluations overwrote one another in a session before anybody
    /// noticed. Reading a list and CHOOSING from it is the opposite of that — the caller still names what it
    /// wants afterwards, and an operator who cannot find an id cannot report on it at all.
    /// </para></summary>
    Task<IReadOnlyList<BenchRun>> RecentAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Records the machine this run measured on, once, at its start.
    /// <para>
    /// Write-once and never updated: the facts describe the machine as it was when the run began, and a run
    /// that spans a driver update is not two machines — it is one measurement whose machine changed under it,
    /// which is a different and worse problem than a stale row.
    /// </para>
    /// <para>
    /// It never refuses. A machine nobody could read is <c>MachineFacts.NotRecorded</c>, stored as such, so a
    /// report can say <em>not recorded</em> rather than infer it from an absent row — the same three-state
    /// discipline the index commit uses.
    /// </para></summary>
    Task RecordMachineAsync(Guid runId, MachineFacts facts, CancellationToken cancellationToken);

    /// <summary>What machine a run measured on. <c>NotRecorded</c> for every run stored before this
    /// existed, which is the honest answer rather than an absence a caller has to interpret.</summary>
    Task<MachineFacts> MachineAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>One cell by id — what a phase close reads to learn how the leg SETTLED. A miss is a
    /// refusal naming the id, never an empty cell.</summary>
    Task<Outcome<RunCell>> CellAsync(Guid cellId, CancellationToken cancellationToken);

    /// <summary>Advances the run's status FORWARD — <c>Running</c> when a drain begins, <c>Completed</c>
    /// when every cell is terminal — and never backwards: a completed run that starts "running" again
    /// would be a second campaign wearing the first's identity. Idempotent for the state it targets,
    /// because a second worker draining the same run is the architecture's normal case, not a race to
    /// referee. Returns the status the run HOLDS afterwards.
    /// <para>
    /// This existed as a column and a word for six days while nothing wrote it: sixteen finished runs
    /// read <c>Planned</c> in every listing, which tells an operator the opposite of the truth.
    /// </para></summary>
    Task<Outcome<RunStatus>> AdvanceStatusAsync(Guid runId, RunStatus to, CancellationToken cancellationToken);

    /// <summary>A fix leg's phase record, materialised ONCE: rows that already exist come back as
    /// stored — a swept-back leg resumes the record its crash left — and only a cell with no record
    /// gets <paramref name="phases"/> inserted. Two workers racing this (the claim makes it
    /// theoretical) are settled by the unique index, and the loser reads what the winner wrote.</summary>
    Task<IReadOnlyList<LegPhase>> EnsurePhasesAsync(
        Guid cellId, IReadOnlyList<LegPhase> phases, CancellationToken cancellationToken);

    /// <summary>Writes phase transitions — start, end, stop — by id. Refuses when a phase named here
    /// was never materialised: a transition on a row that does not exist is a defect, not an upsert.</summary>
    Task<Outcome<int>> SavePhasesAsync(IReadOnlyList<LegPhase> phases, CancellationToken cancellationToken);

    /// <summary>One cell's phases, in ordinal order. Empty for a reading leg — reading has phases only
    /// conceptually, and rows nobody transitions would be noise claiming to be record.</summary>
    Task<IReadOnlyList<LegPhase>> PhasesAsync(Guid cellId, CancellationToken cancellationToken);
}

/// <summary>The one phrase that means "there is nothing left to claim".
/// <para>
/// A drain loop has to tell "the queue is empty" apart from "this claim went wrong", and today it does
/// that by reading the refusal's text. That is a substring check, which is exactly what a loop exit
/// should not be — so the phrase lives HERE, on the port, written once and consumed by both sides,
/// rather than hand-typed in an adapter and hand-matched in a host. The typed replacement is a change
/// to <see cref="IRunStore.ClaimNextAsync"/>'s return shape and belongs to its own task.
/// </para></summary>
public static class ClaimRefusal
{
    public const string NoPendingCell = "no pending cell to claim";
}

/// <param name="Requeued">Cells handed back to Pending for another attempt.</param>
/// <param name="Abandoned">Cells that had used up their attempts. Reported separately because a silent
/// abandonment is indistinguishable from a cell that was never planned.</param>
public readonly record struct SweepReport(int Requeued, int Abandoned)
{
    public int Total => Requeued + Abandoned;

    public string Describe => $"{Requeued} requeued, {Abandoned} abandoned";
}
