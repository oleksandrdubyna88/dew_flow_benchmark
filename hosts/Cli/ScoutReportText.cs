using System.Globalization;
using System.Text;
using Bench.Contracts;

namespace Bench.Cli;

/// <summary>The scout report as text — the same object <c>--json</c> prints, laid out for a terminal: the verdict line (a PRELIMINARY run says
/// "preliminary — not a verdict"), the ledger, how the intervals were drawn, one line per workload with both paired intervals, one block per
/// arm with every metric of §3.6, and one line per task. An absent number prints as <c>—</c> (not captured) or <c>not measured</c>, never 0.</summary>
public static class ScoutReportText
{
    public const string PreliminaryLabel = "preliminary — not a verdict";

    public static string Of(ScoutRunReportDto report)
    {
        var text = new StringBuilder();
        var l = report.Ledger;

        text.AppendLine(Inv($"scout run      {report.RunId} — {report.Kind}, created {report.CreatedAt:yyyy-MM-dd HH:mm} UTC · seed {report.Seed} · {report.Repeats} repeat(s)"));
        text.AppendLine($"verdict        {Verdict(report.Verdict)}");
        text.AppendLine($"cells          {report.Progress.Pending} pending · {report.Progress.Claimed} claimed · {report.Progress.Settled} settled · {report.Progress.Abandoned} abandoned — {(report.Progress.Open ? "open" : "finished")}");
        text.AppendLine(Inv($"ledger         ${l.SettledUsd} settled + ${l.UnknownUsd} unknown ({l.UnknownCharges} charge(s)) = ${l.SpentUsd} spent of ${l.CeilingUsd} · ${l.ReservedUsd} reserved · {l.Attempts} attempt(s) · {l.Overshoots} overshoot(s)"));
        text.AppendLine(Inv($"pinned         claude {report.Pins.CliVersion} · opus {report.Pins.ResolvedOpus} · sonnet {report.Pins.ResolvedSonnet}"));
        text.AppendLine(Inv($"intervals      paired, clustered by repository · seed {report.Bootstrap.Seed} · {report.Bootstrap.Resamples} resamples · one-sided 95 % · pass needs Δrecall lower ≥ {report.Bootstrap.RecallMargin} and Δwrong upper ≤ +{report.Bootstrap.WrongMargin}"));
        text.AppendLine();

        foreach (var workload in report.Workloads)
        {
            text.AppendLine(Workload(workload));
        }

        foreach (var arm in report.Arms)
        {
            text.AppendLine();
            Arm(text, arm);
        }

        text.AppendLine();
        foreach (var task in report.Tasks)
        {
            text.AppendLine(Task(task));
        }

        return text.ToString().TrimEnd();
    }

    private static string Verdict(string verdict) => verdict switch
    {
        "preliminary" => PreliminaryLabel,
        "rule-change-shown" => "RULE CHANGE SHOWN — W1, W2 and W3 all pass",
        _ => "not shown — Opus stays (§3.8: any failure or crossing interval is not shown)",
    };

    private static string Workload(ScoutWorkloadDto w) =>
        Inv($"{w.Workload,-3} {w.ArmA,-3} vs {w.ArmB,-3}  {w.Outcome,-12} Δrecall {Interval(w.Recall)} · Δwrong/10 {Interval(w.WrongPerTen)}")
        + (w.Reasons.Count > 0 ? $" · {string.Join(", ", w.Reasons)}" : string.Empty);

    private static string Interval(ScoutIntervalDto i) =>
        i.Known ? Inv($"{Signed(i.Point)} [{Signed(i.Lower)}, {Signed(i.Upper)}] over {i.Tasks} task(s) in {i.Clusters} repo(s)") : "— (no paired task)";

    private static void Arm(StringBuilder text, ScoutArmReportDto a)
    {
        var c = a.Counts;
        var t = a.Tokens;

        text.AppendLine(Inv($"arm {a.Arm,-11} {a.Workload} · {a.Scouts} × {a.Model} · {a.Agent}"));
        text.AppendLine(Inv($"  quality      recall {F(a.Recall)} · wrong claims per 10 {F(a.WrongPerTen)}{(a.WrongGap == "none" ? string.Empty : $" ({a.WrongGap})")}"));
        text.AppendLine(Inv($"  time         team {F(a.TeamMs)} ms (slowest scout {F(a.SlowestScoutMs)} ms) · scout {F(a.ScoutMs)} ms · session {F(a.SessionMs)} ms"));
        text.AppendLine(Inv($"  cost         team ${F(a.TeamUsd)} · session ${F(a.SessionUsd)} · per right file ${F(a.UsdPerRightFile)} · caller load {F(a.CallerLoad)} file(s)"));
        text.AppendLine(Inv($"  tokens       team in {F(t.TeamInput)} out {F(t.TeamOutput)} cache read {F(t.TeamCacheRead)} write {F(t.TeamCacheWrite)} · one scout in {F(t.ScoutInput)} out {F(t.ScoutOutput)}"));
        text.AppendLine(Inv($"  cells        {c.Cells}: {c.Scored} scored · {c.Void} void · {c.Stopped} stopped · {c.Aborted} aborted · {c.SilentCells} silent"));
        text.AppendLine(Inv($"  claims       {c.WrongClaims} wrong of {c.DecidedClaims} decided · {c.UnresolvedClaims} unresolved · {c.UnassessedClaims} unassessed · {c.UnparseableClaims} unparseable · {c.SilentScouts} silent scout(s)")
            + (c.TeamTimeBelowSlowestScout > 0 ? Inv($" · {c.TeamTimeBelowSlowestScout} cell(s) with team time below the slowest scout") : string.Empty));
    }

    private static string Task(ScoutTaskReportDto task) =>
        Inv($"task {task.Task} ({Where(task)}, {task.Kind}, |G| {task.GroundTruth}): ")
        + string.Join(" · ", task.Arms.Select(a => Inv($"{a.Arm} recall {F(a.Recall)} wrong/10 {F(a.WrongPerTen)} ({a.Scored}/{a.Cells} scored)")));

    /// <summary>A public task by its repository (and language when it names one); a private task by the word and its language — the DTO
    /// already carries the label as its repository, and the text says why it reads so.</summary>
    public static string Where(ScoutTaskReportDto task) =>
        task.Privacy == "private" ? $"private · {task.Language}" : task.Language.Length > 0 ? $"{task.Repository} · {task.Language}" : task.Repository;

    private static string F(ScoutFigureDto figure) => figure.State switch
    {
        ScoutFigureDto.KnownState => figure.Value.ToString("0.######", CultureInfo.InvariantCulture),
        ScoutFigureDto.NotMeasuredState => "not measured",
        _ => "—",
    };

    private static string Signed(double value) => value.ToString("+0.####;-0.####;0", CultureInfo.InvariantCulture);

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
