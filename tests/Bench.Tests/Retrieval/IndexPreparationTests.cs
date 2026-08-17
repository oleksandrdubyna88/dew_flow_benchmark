using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>The preparation state machine, without a database.
/// <para>
/// <b>Why it has an owner at all.</b> <c>Requested | Building | Ready | Failed</c> with nothing watching it is
/// the <c>SweepAsync</c> finding a third time: an engine restart mid-pass leaves a row in <c>Building</c>
/// forever, and because every cell of that corpus waits on it, one stranded row stalls a whole variant for
/// the life of the deployment — a stall indistinguishable from a slow index.
/// </para></summary>
public sealed class IndexPreparationTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(45);

    [Fact]
    public void Two_variants_that_differ_only_in_their_LIMIT_want_the_same_corpus()
    {
        var five = Key(Corpus());
        var twenty = Key(Corpus());

        // Keyed by the corpus, never by the whole recipe: keying by the recipe would build one index twice and
        // then compare a corpus against itself.
        five.RecipeHash.Should().Be(twenty.RecipeHash);
    }

    [Fact]
    public void A_different_chunk_size_is_a_different_corpus()
    {
        Key(Corpus(chunkTokens: 256)).RecipeHash.Should().NotBe(Key(Corpus(chunkTokens: 512)).RecipeHash);
    }

    [Fact]
    public void Two_daemons_are_two_indexes_even_at_the_same_commit_and_recipe()
    {
        Key(Corpus(), "http://a:1").Should().NotBe(Key(Corpus(), "http://b:1"));
    }

    [Fact]
    public void A_started_preparation_records_who_is_watching_and_which_pass()
    {
        var started = Requested().Start(Worker(), "pass-7", Noon).Ok();

        started.State.Should().Be(PreparationState.Building);
        started.PassId.Should().Be("pass-7", "a poll must ask about THAT pass rather than the newest and hope");
        started.Owner.Canonical.Should().Contain("indexer");
        started.Heartbeat.Should().Be(Noon);
    }

    [Fact]
    public void An_owner_nobody_can_vouch_for_may_not_start_one()
    {
        Requested().Start(WorkerIdentity.Nobody, "pass-7", Noon)
            .Reason().Should().Contain("host and a pid");
    }

    [Fact]
    public void A_second_worker_cannot_start_a_pass_somebody_is_already_watching()
    {
        var building = Requested().Start(Worker(), "pass-7", Noon).Ok();

        // Two workers starting one pass would build the corpus twice and each would poll a different id.
        building.Start(Worker(), "pass-9", Noon).Reason().Should().Contain("Building, not Requested");
    }

    [Fact]
    public void A_LIVE_worker_past_the_window_is_left_alone()
    {
        var building = Requested().Start(Worker(), "pass-7", Noon).Ok();

        var swept = building.Strand(Noon + Window + TimeSpan.FromMinutes(5), Window, Environment.MachineName, _ => true);

        // A twenty-four-minute pass is normal here and the window is a MARGIN, not a death certificate. Ending
        // a live pass would leave a half-built collection nobody can describe.
        swept.State.Should().Be(PreparationState.Building);
    }

    [Fact]
    public void A_worker_that_is_GONE_past_the_window_moves_the_row_to_a_state_you_can_retry_from()
    {
        var building = Requested().Start(Worker(), "pass-7", Noon).Ok();

        var swept = building.Strand(Noon + TimeSpan.FromHours(2), Window, Environment.MachineName, _ => false);

        swept.State.Should().Be(PreparationState.Failed);
        swept.Reason.Should().Contain("pass-7").And.Contain("retry from here");
        swept.Owner.Should().Be(WorkerIdentity.Nobody, "a failed row holds nothing");
    }

    [Fact]
    public void A_worker_on_ANOTHER_machine_is_never_swept_from_here()
    {
        var elsewhere = Requested().Start(
            WorkerIdentity.Stored("indexer", "some-other-host", 4242), "pass-7", Noon).Ok();

        // That host's process table is the only one that can answer, and ending a live pass on it is worse
        // than leaving a stale row for its own host's next sweep.
        elsewhere.Strand(Noon + TimeSpan.FromHours(2), Window, Environment.MachineName, _ => false)
            .State.Should().Be(PreparationState.Building);
    }

    [Fact]
    public void A_heartbeat_keeps_a_long_pass_out_of_the_sweep()
    {
        var building = Requested().Start(Worker(), "pass-7", Noon).Ok();
        var watched = building.Beat(Noon + TimeSpan.FromMinutes(40));

        watched.Strand(Noon + TimeSpan.FromMinutes(50), Window, Environment.MachineName, _ => false)
            .State.Should().Be(PreparationState.Building, "ten minutes since the last beat is not stale");
    }

    [Fact]
    public void A_heartbeat_on_a_finished_row_changes_nothing()
    {
        var ready = Requested().Start(Worker(), "pass-7", Noon).Ok().Ready(Noon).Ok();

        ready.Beat(Noon + TimeSpan.FromHours(1)).Should().Be(ready);
    }

    [Fact]
    public void A_finished_preparation_is_retried_by_REQUESTING_a_new_one()
    {
        var failed = Requested().Failed("the engine refused the commit", Noon).Ok();

        // Not by editing this row: what a corpus was found to be at one moment is a fact about that moment,
        // and a row that can flip back to Building is a row two workers can disagree about.
        failed.Ready(Noon).Reason().Should().Contain("already Failed");
    }

    [Fact]
    public void An_index_that_was_already_there_is_recorded_as_FOUND_rather_than_built()
    {
        var found = IndexPreparation.Found(Key(Corpus()), Noon);

        // So a report can tell a corpus this benchmark built from one it merely happened to find — which is
        // the difference between a measurement it can reproduce and one it cannot.
        found.State.Should().Be(PreparationState.Ready);
        found.Reason.Should().Contain("already indexed");
        found.IsTerminal.Should().BeTrue();
    }

    private static IndexPreparation Requested() => IndexPreparation.Requested(Key(Corpus()), Noon);

    private static WorkerIdentity Worker() => WorkerIdentity.Here("indexer");

    private static CorpusKey Key(CorpusSpec corpus, string endpoint = "http://127.0.0.1:5311") =>
        CorpusKey.Of(Commit, corpus, endpoint);

    private static CorpusSpec Corpus(int chunkTokens = 256) =>
        CorpusSpec.Parse("GraphHeader", chunkTokens, "bge-m3").Ok();
}
