using Bench.Application;
using Bench.Domain.Engines;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Domain.Variants;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The delivered-work pipeline's raw exchanges, against real Postgres.
/// <para>
/// The guarantee worth a database is the recompute property: a stored score must be reproducible from what
/// is here, with no model call. Everything below is that property's preconditions — the payload arrives
/// unparsed, an attempt cannot be rewritten, and a re-ask is readable from the data rather than from a log.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresStagePayloadStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_payload_comes_back_EXACTLY_as_it_arrived()
    {
        var resultId = await SeedResultAsync();
        const string raw = "Sure!\n```json\n{\"steps\":[{\"key\":\"s1\"}]}\n```\nHope that helps.";

        await Store().AppendAsync(Payload(resultId, DeliveredStage.Decompose, 0, raw), Ct);

        // Unparsed, prose and fence included. A stored parse is a stored INTERPRETATION: it could not be
        // re-read under a fixed parser, which is half of what a rescore is for.
        var stored = await Store().ForResultAsync(resultId, Ct);
        stored.Should().ContainSingle().Which.PayloadJson.Should().Be(raw);
    }

    [Fact]
    public async Task An_attempt_cannot_be_REWRITTEN()
    {
        var resultId = await SeedResultAsync();
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Weigh, 0, "{\"scores\":[]}"), Ct);

        var again = await Store().AppendAsync(Payload(resultId, DeliveredStage.Weigh, 0, "{\"scores\":[1]}"), Ct);

        // A payload that could be rewritten would make an old score unreproducible while still LOOKING
        // reproducible — the worst of the two failures, because nothing would report it.
        again.Reason().Should().Contain("appended once and never rewritten");
        (await Store().ForResultAsync(resultId, Ct)).Should().ContainSingle()
            .Which.PayloadJson.Should().Be("{\"scores\":[]}");
    }

    [Fact]
    public async Task A_RE_ASK_is_readable_from_the_data_rather_than_from_a_log()
    {
        var resultId = await SeedResultAsync();
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Decompose, 0, "first"), Ct);
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Decompose, 1, "second"), Ct);

        // The gate allows exactly ONE re-ask, so an ordinal above zero IS the record that one happened.
        var stored = await Store().ForResultAsync(resultId, Ct);
        stored.Should().HaveCount(2);
        stored[0].IsReAsk.Should().BeFalse();
        stored[1].IsReAsk.Should().BeTrue();
    }

    [Fact]
    public async Task Payloads_come_back_in_the_order_a_RESCORE_replays_them()
    {
        var resultId = await SeedResultAsync();
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Coverage, 0, "c"), Ct);
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Weigh, 1, "w1"), Ct);
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Decompose, 0, "d"), Ct);
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Weigh, 0, "w0"), Ct);

        // Stage then ordinal, so a caller never has to know that a re-ask sorts after its first attempt.
        var stored = await Store().ForResultAsync(resultId, Ct);

        stored.Select(p => $"{p.Stage}{p.Ordinal}").Should()
            .ContainInOrder("Decompose0", "Weigh0", "Weigh1", "Coverage0");
    }

    [Fact]
    public async Task Two_RESULTS_do_not_see_each_others_payloads()
    {
        var mine = await SeedResultAsync();
        var theirs = await SeedResultAsync();

        await Store().AppendAsync(Payload(mine, DeliveredStage.Weigh, 0, "mine"), Ct);
        await Store().AppendAsync(Payload(theirs, DeliveredStage.Weigh, 0, "theirs"), Ct);

        // The same (stage, ordinal) on two results is not a duplicate — the uniqueness is per result, and a
        // rule that read it otherwise would let the first leg of a run block every leg after it.
        (await Store().ForResultAsync(mine, Ct)).Should().ContainSingle().Which.PayloadJson.Should().Be("mine");
    }

    [Fact]
    public async Task A_result_measured_BEFORE_this_existed_answers_EMPTY_rather_than_refusing()
    {
        var resultId = await SeedResultAsync();

        // Those runs are simply not rescorable. A reader must be able to tell that apart from a run whose
        // model said nothing, and a refusal here would read as a broken store instead.
        (await Store().ForResultAsync(resultId, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_FOOTPRINT_counts_what_nobody_would_otherwise_notice_growing()
    {
        var resultId = await SeedResultAsync();
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Decompose, 0, new string('x', 100)), Ct);
        await Store().AppendAsync(Payload(resultId, DeliveredStage.Weigh, 0, new string('y', 50)), Ct);

        var footprint = await Store().FootprintAsync(Ct);

        // This is the one table kept forever, so its size is a budget line that gets PRINTED rather than a
        // cleanup target. Rows and bytes together: a million small payloads and a thousand enormous ones
        // are different problems with the same row count.
        footprint.Rows.Should().BeGreaterThanOrEqualTo(2);
        footprint.Bytes.Should().BeGreaterThanOrEqualTo(150);
        footprint.Results.Should().BeGreaterThanOrEqualTo(1);
        footprint.Describe.Should().Contain("kept permanently");
    }

    [Fact]
    public async Task The_PROTOCOL_travels_with_the_payload_rather_than_being_looked_up()
    {
        var resultId = await SeedResultAsync();
        await Store().AppendAsync(
            Payload(resultId, DeliveredStage.Weigh, 0, "{}") with { Protocol = "delivered-work-v0 (old)" }, Ct);

        // A score is comparable only with scores made under the same protocol. Looking it up from whatever
        // is current would silently re-label every historical run the day the scale changes.
        (await Store().ForResultAsync(resultId, Ct)).Single().Protocol.Should().Be("delivered-work-v0 (old)");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private PostgresStagePayloadStore Store() => new(postgres.NewContext());

    private static StagePayload Payload(Guid resultId, DeliveredStage stage, int ordinal, string json) =>
        StagePayload.Of(resultId, stage, ordinal, json, "prompt-hash", "delivered-work-v1", Noon);

    /// <summary>A run, a cell and a result — the payload's foreign key needs a real one.</summary>
    private async Task<Guid> SeedResultAsync()
    {
        var commit = CommitSha.Parse(new string('b', 40)).Ok();
        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), commit);
        var run = BenchRun.Planned("payloads", target, new EngineRef(EngineKind.NoRetrieval, "", "", "fp"), "s@v1#abc", Noon);

        var question = new Question("q1", "prompt", [Expectation.File(SourceAnchor.File("src/F.cs", commit))], string.Empty);

        var cells = Matrix.Plan(
            [question],
            repeats: 1,
            [new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1))],
            [Lane.Named("lane1")],
            [VariantSelection.None]).Ok()
            .Select(c => RunCell.Pending(run.Id, c))
            .ToList();

        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        var saved = await postgres.NewResults(new TestClock(Noon)).SaveAsync(
            LegResult.Of(cells[0].Id, "q", "a", [StoredMetric.Boolean("x", true, string.Empty, false, "Good")], Noon), Ct);

        return saved.Ok().Id;
    }
}
