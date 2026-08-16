using Bench.Domain;
using Bench.Domain.Runs;

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
