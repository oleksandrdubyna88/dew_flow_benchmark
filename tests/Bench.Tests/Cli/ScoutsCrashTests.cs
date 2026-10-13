using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Infrastructure.Scouts;
using Bench.Tests.Infrastructure;
using Bench.Tests.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>§5 S2's crash tests at the money boundaries (§3.10, "money survives a crash"), by fault injection through the checkpoint port:
/// the harness dies at ONE boundary while claiming as an owner that is then provably gone, and the next entry step — the owner-checked sweep,
/// then recovery — settles that attempt EXACTLY ONCE: released when it was never marked launching, charged its whole reservation when it was
/// marked and no result reached the disk, settled from the stream's <c>total_cost_usd</c> when one did. A second entry moves nothing; the
/// resumed run measures the cell again in a fresh attempt; and the ledger identity holds at the end.</summary>
[Collection("postgres")]
public sealed class ScoutsCrashTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ScoutArmId[] OneArm = [ScoutArmId.A1];

    [Theory]
    [InlineData(ScoutCheckpoint.BeforeLaunchingMark, ScoutAttemptState.Released)]
    [InlineData(ScoutCheckpoint.AfterLaunchingMark, ScoutAttemptState.ChargedUnknown)]
    [InlineData(ScoutCheckpoint.AfterFirstOutputLine, ScoutAttemptState.ChargedUnknown)]
    [InlineData(ScoutCheckpoint.AfterResultLine, ScoutAttemptState.Settled)]
    [InlineData(ScoutCheckpoint.AfterArtifactCommit, ScoutAttemptState.Settled)]
    [InlineData(ScoutCheckpoint.BeforeSettlement, ScoutAttemptState.Settled)]
    public async Task A_crash_at_each_money_boundary_is_settled_once_by_the_next_entry_and_the_ledger_identity_holds(ScoutCheckpoint point, ScoutAttemptState recovered)
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(OneArm);
        var stream = setup.StreamOf(setup.Tasks[0], ScoutArmId.A1);
        var runId = await ScoutsCampaignTests.CrashedRunAsync(setup, point, OneArm);

        var crashed = (await setup.NewStore().AttemptsAsync(runId, Ct)).Single();
        crashed.IsOpen.Should().BeTrue("a crash leaves its attempt open — only recovery may close it");

        var sweep = await setup.RunAsync(setup.Services(), "sweep", "--run", runId.ToString());
        sweep.Code.Should().Be(ExitCodes.Pass, sweep.Output + sweep.Error);
        sweep.Output.Should().Contain("1 claim(s) handed back", "the dead owner's claim goes back to the queue");

        var after = (await setup.NewStore().AttemptsAsync(runId, Ct)).Single();
        after.State.Should().Be(recovered);
        Money(after).Should().Be(recovered switch
        {
            ScoutAttemptState.Settled => ScoutCellStreams.CostOf(stream),
            ScoutAttemptState.ChargedUnknown => SpendLedger.ReservationFor(1m),
            _ => 0m,
        }, "a launched attempt with no surviving result is charged its WHOLE reservation; one with a result, exactly its total_cost_usd");

        var again = await setup.RunAsync(setup.Services(), "sweep", "--run", runId.ToString());
        again.Output.Should().Contain("0 released, 0 settled from their stream, 0 charged as unknown cost", "an attempt is settled once, never twice");
        (await setup.NewStore().AttemptsAsync(runId, Ct)).Single().Should().Be(after);

        var resumed = await setup.RunAsync(setup.Services(), "resume", "--run", runId.ToString());
        resumed.Code.Should().Be(ExitCodes.Pass, resumed.Output + resumed.Error);
        var cell = (await setup.NewStore().CellsAsync(runId, Ct)).Single();
        cell.Should().Match<ScoutCell>(c => c.State == CellState.Settled && c.Outcome == ScoutCellOutcome.Scored && c.Attempts == 2, "the cell is measured again in a fresh attempt");
        await ScoutsCampaignTests.AssertLedgerIdentityAsync(setup, runId,
            expectedSettled: recovered == ScoutAttemptState.Settled ? 2 : 1,
            expectedUnknown: recovered == ScoutAttemptState.ChargedUnknown ? 1 : 0);
    }

    [Fact]
    public async Task A_crash_after_the_launching_mark_is_never_released_whatever_recovery_runs_after_it()
    {
        await using var setup = await ScoutsCliSetup.StartAsync(postgres);
        setup.Answer(OneArm);
        var runId = await ScoutsCampaignTests.CrashedRunAsync(setup, ScoutCheckpoint.AfterLaunchingMark, OneArm);
        var attempt = (await setup.NewStore().AttemptsAsync(runId, Ct)).Single();

        File.Exists(new ScoutArtifacts(setup.ArtifactRoot).PathOf(attempt, ScoutArtifactKind.Stream)).Should().BeTrue("the stream file is opened before the mark");
        setup.Fake.Calls().Should().BeEmpty("the CLI was never invoked — and the attempt is charged anyway, because the mark says it may have been");

        await setup.RunAsync(setup.Services(), "sweep", "--run", runId.ToString());
        (await setup.NewStore().ReleaseAsync(attempt.Id, Ct)).Reason().Should().Contain("ChargedUnknown");
        (await setup.NewStore().LoadAsync(runId, Ct)).Ok().Ledger.UnknownUsd.Should().Be(SpendLedger.ReservationFor(1m));
    }

    private static decimal Money(ScoutAttempt attempt) => attempt.State switch
    {
        ScoutAttemptState.Settled => attempt.SettledUsd,
        ScoutAttemptState.ChargedUnknown => attempt.ReservedUsd,
        _ => 0m,
    };
}
