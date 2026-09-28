using Bench.Contracts;
using Bench.Ui.Pages;
using Bench.Ui.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Bench.Tests.Ui.GateUiFixtures;

namespace Bench.Tests.Ui;

/// <summary>The Gate tab (E6, S6.3): the coai reviewer models per gate. <see cref="Bench.Domain.Gate.GateReport"/> decides
/// what every figure is over; these pin that the page SHOWS it — only the scopes the runs echoed, only the rubrics the
/// verdicts carry, one rubric per table, and every refusal in words, never a zero.</summary>
public sealed class GatePagesTests : BunitContext
{
    private const string Models = "/api/bench/gate/feature/models";
    private const string Runs = "/api/bench/gate/feature/runs";
    private const string Scopes = "/api/bench/gate/scopes";

    [Fact]
    public void The_Gate_tab_sits_between_Code_and_Math()
    {
        var markup = Render<Benchmarking>(new ScriptedBenchApi().Answers("/api/bench/runs", Array.Empty<RunSummaryDto>())).Markup;

        markup.Should().Contain(">Gate<").And.Contain("/benchmarking/gate");
        markup.IndexOf(">Code<", StringComparison.Ordinal).Should().BeLessThan(markup.IndexOf(">Gate<", StringComparison.Ordinal));
        markup.IndexOf(">Gate<", StringComparison.Ordinal).Should().BeLessThan(markup.IndexOf(">Math<", StringComparison.Ordinal));
    }

    [Fact]
    public void The_scope_control_offers_only_the_scopes_the_runs_echoed()
    {
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { Scope("aaaaaaaaaaa1", "0.39.0+abc", true, Strict), Scope("aaaaaaaaaaa2", "0.39.1+def", true, Strict) });

        var page = Render<GateFeature>(api);

