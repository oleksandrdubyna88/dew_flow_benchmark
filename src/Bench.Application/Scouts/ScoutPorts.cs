using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Domain.Trace;

namespace Bench.Application.Scouts;

/// <summary>Durable scout state — the probe store's guarantees over scout cells (persist before enqueue, one winner per claim, an
/// ownership-checked sweep, generations appended on re-run, a run with no status) plus the SPEND LEDGER (§3.10): every attempt holds its
/// own reservation row, the run row holds the ledger, and every money move is ONE guarded UPDATE re-checking the state it was decided on.
/// <para>
/// The ledger's rules, enforced here: a reservation that does not fit under the ceiling is refused as a VALUE
/// (<see cref="ScoutReservation.DoesNotFit"/>); an attempt is marked <i>launching</i> before the CLI is invoked; only an attempt with no
/// launching mark is released; an attempt marked launching settles from its result's cost or is charged its WHOLE reservation as
/// unknown cost, never released after; each attempt settles exactly once. The identity: Σ settled + Σ unknown = the ledger's spent.
/// </para></summary>
public interface IScoutStore
{
    /// <summary>The run, its frozen tasks and every cell in ONE transaction; refuses a run with no cells, a cell of another run, a cell
    /// naming a task the run does not hold, or an id taken.</summary>
    Task<Outcome<ScoutRun>> PlanAsync(ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, CancellationToken cancellationToken);

