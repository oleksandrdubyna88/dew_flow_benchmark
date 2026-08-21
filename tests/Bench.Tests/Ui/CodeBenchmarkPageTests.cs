using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>The CODE kind's own tab (`PLAN_code_lane.md` §7 step 8 — "the code group's pages").
/// <para>
/// Fix runs used to land on the Sidecar tab, whose list is the sidecar question's population — the
/// operator caught an investigate run rendering under a hardware comparison it had nothing to do with.
/// The kinds are the tabs, so the code kind gets its OWN: its population is the fix runs, and the
/// sidecar tab is left exactly as it was.
/// </para></summary>
public sealed class CodeBenchmarkPageTests : BunitContext
{
    [Fact]
    public void The_Code_tab_is_declared_beside_the_other_kinds()
    {
        var page = Render<Benchmarking>(
            new ScriptedBenchApi().Answers("/api/bench/runs", Array.Empty<RunSummaryDto>()));

        page.Markup.Should().Contain(">Code<").And.Contain("/benchmarking/code");
        page.Markup.Should().Contain(">Sidecar<", "the code kind is ADDED — the sidecar kind keeps its tab untouched");
    }

    [Fact]
    public void The_code_page_lists_only_fix_runs_never_the_other_kinds()
    {
        var page = Render<CodeBenchmark>(Runs(
            Summary("investigate-sonnet-1200", "Fix"),
            Summary("agentic-smoke-1", "Reading")));

        page.Markup.Should().Contain("investigate-sonnet-1200");
        page.Markup.Should().NotContain("agentic-smoke-1",
            "a reading run in the code population would repeat the mislabelling this tab exists to end");
    }

    [Fact]
    public void A_fix_run_links_to_its_own_report()
    {
        var summary = Summary("investigate-sonnet-1200", "Fix");

        Render<CodeBenchmark>(Runs(summary)).Find("tbody a").GetAttribute("href")
            .Should().Be($"/benchmarking/runs/{summary.RunId}");
    }

    [Fact]
    public void No_fix_runs_says_so_rather_than_rendering_an_empty_table()
    {
        var page = Render<CodeBenchmark>(Runs(Summary("agentic-smoke-1", "Reading")));

        page.Markup.Should().Contain("no fix run has been measured yet");
    }

    [Fact]
    public void An_unreachable_api_is_a_warning_and_never_an_empty_population()
    {
        var page = Render<CodeBenchmark>(new ScriptedBenchApi());

        page.Markup.Should().Contain("alert-warning");
        page.Markup.Should().NotContain("no fix run has been measured yet",
            "an unread store and an empty one are opposite readings of the same blank page");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private IRenderedComponent<T> Render<T>(ScriptedBenchApi api)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        Services.AddSingleton(new BenchConsoleApi(api.Client()));
        return Render<T>();
    }

    private static ScriptedBenchApi Runs(params RunSummaryDto[] runs) =>
        new ScriptedBenchApi().Answers("/api/bench/runs", runs);

    private static RunSummaryDto Summary(string label, string kind) =>
        new(Guid.CreateVersion7(), label, "repo@commit", "s@v3#abcdef012345", "Qln|e|1.0|fp|a/b/c", "a/b/c",
            "Completed", kind, DateTimeOffset.UnixEpoch);
}
