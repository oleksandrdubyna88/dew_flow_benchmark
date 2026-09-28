using System.Globalization;
using System.Text;
using Bench.Contracts;

namespace Bench.Cli;

/// <summary>The gate report as text — the same object <c>--json</c> prints, laid out for a terminal. Every figure goes through
/// <see cref="GateFigureWords"/>, so a refusal reads here exactly as it reads on the page; the verdict columns are headed
/// with the rubric's own id, so a strict and a lenient table can never be read as one.</summary>
public static class GateReportText
{
    public static string Of(GateModelTableDto table)
    {
        var text = new StringBuilder();
        var scope = table.Scope;

        text.AppendLine($"scope          {scope.Id} — {scope.Gate} gate · suite {scope.SuiteStamp} · settings {Short(scope.SettingsHash)}");
        text.AppendLine($"product        {scope.ProductVersion}{(scope.BinarySha256.Length > 0 ? $" · binary {Short(scope.BinarySha256)}" : string.Empty)}");
        text.AppendLine($"source         {string.Join(", ", scope.Sources.Select(Source))}");
        text.AppendLine($"rubric         {table.RubricId} ({table.RubricKind}) — no figure below is over any other rubric");
        text.AppendLine();
        Rows(text, "measured tasks", table.Rows, table);
        Rows(text, "calibration tasks — reported apart (they settled a transport, so they describe the tuning as much as the model)", table.CalibrationRows, table);

        if (table.CalibrationRows.Count > 0)
        {
            Rows(text, "all tasks, calibration included — the population an imported harness published; never the default reading", table.AllTaskRows, table);
        }

        text.Append(Variance(table.Variance));
        return text.ToString().TrimEnd();
    }

    private static void Rows(StringBuilder text, string title, IReadOnlyList<GateModelRowDto> rows, GateModelTableDto table)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var (supported, partial) = Headings(table.RubricKind, table.RubricId);
        text.AppendLine(title);
        text.AppendLine(Line("reviewer", "runs", "valid %", "findings/run", "seeds hit", supported, partial, "assessment failed",
            "high-value/run", "overstated %", "s p50", "s p90", "tokens in/run", "cache %", "cost/run", "cost/seed"));

        foreach (var r in rows)
        {
            text.AppendLine(Line(
                r.ReviewerId, Count(r.Runs), W(r.ValidPct), W(r.FindingsPerRun), Seeds(r), W(r.SupportedPct), W(r.SupportedOrPartialPct),
                Count(r.AssessmentFailed), W(r.HighValuePerRun), W(r.OverstatedPct), W(r.SecondsP50), W(r.SecondsP90),
                W(r.TokensInPerRun, "0"), W(r.CachePct), W(r.CostPerRun, "0.####"), W(r.CostPerSeed, "0.####")));
        }

        text.AppendLine();
    }

    /// <summary>The verdict columns' headings, in the rubric's own words and with its id: a strict <i>supported</i> and a
    /// lenient <i>worth having</i> are different questions, and a heading that said only "supported" would let a reader carry
    /// one into the other.</summary>
    public static (string Supported, string Partial) Headings(string rubricKind, string rubricId) =>
        rubricKind == "LenientWorth"
            ? ($"worth having % ({rubricId})", $"— ({rubricId} has no partial)")
            : ($"supported % ({rubricId})", $"supported+partial % ({rubricId})");

    private static string Variance(IReadOnlyList<GateVarianceDto> variance)
    {
        var withheld = variance.Count(v => v.SeedsState != "stated");

        return variance.Count == 0
            ? string.Empty
            : $"variance       seeds-hit spread stated for {variance.Count - withheld} of {variance.Count} task × reviewer pair(s); "
              + $"withheld for {withheld} — fewer than three assessed repeats is a difference, not a spread";
    }

    private static string Seeds(GateModelRowDto r) =>
        r.SeedsHitMean.Known ? $"{W(r.SeedsHitMean)} ({r.SeedsHitMin}–{r.SeedsHitMax})" : W(r.SeedsHitMean);

    private static string Source(string label) => label == "native" ? "native (bench gate run)" : $"imported from {label}";

    private static string W(GateFigureDto figure, string format = "0.##") => GateFigureWords.Of(figure, format);

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Short(string hash) => Bench.Domain.Gate.HashText.Short(hash);

    private static string Line(params string[] cells) => string.Join(" | ", cells);
}
