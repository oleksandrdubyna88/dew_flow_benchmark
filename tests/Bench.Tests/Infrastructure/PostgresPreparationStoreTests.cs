using Bench.Application;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The readiness table, against real Postgres.
/// <para>
/// Every guarantee here is about two workers reaching one row at the same moment, which is precisely what a
/// fake cannot test: it would only prove the fake agrees with what we assumed the database does.
/// </para>
/// <para>
/// The SWEEP is tested in <see cref="PostgresPreparationSweepTests"/> instead, because it is database-wide and
/// cannot share one. Observed while writing this class: a Building row another test had left behind, with a
/// dead owner and an older heartbeat, was stranded by the live-worker test and counted as its own.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresPreparationStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_runs_asking_for_the_same_corpus_reach_ONE_row()
    {
        var key = Key();

        var first = await Store().RequestAsync(key, Ct);
        var second = await Store().RequestAsync(key, Ct);

        // Held by a unique index rather than by a read-then-write: two runs starting together would both find
        // nothing and both insert, and the matrix would then hold two readiness answers for one collection.
        first.Ok().Id.Should().Be(second.Ok().Id);
        (await Store().ListAsync(Ct)).Count(p => p.Key == key).Should().Be(1);
    }

    [Fact]
    public async Task Two_workers_racing_to_request_one_corpus_both_get_the_same_row()
    {
        var key = Key();

        // Concurrently, on separate contexts — EF's is not thread-safe, exactly as in production.
        var raced = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => Store().RequestAsync(key, Ct)));

        raced.Should().OnlyContain(r => !r.Failed());
        raced.Select(r => r.Ok().Id).Distinct().Should().ContainSingle("the index absorbs the race");
    }

    [Fact]
    public async Task Only_one_worker_can_start_the_pass()
    {
        var key = Key();
        await Store().RequestAsync(key, Ct);

        var mine = await Store().StartAsync(key, Worker("indexer-a"), "pass-7", Ct);
        var theirs = await Store().StartAsync(key, Worker("indexer-b"), "pass-9", Ct);

        mine.Ok().PassId.Should().Be("pass-7");
        theirs.Failed().Should().BeTrue("two workers starting one pass would build the corpus twice");
        theirs.Reason().Should().Contain("Building").And.Contain("indexer-a");
    }

    [Fact]
    public async Task A_worker_cannot_end_a_pass_another_one_is_watching()
    {
        var key = Key();
        await Store().RequestAsync(key, Ct);
        await Store().StartAsync(key, Worker("indexer-a"), "pass-7", Ct);

        var stolen = await Store().EndAsync(key, Worker("indexer-b"), PreparationState.Ready, string.Empty, Ct);

        // The whole owner triple is in the WHERE, never the label alone: two machines may honestly both call
        // themselves "indexer".
        stolen.Failed().Should().BeTrue();
        (await Store().FindAsync(key, Ct)).Ok().State.Should().Be(PreparationState.Building);
    }

    [Fact]
    public async Task The_owner_ends_it_and_the_row_holds_nothing_afterwards()
    {
        var key = Key();
        await Store().RequestAsync(key, Ct);
        await Store().StartAsync(key, Worker("indexer-a"), "pass-7", Ct);

        var ready = await Store().EndAsync(key, Worker("indexer-a"), PreparationState.Ready, string.Empty, Ct);

        ready.Ok().State.Should().Be(PreparationState.Ready);
        ready.Ok().Owner.Should().Be(WorkerIdentity.Nobody);
        ready.Ok().IsTerminal.Should().BeTrue();
    }

    [Fact]
    public async Task A_preparation_cannot_be_ended_into_a_state_that_is_not_an_ending()
    {
        var key = Key();
        await Store().RequestAsync(key, Ct);

        (await Store().EndAsync(key, Worker("indexer-a"), PreparationState.Building, string.Empty, Ct))
            .Reason().Should().Contain("not an ending");
    }

    [Fact]
    public async Task A_worker_that_does_not_hold_it_cannot_refresh_its_heartbeat()
    {
        var key = Key();
        await Store().RequestAsync(key, Ct);
        await Store().StartAsync(key, Worker("indexer-a"), "pass-7", Ct);

        (await Store().BeatAsync(key, Worker("indexer-b"), Ct))
            .Reason().Should().Contain("could not be refreshed");
    }

    [Fact]
    public async Task Two_daemons_at_one_commit_and_one_recipe_are_two_preparations()
    {
        var commit = CommitSha.Parse(new string('c', 40)).Ok();
        var here = CorpusKey.Of(commit, Corpus(), "http://127.0.0.1:5311");
        var there = CorpusKey.Of(commit, Corpus(), "http://127.0.0.1:5999");

        await Store().RequestAsync(here, Ct);
        await Store().RequestAsync(there, Ct);

        (await Store().FindAsync(here, Ct)).Ok().Id.Should().NotBe((await Store().FindAsync(there, Ct)).Ok().Id);
    }

    private PostgresPreparationStore Store() => Store(TimeProvider.System);

    private PostgresPreparationStore Store(TimeProvider clock) => new(postgres.NewContext(), clock);

    private static WorkerIdentity Worker(string label) => WorkerIdentity.Here(label);

    /// <summary>A key nothing else in the shared database can collide with — the commit is per-test.</summary>
    private static CorpusKey Key() =>
        CorpusKey.Of(
            CommitSha.Parse(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8]).Ok(),
            Corpus(),
            "http://127.0.0.1:5311");

    private static CorpusSpec Corpus() => CorpusSpec.Parse("GraphHeader", 256, "bge-m3").Ok();
}