        page.FindAll("#gate-scope option").Select(o => o.GetAttribute("value")).Where(v => v!.Length > 0)
            .Should().Equal(["aaaaaaaaaaa1", "aaaaaaaaaaa2"], "a scope nobody measured would render an empty table that reads as a broken run");
        api.Calls.Should().Contain("/api/bench/gate/scopes?gate=feature");
        api.Calls.Should().NotContain(c => c.StartsWith(Models, StringComparison.Ordinal), "two scopes are a choice, and nothing is read before it is made");
    }

    [Fact]
    public void The_rubric_control_offers_only_the_rubrics_the_verdicts_carry_and_the_table_re_reads_when_it_changes()
    {
        var scope = Scope("aaaaaaaaaaa1", rubrics: [Strict, Lenient]);
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { scope })
            .Answers(Models, Table(scope, Strict, [Row("grok")])).Answers(Runs, Array.Empty<GateRunSummaryDto>());
        var page = Render<GateFeature>(api);

        page.FindAll("#gate-rubric option").Select(o => o.GetAttribute("value")).Where(v => v!.Length > 0)
            .Should().Equal([Strict.Stamp, Lenient.Stamp]);
        api.Calls.Should().NotContain(c => c.StartsWith(Models, StringComparison.Ordinal), "two rubrics are a choice — there is no default");

        page.Find("#gate-rubric").Change(Strict.Stamp);
        page.Find("#gate-rubric").Change(Lenient.Stamp);

        api.Calls.Where(c => c.StartsWith(Models, StringComparison.Ordinal)).Should().Equal(
            $"{Models}?scope=aaaaaaaaaaa1&rubric={Uri.EscapeDataString(Strict.Stamp)}",
            $"{Models}?scope=aaaaaaaaaaa1&rubric={Uri.EscapeDataString(Lenient.Stamp)}");
    }

    [Fact]
    public void Withheld_variance_is_said_in_words_above_the_table()
    {
        var page = RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [Strict]), Strict, [Row("grok")],
            variance: [Variance("cs2", "withheld"), Variance("rs3", "stated")]));

        var text = page.Find("[data-test=gate-variance]").TextContent;
        text.Should().Contain("withheld for 1 of 2").And.Contain("fewer than three");
        page.Markup.IndexOf("data-test=\"gate-variance\"", StringComparison.Ordinal)
            .Should().BeLessThan(page.Markup.IndexOf("data-test=\"gate-model-table\"", StringComparison.Ordinal), "the refusal is read before the numbers it qualifies");
    }

    [Fact]
    public void An_unassessed_figure_renders_as_a_dash_and_never_as_zero()
    {
        var page = RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [Strict]), Strict, [Row("grok", seedsHit: GateFigureDto.Unassessed)]));

        var cell = page.Find("[data-test=gate-model-table] tbody tr td[data-col=seeds-hit]").TextContent.Trim();
        cell.Should().Be("—", "nobody looked is not zero hits");
    }

    [Fact]
    public void Assessment_failed_is_a_column_of_its_own()
    {
        var page = RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [Strict]), Strict, [Row("grok", assessmentFailed: 3)]));

        page.FindAll("[data-test=gate-model-table] thead th").Select(h => h.TextContent.Trim()).Should().Contain("assessment failed");
        page.Find("[data-test=gate-model-table] tbody tr td[data-col=assessment-failed]").TextContent.Trim().Should().Be("3");
    }

    [Fact]
    public void An_unknown_cost_renders_unknown_and_never_free()
    {
        var page = RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [Strict]), Strict, [Row("codex-cli", cost: GateFigureDto.Unknown)]));

        page.Find("[data-test=gate-model-table] tbody tr td[data-col=cost-per-run]").TextContent.Trim().Should().Be("unknown");
    }

    [Fact]
    public void A_strict_percentage_nobody_hand_checked_is_withheld_in_words()
    {
        var page = RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [Strict]), Strict, [Row("grok")]));

        page.Find("[data-test=gate-model-table] tbody tr td[data-col=supported]").TextContent.Trim().Should().Be("not hand-checked");
    }

    [Fact]
    public void A_calibration_task_is_marked_and_its_rows_are_reported_apart()
    {
        var page = RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [Strict]), Strict, [Row("grok")], calibration: [Row("grok")],
            perTask: [PerTask("cs2", calibration: false), PerTask("js3", calibration: true)]));

        page.Find("[data-test=gate-calibration-table]").TextContent.Should().Contain("reported apart");
        var calibrationRow = page.FindAll("[data-test=gate-per-task] tbody tr").Single(r => r.TextContent.Contains("js3", StringComparison.Ordinal));
        calibrationRow.TextContent.Should().Contain("calibration");
        page.FindAll("[data-test=gate-per-task] tbody tr").Single(r => r.TextContent.Contains("cs2", StringComparison.Ordinal))
            .TextContent.Should().NotContain("calibration");
        page.Find("[data-test=gate-all-tasks-table]").TextContent.Should().Contain("never the default reading");
    }

    [Theory]
    [InlineData("Strict", "supported % (strict-v1)", "lenient", "worth")]
    [InlineData("LenientWorth", "worth having % (lenient-worth-v1)", "strict", "supported")]
    public void Two_rubric_kinds_never_share_a_column(string kind, string heading, string otherWord, string otherQuestion)
    {
        var rubric = kind == "Strict" ? Strict : Lenient;

        var heads = Heads(RenderTable(Table(Scope("aaaaaaaaaaa1", rubrics: [rubric]), rubric, [Row("grok")]), rubric));

        heads.Should().Contain(heading, "a verdict column names its rubric, so it cannot be read as the other kind's");
        heads.Should().NotContain(h => h.Contains(otherWord, StringComparison.Ordinal) || h.Contains(otherQuestion, StringComparison.Ordinal),
            "one table is one rubric — no column of it speaks for the other kind");
    }

    [Fact]
    public void A_scope_whose_suite_tasks_are_not_recorded_says_which_verb_records_them_and_asks_for_no_table()
    {
        var scope = Scope("aaaaaaaaaaa1", tasksRecorded: false, rubrics: [Strict]);
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { scope }).Answers(Runs, new[] { RunSummary("cs2", taskRecorded: false) });

        var page = Render<GateFeature>(api);

        page.Markup.Should().Contain("bench gate suite record");
        api.Calls.Should().NotContain(c => c.StartsWith(Models, StringComparison.Ordinal));
        page.Find("[data-test=gate-run-list]").TextContent.Should().Contain("not recorded");
    }

    [Fact]
    public void A_scope_with_no_verdicts_hides_the_rubric_control_and_says_nothing_was_assessed()
    {
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { Scope("aaaaaaaaaaa1") }).Answers(Runs, new[] { RunSummary("cs2") });

        var page = Render<GateFeature>(api);

        page.FindAll("#gate-rubric").Should().BeEmpty();
        page.Markup.Should().Contain("No assessment recorded");
        page.Find("[data-test=gate-run-list]").TextContent.Should().Contain("cs2");
    }

    [Fact]
    public void The_run_list_marks_a_superseded_attempt_and_an_unrecorded_turn_count()
    {
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { Scope("aaaaaaaaaaa1") })
            .Answers(Runs, new[] { RunSummary("cs2", attempt: 1, superseded: true), RunSummary("cs2", attempt: 2, turns: GateFigureDto.Unknown) });

        var list = Render<GateFeature>(api).Find("[data-test=gate-run-list]").TextContent;

        list.Should().Contain("superseded").And.Contain("unknown").And.Contain("HttpError");
    }

    [Fact]
    public void An_unreachable_api_is_a_warning_and_never_an_empty_gate()
    {
        var page = Render<GateFeature>(new ScriptedBenchApi());

        page.Markup.Should().Contain("alert-warning");
        page.Markup.Should().NotContain("No gate run has been measured");
    }

    [Fact]
    public void A_scope_and_a_rubric_named_in_the_address_are_chosen_when_the_runs_offer_them()
    {
        var scope = Scope("aaaaaaaaaaa2", rubrics: [Strict, Lenient]);
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { Scope("aaaaaaaaaaa1", rubrics: [Strict]), scope })
            .Answers(Models, Table(scope, Lenient, [Row("grok")])).Answers(Runs, Array.Empty<GateRunSummaryDto>());
        Services.AddSingleton(new BenchConsoleApi(api.Client()));

        var view = Render<Bench.Ui.Components.GateScopeView>(p => p
            .Add(v => v.Gate, "feature").Add(v => v.InitialScope, "aaaaaaaaaaa2").Add(v => v.InitialRubric, "lenient-worth-v1"));

        api.Calls.Should().Contain($"{Models}?scope=aaaaaaaaaaa2&rubric={Uri.EscapeDataString(Lenient.Stamp)}",
            "a link to one scope under one rubric opens on that table — the address names the choice, so nobody else made it");
        view.Find("[data-test=gate-model-table]").TextContent.Should().Contain("worth having");
    }

    [Fact]
    public void A_scope_in_the_address_that_the_runs_never_echoed_is_not_chosen()
    {
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { Scope("aaaaaaaaaaa1", rubrics: [Strict]), Scope("aaaaaaaaaaa2", rubrics: [Strict]) });
        Services.AddSingleton(new BenchConsoleApi(api.Client()));

        var view = Render<Bench.Ui.Components.GateScopeView>(p => p.Add(v => v.Gate, "feature").Add(v => v.InitialScope, "ffffffffffff"));

        view.Markup.Should().Contain("Choose a scope");
        api.Calls.Should().NotContain(c => c.StartsWith(Runs, StringComparison.Ordinal) || c.StartsWith(Models, StringComparison.Ordinal));
    }

    [Fact]
    public void The_plan_and_code_pages_read_their_own_gate()
    {
        var api = new ScriptedBenchApi().Answers(Scopes, Array.Empty<GateScopeDto>());
        Services.AddSingleton(new BenchConsoleApi(api.Client()));

        Render<GatePlan>().Markup.Should().Contain("plan gate");
        Render<GateCode>().Markup.Should().Contain("code gate");
        api.Calls.Should().Contain("/api/bench/gate/scopes?gate=plan").And.Contain("/api/bench/gate/scopes?gate=code");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private IRenderedComponent<GateFeature> RenderTable(GateModelTableDto table, GateRubricDto? rubric = null)
    {
        // One scope and one rubric: nothing to choose, so the page reads the table at once.
        var api = new ScriptedBenchApi().Answers(Scopes, new[] { table.Scope with { Rubrics = [rubric ?? Strict] } })
            .Answers(Models, table).Answers(Runs, Array.Empty<GateRunSummaryDto>());
        return Render<GateFeature>(api);
    }

    private IRenderedComponent<T> Render<T>(ScriptedBenchApi api)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        Services.AddSingleton(new BenchConsoleApi(api.Client()));
        return Render<T>();
    }

    private static IReadOnlyList<string> Heads(IRenderedComponent<GateFeature> page) =>
        [.. page.FindAll("[data-test=gate-model-table] thead th").Select(h => h.TextContent.Trim())];
}
