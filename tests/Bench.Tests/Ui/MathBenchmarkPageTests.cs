using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bench.Tests.Ui;

/// <summary>The MATH tab — the investigate view over real agent sessions.
/// <para>
/// The only tab whose population is not runs. What it must get right is the same thing every page here
/// must: an unread store and an empty one are opposite readings of the same blank page, and the panel a
/// reader sees decides whether they go and look at the daemon or at the collector.
/// </para></summary>
public sealed class MathBenchmarkPageTests : BunitContext
{
    [Fact]
    public void The_Math_tab_is_declared_beside_the_other_kinds()
    {
        var page = Render<MathBenchmark>(Sessions());

        page.Markup.Should().Contain(">Math<").And.Contain("/benchmarking/math");
        page.Markup.Should().Contain(">Code<", "the math kind is ADDED — every other tab keeps its place");
        page.Markup.Should().Contain(">Sidecar<");
    }

    [Fact]
    public void A_recorded_session_shows_its_task_its_phase_and_its_call_split()
    {
        var page = Render<MathBenchmark>(Sessions(
            Summary("find the ingest bug", phase: "Execution", research: 7, execution: 3, verification: 2)));

        page.Markup.Should().Contain("find the ingest bug");
        page.Markup.Should().Contain("Execution");
        page.Markup.Should().Contain("7·3·2");
    }

    [Fact]
    public void A_session_links_to_its_own_page()
    {
        var summary = Summary("find the ingest bug");

        Render<MathBenchmark>(Sessions(summary)).Find("tbody a").GetAttribute("href")
            .Should().Be($"/benchmarking/sessions/{summary.SessionId}");
    }

    [Fact]
    public void An_unlinked_plan_says_so_rather_than_rendering_an_empty_cell()
    {
        var page = Render<MathBenchmark>(Sessions(Summary("unnamed work") with
        {
            Task = new SessionTaskDto("t-1", "unnamed work", string.Empty),
        }));

        page.Markup.Should().Contain("not linked");
    }

    [Fact]
    public void A_session_with_no_disagreement_renders_no_alarm_at_all()
    {
        Render<MathBenchmark>(Sessions(Summary("clean run"))).Markup
            .Should().NotContain("text-bg-danger",
                "a column of zeroes reads as data and pulls the eye away from the row that has one");
    }

    [Fact]
    public void A_taxonomy_disagreement_is_shown_as_ours()
    {
        Render<MathBenchmark>(Sessions(Summary("suspicious run") with { Disagreements = 2 })).Markup
            .Should().Contain("text-bg-danger");
    }

    [Fact]
    public void No_sessions_says_how_to_start_recording_rather_than_rendering_an_empty_table()
    {
        var page = Render<MathBenchmark>(Sessions());

        page.Markup.Should().Contain("no agent session has been recorded yet");
        page.Markup.Should().Contain("bench sessions install");
    }

    [Fact]
    public void An_unreachable_api_is_a_warning_and_never_an_empty_population()
    {
        var page = Render<MathBenchmark>(new ScriptedBenchApi());

        page.Markup.Should().Contain("alert-warning");
        page.Markup.Should().NotContain("no agent session has been recorded yet",
            "one panel sends the reader to the daemon and the other to the collector");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private IRenderedComponent<T> Render<T>(ScriptedBenchApi api)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        Services.AddSingleton(new BenchConsoleApi(api.Client()));
        return Render<T>();
    }

    private static ScriptedBenchApi Sessions(params SessionSummaryDto[] sessions) =>
        new ScriptedBenchApi().Answers("/api/bench/sessions", sessions);

    private static SessionSummaryDto Summary(
        string name,
        string phase = "Research",
        int research = 1,
        int execution = 0,
        int verification = 0) =>
        new(
            Guid.CreateVersion7(),
            "Hook",
            "claude-session-key",
            "claude-code",
            new SessionTaskDto("t-1", name, "todo/PLAN_corpus_axis_integrity.md"),
            "d:/rsd/dew_flow_benchmark",
            "main",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(5),
            phase,
            research + execution + verification,
            research,
            execution,
            verification,
            Unfinished: 0,
            CompileFailures: 0,
            Disagreements: 0);
}
