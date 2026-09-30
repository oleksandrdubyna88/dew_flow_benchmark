using System.Text;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Infrastructure.Gate;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>The commit protocol, killed at every step: write (staged → flushed → hashed → renamed, the run record
/// last) → persist the refs → settle. Whatever step the process dies at, the cell is left CLAIMED — never settled
/// over evidence that is not all on disk and recorded — and the same recovery always works: the sweep hands it
/// back, the next claim is attempt 2, attempt 2 gets a new directory, attempt 1 is kept and marked, and the cell
/// settles. A cell never gets stuck.</summary>
[Collection("postgres")]
public sealed class GateCellCompletionTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> CrashPoints() =>
    [
        "write:Staged", "write:Flushed", "write:Hashed", "write:Renamed",
        "record:Renamed-run-record", "store:record", "store:settle",
    ];

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public async Task A_crash_at_any_step_leaves_the_cell_claimed_and_the_next_attempt_recovers_it(string crashPoint)
    {
        using var temp = NewRoot();
        var clock = new TestClock(Noon);
        var (run, cells) = Planned(count: 1);
        var store = new PostgresGateStore(postgres.NewContext(), clock);
        await store.PlanAsync(run, cells, Ct);
        var dead = TestWorkers.Dead("dies-mid-commit");
        var first = ArtifactScope.Of(run, (await store.ClaimNextAsync(run.Id, dead, Pin(), Ct)).Ok()).Ok();
        (await Store(temp).BeginAttemptAsync(first, Ct)).Ok();

        var dying = new GateCellCompletion(Store(temp, ProbeFor(crashPoint)), new CrashingGateStore(store, crashPoint));
        var crash = async () => await dying.CompleteAsync(first, dead, Files(first), RunRecord(first), Completed(), Ct);
        await crash.Should().ThrowAsync<Exception>($"the process dies at {crashPoint}");

        (await store.CellAsync(first.CellId, Ct)).Ok().State.Should().Be(CellState.Claimed,
            $"a crash at {crashPoint} must never leave a settled cell whose evidence is not all committed and recorded");

        clock.Now = Noon.AddHours(2);
        (await store.SweepAsync(TimeSpan.FromMinutes(30), Ct)).Should().Match<GateSweepReport>(r => r.Total >= 1);
        var second = ArtifactScope.Of(run, (await store.ClaimNextAsync(run.Id, Here(), Pin(), Ct)).Ok()).Ok();
        second.Attempt.Should().Be(2);

        var healthy = Store(temp);
        (await healthy.BeginAttemptAsync(second, Ct)).Ok();
        var settled = (await new GateCellCompletion(healthy, store).CompleteAsync(second, Here(), Files(second), RunRecord(second), Completed(), Ct)).Ok();

        settled.State.Should().Be(CellState.Settled, "the cell is never stuck");
        (await healthy.AttemptsAsync(run.Id, first.CellId, Ct)).Select(a => (a.Attempt, a.State))
            .Should().Equal((1, AttemptState.Interrupted), (2, AttemptState.Recorded));
        (await store.ArtifactsAsync(run.Id, Ct)).Where(a => a.Attempt == 2).Select(a => a.Class)
            .Should().Equal(ArtifactClass.Reply, ArtifactClass.RunRecord);
    }

    [Fact]
    public async Task The_run_record_is_the_last_artefact_and_the_settle_comes_after_every_ref_is_recorded()
    {
        using var temp = NewRoot();
        var (run, cells) = Planned(count: 1);
        var store = new PostgresGateStore(postgres.NewContext(), new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        var scope = ArtifactScope.Of(run, (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok()).Ok();
        var artifacts = Store(temp);
        await artifacts.BeginAttemptAsync(scope, Ct);
        var journal = new JournalingGateStore(store);

        (await new GateCellCompletion(artifacts, journal).CompleteAsync(scope, owner, Files(scope), RunRecord(scope), Completed(), Ct)).Ok();

        journal.Calls.Should().Equal("record Reply,RunRecord", "settle");
        (await new GateCellCompletion(artifacts, store).CompleteAsync(scope, owner, [], Files(scope)[0], Completed(), Ct)).Reason()
            .Should().Contain("run record");
    }

    [Fact]
    public async Task A_scope_that_does_not_match_the_owners_claim_is_refused_before_anything_is_written()
    {
        using var temp = NewRoot();
        var (run, cells) = Planned(count: 1);
        var store = new PostgresGateStore(postgres.NewContext(), new TestClock(Noon));
        await store.PlanAsync(run, cells, Ct);
        var owner = Here();
        var claimed = ArtifactScope.Of(run, (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok()).Ok();
        var artifacts = Store(temp);
        await artifacts.BeginAttemptAsync(claimed, Ct);
        var stale = claimed with { Attempt = 2 };
        var completion = new GateCellCompletion(artifacts, store);

        (await completion.CompleteAsync(stale, owner, Files(stale), RunRecord(stale), Completed(), Ct)).Reason()
            .Should().Contain("attempt 1", "a scope for an attempt the claim is not at would file its refs under the wrong attempt");
        (await completion.CompleteAsync(claimed, WorkerIdentity.Here("somebody-else"), Files(claimed), RunRecord(claimed), Completed(), Ct)).Reason()
            .Should().Contain("does not hold");
        (await store.ArtifactsAsync(run.Id, Ct)).Should().BeEmpty("the refusal comes before any file or ref");
        (await artifacts.AttemptsAsync(run.Id, claimed.CellId, Ct)).Should().ContainSingle().Which.State.Should().Be(AttemptState.Open);
    }

    private static ArtifactProbe ProbeFor(string crashPoint) => crashPoint switch
    {
        "write:Staged" => new CrashAt(ArtifactStep.Staged),
        "write:Flushed" => new CrashAt(ArtifactStep.Flushed),
        "write:Hashed" => new CrashAt(ArtifactStep.Hashed),
        "write:Renamed" => new CrashAt(ArtifactStep.Renamed),
        "record:Renamed-run-record" => new CrashAt(ArtifactStep.Renamed, after: 2),
        _ => ArtifactProbe.None,
    };

    private static IReadOnlyList<PendingArtifact> Files(ArtifactScope scope) =>
        [new(ArtifactClass.Reply, CellPaths.AttemptRoot(scope).Then("reply.json").Ok(), Encoding.UTF8.GetBytes("{\"verdict\":\"revise\"}"))];

    private static PendingArtifact RunRecord(ArtifactScope scope) =>
        new(ArtifactClass.RunRecord, CellPaths.AttemptRoot(scope).Then(CellPaths.RunRecordFile).Ok(), Encoding.UTF8.GetBytes("{\"valid\":true}"));
}

/// <summary>The store, dying at one of its two steps of the protocol.</summary>
internal sealed class CrashingGateStore(IGateStore inner, string crashPoint) : ForwardingGateStore(inner)
{
    public override Task<Outcome<int>> RecordArtifactsAsync(IReadOnlyList<ArtifactRef> refs, CancellationToken cancellationToken) =>
        crashPoint == "store:record" ? throw new InvalidOperationException("simulated crash before the refs were persisted") : base.RecordArtifactsAsync(refs, cancellationToken);

    public override Task<Outcome<GateCell>> SettleAsync(Guid cellId, WorkerIdentity owner, GateSettlement settlement, CancellationToken cancellationToken) =>
        crashPoint == "store:settle" ? throw new InvalidOperationException("simulated crash after the refs, before the settle") : base.SettleAsync(cellId, owner, settlement, cancellationToken);
}

/// <summary>The store, remembering the order the protocol called it in.</summary>
internal sealed class JournalingGateStore(IGateStore inner) : ForwardingGateStore(inner)
{
    public List<string> Calls { get; } = [];

    public override Task<Outcome<int>> RecordArtifactsAsync(IReadOnlyList<ArtifactRef> refs, CancellationToken cancellationToken)
    {
        Calls.Add($"record {string.Join(',', refs.Select(r => r.Class))}");
        return base.RecordArtifactsAsync(refs, cancellationToken);
    }

    public override Task<Outcome<GateCell>> SettleAsync(Guid cellId, WorkerIdentity owner, GateSettlement settlement, CancellationToken cancellationToken)
    {
        Calls.Add("settle");
        return base.SettleAsync(cellId, owner, settlement, cancellationToken);
    }
}

internal abstract class ForwardingGateStore(IGateStore inner) : IGateStore
{
    public Task<Outcome<GateRun>> PlanAsync(GateRun run, IReadOnlyList<GateCell> cells, CancellationToken cancellationToken) => inner.PlanAsync(run, cells, cancellationToken);

    public Task<Outcome<GateRun>> LoadAsync(Guid runId, CancellationToken cancellationToken) => inner.LoadAsync(runId, cancellationToken);

    public Task<IReadOnlyList<GateRun>> RecentAsync(int limit, CancellationToken cancellationToken) => inner.RecentAsync(limit, cancellationToken);

    public Task<IReadOnlyList<Guid>> RunsOfSuiteAsync(string suiteStamp, CancellationToken cancellationToken) => inner.RunsOfSuiteAsync(suiteStamp, cancellationToken);

    public Task<Outcome<GateCell>> ClaimNextAsync(Guid runId, WorkerIdentity owner, ProductPin pin, CancellationToken cancellationToken) =>
        inner.ClaimNextAsync(runId, owner, pin, cancellationToken);

    public Task<Outcome<GateCell>> ClaimNextAmongAsync(
        Guid runId, WorkerIdentity owner, ProductPin pin, IReadOnlyCollection<GateReviewerId> among, CancellationToken cancellationToken) =>
        inner.ClaimNextAmongAsync(runId, owner, pin, among, cancellationToken);

    public virtual Task<Outcome<GateCell>> SettleAsync(Guid cellId, WorkerIdentity owner, GateSettlement settlement, CancellationToken cancellationToken) =>
        inner.SettleAsync(cellId, owner, settlement, cancellationToken);

    public Task<GateSweepReport> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken) => inner.SweepAsync(staleAfter, cancellationToken);

    public Task<Outcome<GateRunStatus>> AdvanceAsync(Guid runId, GateRunStatus to, CancellationToken cancellationToken) => inner.AdvanceAsync(runId, to, cancellationToken);

    public Task<Outcome<GateCell>> CellAsync(Guid cellId, CancellationToken cancellationToken) => inner.CellAsync(cellId, cancellationToken);

    public Task<IReadOnlyList<GateCell>> CellsAsync(Guid runId, CancellationToken cancellationToken) => inner.CellsAsync(runId, cancellationToken);

    public Task<IReadOnlyList<GateRunRecord>> FactsAsync(Guid runId, CancellationToken cancellationToken) => inner.FactsAsync(runId, cancellationToken);

    public virtual Task<Outcome<int>> RecordArtifactsAsync(IReadOnlyList<ArtifactRef> refs, CancellationToken cancellationToken) =>
        inner.RecordArtifactsAsync(refs, cancellationToken);

    public Task<IReadOnlyList<ArtifactRef>> ArtifactsAsync(Guid runId, CancellationToken cancellationToken) => inner.ArtifactsAsync(runId, cancellationToken);

    public virtual Task<bool> HasFindingsAsync(CancellationToken cancellationToken) => inner.HasFindingsAsync(cancellationToken);

    public Task<Outcome<GateCell>> HandBackUnmeasuredAsync(Guid cellId, WorkerIdentity owner, int attempt, string cause, CancellationToken cancellationToken) =>
        inner.HandBackUnmeasuredAsync(cellId, owner, attempt, cause, cancellationToken);

    public Task<Outcome<GateCell>> RequeueUnmeasuredAsync(Guid cellId, WorkerIdentity owner, int attempt, string cause, CancellationToken cancellationToken) =>
        inner.RequeueUnmeasuredAsync(cellId, owner, attempt, cause, cancellationToken);

    public Task<IReadOnlyList<(GateReviewerId Reviewer, string ReferencesHash)>> ReferenceHashesAsync(Guid runId, CancellationToken cancellationToken) =>
        inner.ReferenceHashesAsync(runId, cancellationToken);
}
