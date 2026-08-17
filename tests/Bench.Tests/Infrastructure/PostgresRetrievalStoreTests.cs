using Bench.Application;
using Bench.Domain.Models;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>What a retrieval leg leaves in the database.
/// <para>
/// Against real Postgres, applying the real migration: this schema is the artefact the project promises to
/// publish, and a jsonb column that only works against an in-memory provider is a promise nobody can keep.
/// </para>
/// <para>
/// Every assertion here is scoped to the run it wrote, because the fixture's database is shared with the rest
/// of the suite. The database-WIDE guarantee — retention — is tested in
/// <see cref="PostgresHitRetentionTests"/>, which cannot share a database at all.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresRetrievalStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_leg_stores_its_funnel_its_hits_and_its_thinking_in_one_write()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        var store = postgres.NewResults();

        await store.SaveAsync(RetrievalFixtures.Result(cell, RetrievalFixtures.Retrieved(Hit(1), Hit(2))), Ct);

        var read = (await store.ForRunAsync(runId, Ct)).Should().ContainSingle().Subject;
        read.Retrieval.WasPerformed.Should().BeTrue();
        read.Retrieval.Hits.Should().HaveCount(2);
        read.Retrieval.Hits[0].Rank.Should().Be(1, "the order the engine returned IS the measurement");
        read.Retrieval.Hits[0].Ranks.Should().Equal([1, 3]);
        read.Retrieval.Funnel.Stages.Should().ContainSingle(s => s.Name == "rerank");
        read.Retrieval.Funnel.UnattributedMs.Should().Be(3104 - 2500);
        read.Retrieval.Collection.Should().Be("code_ab12");
        read.Thinking.Value.Should().Contain("the delay grows");
        read.Meta.PromptTokens.Value.Should().Be(1200);
        read.Meta.Stop.Should().Be(StopReason.Completed);
        read.Meta.ResponseBytes.Should().Be(4096);
    }

    [Fact]
    public async Task The_axes_asked_for_and_the_axes_applied_both_survive_the_round_trip()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        var store = postgres.NewResults();

        await store.SaveAsync(RetrievalFixtures.Result(cell, RetrievalFixtures.Retrieved(Hit(1))), Ct);

        // The pair is the only evidence that a variant's recipe is what actually served the query. Comparing
        // them and blocking on a mismatch is build-order step 5; storing them is what makes that check
        // possible over runs already measured.
        var read = (await store.ForRunAsync(runId, Ct)).Single().Retrieval;
        read.Requested.Canonical.Should().Contain("limit=20");
        read.Applied.Canonical.Should().Contain("limit=20").And.Contain("rerank=true");
    }

    [Fact]
    public async Task A_control_arm_leg_stores_no_funnel_and_reads_back_as_NOT_PERFORMED()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        var store = postgres.NewResults();

        await store.SaveAsync(RetrievalFixtures.Result(cell, RetrievedContext.NotPerformed), Ct);

        // A baseline that read back as "a search that found nothing" would make the no-retrieval control
        // indistinguishable from a broken index.
        (await store.ForRunAsync(runId, Ct)).Single().Retrieval.WasPerformed.Should().BeFalse();
        (await FunnelsOfAsync(runId)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_degraded_funnel_is_stored_WITH_its_reason_rather_than_dropped()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        var store = postgres.NewResults();

        await store.SaveAsync(
            RetrievalFixtures.Result(
                cell, RetrievalFixtures.Degraded("the engine emits 'trace/v9' and this build reads trace/v0")),
            Ct);

        var funnel = (await FunnelsOfAsync(runId)).Should().ContainSingle().Subject;
        funnel.Degraded.Should().BeTrue();
        funnel.DegradationReason.Should().Contain("trace/v9");

        // The black-box reading is evidence too — an engine that broke its own trace contract must not render
        // like one that never claimed a contract.
        (await store.ForRunAsync(runId, Ct)).Single().Retrieval.WasPerformed.Should().BeTrue();
    }

    [Fact]
    public async Task A_runtime_that_reports_no_reasoning_says_so_rather_than_reading_back_as_empty()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        var store = postgres.NewResults();

        await store.SaveAsync(
            RetrievalFixtures.Result(cell, RetrievedContext.NotPerformed) with
            {
                Thinking = Captured.Unavailable("the response carried no reasoning field"),
            },
            Ct);

        var read = (await store.ForRunAsync(runId, Ct)).Single().Thinking;
        read.WasCaptured.Should().BeFalse(
            "a model that hides its reasoning and one that reasoned about nothing are different facts");
        read.Reason.Should().Contain("no reasoning field");
    }

    [Fact]
    public async Task A_result_written_before_these_columns_existed_reads_as_NOBODY_COUNTED()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        await postgres.NewResults().SaveAsync(
            RetrievalFixtures.Result(cell, RetrievedContext.NotPerformed), Ct);

        // The value the migration gave every existing row. Read as a meta of zeroes it would claim a leg that
        // reported no tokens and cost nothing, which is a different statement from "this row predates the
        // column".
        await using (var db = postgres.NewContext())
        {
            var row = await db.Results.FirstAsync(r => r.Cell!.RunId == runId, Ct);
            row.ResponseMetaJson = "{}";
            await db.SaveChangesAsync(Ct);
        }

        var meta = (await postgres.NewResults().ForRunAsync(runId, Ct)).Single().Meta;
        meta.PromptTokens.WasCaptured.Should().BeFalse();
        meta.PromptTokens.Reason.Should().Contain("no runtime");
    }

    [Fact]
    public async Task One_leg_cannot_end_up_with_two_funnels()
    {
        var (runId, cell) = await RetrievalFixtures.PlanAsync(postgres.NewStore(new TestClock(Noon)), Noon, Ct);
        var store = postgres.NewResults();

        await store.SaveAsync(RetrievalFixtures.Result(cell, RetrievalFixtures.Retrieved(Hit(1))), Ct);
        var second = await store.SaveAsync(RetrievalFixtures.Result(cell, RetrievalFixtures.Retrieved(Hit(1))), Ct);

        // Held by the database, not by a read-then-write above it: two readings of one search with no way to
        // say which served the answer is worse than none.
        second.Failed().Should().BeTrue();
        (await FunnelsOfAsync(runId)).Should().ContainSingle();
    }

    private async Task<IReadOnlyList<FunnelRow>> FunnelsOfAsync(Guid runId)
    {
        await using var db = postgres.NewContext();
        return await db.Funnels.AsNoTracking().Where(f => f.Result!.Cell!.RunId == runId).ToListAsync(Ct);
    }

    private static RetrievedHit Hit(int rank) => RetrievalFixtures.Hit(rank);
}

