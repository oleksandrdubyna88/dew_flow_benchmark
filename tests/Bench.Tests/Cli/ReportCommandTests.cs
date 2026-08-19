using Bench.Cli;
using Bench.Domain.Splitting;
using Bench.Tests.Application;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench report` — the renderer, and the exit codes an agent reads.
/// <para>
/// The load-bearing property here is the one <c>bench run</c> already has: a bad SCORE is not a bad run.
/// A report whose subject answered poorly exits <c>0</c>, because no bar has been agreed and an agent that
/// reads "the model was wrong" as "the harness is broken" keeps reporting the wrong news.
/// </para></summary>
public sealed class ReportCommandTests
{
    private const string Metric = "Anchor recall";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_configuration_that_won_only_where_it_was_chosen_is_printed_as_UNPROVEN()
    {
        var (code, output, _) = await ReportAsync(
            Arm("-", selection: 0.5, heldOut: 0.5),
            Arm("cand", selection: 1.0, heldOut: 0.5));

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("UNPROVEN").And.Contain("won only where it was chosen");
        output.Should().NotContain("CONFIRMED", "the one thing this line must never read as is a result");
    }

    [Fact]
    public async Task A_configuration_that_survived_the_half_that_did_not_choose_it_prints_CONFIRMED_and_its_margin()
    {
        var (code, output, _) = await ReportAsync(
            Arm("-", selection: 0.25, heldOut: 0.25),
            Arm("cand", selection: 1.0, heldOut: 0.75));

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("CONFIRMED +0.5");
    }

    [Fact]
    public async Task A_low_score_still_exits_pass_because_the_exit_code_answers_whether_the_measurement_happened()
    {
        var (code, output, _) = await ReportAsync(Arm("-", selection: 0, heldOut: 0));

        code.Should().Be(ExitCodes.Pass, "a subject answering badly is a RESULT, not a broken harness");
        code.Should().NotBe(ExitCodes.Regression);
        output.Should().Contain("scored   ");
    }

    [Fact]
    public async Task A_run_nobody_has_scored_exits_NoReport_rather_than_pass()
    {
        var halves = Halves(2);
        var run = new ScriptedRun(halves);

        var (code, output, _) = await RunAsync(run, new ScriptedResults([], Metric), "--run", run.Run.Id.ToString(), "--metric", Metric);

        code.Should().Be(ExitCodes.NoReport,
            "an orchestrator must be able to tell a run that produced nothing from one that produced a comparison");
        output.Should().Contain("nothing here to compare");
    }

    [Fact]
    public async Task An_unknown_run_is_an_environment_problem_not_a_regression()
    {
        var halves = Halves(2);
        var run = new ScriptedRun(halves);

        var (code, _, error) = await RunAsync(
            run, new ScriptedResults([], Metric), "--run", Guid.CreateVersion7().ToString(), "--metric", Metric);

        code.Should().Be(ExitCodes.Environment);
        code.Should().NotBe(ExitCodes.Regression, "a run that is not there has measured nothing");
        error.Should().Contain("no run ");
    }

    [Fact]
    public async Task A_missing_run_id_is_a_configuration_problem()
    {
        var halves = Halves(2);
        var run = new ScriptedRun(halves);

        var (code, _, error) = await RunAsync(run, new ScriptedResults([], Metric), "--metric", Metric);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--run <guid> is required");
    }

    [Fact]
    public async Task A_report_without_a_metric_is_refused_rather_than_given_a_default()
    {
        var halves = Halves(2);
        var run = new ScriptedRun(halves);

        var (code, _, error) = await RunAsync(run, new ScriptedResults([], Metric), "--run", run.Run.Id.ToString());

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("name the metric").And.Contain("means nothing for the control arm");
    }

    [Fact]
    public async Task The_json_carries_the_verdict_as_a_NAME_so_a_reader_never_sees_an_enum_ordinal()
    {
        var (code, output, _) = await ReportAsync(
            ["--json"],
            Arm("-", selection: 0.5, heldOut: 0.5),
            Arm("cand", selection: 1.0, heldOut: 0.5));

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("\"proof\": \"Unproven\"",
            "an ordinal changes meaning the day somebody inserts an enum member, and this object is published");
        output.Should().Contain("\"metricName\": \"Anchor recall\"");
    }

    [Fact]
    public void The_verb_appears_in_help_with_the_word_the_whole_split_exists_to_print()
    {
        var output = new StringWriter();

        Bench.Cli.Program.Run(["help"], output, new StringWriter(), Ct);

        output.ToString().Should().Contain("bench report").And.Contain("UNPROVEN");
    }

    [Fact]
    public void Without_a_database_the_verb_refuses_rather_than_guessing_at_localhost()
    {
        var previous = Environment.GetEnvironmentVariable("BENCH_DB");
        Environment.SetEnvironmentVariable("BENCH_DB", null);

        try
        {
            var error = new StringWriter();
            var code = Bench.Cli.Program.Run(["report", "--run", Guid.CreateVersion7().ToString(), "--metric", Metric],
                new StringWriter(), error, Ct);

            code.Should().Be(ExitCodes.Environment);
            error.ToString().Should().Contain("no database");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BENCH_DB", previous);
        }
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private Task<(int Code, string Output, string Error)> ReportAsync(params ArmScript[] arms) =>
        ReportAsync([], arms);

    private async Task<(int Code, string Output, string Error)> ReportAsync(string[] extra, params ArmScript[] arms)
    {
        var halves = Halves(2);
        var selection = halves.Take(2).ToList();
        var heldOut = halves.Skip(2).ToList();

        var legs = arms.SelectMany(a => (IEnumerable<ScriptedLeg>)
        [
            .. selection.Select(q => new ScriptedLeg(q, "m", a.Name, a.Selection)),
            .. heldOut.Select(q => new ScriptedLeg(q, "m", a.Name, a.HeldOut)),
        ]).ToList();

        var run = new ScriptedRun(halves);

        return await RunAsync(run, new ScriptedResults(legs, Metric),
            [.. extra, "--run", run.Run.Id.ToString(), "--metric", Metric]);
    }

    private async Task<(int Code, string Output, string Error)> RunAsync(
        ScriptedRun run, ScriptedResults results, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await ReportCommand.RunAsync(
            CommandLine.Parse(["report", .. args]), run, results, output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }

    /// <summary>Two questions from each half, ordered selection-first. Probed rather than hard-coded: the
    /// assignment is a hash, and a test asserting that <c>q1</c> is a selection question would be asserting
    /// the hash instead of the behaviour.</summary>
    private static List<string> Halves(int perHalf)
    {
        var selection = new List<string>();
        var heldOut = new List<string>();

        foreach (var id in Enumerable.Range(1, 64).Select(i => $"q{i}"))
        {
            var half = SeedSplit.Assign(ScriptedRun.SuiteId, id);
            var target = half is Bench.Domain.Outcome<SplitHalf>.Ok { Value: SplitHalf.Selection } ? selection : heldOut;

            if (target.Count < perHalf)
            {
                target.Add(id);
            }
        }

        selection.Should().HaveCount(perHalf);
        heldOut.Should().HaveCount(perHalf);

        return [.. selection, .. heldOut];
    }

    private readonly record struct ArmScript(string Name, double Selection, double HeldOut);

    private static ArmScript Arm(string name, double selection, double heldOut) => new(name, selection, heldOut);
}
