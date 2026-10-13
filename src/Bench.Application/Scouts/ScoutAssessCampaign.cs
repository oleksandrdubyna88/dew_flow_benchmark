using System.Globalization;
using Bench.Domain;
using Bench.Domain.Scouts;

namespace Bench.Application.Scouts;

/// <summary>What one <c>assess</c> pass did. <see cref="Remaining"/> is what is still unassessed afterwards — zero is the goal, and the
/// next pass asks exactly those.</summary>
public sealed record ScoutAssessReport(int Claims, int AlreadyAssessed, int Asked, int Stored, int Remaining, int FailedBatches, IReadOnlyList<string> Problems)
{
    public bool Stopped { get; init; }
}

/// <summary><c>bench scouts assess</c> (§3.7): every <c>CLAIMS</c> line of every scored cell — the highest settled generation of each lineage —
/// that carries no verdict yet, put to the blinded assessor in the task's tree, the verdicts STORED AFTER EVERY BATCH (a paid batch is saved
/// the moment it is earned, reliability rule). Idempotent by construction: the claims already in the store are subtracted first, and the store
/// refuses a second verdict for a claim, so a re-run asks only what is missing.
/// <para>
/// The claims of one task are blinded and shuffled TOGETHER, across its arms and repeats, so a batch's order says nothing about which team
/// wrote a line. A failed batch is recorded and skipped; three failures in a row stop the pass (a codex that is down must not be asked forty
/// more times).
/// </para></summary>
public sealed class ScoutAssessCampaign(IScoutVerdicts verdicts, IScoutTrees trees, IScoutStreams streams, ScoutClaimAssessor assessor, TimeProvider clock)
{
    public const int ConsecutiveFailureLimit = 3;

    /// <summary>What the pass has done so far — immutable: every task and every batch returns the NEXT pass, never edits this one.</summary>
    private sealed record Pass(int Asked, int Stored, int Failed, int InARow, IReadOnlyList<string> Problems)
    {
        public static Pass Start(IEnumerable<string> problems) => new(0, 0, 0, 0, [.. problems]);

        public bool Exhausted => InARow >= ConsecutiveFailureLimit;

        public Pass Noting(string problem) => this with { Problems = [.. Problems, problem] };

        /// <summary>A batch that got no verdicts: counted as asked, one more failure, one more in a row.</summary>
        public Pass Failing(int asked, string problem) =>
            this with { Asked = Asked + asked, Failed = Failed + 1, InARow = InARow + 1, Problems = [.. Problems, problem] };

        /// <summary>A batch that was answered: its verdicts stored, the run of failures broken.</summary>
        public Pass Storing(int asked, int stored) => this with { Asked = Asked + asked, Stored = Stored + stored, InARow = 0 };
    }

    /// <param name="digest">How a stored claim's statement is digested per task — a private task's as its HMAC (S4).</param>
    public async Task<ScoutAssessReport> RunAsync(
        ScoutRun run, IReadOnlyList<ScoutTask> tasks, IReadOnlyList<ScoutCell> cells, IReadOnlyList<ScoutAttempt> attempts,
        ScoutAssessorLaunch launch, Random blinding, ScoutClaimDigest digest, Action<string> progress, CancellationToken cancellationToken)
    {
        var read = ScoutSettledOutputs.Of(tasks, cells, attempts, streams);
        var assessed = (await verdicts.AssessedKeysAsync(run.Id, cancellationToken)).ToHashSet();
        var claims = read.Outputs.SelectMany(o => ScoutAssessment.ClaimsOf(o.Cell.Id, o.Task, o.Arm, o.Transcript)).ToList();
        var open = claims.Where(c => !assessed.Contains(c.Key)).ToList();
        var openByTask = open.ToLookup(c => c.Task, StringComparer.Ordinal);
        var pass = Pass.Start(read.StreamMissing.Select(c => $"{ScoutCampaign.Name(c)}: its stream is not in the artefact root — its claims cannot be read"));

        foreach (var task in tasks.Where(t => openByTask.Contains(t.Id.Value)))
        {
            if (pass.Exhausted || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            pass = await TaskAsync(run, task, [.. openByTask[task.Id.Value]], launch, blinding, digest, pass, progress, cancellationToken);
        }

        var remaining = open.Count - pass.Stored;

        return new ScoutAssessReport(claims.Count, claims.Count - open.Count, pass.Asked, pass.Stored, remaining, pass.Failed, pass.Problems)
        {
            Stopped = pass.Exhausted,
        };
    }

    private async Task<Pass> TaskAsync(
        ScoutRun run, ScoutTask task, IReadOnlyList<ScoutClaimToAssess> claims, ScoutAssessorLaunch launch, Random blinding, ScoutClaimDigest digest, Pass pass,
        Action<string> progress, CancellationToken cancellationToken)
    {
        if (await trees.PrepareAsync(task, cancellationToken) is not Outcome<ScoutTree>.Ok { Value: var tree })
        {
            return pass.Noting($"task {task.Id}: its tree could not be prepared — {claims.Count} claim(s) left unassessed");
        }

        foreach (var batch in ScoutAssessment.Batches(ScoutAssessment.Blind(claims, blinding)))
        {
            if (pass.Exhausted || cancellationToken.IsCancellationRequested)
            {
                break;
            }

            pass = await BatchAsync(run, task, tree, batch, launch, digest, pass, progress, cancellationToken);
        }

        return pass;
    }

    /// <summary>One batch, its own unit: asked, read, and its verdicts stored before the next batch is asked; a failure is recorded on the
    /// pass it returns and the campaign goes on.</summary>
    private async Task<Pass> BatchAsync(
        ScoutRun run, ScoutTask task, ScoutTree tree, IReadOnlyList<ScoutBlindClaim> batch, ScoutAssessorLaunch launch, ScoutClaimDigest digest, Pass pass,
        Action<string> progress, CancellationToken cancellationToken)
    {
        var answers = await assessor.AskAsync(launch, run.Id, batch, tree.Root, cancellationToken);

        if (answers is Outcome<IReadOnlyList<ScoutClaimAnswer>>.Fail fail)
        {
            progress(string.Create(CultureInfo.InvariantCulture, $"failed         task {task.Id} — a batch of {batch.Count} claim(s): {fail.Reason}"));
            return pass.Failing(batch.Count, $"task {task.Id}: a batch of {batch.Count} claim(s) got no verdicts — {fail.Reason}");
        }

        var now = clock.GetUtcNow();
        var given = ((Outcome<IReadOnlyList<ScoutClaimAnswer>>.Ok)answers).Value;
        var stored = await verdicts.StoreVerdictsAsync([.. given.Select(a => ScoutAssessment.Stored(a, run.Id, tree.Root, launch.Model, now, digest))], cancellationToken);
        progress(string.Create(CultureInfo.InvariantCulture,
            $"assessed       task {task.Id} — {stored} of {batch.Count} claim(s) given a verdict{(given.Count < batch.Count ? $"; {batch.Count - given.Count} unanswered, asked again next pass" : string.Empty)}"));
        return pass.Storing(batch.Count, stored);
    }
}
