using System.Net;
using Bench.Cli;
using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Bench.Tests.Ui.ProbeUiFixtures;

namespace Bench.Tests.Ui;

/// <summary>The Probes tab (S4 of the question-consultant probes plan): the capability probes' runs, read-only (D10). The report
/// decides every word (<see cref="Bench.Application.Probes.ProbeReport"/>); these pin that the page SHOWS it — every state, every
/// verdict word with <i>not captured</i> never rendered as a zero, the generation shown over a lower one, the CLI build, the
/// control's void, the pairs not measured, and a re-measurement offered as the command the CLI accepts rather than a button.</summary>
public sealed class ProbesPageTests : BunitContext
{
    private static readonly Guid Older = Guid.Parse("01a0f800-0000-7000-8000-000000000001");
    private static readonly Guid Newer = Guid.Parse("01a0f800-0000-7000-8000-000000000002");

    [Fact]
    public void The_Probes_tab_sits_between_Gate_and_Math()
    {
        var markup = Render<ProbesBenchmark>(Api(Done(Newer))).Markup;

        markup.Should().Contain(">Probes<").And.Contain("/benchmarking/probes");
        markup.IndexOf(">Gate<", StringComparison.Ordinal).Should().BeLessThan(markup.IndexOf(">Probes<", StringComparison.Ordinal));
        markup.IndexOf(">Probes<", StringComparison.Ordinal).Should().BeLessThan(markup.IndexOf(">Math<", StringComparison.Ordinal));
    }

    [Fact]
    public void The_run_picker_offers_the_runs_newest_first_and_opens_the_newest()
    {
        var api = new ScriptedBenchApi()
            .Answers(RunsRoute, new[] { Summary(Older, Noon, open: false), Summary(Newer, Noon.AddHours(2), open: false) })
            .Answers(RunRoute(Newer), Done(Newer));

        var page = Render<ProbesBenchmark>(api);

        page.FindAll("#probe-run option").Select(o => o.GetAttribute("value")).Should().Equal([Newer.ToString(), Older.ToString()]);
        api.Calls.Should().Equal($"{RunsRoute}?limit=50", RunRoute(Newer));
    }

    [Fact]
    public void A_run_named_in_the_address_is_the_one_opened_and_choosing_another_reads_that_one()
    {
        var api = new ScriptedBenchApi()
            .Answers(RunsRoute, new[] { Summary(Newer, Noon.AddHours(2), open: false), Summary(Older, Noon, open: false) })
            .Answers(RunRoute(Older), Done(Older)).Answers(RunRoute(Newer), Done(Newer));
        Register(api);
        Services.GetRequiredService<BunitNavigationManager>().NavigateTo($"/benchmarking/probes?run={Older}");

        var page = Render<ProbesBenchmark>();
        page.Find("#probe-run").Change(Newer.ToString());

        api.Calls.Should().Equal($"{RunsRoute}?limit=50", RunRoute(Older), RunRoute(Newer));
        page.Find("[data-test=probe-run-id]").TextContent.Should().Contain(Newer.ToString());
    }

