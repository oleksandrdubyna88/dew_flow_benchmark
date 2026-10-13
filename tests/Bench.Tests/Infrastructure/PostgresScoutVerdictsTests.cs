using Bench.Application.Scouts;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.ScoutStoreFixtures;

namespace Bench.Tests.Infrastructure;

/// <summary>S3's writes against a real Postgres: a claim gets ONE verdict — a second store of the same claim is skipped, never an overwrite,
/// even inside one batch — recall is rescored only on a settled, scored cell, and the run's kind and a cell's team facts read back exactly.</summary>
[Collection("postgres")]
public sealed class PostgresScoutVerdictsTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ScoutAssessedClaim Verdict(Guid run, Guid cell, int index, ScoutClaimVerdict verdict) =>
        new(new ScoutClaimKey(cell, 1, index), run, "src/Store.cs", CapturedCount.Number(3), new string('e', 64), verdict, "gpt-test", ScoutAssessment.RubricSha256, Noon);

    private async Task<(ScoutRun Run, ScoutCell Scored, ScoutCell Voided)> SettledAsync(ScoutRunKind kind = ScoutRunKind.Preliminary)
    {
        var (run, tasks, cells) = Planned(tasks: 1);
        var store = new PostgresScoutStore(postgres.NewContext(), new TestClock(Noon));
        (await store.PlanAsync(run with { Kind = kind }, tasks, cells, Ct)).Ok();
        var owner = Here();
        var scored = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        var team = Scored() with { Facts = Scored().Facts with { TeamUsd = CapturedUsd.Amount(0.0123m), TeamTokens = CapturedTokens.Of(new TokenUsage(4, 50, 0, 200, 7)) } };
        (await store.SettleAsync(scored.Id, owner, team, [Scout(1)], Ct)).Ok();
        var voided = (await store.ClaimNextAsync(run.Id, owner, Pin(), Ct)).Ok();
        (await store.SettleAsync(voided.Id, owner, new ScoutSettlement(ScoutCellOutcome.Void, ScoutReason.WrongCallCount, ScoutCellFacts.None), [], Ct)).Ok();

        return (run, (await store.CellAsync(scored.Id, Ct)).Ok(), (await store.CellAsync(voided.Id, Ct)).Ok());
    }

    [Fact]
    public async Task A_claim_gets_one_verdict_and_a_second_store_of_it_is_skipped_even_inside_one_batch()
    {
        var (run, scored, _) = await SettledAsync();
        var verdicts = new PostgresScoutVerdicts(postgres.NewContext());

        var first = await verdicts.StoreVerdictsAsync([Verdict(run.Id, scored.Id, 1, ScoutClaimVerdict.Wrong), Verdict(run.Id, scored.Id, 1, ScoutClaimVerdict.Right)], Ct);
        var second = await verdicts.StoreVerdictsAsync([Verdict(run.Id, scored.Id, 1, ScoutClaimVerdict.Right), Verdict(run.Id, scored.Id, 2, ScoutClaimVerdict.Right)], Ct);

        (first, second).Should().Be((1, 1), "the duplicate inside the first batch and the re-assessed claim in the second are both skipped");
        var stored = await new PostgresScoutReads(postgres.NewContext()).VerdictsAsync(run.Id, Ct);
        stored.Select(v => (v.Key.Index, v.Verdict)).Should().Equal([(1, ScoutClaimVerdict.Wrong), (2, ScoutClaimVerdict.Right)], "the first verdict stands — never an overwrite");
        stored[0].Should().Be(Verdict(run.Id, scored.Id, 1, ScoutClaimVerdict.Wrong), "a verdict reads back exactly");
        (await verdicts.AssessedKeysAsync(run.Id, Ct)).Should().BeEquivalentTo([new ScoutClaimKey(scored.Id, 1, 1), new ScoutClaimKey(scored.Id, 1, 2)]);
    }

    [Fact]
    public async Task Recall_is_rescored_only_on_a_settled_scored_cell_and_the_scouts_key_files_with_it()
    {
        var (run, scored, voided) = await SettledAsync();
        var verdicts = new PostgresScoutVerdicts(postgres.NewContext());

        var rescored = await verdicts.RescoreAsync(scored.Id, new ScoutRescore(2, 2, [new ScoutRescoredKeyFiles(1, ["src/Store.cs", "src/Sweep.cs"])]), Ct);
        var refused = await verdicts.RescoreAsync(voided.Id, new ScoutRescore(1, 2, []), Ct);

        rescored.Ok().Facts.Should().Match<ScoutCellFacts>(f => f.RecallFound.Value == 2 && f.RecallTotal.Value == 2);
        refused.Reason().Should().Contain("not a settled, scored cell");
        (await new PostgresScoutReads(postgres.NewContext()).ScoutRecordsAsync(run.Id, Ct)).Single().Scout.KeyFiles.Should().Equal("src/Store.cs", "src/Sweep.cs");
        (await new PostgresScoutStore(postgres.NewContext(), new TestClock(Noon)).CellAsync(voided.Id, Ct)).Ok().Facts.RecallFound.WasCaptured.Should().BeFalse();
    }

    [Fact]
    public async Task The_run_kind_and_a_cells_team_tokens_and_cost_read_back_exactly()
    {
        var (run, scored, _) = await SettledAsync(ScoutRunKind.Decision);
        var store = new PostgresScoutStore(postgres.NewContext(), new TestClock(Noon));

        (await store.LoadAsync(run.Id, Ct)).Ok().Kind.Should().Be(ScoutRunKind.Decision);
        scored.Facts.TeamUsd.Should().Be(CapturedUsd.Amount(0.0123m));
        scored.Facts.TeamTokens.Should().Be(CapturedTokens.Of(new TokenUsage(4, 50, 0, 200, 7)));
        (await store.CellsAsync(run.Id, Ct)).Single(c => c.Outcome == ScoutCellOutcome.Void).Facts.TeamTokens.WasCaptured.Should().BeFalse("not captured is never a zero");
    }
}
