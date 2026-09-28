using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.3 — the three protocols against the fake product, through the real MCP client: the order the product
/// enforces, the resolve reply kept, <c>again</c> never sent, and the code gate refused without a passed plan round.</summary>
[Collection("postgres")]
public sealed class GateProtocolTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_code_gate_is_refused_when_no_plan_round_passed_the_products_rule_replayed_by_the_fake()
    {
        using var fake = new FakeCoai();
        await using var session = (await fake.OpenAsync()).Ok();

        var answer = (await session.CallToolAsync("review_code", new JsonObject { ["repoPath"] = "r", ["branch"] = "b", ["baseRef"] = "x", ["planText"] = "p" }, TimeSpan.FromSeconds(30), Ct)).Ok();

        GateReplyParser.Parse(answer.Text).Reply.Should().BeOfType<GateReply.Refused>()
            .Which.Error.Should().Contain("no plan round has reached 'proceed'");
    }

    [Fact]
    public async Task A_code_cell_whose_plan_loop_never_passes_never_calls_review_code_and_is_recorded_invalid()
    {
        var revise = new JsonObject { ["verdict"] = "revise", ["findings"] = new JsonArray() };
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject { ["replies"] = new JsonObject { ["review_plan"] = new JsonArray(revise) } });
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, _) = await rig.PlanAsync(GateKind.Code, [reviewer], repeats: 1);

        var report = await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        report.Settled.Should().Be(1);
        var calls = rig.Fake.Events().Select(e => e.Text).Where(t => t.StartsWith("call ", StringComparison.Ordinal)).Select(t => t.Split(' ')[1]).ToList();
        calls.Count(c => c == "review_plan").Should().Be(ProtocolInputs.MaxPlanRounds);
        calls.Should().NotContain("review_code", "the product refuses it until a plan round passed — the bench does not ask to be refused");
        calls.Count(c => c == "resolve").Should().Be(ProtocolInputs.MaxPlanRounds, "every plan round is resolved, accept-all");
        var record = (await rig.NewStore().FactsAsync(run.Id, Ct)).Should().ContainSingle().Subject;
        record.Facts.Valid.Should().BeFalse();
        record.Facts.Failure.Kind.Should().Be(FailureKind.VerdictNotPassing);
    }

    [Fact]
    public async Task A_code_cell_opens_passes_its_plan_round_then_reviews_the_code_on_its_own_ref_and_never_sends_again()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, _) = await rig.PlanAsync(GateKind.Code, [reviewer], repeats: 1);

        await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        var calls = rig.Fake.Events().Select(e => e.Text).Where(t => t.StartsWith("call ", StringComparison.Ordinal)).ToList();
        calls.Select(c => c.Split(' ')[1]).Should().Equal(["open", "review_plan", "resolve", "review_code", "resolve"]);
        calls.Should().OnlyContain(c => !c.Contains("\"again\":true", StringComparison.Ordinal), "every attempt is a fresh session; again is never sent");
        var runRef = $"bench/gate/{run.Id.ToString("N")[..8]}/rev-a/cs2-r1-a1";
        calls.Should().Contain(c => c.Contains(runRef, StringComparison.Ordinal), "a ref per run, repeat and attempt");
        var record = (await rig.NewStore().FactsAsync(run.Id, Ct)).Should().ContainSingle().Subject;
        record.Facts.Valid.Should().BeTrue(record.Facts.Failure.Text);
        record.Facts.Turns.Should().Be(1, "only the CodeReview ledger rows are the code reviewer's spend");
    }

    [Fact]
    public async Task The_resolve_replys_refusal_is_kept_on_the_stage()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject
        {
            ["replies"] = new JsonObject { ["resolve"] = new JsonArray(new JsonObject { ["error"] = "finding 3 does not exist" }) },
        });
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, cells) = await rig.PlanAsync(GateKind.Plan, [reviewer], repeats: 1);

        await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        var runRecord = await File.ReadAllTextAsync(Path.Combine(rig.Artifacts.Root, "runs", run.Id.ToString("D"), "cells", cells[0].Id.ToString("D"), "attempt-1", "run.json"), Ct);
        runRecord.Should().Contain("plan-1: finding 3 does not exist", "a refusal here is the product saying its answer cannot be acted on");
    }

    [Fact]
    public async Task A_feature_cell_sends_the_suites_inputs_from_the_variant_head_and_needs_no_open()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres);
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, _) = await rig.PlanAsync(GateKind.Feature, [reviewer], repeats: 1);

        await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        var call = rig.Fake.Events().Select(e => e.Text).Should().ContainSingle(t => t.StartsWith("call ", StringComparison.Ordinal)).Subject;
        call.Should().StartWith("call review_feature ");
        var arguments = JsonNode.Parse(call["call review_feature ".Length..])!.AsObject();
        arguments["planPath"]!.GetValue<string>().Should().Be("docs/plan.md");
        arguments["baseRef"]!.GetValue<string>().Should().Be(rig.Task.Case.Base.Value);
        arguments["epics"]!.GetValue<string>().Should().Contain("Epic 1");
        arguments["again"]!.GetValue<bool>().Should().BeFalse();
        arguments["callerModel"]!.GetValue<string>().Should().Be("bench-gate");
        var record = (await rig.NewStore().FactsAsync(run.Id, Ct)).Should().ContainSingle().Subject;
        record.Facts.Valid.Should().BeTrue(record.Facts.Failure.Text);
        record.Findings.Should().ContainSingle().Which.Category.Should().Be(FindingCategory.Reliability);
    }

    [Fact]
    public async Task A_product_that_echoes_its_creds_key_to_stderr_leaves_no_key_in_the_artefact_root()
    {
        await using var rig = await GateDriverRig.StartAsync(postgres, new JsonObject { ["echoCredsKey"] = true });
        var reviewer = GateDriverRig.Reviewer("rev-a", "api.vendor-a.example.com");
        var (run, _) = await rig.PlanAsync(GateKind.Plan, [reviewer], repeats: 1);

        await rig.CampaignAsync(run, [reviewer], parallel: 1, perEndpoint: 1);

        var files = Directory.EnumerateFiles(rig.Artifacts.Root, "*", SearchOption.AllDirectories).ToList();
        files.Should().Contain(f => f.EndsWith("stderr.txt", StringComparison.Ordinal));
        File.ReadAllText(files.Single(f => f.EndsWith("stderr.txt", StringComparison.Ordinal))).Should().Contain("COAI_CREDS_KEY=[redacted]");
        files.Where(f => !f.EndsWith(".key", StringComparison.Ordinal)).Select(File.ReadAllText).Should().NotContain(t => t.Contains(GateDriverRig.CredsKey, StringComparison.Ordinal));
    }
}