    [Fact]
    public void The_matrix_renders_every_state_and_every_verdict_word_and_an_uncaptured_fact_or_exit_is_never_a_zero()
    {
        var report = Report(Newer, [
            Cell("read-inside", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0)),
            Cell("read-inside", "claude-a", 2, ProbeWords.Claimed),
            Cell("read-inside", "claude-a", 3, ProbeWords.Pending),
            Cell("read-outside-bare", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.No), new ProbeExitDto(true, 0)),
            Cell("read-outside-bare", "claude-a", 2, ProbeWords.Abandoned, reason: "abandoned"),
            Cell("read-outside-bare", "claude-a", 3, ProbeWords.Settled, "timed-out"),
            Cell("web-search", "codex-b", 1, ProbeWords.Settled, "launch-refused", exit: new ProbeExitDto(true, 2)),
        ]);

        var page = Render<ProbesBenchmark>(Api(report));

        State(page, "read-inside", "claude-a", 1).Should().Be(ProbeWords.Settled);
        State(page, "read-inside", "claude-a", 2).Should().Be(ProbeWords.Claimed);
        State(page, "read-inside", "claude-a", 3).Should().Be(ProbeWords.Pending);
        State(page, "read-outside-bare", "claude-a", 2).Should().Be(ProbeWords.Abandoned);
        Fact(page, "read-inside", "claude-a", 1, "canaryRead").Should().Be("yes");
        Fact(page, "read-outside-bare", "claude-a", 1, "canaryRead").Should().Be("no");
        Fact(page, "read-outside-bare", "claude-a", 3, "canaryRead").Should().Be("not captured");
        var timedOut = Repeat(page, "read-outside-bare", "claude-a", 3).TextContent;
        timedOut.Should().Contain("timed out").And.Contain("exit not captured").And.NotContain("exit 0", "an exit code nobody captured is not a zero");
        Repeat(page, "web-search", "codex-b", 1).TextContent.Should().Contain("launch refused").And.Contain("exit 2");
        Repeat(page, "read-outside-bare", "claude-a", 2).TextContent.Should().Contain("abandoned");
        page.FindAll("[data-fact]").Select(f => f.TextContent.Trim()).Should().OnlyContain(w => w == "yes" || w == "no" || w == "not captured");
    }

    [Fact]
    public void A_higher_generation_is_shown_over_a_lower_with_its_badge_and_a_newer_one_still_measuring_is_named()
    {
        var report = Report(Newer, [
            Cell("read-inside", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0), generation: 2),
            Cell("read-inside", "claude-a", 2, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0), generation: 2,
                latestGeneration: 3, latestState: ProbeWords.Pending),
            Cell("read-inside", "claude-a", 3, ProbeWords.Settled, "answered", Canary(ProbeWords.No), new ProbeExitDto(true, 0)),
        ]);

        var page = Render<ProbesBenchmark>(Api(report));

        Repeat(page, "read-inside", "claude-a", 1).QuerySelector("[data-test=probe-generation]")!.TextContent.Trim().Should().Be("g2");
        Repeat(page, "read-inside", "claude-a", 2).QuerySelector("[data-test=probe-newer]")!.TextContent.Should().Contain("g3").And.Contain("pending");
        Repeat(page, "read-inside", "claude-a", 3).QuerySelector("[data-test=probe-generation]").Should().BeNull("generation 1 is the plain case and carries no badge");
        Fact(page, "read-inside", "claude-a", 1, "canaryRead").Should().Be("yes", "the shown generation's verdict, not a lower one's");
    }

    [Fact]
    public void Each_cell_shows_the_cli_build_the_control_s_void_and_a_rerun_command_the_cli_accepts()
    {
        var cellId = Guid.Parse("01a0f800-0000-7000-8000-0000000000c1");
        var report = Report(Newer, [
            Cell("read-outside-bare", "claude-a", 1, ProbeWords.Settled, "answered", NothingCaptured, new ProbeExitDto(true, 0), voided: true,
                pin: "2.1.286 (Claude Code)", id: cellId),
        ]);

        var page = Render<ProbesBenchmark>(Api(report));

        var repeat = Repeat(page, "read-outside-bare", "claude-a", 1);
        repeat.QuerySelector("[data-test=probe-pin]")!.TextContent.Should().Contain("2.1.286 (Claude Code)");
        repeat.QuerySelector("[data-test=probe-voided]")!.TextContent.Should().Contain("read-inside");
        var shown = repeat.QuerySelector("[data-test=probe-rerun]")!.TextContent.Trim();
        var words = shown.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words[0].Should().Be("bench");
        var parsed = CommandLine.Parse(words[1..]);
        (parsed.Verb, parsed.Operand(0), parsed.Value("cell")).Should().Be(("probes", "rerun", cellId.ToString("D")),
            "the command shown is S3's `bench probes rerun --cell <id>`, exactly as the CLI parses it");
        page.FindAll("button").Should().OnlyContain(b => b.GetAttribute("type") == "button", "D10: nothing on this page submits or writes");
    }

    [Fact]
    public void The_pairs_not_measured_are_listed_and_a_pruned_run_says_its_verdicts_are_no_longer_auditable()
    {
        var report = Report(Newer, [Cell("read-inside", "claude-a", 1, ProbeWords.Settled, "answered", Canary(ProbeWords.Yes), new ProbeExitDto(true, 0))],
            dropped: [new ProbeDroppedPairDto("read-denied", "agy-c", "no-web-off-flag"), new ProbeDroppedPairDto("api-reachable", "claude-a", "api-probe-on-cli")],
            pruned: true);

        var page = Render<ProbesBenchmark>(Api(report));

        page.FindAll("[data-test=probe-dropped] li").Select(li => li.TextContent).Should().SatisfyRespectively(
            first => first.Should().Contain("read-denied").And.Contain("agy-c").And.Contain("no-web-off-flag"),
            second => second.Should().Contain("api-reachable").And.Contain("claude-a").And.Contain("api-probe-on-cli"));
        page.Find("[data-test=probe-pruned]").TextContent.Should().Contain("no longer auditable");
        Cells(page, "read-denied", "agy-c").Should().BeEmpty("a dropped pair is listed, never rendered as an empty measurement");
    }

    [Fact]
    public void The_matrix_is_a_semantic_table_and_the_progress_line_is_announced()
    {
        var page = Render<ProbesBenchmark>(Api(InFlight(Newer)));

        page.FindAll("[data-test=probe-matrix] thead th").Should().OnlyContain(th => th.GetAttribute("scope") == "col");
        page.FindAll("[data-test=probe-matrix] thead th").Select(th => th.TextContent.Trim()).Should().Equal(["Probe", "claude-a", "codex-b"]);
        page.FindAll("[data-test=probe-matrix] tbody th").Should().OnlyContain(th => th.GetAttribute("scope") == "row");
        var progress = page.Find("[data-test=probe-progress]");
        progress.GetAttribute("aria-live").Should().Be("polite");
        progress.TextContent.Should().Contain("1 claimed").And.Contain("1 settled");
    }

    [Fact]
    public void An_unreachable_api_is_a_warning_and_never_an_empty_matrix()
    {
        var markup = Render<ProbesBenchmark>(new ScriptedBenchApi()).Markup;

        markup.Should().Contain("alert-warning").And.Contain(RunsRoute.TrimStart('/'));
        markup.Should().NotContain("data-test=\"probe-matrix\"");
    }

    [Fact]
    public void No_probe_run_says_how_to_measure_one_rather_than_rendering_an_empty_matrix()
    {
        var markup = Render<ProbesBenchmark>(new ScriptedBenchApi().Answers(RunsRoute, Array.Empty<ProbeRunSummaryDto>())).Markup;

        markup.Should().Contain("bench probes run").And.NotContain("data-test=\"probe-matrix\"");
    }

    private static string State(IRenderedComponent<ProbesBenchmark> page, string probe, string subject, int repeat) =>
        Repeat(page, probe, subject, repeat).GetAttribute("data-state")!;

    private static string Fact(IRenderedComponent<ProbesBenchmark> page, string probe, string subject, int repeat, string fact) =>
        Repeat(page, probe, subject, repeat).QuerySelector($"[data-fact={fact}]")!.TextContent.Trim();

    private static AngleSharp.Dom.IElement Repeat(IRenderedComponent<ProbesBenchmark> page, string probe, string subject, int repeat) =>
        page.Find($"tr[data-probe={probe}] td[data-subject={subject}] [data-repeat=\"{repeat}\"]");

    private static IReadOnlyList<AngleSharp.Dom.IElement> Cells(IRenderedComponent<ProbesBenchmark> page, string probe, string subject) =>
        [.. page.FindAll($"tr[data-probe={probe}] td[data-subject={subject}] [data-repeat]")];

    private static ScriptedBenchApi Api(ProbeRunReportDto report) =>
        new ScriptedBenchApi().Answers(RunsRoute, new[] { Summary(report.RunId, Noon, report.Progress.Open) }).Answers(RunRoute(report.RunId), report);

    private IRenderedComponent<T> Render<T>(ScriptedBenchApi api)
        where T : IComponent
    {
        Register(api);
        return Render<T>();
    }

    private void Register(ScriptedBenchApi api)
    {
        Services.AddSingleton(new BenchConsoleApi(api.Client()));
        Services.AddSingleton<TimeProvider>(new ManualClock());
    }
}