/// <summary>The preparation sweep, in a database of its own.
/// <para>
/// It moves EVERY row a dead worker left building, deliberately — a sweep scoped to one run would leave a
/// corpus stalled for every other run that wants it. So it cannot share a database with tests that also leave
/// building rows: the first version of this shared the fixture, and a leftover row with a dead owner and an
/// older heartbeat was stranded by the live-worker test and counted as its own. The same reason
/// <see cref="PostgresHitRetentionTests"/> has its own.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresPreparationSweepTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_pass_a_dead_worker_was_watching_is_moved_to_a_state_you_can_retry_from()
    {
        var (connection, key) = await AloneAsync("strand");
        var clock = new TestClock(Noon);
        await Store(connection, clock).RequestAsync(key, Ct);
        await Store(connection, clock).StartAsync(key, TestWorkers.Dead("indexer-gone"), "pass-7", Ct);

        clock.Now = Noon + TimeSpan.FromHours(2);
        var swept = await Store(connection, clock).SweepAsync(TimeSpan.FromMinutes(45), Ct);

        // One engine restart would otherwise stall every cell of this corpus for the life of the deployment,
        // and the stall is indistinguishable from a slow index.
        swept.Stranded.Should().Be(1);
        swept.Describe.Should().Contain("stranded");

        var failed = (await Store(connection, TimeProvider.System).FindAsync(key, Ct)).Ok();
        failed.State.Should().Be(PreparationState.Failed);
        failed.Reason.Should().Contain("pass-7").And.Contain("retry from here");
    }


    [Fact]
    public async Task A_pass_a_LIVE_worker_is_watching_survives_the_sweep()
    {
        var (connection, key) = await AloneAsync("strand");
        var clock = new TestClock(Noon);
        await Store(connection, clock).RequestAsync(key, Ct);
        await Store(connection, clock).StartAsync(key, WorkerIdentity.Here("indexer-live"), "pass-7", Ct);

        clock.Now = Noon + TimeSpan.FromHours(2);
        var swept = await Store(connection, clock).SweepAsync(TimeSpan.FromMinutes(45), Ct);

        // This process IS alive, so the window is a margin rather than a death certificate — a 24-minute pass
        // must never be swept out from under itself.
        swept.Stranded.Should().Be(0);
        (await Store(connection, TimeProvider.System).FindAsync(key, Ct)).Ok().State.Should().Be(PreparationState.Building);
    }


    [Fact]
    public async Task A_heartbeat_keeps_a_long_pass_out_of_the_sweep()
    {
        var (connection, key) = await AloneAsync("beat");
        var clock = new TestClock(Noon);
        var owner = TestWorkers.Dead("indexer-gone");
        await Store(connection, clock).RequestAsync(key, Ct);
        await Store(connection, clock).StartAsync(key, owner, "pass-7", Ct);

        clock.Now = Noon + TimeSpan.FromMinutes(40);
        (await Store(connection, clock).BeatAsync(key, owner, Ct)).Failed().Should().BeFalse();

        clock.Now = Noon + TimeSpan.FromMinutes(50);
        (await Store(connection, clock).SweepAsync(TimeSpan.FromMinutes(45), Ct)).Stranded
            .Should().Be(0, "ten minutes since the last beat is not stale, whatever the owner is");
    }

    private async Task<(string Connection, CorpusKey Key)> AloneAsync(string name)
    {
        var connection = await postgres.NewDatabaseAsync($"bench_prep_{name}_{Guid.NewGuid():N}");

        return (connection, CorpusKey.Of(
            CommitSha.Parse(new string('f', 40)).Ok(),
            CorpusSpec.Parse("GraphHeader", 256, "bge-m3").Ok(),
            "http://127.0.0.1:5311"));
    }

    private static PostgresPreparationStore Store(string connection, TimeProvider clock) =>
        new(PostgresFixture.Context(connection), clock);
}
