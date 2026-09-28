using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Assessment;

/// <summary>The verdict-ingestion contract (a native pass and E5's import both write through it), and the hand-check
/// verbs over real verdicts.</summary>
[Collection("postgres")]
public sealed class GateVerdictStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_verdict_naming_a_finding_no_settled_attempt_stored_refuses_the_whole_batch()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, ct: Ct);
        var good = Verdict(rig, rig.Cells[0], 1, "b1");
        var stranger = Verdict(rig, rig.Cells[0], 7, "b1");

        (await rig.NewVerdicts().RecordAsync([good, stranger], Ct)).Reason().Should().Contain("finding 7").And.Contain("refused whole");
        (await rig.VerdictsAsync()).Should().BeEmpty("all or none — the good verdict did not land beside the refused one");
    }

    [Fact]
    public async Task A_replayed_batch_changes_nothing()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, ct: Ct);
        IReadOnlyList<GateVerdict> batch = [Verdict(rig, rig.Cells[0], 0, "b1"), Verdict(rig, rig.Cells[0], 1, "b1")];

        (await rig.NewVerdicts().RecordAsync(batch, Ct)).Ok().Should().Be(2);
        (await rig.NewVerdicts().RecordAsync(batch, Ct)).Ok().Should().Be(0, "a crash between the verdict log and the database is replayed, not doubled");
        (await rig.VerdictsAsync()).Should().HaveCount(2);
        (await rig.VerdictsAsync()).Should().BeEquivalentTo(batch, "every field reads back under the catalog as it was written");
    }

    [Fact]
    public async Task A_hand_check_is_sampled_answered_and_recorded_and_then_unlocks_the_strict_percentage()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 24, ct: Ct);
        await rig.Pass(ScriptedAssessor.AllSupported()).RunAsync(rig.Request(), _ => { }, Ct);
        var scope = new HandCheckScope([rig.Campaign], AssessRig.Assessor().Id, rig.Strict.Rubric, rig.Catalog);
        var checks = new GateHandChecks(rig.NewStore(), rig.Artifacts, rig.NewVerdicts(), rig.Files, TimeProvider.System, new Random(9));

        var sample = (await checks.SampleAsync(scope, 20, Ct)).Ok();
        var lines = (await File.ReadAllLinesAsync(sample, Ct)).ToList();
        lines.Should().HaveCount(21, "a header and twenty drawn verdicts");
        lines[1].Should().Contain("\"finding\"").And.Contain("\"note\"").And.Contain("\"agree\":null");

        (await checks.RecordAsync(scope, sample, Ct)).Reason().Should().Contain("0 row(s) answered", "nothing is recorded before a person answers");

        await File.WriteAllLinesAsync(sample, [lines[0], .. lines.Skip(1).Select((l, i) => Answered(l, agree: i != 3))], Ct);
        var recorded = (await checks.RecordAsync(scope, sample, Ct)).Ok();

        recorded.Read.Should().Be(20);
        recorded.Agreed.Should().Be(19);
        (await rig.ReportAsync()).Rows.Single().SupportedPct.Should().Be(Figure.NotHandChecked);
        (await rig.ReportAsync(await rig.NewVerdicts().HandChecksAsync(rig.Catalog, Ct))).Rows.Single().SupportedPct.Should().Be(Figure.Of(100));
    }

    [Fact]
    public async Task A_sample_whose_verdict_was_edited_after_the_draw_is_not_recorded()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 21, ct: Ct);
        await rig.Pass(ScriptedAssessor.AllSupported()).RunAsync(rig.Request(), _ => { }, Ct);
        var scope = new HandCheckScope([rig.Campaign], AssessRig.Assessor().Id, rig.Strict.Rubric, rig.Catalog);
        var checks = new GateHandChecks(rig.NewStore(), rig.Artifacts, rig.NewVerdicts(), rig.Files, TimeProvider.System, new Random(9));
        var sample = (await checks.SampleAsync(scope, 20, Ct)).Ok();
        var lines = await File.ReadAllLinesAsync(sample, Ct);

        await File.WriteAllLinesAsync(sample, [lines[0], .. lines.Skip(1).Select((l, i) => i == 0 ? Answered(l, true).Replace("\"supported\"", "\"refuted\"") : Answered(l, true))], Ct);

        (await checks.RecordAsync(scope, sample, Ct)).Reason().Should().Contain("no longer shows the verdict as stored");
        (await rig.NewVerdicts().HandChecksAsync(rig.Catalog, Ct)).Should().NotContain(c => c.Campaigns.Contains(rig.Campaign),
            "the store is shared with other tests; the guarantee is that THIS campaign has no hand-check");
    }

    private static string Answered(string line, bool agree)
    {
        var row = (JsonObject)JsonNode.Parse(line)!;
        row["agree"] = agree;
        return row.ToJsonString();
    }

    private static GateVerdict Verdict(AssessRig rig, Guid cell, int ordinal, string batch) =>
        GateVerdict.Under(rig.Catalog, rig.Strict.Rubric.Hash, cell, ordinal,
            new Verdict.Strict(StrictReading.Supported, ValueLevel.High, SeverityFairness.Yes, Grounding.Near, new string('c', 64), SeedHit.Parse("cs2-S1")),
            AssessRig.Assessor().Id, batch, StableHash.Of("prompt"), false).Ok();
}