/// <summary>Retention over the hit snippets: the owner of the one surface in this schema that grows without
/// bound.
/// <para>
/// <b>A database per test, and it has to be.</b> The prune is deliberately database-wide — a budget that only
/// applied to the run which asked for it would not be a budget — so two tests sharing a database means the
/// first one releases the other's snippets and counts them as its own. Written first on the shared fixture:
/// five of nine tests failed on rows they had never written, and the pruned-at stamp came from whichever test
/// ran first. "It released exactly these two" is only a true statement in a database of one's own.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresHitRetentionTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Retention_releases_a_hits_TEXT_and_keeps_everything_a_metric_is_computed_from()
    {
        var (connection, cell) = await AloneAsync("released");
        var pruned = Noon.AddDays(30);

        var released = await Store(connection, new TestClock(pruned)).PruneHitSnippetsAsync(Noon.AddDays(7), Ct);

        released.Hits.Should().Be(2);
        released.BytesFreed.Should().BeGreaterThan(0, "a pass that cannot say what it reclaimed cannot size a disk");
        released.Describe.Should().Contain("ranks, scores and spans kept");

        await using var db = PostgresFixture.Context(connection);
        var hit = await db.RetrievedHits.AsNoTracking().OrderBy(h => h.Rank).FirstAsync(Ct);
        hit.Snippet.Should().BeEmpty();
        hit.SnippetBytes.Should().BeGreaterThan(0, "the count justified the drop; losing it leaves nothing to report");
        hit.SnippetPrunedAt.Should().Be(pruned);
        hit.Rank.Should().Be(1);
        hit.RelativePath.Should().NotBeEmpty();
        hit.Score.Should().BeGreaterThan(0);
        hit.ChannelsJson.Should().Contain("dense");
        hit.RanksJson.Should().Contain("1");
    }

    [Fact]
    public async Task A_pruned_snippet_reads_back_as_PRUNED_rather_than_as_a_hit_the_engine_sent_no_text_for()
    {
        var (connection, cell) = await AloneAsync("state");

        await Store(connection, new TestClock(Noon.AddDays(30))).PruneHitSnippetsAsync(Noon.AddDays(7), Ct);

        // The distinction retention must not destroy: "we deleted it" is a fact about this database, and
        // "the engine sent none" is a fact about that engine.
        var read = await Store(connection, TimeProvider.System).ForRunAsync(await RunOfAsync(connection, cell), Ct);
        var snippet = read.Single().Retrieval.Hits[0].Snippet;
        snippet.State.Should().Be(HitTextState.Pruned);
        snippet.Bytes.Should().BeGreaterThan(0);
        snippet.Reason.Should().Contain("retention");
    }

    [Fact]
    public async Task Retention_leaves_hits_inside_the_window_untouched_and_never_prunes_twice()
    {
        var (connection, _) = await AloneAsync("window");

        var fresh = await Store(connection, TimeProvider.System).PruneHitSnippetsAsync(Noon.AddDays(-1), Ct);
        var stale = await Store(connection, TimeProvider.System).PruneHitSnippetsAsync(Noon.AddDays(7), Ct);
        var again = await Store(connection, TimeProvider.System).PruneHitSnippetsAsync(Noon.AddDays(7), Ct);

        fresh.Hits.Should().Be(0, "a hit inside the window keeps its text");
        stale.Hits.Should().Be(2);
        again.Hits.Should().Be(0, "and a second pass must not re-count what it already released");
        again.Describe.Should().Contain("no hit snippets were old enough");
    }

    [Fact]
    public async Task A_hit_the_engine_sent_no_text_for_is_not_counted_as_something_retention_reclaimed()
    {
        var connection = await postgres.NewDatabaseAsync($"bench_retention_textless_{Guid.NewGuid():N}");
        var cell = await PlanAsync(connection);

        await Store(connection, TimeProvider.System).SaveAsync(
            RetrievalFixtures.Result(cell, RetrievalFixtures.Retrieved(Textless(1))), Ct);

        var released = await Store(connection, TimeProvider.System).PruneHitSnippetsAsync(Noon.AddDays(7), Ct);

        // Reclaiming zero bytes and reporting it as a release would make the retention listing describe work
        // that did not happen — and would stamp a row as pruned that nothing was ever dropped from.
        released.Hits.Should().Be(0);
        released.BytesFreed.Should().Be(0);
    }

    /// <summary>A database of this test's own, holding one leg with two text-carrying hits.</summary>
    private async Task<(string Connection, Guid CellId)> AloneAsync(string name)
    {
        var connection = await postgres.NewDatabaseAsync($"bench_retention_{name}_{Guid.NewGuid():N}");
        var cell = await PlanAsync(connection);

        await Store(connection, TimeProvider.System).SaveAsync(
            RetrievalFixtures.Result(cell, RetrievalFixtures.Retrieved(RetrievalFixtures.Hit(1), RetrievalFixtures.Hit(2))),
            Ct);

        return (connection, cell);
    }

    private async Task<Guid> PlanAsync(string connection)
    {
        var (_, cell) = await RetrievalFixtures.PlanAsync(
            new PostgresRunStore(PostgresFixture.Context(connection), new TestClock(Noon)), Noon, Ct);

        return cell;
    }

    private async Task<Guid> RunOfAsync(string connection, Guid cellId)
    {
        await using var db = PostgresFixture.Context(connection);
        return (await db.Cells.AsNoTracking().FirstAsync(c => c.Id == cellId, Ct)).RunId;
    }

    private static PostgresResultStore Store(string connection, TimeProvider clock) =>
        new(PostgresFixture.Context(connection), clock);

    private static RetrievedHit Textless(int rank) =>
        RetrievalFixtures.Hit(rank) with { Snippet = HitSnippet.NotReported("the engine's hit carried an empty text field") };
}

