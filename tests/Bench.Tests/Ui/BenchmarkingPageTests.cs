using System.Net;
using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>The pages, rendered against a real Blazor renderer.
/// <para>
/// <see cref="Bench.Ui.ArmGrouping"/> already pins the decisions; what these add is that the page DISPLAYS
/// them. The two are not the same claim — a table that compiles is not a table that shows anything, and the
/// refusal in particular is worthless if it renders below the numbers or not at all.
/// </para>
/// <para>
/// Components are constructed by DI, exactly as in the app: both pages take their dependency through a
/// primary constructor rather than <c>[Inject]</c>, per the family's Razor convention.
/// </para></summary>
public sealed class BenchmarkingPageTests : BunitContext
{
    [Fact]
    public void Two_arms_appear_side_by_side_with_the_refusal_ABOVE_them()
    {
        var page = Render<Benchmarking>(
            Runs(Summary("wsl-to-wsl/migraphx/R9700"), Summary("windows-to-windows/directml/R9700")));

        var markup = page.Markup;

        markup.Should().Contain("wsl-to-wsl/migraphx/R9700").And.Contain("windows-to-windows/directml/R9700");
        markup.Should().Contain("Not a ranking");

        // Position is the assertion. A reader who meets two averages before meeting the caveat has already
        // formed the conclusion the caveat exists to prevent, whatever the text further down says.
        markup.IndexOf("Not a ranking", StringComparison.Ordinal).Should()
            .BeLessThan(markup.IndexOf("migraphx", StringComparison.Ordinal));
    }

    [Fact]
    public void An_undeclared_backend_is_shown_apart_and_labelled_as_saying_nothing()
    {
        var page = Render<Benchmarking>(
            Runs(Summary("wsl-to-wsl/migraphx/R9700"), Summary(ComputeArmLabel.NotDeclared)));

        // The state every run in this database is in today. It must read as an absence rather than as an arm
        // that happens to be called "not declared".
        page.Markup.Should().Contain("not declared")
            .And.Contain("say nothing about a sidecar");
    }

    [Fact]
    public void An_unreachable_api_is_a_warning_and_never_an_empty_table()
    {
        // Nothing scripted — the API is simply not there, which is exactly the case a page must not render as
        // "no runs".
        var page = Render<Benchmarking>(new ScriptedBenchApi());

        page.Markup.Should().Contain("404").And.Contain("alert-warning");
        page.Markup.Should().NotContain("run(s)", "there is no run count to report when nothing was read");
    }

    [Fact]
    public void An_empty_database_says_nothing_has_been_measured_rather_than_that_it_could_not_be_read()
    {
        var page = Render<Benchmarking>(Runs());

        page.Markup.Should().Contain("no run has been measured yet");
        page.Markup.Should().NotContain("alert-warning", "the API answered perfectly well");
    }

    [Fact]
    public void A_run_links_to_its_own_report()
    {
        var summary = Summary("a/b/c");

        Render<Benchmarking>(Runs(summary)).Find("tbody a").GetAttribute("href")
            .Should().Be($"/benchmarking/runs/{summary.RunId}");
    }

    [Fact]
    public void The_report_page_reads_nothing_until_a_metric_is_chosen()
    {
        var api = new ScriptedBenchApi();

        var page = Render<BenchmarkRun>(api, parameters => parameters.Add(p => p.Id, Guid.CreateVersion7()));

        // The no-default rule, on screen. A page that picked one would report on an axis nobody chose, and
        // every number below it would be about that choice.
        page.Markup.Should().Contain("there is no default");
        api.Calls.Should().BeEmpty("nothing may be fetched before the reader has named a metric");
    }

    [Fact]
    public void A_report_names_an_unhealthy_adapter_before_any_number_it_produced()
    {
        var page = Render<BenchmarkRun>(
            Report(unhealthy: ["Radeon RX 9700 — error code 31"]),
            parameters => parameters.Add(p => p.Id, Guid.Empty),
            metric: WellKnownMetrics.AnchorRecall);

        var markup = page.Markup;

        markup.Should().Contain("error code 31").And.Contain("may have been produced on the CPU");
        markup.IndexOf("error code 31", StringComparison.Ordinal).Should()
            .BeLessThan(markup.IndexOf("Scored", StringComparison.Ordinal),
                "a card that fell off the bus changes how every number below it reads");
    }

