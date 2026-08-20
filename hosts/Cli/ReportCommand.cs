using System.Globalization;
using System.Text.Json;
using Bench.Application;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Splitting;

namespace Bench.Cli;

/// <summary>`bench report` — the comparison, read out of a finished run.
/// <para>
/// It reports; it does not judge, exactly as <c>bench run</c> does not. A low score exits <c>0</c>: no bar
/// has been agreed, so the exit code answers <em>did the measurement happen</em>, and an agent that reads
/// "the subject answered badly" as "the harness is broken" keeps reporting the wrong news.
/// </para>
/// <para>
/// IO and nothing else. Every decision — which arms may be ranked, what renders as <em>unproven</em>,
/// where a baseline comes from — is <see cref="RunReport"/>, which the API calls too.
/// </para></summary>
public static class ReportCommand
{
    public static async Task<int> RunAsync(
        CommandLine command,
        IRunStore runs,
        IResultStore results,
        TextWriter output,
        TextWriter error,
        CancellationToken stopping)
    {
        var request = Read(command);

        if (request is Outcome<RunReportRequest>.Fail bad)
        {
            return Fail(error, bad.Reason, ExitCodes.Configuration);
        }

        try
        {
            var built = await RunReport.BuildAsync(
                runs, results, ((Outcome<RunReportRequest>.Ok)request).Value, stopping);

            return built.Match(view => Write(command, output, view), reason => Fail(error, reason, ExitCodes.Environment));
        }
        catch (Npgsql.NpgsqlException ex)
        {
            return Fail(error, $"database unreachable — {ex.Message}", ExitCodes.Environment);
        }
    }

    /// <summary>Whether this INVOCATION is well-formed — which is the surface's own job, and the reason the
    /// metric is checked here as well as in the use case.
    /// <para>
    /// The two failures are not the same news. A missing or unparseable flag is <c>4</c>, configuration; a
    /// run that is not in this database is <c>3</c>, environment. Letting both arrive as one refusal from
    /// <see cref="RunReport"/> would collapse the distinction the whole exit-code contract rests on — which
    /// is what it did until <c>A_report_without_a_metric_is_refused_rather_than_given_a_default</c> exited
    /// <c>3</c>. The PHRASE is not duplicated: it comes from <see cref="RunReport.NoMetricNamed"/>.
    /// </para></summary>
    private static Outcome<RunReportRequest> Read(CommandLine command)
    {
        if (!Guid.TryParse(command.Value("run"), out var runId))
        {
            return Outcome<RunReportRequest>.Failure("--run <guid> is required — a report names the run it reads");
        }

        if (command.Value("metric").Length == 0)
        {
            return Outcome<RunReportRequest>.Failure(RunReport.NoMetricNamed);
        }

        return Outcome<RunReportRequest>.Success(new RunReportRequest(
            runId,
            command.Value("metric"),
            command.Int("min-legs", 2),
            command.Double("min-spread", Discrimination.DefaultMinSpread),
            command.Value("baseline")));
    }

    /// <summary>Renders, and picks the exit code from whether a measurement HAPPENED.
    /// <para>
    /// <c>5</c> when nothing was scored — a run with no legs produced no report, which an orchestrator must
    /// be able to tell from a finished one. Never <c>1</c>: that is a real regression, and a subject
    /// scoring badly is a result rather than a fault.
    /// </para></summary>
    private static int Write(CommandLine command, TextWriter output, RunReportView view)
    {
        if (command.Has("json"))
        {
            // The CONTRACT, not the view: the same object the endpoint returns, so an agent reading this
            // and a browser reading the API never see different truths. The typed view is what the human
            // rendering below switches over, where a missing ProofState member is a compiler error.
            output.WriteLine(JsonSerializer.Serialize(RunReportContract.From(view), Json));
            return Exit(view);
        }

        output.WriteLine($"run      {view.Label}  {view.RunId}");
        output.WriteLine($"target   {view.TargetCanonical}");
        output.WriteLine($"suite    {view.SuiteStamp}  ({view.SelectionQuestions} selection, {view.HeldOutQuestions} held out)");
        output.WriteLine($"engine   {view.EngineCanonical}");
        output.WriteLine($"metric   {view.MetricName}");
        output.WriteLine($"scored   {view.Scoreboard.Scored} leg(s), {view.Scoreboard.Passed} passed");
        output.WriteLine($"machine  {Machine(view)}");
        output.WriteLine($"load     {view.Load.Describe}");

        foreach (var dimension in view.Dimensions.Where(d => d.Arms.Count > 0))
        {
            WriteDimension(output, dimension);
        }

        output.WriteLine($"spread   {view.Discrimination.Describe}");

        foreach (var warning in view.Warnings)
        {
            output.WriteLine($"warn     {warning}");
        }

        return Exit(view);
    }

    private static void WriteDimension(TextWriter output, DimensionReport dimension)
    {
        var baseline = dimension.Baseline.Length > 0 ? $"against {dimension.Baseline}" : "no baseline stated";
        output.WriteLine($"---- {dimension.Dimension.ToString().ToLowerInvariant()}  ({baseline})");

        foreach (var arm in dimension.Arms)
        {
            output.WriteLine(
                $"  {arm.Arm,-24} {Number(arm.Average)}  {arm.Legs,4} leg(s)"
                + $"   sel {arm.Selection.Describe,-18} held {arm.HeldOut.Describe,-18} {Verdict(arm)}");
        }

        if (dimension.RankingRefusal.Length > 0)
        {
            output.WriteLine($"  ! {dimension.RankingRefusal}");
        }
    }

    /// <summary>The verdict, and the margin beside it. <see cref="ProofState.Unproven"/> prints its own
    /// WORD rather than a smaller number, because every false winner this harness exists to catch reads as
    /// a modest success until it is named.</summary>
    private static string Verdict(ArmReading arm) =>
        arm.Proof switch
        {
            ProofState.Confirmed => $"CONFIRMED {Signed(arm.Margin)}",
            ProofState.Unproven => "UNPROVEN — won only where it was chosen",
            ProofState.Suspicious => "SUSPICIOUS — won only on the held-out half",
            _ => string.Empty,
        };

    /// <summary>The machine, in one line, because a number's meaning depends on it.
    /// <para>
    /// A run measured before the probe existed says NOT RECORDED rather than printing a blank: a reader who
    /// cannot tell "no machine was read" from "the fields happened to be empty" has been told nothing twice.
    /// </para></summary>
    private static string Machine(RunReportView view) =>
        view.Machine.Recorded
            ? $"{view.Machine.Os.Describe} · {view.Machine.Cpu.Describe} · {view.Machine.Fingerprint[..12]}"
            : "NOT RECORDED — nothing here can say which host produced these numbers";

    private static int Exit(RunReportView view) =>
        view.Scoreboard.Scored == 0 ? ExitCodes.NoReport : ExitCodes.Pass;

    private static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Signed(double value) =>
        (value >= 0 ? "+" : string.Empty) + Number(value);

    private static int Fail(TextWriter error, string reason, int code)
    {
        error.WriteLine($"bench: {reason}");
        return code;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
