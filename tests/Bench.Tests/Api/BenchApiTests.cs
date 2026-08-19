using System.Text.Json;
using Bench.Api;
using Bench.Cli;
using Bench.Contracts;
using Bench.Domain.Splitting;
using Bench.Tests.Application;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bench.Tests.Api;

/// <summary>The read surface, and the promise that it and the CLI answer with ONE object.
/// <para>
/// <c>RunPlanDto</c>'s own comment states the rule this file enforces: an agent reading the CLI and a
/// browser reading the API must never see different truths. Two renderers agree until somebody edits one,
/// so the equivalence is asserted rather than intended.
/// </para></summary>
public sealed class BenchApiTests
{
    private const string Metric = "Anchor recall";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_json_the_CLI_prints_is_the_object_the_endpoint_returns()
    {
        var (run, results) = Scripted();

        var fromApi = await BenchApi.ReportAsync(run.Run.Id, run, results, Ct, Metric);
        var fromCli = await CliJsonAsync(run, results);

        var body = fromApi.Should().BeAssignableTo<IValueHttpResult<RunReportDto>>().Subject.Value;

        // Serialised through ONE options object, because the comparison is about the OBJECT rather than
        // about two writers' formatting. What must match is the payload a consumer parses.
        Canonical(body).Should().Be(Canonical(JsonSerializer.Deserialize<RunReportDto>(fromCli, Web)),
            "one shape, or an agent and a browser are reading two different truths about the same run");
    }

    [Fact]
    public async Task A_request_that_named_no_metric_is_a_bad_request_rather_than_a_missing_run()
    {
        var (run, results) = Scripted();

        var answered = await BenchApi.ReportAsync(run.Run.Id, run, results, Ct, metric: null);

        // 400 against 404 is the HTTP spelling of the CLI's 4 against 3, and it is the same defect if
        // collapsed: a caller told "not found" goes looking for the run, when the run is fine and the
        // request was not.
        Status(answered).Should().Be(StatusCodes.Status400BadRequest);
        Reason(answered).Should().Contain("name the metric");
    }

    [Fact]
    public async Task A_run_this_database_does_not_hold_is_a_not_found()
    {
        var (run, results) = Scripted();

        var answered = await BenchApi.ReportAsync(Guid.CreateVersion7(), run, results, Ct, Metric);

        Status(answered).Should().Be(StatusCodes.Status404NotFound);
        Reason(answered).Should().Contain("no run ");
    }

    [Fact]
    public async Task The_verdict_travels_as_a_NAME_so_an_inserted_enum_member_cannot_relabel_a_published_row()
    {
        var (run, results) = Scripted();

        var body = (await BenchApi.ReportAsync(run.Run.Id, run, results, Ct, Metric))
            .Should().BeAssignableTo<IValueHttpResult<RunReportDto>>().Subject.Value!;

        var candidate = body.Dimensions
            .Single(d => d.Dimension == "Variant").Arms
            .Single(a => a.Arm == "cand");

        candidate.Proof.Should().Be("Unproven", "a wire value that is an ordinal changes meaning the day "
            + "somebody inserts an enum member, and this object is published beside the results");
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>A run where the candidate won only on the half that chose it — a report with something in
    /// every part of it, including a verdict worth transporting.</summary>
    private static (ScriptedRun Run, ScriptedResults Results) Scripted()
    {
        var (selection, heldOut) = Halves(2);
        var legs = new List<ScriptedLeg>();

        foreach (var (arm, onSelection, onHeldOut) in new[] { ("-", 0.5, 0.5), ("cand", 1.0, 0.5) })
        {
            legs.AddRange(selection.Select(q => new ScriptedLeg(q, "m", arm, onSelection)));
            legs.AddRange(heldOut.Select(q => new ScriptedLeg(q, "m", arm, onHeldOut)));
        }

        return (new ScriptedRun([.. selection, .. heldOut]), new ScriptedResults(legs, Metric));
    }

    private async Task<string> CliJsonAsync(ScriptedRun run, ScriptedResults results)
    {
        var output = new StringWriter();

        await ReportCommand.RunAsync(
            CommandLine.Parse(["report", "--run", run.Run.Id.ToString(), "--metric", Metric, "--json"]),
            run,
            results,
            output,
            new StringWriter(),
            Ct);

        return output.ToString();
    }

    private static string Canonical(RunReportDto? dto) => JsonSerializer.Serialize(dto, Web);

    private static int Status(IResult result) =>
        result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject.StatusCode ?? 0;

    private static string Reason(IResult result) =>
        result.Should().BeAssignableTo<IValueHttpResult<ProblemDto>>().Subject.Value?.Reason ?? string.Empty;

    private static (List<string> Selection, List<string> HeldOut) Halves(int perHalf)
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

        return (selection, heldOut);
    }
}