    [Fact]
    public void A_machine_nobody_recorded_says_so_rather_than_rendering_blank_fields()
    {
        var page = Render<BenchmarkRun>(
            Report(),
            parameters => parameters.Add(p => p.Id, Guid.Empty),
            metric: WellKnownMetrics.AnchorRecall);

        page.Markup.Should().Contain("not recorded")
            .And.Contain("nothing here can say which host produced these numbers");
    }

    [Fact]
    public void An_unproven_arm_is_rendered_as_a_warning_and_never_as_a_smaller_win()
    {
        var page = Render<BenchmarkRun>(
            Report(proof: "Unproven"),
            parameters => parameters.Add(p => p.Id, Guid.Empty),
            metric: WellKnownMetrics.AnchorRecall);

        // Every false winner reads as a modest success until it is named. The badge must not be a paler
        // green.
        var badge = page.Find("tbody .badge");
        badge.TextContent.Trim().Should().Be("Unproven");
        badge.ClassList.Should().Contain("text-bg-warning");
    }

    [Fact]
    public void A_half_nobody_measured_is_not_rendered_as_zero()
    {
        var page = Render<BenchmarkRun>(
            Report(heldOutMeasured: false),
            parameters => parameters.Add(p => p.Id, Guid.Empty),
            metric: WellKnownMetrics.AnchorRecall);

        // An absence and a failure are opposite readings of the same blank cell.
        page.Markup.Should().Contain("not measured");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private IRenderedComponent<T> Render<T>(
        ScriptedBenchApi api,
        Action<ComponentParameterCollectionBuilder<T>>? parameters = null,
        string metric = "")
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        Services.AddSingleton(new BenchConsoleApi(api.Client()));

        var rendered = parameters is null ? Render<T>() : Render(parameters);

        if (metric.Length > 0)
        {
            // Choosing is what triggers the read, so the test chooses the way a reader does rather than
            // reaching past the page into its state.
            rendered.Find("#metric").Change(metric);
        }

        return rendered;
    }

    private static ScriptedBenchApi Runs(params RunSummaryDto[] runs) =>
        new ScriptedBenchApi().Answers("/api/bench/runs", runs);

    private static ScriptedBenchApi Report(
        string proof = "NotAWinner",
        bool heldOutMeasured = true,
        IReadOnlyList<string>? unhealthy = null) =>
        new ScriptedBenchApi().Answers(
            $"/api/bench/runs/{Guid.Empty}/report",
            new RunReportDto(
                Guid.Empty,
                "arms",
                "repo@commit",
                "s@v3#abcdef012345",
                "Qln|e|1.0|fp|",
                WellKnownMetrics.AnchorRecall,
                4,
                2,
                2,
                2,
                [
                    new DimensionReportDto(
                        "Variant",
                        [
                            new ArmReadingDto(
                                "cand",
                                0.75,
                                4,
                                new HalfReadingDto(true, 1.0, 2),
                                new HalfReadingDto(heldOutMeasured, heldOutMeasured ? 0.5 : 0, heldOutMeasured ? 2 : 0),
                                proof,
                                0.25),
                        ],
                        "-",
                        string.Empty),
                ],
                new DiscriminationDto(1, 0, 0, 0, 0, "1 discriminating"),
                [],
                new MachineDto(false, string.Empty, string.Empty, string.Empty, string.Empty, 0, [],
                    unhealthy ?? [], string.Empty),
                "3 of 3 leg(s) sampled"));

    private static RunSummaryDto Summary(string arm) =>
        new(Guid.CreateVersion7(), "run", "repo@commit", "s@v3#abcdef012345", $"Qln|e|1.0|fp|{arm}", arm,
            "Planned", DateTimeOffset.UnixEpoch);
}