    Task<Outcome<ScoutRun>> LoadAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutTask>> TasksAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Takes the next pending cell of the run in SCHEDULE order (slot, position, generation), atomically, under <paramref name="pin"/>.</summary>
    Task<Outcome<ScoutCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken);

    /// <summary>Settles a claimed cell this owner holds: the outcome, its reason, its facts and its per-scout records, in one transaction.</summary>
    Task<Outcome<ScoutCell>> SettleAsync(Guid cellId, WorkerIdentity owner, ScoutSettlement settlement, IReadOnlyList<ScoutRecord> scouts, CancellationToken cancellationToken);

    /// <summary>Hands back the stale claims whose owner is provably gone — the owner-checked sweep. It moves no money: an open attempt of
    /// a swept cell is settled by recovery (<see cref="ScoutAttemptLifecycle.Recover"/>), from the stream on disk.</summary>
    Task<ScoutSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken);

    /// <summary>Appends the next generation of the lineage <paramref name="cellId"/> belongs to; refused while any generation is open.</summary>
    Task<Outcome<ScoutCell>> NextGenerationAsync(Guid cellId, Guid newId, CancellationToken cancellationToken);

    Task<Outcome<ScoutCell>> CellAsync(Guid cellId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutCell>> CellsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Reserves <paramref name="reservedUsd"/> for the next attempt of a cell this owner holds, numbered by the claim's attempt —
    /// one guarded UPDATE of the run's ledger that refuses when it does not fit, and the attempt row, in one transaction.</summary>
    Task<ScoutReservation> ReserveAsync(Guid cellId, WorkerIdentity owner, Guid attemptId, decimal reservedUsd, CancellationToken cancellationToken);

    /// <summary>The durable launch boundary — written BEFORE the CLI is invoked.</summary>
    Task<Outcome<ScoutAttempt>> MarkLaunchingAsync(Guid attemptId, CancellationToken cancellationToken);

    /// <summary>Settles a launching attempt from its result's <c>total_cost_usd</c>: its reservation leaves the ledger, the cost is booked.</summary>
    Task<Outcome<ScoutAttempt>> SettleAttemptAsync(Guid attemptId, decimal actualUsd, CancellationToken cancellationToken);

    /// <summary>Charges a launching attempt whose result did not survive its WHOLE reservation as unknown cost — booked now, never released.</summary>
    Task<Outcome<ScoutAttempt>> ChargeUnknownAsync(Guid attemptId, CancellationToken cancellationToken);

    /// <summary>Releases an attempt that was NEVER marked launching; refused for any other.</summary>
    Task<Outcome<ScoutAttempt>> ReleaseAsync(Guid attemptId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutAttempt>> AttemptsAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>What one sweep did.</summary>
public sealed record ScoutSweepReport(int Requeued, int Abandoned)
{
    public int Total => Requeued + Abandoned;
}

/// <summary>The scouts' READ side — what the report, the API and the tab are computed from, and nothing that writes.</summary>
public interface IScoutReads
{
    Task<IReadOnlyList<ScoutRun>> RecentRunsAsync(int limit, CancellationToken cancellationToken);

    Task<Outcome<ScoutRun>> RunAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutTask>> TasksAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutCell>> CellsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>One cell per lineage — the highest SETTLED generation (<see cref="ScoutGenerations.LatestSettled"/>).</summary>
    Task<IReadOnlyList<ScoutCell>> LatestSettledAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutRecord>> ScoutsAsync(Guid cellId, CancellationToken cancellationToken);

    /// <summary>Every per-scout record of the run, with the cell it belongs to — one read for the report instead of one per cell.</summary>
    Task<IReadOnlyList<ScoutCellRecord>> ScoutRecordsAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Every claim verdict the run's cells received (S3's <c>assess</c>).</summary>
    Task<IReadOnlyList<ScoutAssessedClaim>> VerdictsAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoutAttempt>> AttemptsAsync(Guid runId, CancellationToken cancellationToken);
}

/// <summary>A per-scout record and the cell it belongs to.</summary>
public sealed record ScoutCellRecord(Guid CellId, ScoutRecord Scout);

/// <summary>The analysis's WRITES (S3): claim verdicts as <c>assess</c> earns them — written per batch, each claim at most once — and the recall
/// <c>score</c> recomputes from the streams. Kept apart from <see cref="IScoutStore"/>, which owns the claim/settle/ledger lifecycle; nothing
/// here moves a cell's state or money.</summary>
public interface IScoutVerdicts
{
    /// <summary>Stores the verdicts whose (cell, ordinal, index) has none yet and answers how many were NEW — a verdict for an already assessed
    /// claim is skipped, never overwritten, so re-running <c>assess</c> can only fill gaps.</summary>
    Task<int> StoreVerdictsAsync(IReadOnlyList<ScoutAssessedClaim> verdicts, CancellationToken cancellationToken);

    /// <summary>The claims of the run that already carry a verdict.</summary>
    Task<IReadOnlyList<ScoutClaimKey>> AssessedKeysAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Writes a SCORED cell's recomputed recall and its scouts' resolved KEY FILES; refused for a cell that is not settled scored.</summary>
    Task<Outcome<ScoutCell>> RescoreAsync(Guid cellId, ScoutRescore rescore, CancellationToken cancellationToken);
}

/// <summary>One scored cell's recall as <c>score</c> recomputed it from the stream, and each scout's KEY FILES as the matcher resolved them.</summary>
public sealed record ScoutRescore(long Found, long Total, IReadOnlyList<ScoutRescoredKeyFiles> Scouts);

public sealed record ScoutRescoredKeyFiles(int Ordinal, IReadOnlyList<string> KeyFiles);

/// <summary>How one attempt ended, as a runner hands it back (S2 builds the runner).</summary>
public abstract record ScoutAttemptResult
{
    private ScoutAttemptResult()
    {
    }

    /// <summary>The cell ends — scored, void, stopped or aborted — with what the result reported it cost.</summary>
    public sealed record Settled(ScoutSettlement Settlement, IReadOnlyList<ScoutRecord> Scouts, CapturedUsd ReportedUsd) : ScoutAttemptResult;

    /// <summary>The attempt never launched — never marked launching (a refused argv, a missing executable, an unbuildable tree): its
    /// reservation is released, the cell stays claimed for the next entry's sweep, and the campaign stops.</summary>
    public sealed record NotLaunched(string Reason) : ScoutAttemptResult;

    /// <summary>The attempt never launched, and the cell STOPS the campaign before it could (§3.2: the task's tree no longer hashes to its
    /// frozen hash — "a changed tree stops the cell"): the reservation is released, the cell settles <see cref="ScoutCellOutcome.Stopped"/>
    /// with <paramref name="Reason"/>, and the run stops.</summary>
    public sealed record Stopped(ScoutReason Reason, string Detail) : ScoutAttemptResult;
}

/// <summary>One attempt of one claimed cell — the campaign has claimed it and RESERVED its money; the runner marks the attempt launching
/// (durable, BEFORE the CLI is invoked) → launches → streams to the artefact root as it arrives → commits → reads → read-back → cost check,
/// and hands back how it ended. The campaign settles the money and the cell. S2's <c>CliScoutRunner</c>.</summary>
public interface IScoutRunner
{
    Task<ScoutAttemptResult> RunAsync(ScoutRun run, ScoutTask task, ScoutCell claimed, ScoutAttempt attempt, CancellationToken cancellationToken);
}

/// <summary>A history-free tree (§3.2): where it is, its hash, and every file in it (what the recall matcher resolves basenames against).
/// <paramref name="Removed"/> are the files taken out after the archive (the PR's own plan, when it existed at P).</summary>
public sealed record ScoutTree(string Root, string TreeHash, IReadOnlyList<string> Files)
{
    public IReadOnlyList<string> Removed { get; init; } = [];
}

/// <summary>What a tree is built from: the task id (its folder), the repository (a public slug, or a private task's local url), P, and the
/// files to take out of it. <see cref="Privacy"/> decides what a refusal or a log line about the build may say (<see cref="ScoutPublication"/>):
/// for a PRIVATE task, the label and nothing the tree holds.</summary>
public sealed record ScoutTreeSpec(ScoutTaskId Task, string Repository, CommitSha Parent, IReadOnlyList<string> Remove)
{
    public ScoutTaskPrivacy Privacy { get; init; } = ScoutTaskPrivacy.Public;
}

/// <summary>The phrase a tree refusal carries when the tree exists and no longer hashes to the task's frozen hash — read by the runner as
/// a STOP (§3.2), not as an environment fault.</summary>
public static class ScoutTreeRefusal
{
    public const string HashChanged = "no longer hashes to its frozen tree hash";
}

/// <summary>The history-free trees of §3.2 — <c>git archive P</c> from the bare mirror, a fresh <c>git init</c> and ONE commit under the
/// trees root. S2's <c>ScoutTrees</c>.</summary>
public interface IScoutTrees
{
    /// <summary>Builds the tree of <paramref name="spec"/> (or finds the one already built, checked to be one commit with no other history)
    /// and answers its hash — what <c>bench scouts tasks</c> freezes on the task.</summary>
    Task<Outcome<ScoutTree>> BuildAsync(ScoutTreeSpec spec, CancellationToken cancellationToken);

    /// <summary>Builds (or finds) the task's tree and answers it; refused — with <see cref="ScoutTreeRefusal.HashChanged"/> in the reason —
    /// when it does not hash to the task's frozen hash.</summary>
    Task<Outcome<ScoutTree>> PrepareAsync(ScoutTask task, CancellationToken cancellationToken);
}

/// <summary>The money boundaries a runner passes, in order — the fault-injection port the crash tests stop the harness at.</summary>
public enum ScoutCheckpoint
{
    /// <summary>The reservation is held; the attempt is NOT marked launching.</summary>
    BeforeLaunchingMark,

    /// <summary>The attempt is marked launching; the CLI has not been invoked.</summary>
    AfterLaunchingMark,

    /// <summary>The first line of the stream reached the artefact root.</summary>
    AfterFirstOutputLine,

    /// <summary>The <c>result</c> envelope reached the artefact root.</summary>
    AfterResultLine,

    /// <summary>The launch record, stderr, the prompt and the stream's seal are committed.</summary>
    AfterArtifactCommit,

    /// <summary>The cell is read; neither the attempt nor the cell is settled yet.</summary>
    BeforeSettlement,
}

/// <summary>Where a runner and the campaign report each <see cref="ScoutCheckpoint"/> — nothing in production; a test throws there to
/// play a crash of the harness at exactly that boundary.</summary>
public interface IScoutCheckpoints
{
    void Reached(ScoutCheckpoint checkpoint, Guid attemptId);
}

/// <summary>Production: the checkpoints are passed, and nothing happens at them.</summary>
public sealed class NoScoutCheckpoints : IScoutCheckpoints
{
    public static NoScoutCheckpoints Instance { get; } = new();

    public void Reached(ScoutCheckpoint checkpoint, Guid attemptId)
    {
    }
}

/// <summary>What recovery reads of an attempt it finds open: the <c>total_cost_usd</c> of the <c>result</c> envelope that SURVIVED on disk in
/// the attempt's stream, or <i>not captured</i> — no stream, or one the result never reached (§3.10).</summary>
public interface IScoutStreams
{
    CapturedUsd SurvivingCost(ScoutAttempt attempt);

    /// <summary>The attempt's stream as it stands on disk, or empty when none was written — what <c>score</c>, <c>assess</c> and the hand-check
    /// re-read a settled cell's scouts from (S3).</summary>
    string StreamText(ScoutAttempt attempt);
}

/// <summary>The pins this machine can read before a claim without a paid call (§3.4): the CLI binary's <see cref="ProductPin"/> (its version is
/// the CLI-version pin), the plugin cache hash and the settings hash. The resolved model ids are read off each cell's own stream.</summary>
public sealed record ScoutLocalPins(ProductPin Binary, string CliVersion, string PluginCacheHash, string SettingsHash)
{
    /// <summary>The plugin directory the hash was taken over — what the launch passes as <c>--plugin-dir</c> (S5).</summary>
    public string PluginDirectory { get; init; } = string.Empty;

    /// <summary>The run's pins as they stand on this machine now — the frozen resolved ids carried over, since they are checked per cell.</summary>
    public Outcome<ScoutPins> Against(ScoutPins frozen) =>
        ScoutPins.Of(CliVersion, PluginCacheHash, frozen.ResolvedOpus, frozen.ResolvedSonnet, SettingsHash);
}

/// <summary>Reads <see cref="ScoutLocalPins"/> — S2's <c>ScoutPinReader</c>.</summary>
public interface IScoutPinReader
{
    Task<Outcome<ScoutLocalPins>> ReadAsync(string executable, CancellationToken cancellationToken);
}
