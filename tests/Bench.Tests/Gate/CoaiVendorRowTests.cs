using System.Reflection;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>A reviewer row becomes the vendor row the product reads in exactly one place, and that place pins
/// everything the run is not varying. The literal's one-file rule is <c>ArchitectureTests</c>; this is what
/// the one producer does.</summary>
public sealed class CoaiVendorRowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_row_the_product_reads_ticks_only_the_gate_under_measurement()
    {
        var reviewer = Reviewer("grok-medium");

        var setting = CoaiVendorRow.From([reviewer], GateKind.Feature, ResolvedReferences.Empty).Ok();
        var row = Rows(setting.Json).Single();

        row["feature"]!.GetValue<bool>().Should().BeTrue();
        row["plan"]!.GetValue<bool>().Should().BeFalse("the row hosts every gate, and the run measures ONE — pin everything you are not varying");
        row["code"]!.GetValue<bool>().Should().BeFalse();
        row["document"]!.GetValue<bool>().Should().BeFalse();
        setting.Reviewers.Select(r => r.Value).Should().Equal(["grok-medium"]);
    }

    [Fact]
    public void The_row_carries_the_model_the_transport_and_the_vault_entry_name_and_never_a_secret()
    {
        var row = Rows(CoaiVendorRow.From([Reviewer("grok-medium")], GateKind.Plan, ResolvedReferences.Empty).Ok().Json).Single();

        row["id"]!.GetValue<string>().Should().Be("grok-medium");
        row["runtime"]!.GetValue<string>().Should().Be("api");
        row["model"]!.GetValue<string>().Should().Be("grok-4.7");
        row["baseUrl"]!.GetValue<string>().Should().Be("https://api.x.ai/v1");
        row["dialect"]!.GetValue<string>().Should().Be("xai");
        row["effort"]!.GetValue<string>().Should().Be("medium");
        row["reviewMinutes"]!.GetValue<int>().Should().Be(20);
        row["key"]!.GetValue<string>().Should().Be("grok", "the vault ENTRY name — the product reads the key from the vault by it");
        row["price"]!["tierFrom"]!.GetValue<long>().Should().Be(200_000);
        row.ContainsKey("credsKeyRef").Should().BeFalse("the vault's access key travels in the environment, never in the row");
        row.Select(p => p.Key).Should().NotContain("remoteVendor", "written only for a remote row that has one");
    }

    [Fact]
    public void Every_field_the_row_writes_is_one_the_product_knows()
    {
        var row = Rows(CoaiVendorRow.From([Reviewer("grok-medium")], GateKind.Code, ResolvedReferences.Empty).Ok().Json).Single();

        row.Select(p => p.Key).Should().BeSubsetOf(CoaiVendorRow.KnownFields,
            "the writer and the reader enumerate ONE list — a field the product does not know would be silently ignored by it");
    }

    [Fact]
    public void A_referenced_endpoint_is_resolved_on_this_machine_or_refused_by_name()
    {
        var local = Reviewer("qwen-local", endpoint: "LOCAL_LLM_URL");

        CoaiVendorRow.From([local], GateKind.Plan, ResolvedReferences.Empty).Reason()
            .Should().Contain("'qwen-local'").And.Contain("LOCAL_LLM_URL").And.Contain("nothing resolved it");

        var resolved = new ResolvedReferences(new Dictionary<string, string> { ["LOCAL_LLM_URL"] = "http://127.0.0.1:11434/v1" });
        Rows(CoaiVendorRow.From([local], GateKind.Plan, resolved).Ok().Json).Single()["baseUrl"]!.GetValue<string>()
            .Should().Be("http://127.0.0.1:11434/v1", "the VALUE reaches the product; the row keeps the NAME");
    }

    [Fact]
    public void A_retired_reviewer_or_one_that_does_not_host_the_gate_is_refused()
    {
        var retired = Reviewer("grok-medium").Retire(Now).Ok();
        var planOnly = Reviewer("plan-only", gates: [GateKind.Plan]);

        CoaiVendorRow.From([retired], GateKind.Plan, ResolvedReferences.Empty).Reason().Should().Contain("retired");
        CoaiVendorRow.From([planOnly], GateKind.Code, ResolvedReferences.Empty).Reason()
            .Should().Contain("'plan-only'").And.Contain("not ticked for the code gate");
        CoaiVendorRow.From([], GateKind.Code, ResolvedReferences.Empty).Reason().Should().Contain("at least one reviewer");
    }

    [Fact]
    public void An_unknown_vendor_field_is_refused_by_name()
    {
        const string json = """[{"id":"grok","runtime":"api","model":"grok-4.7","baseUrl":"https://api.x.ai/v1","temperature":0.2}]""";

        CoaiVendorRow.Read(json).Reason().Should().Contain("'temperature'").And.Contain("does not know",
            "a field dropped on import is a reviewer that is not the one the operator runs");
    }

    [Fact]
    public void The_panels_rows_read_back_with_the_products_own_defaults_for_absent_flags()
    {
        const string json = """[{"id":"grok","runtime":"api","model":"grok-4.7","baseUrl":"https://api.x.ai/v1","dialect":"xai","key":"grok"}]""";

        var row = CoaiVendorRow.Read(json).Ok().Single();

        row.Id.Should().Be("grok");
        row.Plan.Should().BeTrue("absent means yes for plan and code — what an older configuration meant by saying nothing");
        row.Code.Should().BeTrue();
        row.Feature.Should().BeFalse("absent is NO for feature");
        row.Document.Should().BeFalse();
        row.Dialect.Should().Be("xai");
    }

    [Fact]
    public void A_row_written_here_round_trips_through_the_reader()
    {
        var json = CoaiVendorRow.From([Reviewer("grok-medium")], GateKind.Feature, ResolvedReferences.Empty).Ok().Json;

        var read = CoaiVendorRow.Read(json).Ok().Single();

        read.Id.Should().Be("grok-medium");
        read.Feature.Should().BeTrue();
        read.Plan.Should().BeFalse();
        read.ReviewMinutes.Should().Be(20);
    }

    [Fact]
    public void Malformed_vendor_json_is_refused_rather_than_read_as_nobody()
    {
        CoaiVendorRow.Read("not json").Reason().Should().Contain("not JSON");
        CoaiVendorRow.Read("{}").Reason().Should().Contain("JSON ARRAY");
        CoaiVendorRow.Read("""[{"runtime":"api"}]""").Reason().Should().Contain("without an id");
    }

    /// <summary>The one-producer guarantee as a type: nothing but <c>CoaiVendorRow.From</c> can make the value
    /// the environment builder takes. Checked by reflection over the whole domain assembly, so a second public
    /// factory added anywhere is a red test rather than a review comment.</summary>
    [Fact]
    public void The_setting_has_no_public_constructor_and_exactly_one_public_factory()
    {
        typeof(CoaiVendorsSetting).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();

        var factories = typeof(CoaiVendorsSetting).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.ReturnType == typeof(Outcome<CoaiVendorsSetting>) || m.ReturnType == typeof(CoaiVendorsSetting))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        factories.Should().Equal(["CoaiVendorRow.From"], "a recipe becomes a request in exactly one place");
    }

    private static IReadOnlyList<JsonObject> Rows(string json) =>
        [.. (JsonNode.Parse(json) as JsonArray)!.OfType<JsonObject>()];

    private static GateReviewer Reviewer(string id, string endpoint = "https://api.x.ai/v1", IReadOnlyList<GateKind>? gates = null) =>
        GateReviewer.Create(
            GateReviewerId.Parse(id).Ok(),
            GateReviewerTests.Definition(endpoint: endpoint, gates: gates).Ok(),
            Now);
}