/// <summary>The leg-evidence shapes both retrieval store classes build. Written once: two copies of a fixture
/// this detailed is two things to keep true.</summary>
internal static class RetrievalFixtures
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('e', 40)).Ok();

    public static async Task<(Guid RunId, Guid CellId)> PlanAsync(
        PostgresRunStore runs, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/retrieval.git").Ok(), Commit);
        var run = BenchRun.Planned("retrieval", target, EngineRef.Filesystem(), "suite@v1#abc", now);

        var cells = Matrix.Plan(
            [new Question("q1", "how is the delay computed?", [], string.Empty)],
            repeats: 1,
            [new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1))],
            [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        await runs.CreateAsync(run, cells, cancellationToken);

        return (run.Id, cells[0].Id);
    }

    public static LegResult Result(Guid cellId, RetrievedContext retrieval) =>
        LegResult.Of(cellId, "the assembled prompt", "the answer", [Metric()], Noon) with
        {
            Thinking = Captured.Text("the delay grows exponentially, with jitter"),
            Meta = new ResponseMeta(
                CapturedCount.Number(1200),
                CapturedCount.Number(90),
                TimeSpan.FromSeconds(3),
                SamplingAsSent.From(Sampling.Deterministic(7), "request-body"),
                StopReason.Completed,
                "stop",
                ResponseBytes: 4096),
            Retrieval = retrieval,
        };

    public static RetrievedContext Retrieved(params RetrievedHit[] hits) =>
        RetrievedContext.Of(
            "code_ab12",
            hits,
            new RetrievalFunnel(TraceContract.V0, [new FunnelStage("rerank", 50, 20, 2500)], 3104, ["graph-enrich"]),
            string.Empty,
            new EngineAxes([new Axis("limit", "20")]),
            new EngineAxes([new Axis("limit", "20"), new Axis("rerank", "true")]),
            payloadBytes: 8192,
            elapsedMs: 3200);

    public static RetrievedContext Degraded(string reason) =>
        RetrievedContext.Of(
            "code_ab12", [], RetrievalFunnel.None, reason, EngineAxes.None, EngineAxes.None, 512, 40);

    public static RetrievedHit Hit(int rank) => new(
        rank,
        $"src/File{rank}.cs",
        rank * 10,
        rank * 10 + 20,
        $"Type{rank}.Member",
        $"csharp|Ns|Type{rank}|Member`0|()",
        $"public void Member{rank}()",
        0.9 - rank * 0.1,
        "rerank",
        ["dense", "sparse"],
        [rank, rank + 2],
        HitSnippet.Text($"the source of member {rank}, which is the bulk of what a retrieval run stores"));

    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    private static StoredMetric Metric() =>
        StoredMetric.Numeric(RetrievalScoring.Mrr, 0.5, "1 of 2 anchor(s) surfaced", false, "Average");
}
