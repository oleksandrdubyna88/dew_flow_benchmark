using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>The arms view, and the tab structure around it.
/// <para>
/// <see cref="Bench.Application.ArmComparison"/> pins what may be CONCLUDED; these pin that the page shows it.
/// The scope in particular: it is the pair that must be identical before two numbers may be read against each
/// other, and a page that rendered one flat table would be the project's most expensive recorded mistake with
/// a tidier presentation.
/// </para></summary>
public sealed class SidecarArmsPageTests : BunitContext
{
    private const string Wsl = "wsl-to-wsl/migraphx/R9700";
    private const string Windows = "windows-to-windows/directml/R9700";

    [Fact]
    public void The_tabs_are_the_KINDS_of_test_and_all_three_are_declared()
    {
        var page = Render<Benchmarking>(new ScriptedBenchApi().Answers("/api/bench/runs", Array.Empty<RunSummaryDto>()));

        // Declared before they are built, deliberately: the structure is the claim about what this section is
        // for, and a tab appearing later reads as a feature nobody planned.
        page.Markup.Should().Contain(">RAG<").And.Contain(">MCP<").And.Contain(">Sidecar<");
        page.Markup.Should().Contain("/benchmarking/rag").And.Contain("/benchmarking/mcp");
    }

    [Fact]
    public void Runs_and_arms_are_two_views_INSIDE_the_sidecar_kind()
    {
        var page = Render<Benchmarking>(new ScriptedBenchApi().Answers("/api/bench/runs", Array.Empty<RunSummaryDto>()));

        page.Markup.Should().Contain("/benchmarking/sidecar/arms");
    }

    [Fact]
    public void A_declared_but_unbuilt_kind_says_what_will_live_there_and_which_plan_owns_it()
    {
        var page = Render<RagBenchmark>(new ScriptedBenchApi());

        // "Coming soon" tells a reader nothing they can act on, and an empty tab reads as a page that failed
        // to load rather than as work not done.
        page.Markup.Should().Contain("not built yet").And.Contain("PLAN_variant_matrix.md");
    }

    [Fact]
    public void Each_scope_is_its_own_table_and_nothing_crosses_the_boundary()
    {
        var page = Arms(
            Scope("polly@v1#aaaa", Arm(Wsl, 0.8), Arm(Windows, 0.4)),
            Scope("aspnet@v1#bbbb", Arm(Wsl, 0.6)));

        page.FindAll("table").Should().HaveCount(2, "two scopes are two comparisons, never one table");
        page.Markup.Should().Contain("polly@v1#aaaa").And.Contain("aspnet@v1#bbbb");
    }

    [Fact]
    public void The_scope_is_a_header_rather_than_a_footnote()
    {
        var page = Arms(Scope("polly@v1#aaaa", Arm(Wsl, 0.8), Arm(Windows, 0.4)));

        // Structural rather than positional: the arm names also appear ABOVE, in the baseline control, so a
        // string-index comparison would measure the wrong occurrence. What matters is that inside the card,
        // the scope is the HEADER over the table it licenses.
        var card = page.Find(".card");

        card.QuerySelector(".card-header")!.TextContent.Should().Contain("polly@v1#aaaa");
        card.QuerySelector(".card-header")!.CompareDocumentPosition(card.QuerySelector("tbody")!)
            .Should().HaveFlag(AngleSharp.Dom.DocumentPositions.Following,
                "the pair that licenses the comparison must be read before the numbers it licenses");
    }

    [Fact]
    public void An_arm_reports_how_many_RUNS_it_rests_on()
    {
        var page = Arms(Scope("polly@v1#aaaa", Arm(Wsl, 0.8, legs: 8, runs: 2), Arm(Windows, 0.4)));

        page.Find("tbody tr").TextContent.Should().Contain("8").And.Contain("2");
    }

    [Fact]
    public void The_top_level_refusal_is_shown_apart_from_a_scopes_own()
    {
        var page = Arms(refusal: "no run carries a compute backend, so there is no arm to compare");

        // A wiring problem and a thin population are different news; merging them sends the reader to the
        // wrong place.
        page.Markup.Should().Contain("Nothing to compare").And.Contain("no arm to compare");
    }

    [Fact]
    public void Excluded_runs_are_named_so_the_population_is_knowable()
    {
        var page = Arms(excluded: ["6 run(s) declared no compute backend and cannot be attributed to an arm"]);

        page.Markup.Should().Contain("Left out").And.Contain("6 run(s) declared no compute backend");
    }

    [Fact]
    public void The_baseline_is_marked_and_an_unproven_arm_is_a_warning()
    {
        var page = Arms(Scope(
            "polly@v1#aaaa",
            Arm(Windows, 0.4) with { Arm = Windows },
            Arm(Wsl, 0.8, proof: "Unproven"),
            baseline: Windows));

        page.Markup.Should().Contain("baseline");
        page.Find("tbody .badge.text-bg-warning").TextContent.Trim().Should().Be("Unproven");
    }

    [Fact]
    public void Nothing_is_read_until_a_metric_is_chosen()
    {
        var api = new ScriptedBenchApi();

        var page = Render<SidecarArms>(api);

        page.Markup.Should().Contain("there is no default");
        api.Calls.Should().BeEmpty("a default metric would make every number below it about a choice nobody made");
    }

    [Fact]
    public void The_baseline_control_offers_only_arms_the_comparison_actually_found()
    {
        var page = Arms(Scope("polly@v1#aaaa", Arm(Wsl, 0.8), Arm(Windows, 0.4)));

        var options = page.FindAll("#baseline option").Select(option => option.TextContent.Trim()).ToList();

        // Offering an arm nothing was measured on leaves the reader wondering why choosing it changed
        // nothing.
        options.Should().HaveCount(3, "the two arms plus the explicit none");
        options.Should().Contain(Wsl).And.Contain(Windows);
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private IRenderedComponent<T> Render<T>(ScriptedBenchApi api)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        Services.AddSingleton(new BenchConsoleApi(api.Client()));
        return Render<T>();
    }

    private IRenderedComponent<SidecarArms> Arms(
        params ScopedArmsDto[] scopes) =>
        Arms(scopes, [], string.Empty);

    private IRenderedComponent<SidecarArms> Arms(
        string refusal = "", IReadOnlyList<string>? excluded = null) =>
        Arms([], excluded ?? [], refusal);

    private IRenderedComponent<SidecarArms> Arms(
        IReadOnlyList<ScopedArmsDto> scopes, IReadOnlyList<string> excluded, string refusal)
    {
        var api = new ScriptedBenchApi().Answers(
            "/api/bench/arms",
            new ArmComparisonDto(WellKnownMetrics.AnchorRecall, scopes, excluded, refusal));

        var page = Render<SidecarArms>(api);
        page.Find("#metric").Change(WellKnownMetrics.AnchorRecall);

        return page;
    }

    private static ScopedArmsDto Scope(string suite, params ArmAverageDto[] arms) =>
        new("https://example.invalid/x.git@" + new string('c', 40), suite, arms, string.Empty, string.Empty);

    private static ScopedArmsDto Scope(string suite, ArmAverageDto first, ArmAverageDto second, string baseline) =>
        new("https://example.invalid/x.git@" + new string('c', 40), suite, [first, second], baseline, string.Empty);

    private static ArmAverageDto Arm(
        string arm, double average, int legs = 4, int runs = 1, string proof = "NotAWinner") =>
        new(arm, average, legs, runs, new HalfReadingDto(true, average, legs / 2),
            new HalfReadingDto(true, average, legs / 2), proof, 0);
}
