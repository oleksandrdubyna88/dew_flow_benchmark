using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>A reviewer row becomes the vendor row the product reads in exactly one place, and that place pins
/// everything the run is not varying. The one-producer rule (the literal in one file, no other factory of the
/// setting anywhere) is <c>ArchitectureTests</c>; this is what the one producer does.</summary>
public sealed class CoaiVendorRowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_row_the_product_reads_ticks_only_the_gate_under_measurement()
    {
        var reviewer = Reviewer("grok-medium");

        var setting = CoaiVendorsSetting.From([reviewer], GateKind.Feature, ResolvedReferences.Empty).Ok();
        var row = Rows(setting.Json).Single();

        row["feature"]!.GetValue<bool>().Should().BeTrue();
        row["plan"]!.GetValue<bool>().Should().BeFalse("the row hosts every gate, and the run measures ONE — pin everything you are not varying");
        row["code"]!.GetValue<bool>().Should().BeFalse();
        row["document"]!.GetValue<bool>().Should().BeFalse();
        setting.Reviewers.Select(r => r.Value).Should().Equal(["grok-medium"]);
    }

    /// <summary>The code gate's protocol runs the plan loop FIRST, through the same reviewer, until a round reaches
    /// <c>proceed</c> — so a code-gate row is ticked for plan and code. S7.3, 2026-09-29: ticked for code only, the product
    /// refused all 84 plan rounds ("nothing could review the PlanReview stage") and the code gate measured nothing.</summary>
    [Fact]
    public void A_code_gate_row_is_ticked_for_the_plan_loop_its_protocol_runs_first()
    {
        var row = Rows(CoaiVendorsSetting.From([Reviewer("grok-medium")], GateKind.Code, ResolvedReferences.Empty).Ok().Json).Single();

        row["code"]!.GetValue<bool>().Should().BeTrue();
        row["plan"]!.GetValue<bool>().Should().BeTrue("review_code refuses until a plan round reached proceed, and that round runs on this row");
        row["feature"]!.GetValue<bool>().Should().BeFalse();
        row["document"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public void The_row_carries_the_model_the_transport_and_the_vault_entry_name_and_never_a_secret()
    {
        var row = Rows(CoaiVendorsSetting.From([Reviewer("grok-medium")], GateKind.Plan, ResolvedReferences.Empty).Ok().Json).Single();

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
        var row = Rows(CoaiVendorsSetting.From([Reviewer("grok-medium")], GateKind.Code, ResolvedReferences.Empty).Ok().Json).Single();

        row.Select(p => p.Key).Should().BeSubsetOf(CoaiVendorRow.KnownFields,
            "the writer and the reader enumerate ONE list — a field the product does not know would be silently ignored by it");
    }

    /// <summary>The runtime words are checked against the PRODUCT's set — a fixture copied from its source with
    /// the commit it was read at — never against a list typed in this file, which could only agree with itself.
    /// Every member of the harness's enum is enumerated, so a runtime added here without the product knowing
    /// it is red.</summary>
    [Fact]
    public void Every_runtime_word_the_row_can_write_is_one_the_product_knows()
    {
        var productWords = ProductRuntimeNames();

        var written = Enum.GetValues<ReviewerRuntime>()
            .Select(runtime => (Runtime: runtime, Word: Rows(CoaiVendorsSetting.From(
                [Reviewer($"r-{runtime.Word()}", runtime: runtime)], GateKind.Plan, ResolvedReferences.Empty).Ok().Json).Single()["runtime"]!.GetValue<string>()))
            .ToList();

        written.Should().OnlyContain(w => productWords.Contains(w.Word),
            "a word the product does not know is run on the Codex CLI — a Claude or Gemini reviewer measured on another vendor's agent");
        written.Single(w => w.Runtime == ReviewerRuntime.Claude).Word.Should().Be("claude");
        written.Single(w => w.Runtime == ReviewerRuntime.Gemini).Word.Should().Be("gemini");
    }

    [Fact]
    public void The_product_runtime_fixture_is_read_and_is_the_products_own_set()
    {
        ProductRuntimeNames().Should().Contain(["codex", "api", "local"],
            "a fixture that failed to load would make the runtime check vacuous");
    }

    [Fact]
    public void An_effort_that_asks_for_the_module_default_is_not_written()
    {
        var byDefault = Rows(CoaiVendorsSetting.From([Reviewer("grok-default", effort: ReviewerTransport.ModuleDefault)], GateKind.Plan, ResolvedReferences.Empty).Ok().Json).Single();
        var medium = Rows(CoaiVendorsSetting.From([Reviewer("grok-medium")], GateKind.Plan, ResolvedReferences.Empty).Ok().Json).Single();

        byDefault.ContainsKey("effort").Should().BeFalse(
            "'none' is the harness's word for the module's default; on an OpenAI-style dialect the same word turns reasoning OFF");
        medium["effort"]!.GetValue<string>().Should().Be("medium");
    }

    [Fact]
    public void The_setting_applies_itself_to_an_environment_and_is_the_only_writer_of_its_variable()
    {
        var setting = CoaiVendorsSetting.From([Reviewer("grok-medium")], GateKind.Plan, ResolvedReferences.Empty).Ok();
        var inherited = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin", ["coai_vendors"] = "[]" };

        var applied = setting.ApplyTo(inherited);

        applied.Values.Should().Contain(setting.Json);
        applied.Keys.Where(k => k.Equals("coai_vendors", StringComparison.OrdinalIgnoreCase)).Should().ContainSingle(
            "whatever case the inherited environment spelled it in, the product reads ONE vendor list — this one");
        applied["PATH"].Should().Be("/usr/bin");
        inherited["coai_vendors"].Should().Be("[]", "the caller's environment is not mutated; a new one is returned");
    }

    [Fact]
    public void A_referenced_endpoint_is_resolved_on_this_machine_or_refused_by_name()
    {
        var local = Reviewer("qwen-local", endpoint: "LOCAL_LLM_URL");

        CoaiVendorsSetting.From([local], GateKind.Plan, ResolvedReferences.Empty).Reason()
            .Should().Contain("'qwen-local'").And.Contain("LOCAL_LLM_URL").And.Contain("nothing resolved it");

        var resolved = new ResolvedReferences(new Dictionary<string, string> { ["LOCAL_LLM_URL"] = "http://127.0.0.1:11434/v1" });
        Rows(CoaiVendorsSetting.From([local], GateKind.Plan, resolved).Ok().Json).Single()["baseUrl"]!.GetValue<string>()
            .Should().Be("http://127.0.0.1:11434/v1", "the VALUE reaches the product; the row keeps the NAME");
    }

    [Fact]
    public void A_retired_reviewer_or_one_that_does_not_host_the_gate_is_refused()
    {
        var retired = Reviewer("grok-medium").Retire(Now).Ok();
        var planOnly = Reviewer("plan-only", gates: [GateKind.Plan]);

        CoaiVendorsSetting.From([retired], GateKind.Plan, ResolvedReferences.Empty).Reason().Should().Contain("retired");
        CoaiVendorsSetting.From([planOnly], GateKind.Code, ResolvedReferences.Empty).Reason()
            .Should().Contain("'plan-only'").And.Contain("not ticked for the code gate");
        CoaiVendorsSetting.From([], GateKind.Code, ResolvedReferences.Empty).Reason().Should().Contain("at least one reviewer");
    }

    [Fact]
    public void An_unknown_vendor_field_is_refused_by_name()
    {
        const string json = """[{"id":"grok","runtime":"api","model":"grok-4.7","baseUrl":"https://api.x.ai/v1","temperature":0.2}]""";

        CoaiVendorRow.Read(json).Reason().Should().Contain("'temperature'").And.Contain("does not know",
            "a field dropped on import is a reviewer that is not the one the operator runs");
    }

    [Theory]
    [InlineData("""{"id":"grok","plan":"false"}""", "'plan'", "boolean")]
    [InlineData("""{"id":"grok","reviewMinutes":"20"}""", "'reviewMinutes'", "integer")]
    [InlineData("""{"id":"grok","reviewMinutes":20.5}""", "'reviewMinutes'", "integer")]
    [InlineData("""{"id":"grok","model":42}""", "'model'", "string")]
    [InlineData("""{"id":"grok","thinking":1}""", "'thinking'", "boolean")]
    [InlineData("""{"id":"grok","price":"cheap"}""", "'price'", "object")]
    [InlineData("""{"id":7}""", "'id'", "string")]
    public void A_field_of_the_wrong_type_is_refused_by_name_rather_than_read_as_its_default(string row, string field, string expected)
    {
        CoaiVendorRow.Read($"[{row}]").Reason().Should().Contain(field).And.Contain(expected,
            "\"plan\":\"false\" read as absent would TICK the plan gate — the opposite of what the operator wrote");
    }

    [Fact]
    public void An_absent_or_null_field_is_the_products_default_and_not_a_type_error()
    {
        var row = CoaiVendorRow.Read("""[{"id":"grok","plan":null,"reviewMinutes":null}]""").Ok().Single();

        row.Plan.Should().BeTrue("null is how the product's own reader spells absence for a flag");
        row.ReviewMinutes.Should().Be(0);
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
        var json = CoaiVendorsSetting.From([Reviewer("grok-medium")], GateKind.Feature, ResolvedReferences.Empty).Ok().Json;

        var read = CoaiVendorRow.Read(json).Ok().Single();

        read.Id.Should().Be("grok-medium");
        read.Feature.Should().BeTrue();
        read.Plan.Should().BeFalse();
        read.ReviewMinutes.Should().Be(20);
    }

    /// <summary>D1: the row spells `thinking` only when a state was chosen — a row with no flag reaches the product with no
    /// field, which the product reads as the vendor's default (E7's first A/A: `false` made it skip grok and glm).</summary>
    [Theory]
    [InlineData(ThinkingSetting.VendorDefault, null)]
    [InlineData(ThinkingSetting.On, true)]
    [InlineData(ThinkingSetting.Off, false)]
    public void The_row_writes_thinking_only_for_a_chosen_state_and_reads_back_the_same_state(ThinkingSetting thinking, bool? written)
    {
        var json = CoaiVendorsSetting.From([Reviewer("grok-medium", thinking: thinking)], GateKind.Feature, ResolvedReferences.Empty).Ok().Json;
        var row = Rows(json).Single();

        (row.ContainsKey("thinking") ? row["thinking"]!.GetValue<bool>() : (bool?)null).Should().Be(written);
        CoaiVendorRow.Read(json).Ok().Single().Thinking.Should().Be(thinking);
    }

    [Fact]
    public void A_panel_row_without_thinking_reads_as_the_vendors_default_as_the_product_reads_it()
    {
        CoaiVendorRow.Read("""[{"id":"grok"}]""").Ok().Single().Thinking.Should().Be(ThinkingSetting.VendorDefault,
            "the product reads an absent field as the vendor's default, not as on");
    }

    [Fact]
    public void Malformed_vendor_json_is_refused_rather_than_read_as_nobody()
    {
        CoaiVendorRow.Read("not json").Reason().Should().Contain("not JSON");
        CoaiVendorRow.Read("{}").Reason().Should().Contain("JSON ARRAY");
        CoaiVendorRow.Read("""[{"runtime":"api"}]""").Reason().Should().Contain("without an id");
    }

    private static IReadOnlySet<string> ProductRuntimeNames()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "coai-runtime-names.json")));

        return new HashSet<string>(
            fixture.RootElement.GetProperty("runtimeNames").EnumerateArray().Select(e => e.GetString()!),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<JsonObject> Rows(string json) =>
        [.. (JsonNode.Parse(json) as JsonArray)!.OfType<JsonObject>()];

    private static GateReviewer Reviewer(
        string id, string endpoint = "https://api.x.ai/v1", IReadOnlyList<GateKind>? gates = null,
        ReviewerRuntime runtime = ReviewerRuntime.Api, string effort = "medium", ThinkingSetting thinking = ThinkingSetting.On) =>
        GateReviewer.Create(
            GateReviewerId.Parse(id).Ok(),
            GateReviewerTests.Definition(endpoint: endpoint, gates: gates, runtime: runtime, effort: effort, thinking: thinking).Ok(),
            Now);
}
