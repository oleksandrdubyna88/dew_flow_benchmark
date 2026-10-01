using System.Text;
using Bench.Contracts;

namespace Bench.Cli;

/// <summary>The probe report as text — the same object <c>--json</c> prints, laid out for a terminal: one line per lineage with
/// its generation, how its attempt ended (with the exit code when it did not answer), the facts its probe reads, the CLI build that
/// answered, and the command that re-measures it.</summary>
public static class ProbeReportText
{
    public static string Of(ProbeRunReportDto report)
    {
        var text = new StringBuilder();

        text.AppendLine($"probe run      {report.RunId} — created {report.CreatedAt:yyyy-MM-dd HH:mm} UTC, {report.Repeats} repeat(s)");
        text.AppendLine($"oracle         @openai/codex {report.Oracle.Version} ({report.Oracle.Source})");
        text.AppendLine($"cells          {report.Progress.Pending} pending · {report.Progress.Claimed} claimed · {report.Progress.Settled} settled · "
                        + $"{report.Progress.Abandoned} abandoned — {(report.Progress.Open ? "open" : "finished")}");
        text.Append(report.Auditable
            ? string.Empty
            : "pruned         the artefacts of this run were deleted — its verdicts are no longer auditable from disk\n");

        foreach (var subject in report.Subjects)
        {
            text.AppendLine($"subject        {subject.Id} · {subject.Runtime} · {subject.Model} · {subject.ExecutableRef}"
                            + (subject.Endpoint.Length > 0 ? $" · {subject.Vendor} @ {subject.Endpoint} ({subject.Dialect})" : string.Empty));
        }

        text.AppendLine();
        foreach (var cell in report.Cells)
        {
            text.AppendLine(Line(cell));
        }

        return text.ToString().TrimEnd();
    }

    private static string Line(ProbeCellReportDto cell) =>
        $"{cell.Probe,-21} {cell.Subject} r{cell.Repeat} g{cell.Generation}{Newer(cell)}  {Outcome(cell)}{Facts(cell)}"
        + (cell.Pin.Version.Length > 0 ? $"  · {cell.Pin.Version}" : string.Empty)
        + $"  · {cell.RerunCommand}";

    private static string Newer(ProbeCellReportDto cell) =>
        cell.LatestGeneration > cell.Generation ? $" (g{cell.LatestGeneration} {cell.LatestState})" : string.Empty;

    /// <summary>The state for a lineage nothing settled; the attempt's kind for one that did — with the exit code whenever the CLI did
    /// not simply answer, so a refused launch reads as a launch fact, never as a capability.</summary>
    private static string Outcome(ProbeCellReportDto cell) => (cell.State, cell.Kind, cell.Exit.Captured) switch
    {
        (not ProbeWords.Settled, _, _) => cell.Reason == "none" ? cell.State : $"{cell.State} ({cell.Reason})",
        (_, "answered", _) => "answered",
        (_, var kind, true) => $"{kind} (exit {cell.Exit.Code})",
        (_, var kind, false) => kind,
    };

    private static string Facts(ProbeCellReportDto cell) =>
        cell.Kind != "answered"
            ? string.Empty
            : cell.Probe switch
            {
                "web-search" => $"  answerCurrent={cell.Facts.AnswerCurrent} toolEvidence={cell.Facts.ToolEvidence}",
                "read-denied" => $"  canaryRead={cell.Facts.CanaryRead} readAttempted={cell.Facts.ReadAttempted}",
                "web-confined" => $"  canaryRead={cell.Facts.CanaryRead} readAttempted={cell.Facts.ReadAttempted} answerCurrent={cell.Facts.AnswerCurrent}",
                "api-reachable" => $"  reachable={cell.Facts.Reachable} accountOut={cell.Facts.AccountOut}",
                _ => $"  canaryRead={cell.Facts.CanaryRead}",
            } + (cell.VoidedByControl ? " (voided: read-inside read no)" : string.Empty);
}
