namespace Bench.Domain.Gate;

/// <summary>WHICH runs and WHICH verdicts a report figure is computed over, decided once, before any arithmetic.
/// <list type="bullet">
/// <item><b>Runs</b> — one per cell (task, reviewer, repeat): its LATEST attempt. An interrupted attempt that was
/// restarted is not a run of the model; it stays in <see cref="Attempts"/>, which the attempts columns count.</item>
/// <item><b>Verdicts</b> — under ONE rubric (id, kind and hash: an edited prompt is another rubric), and ONE per
/// finding (<c>RunId</c>, <c>FindingOrdinal</c>): a re-ask that superseded an <c>AssessmentFailure</c>, or a
/// second assessor reading the same finding, is not a second finding. The choice is ordered — a real reading
/// over a failure, then an assessor of another family over one of the reviewer's own, then the assessor id and
/// the batch id so the pick is deterministic.</item>
/// <item><b>Assessed</b> — a run with verdicts, OR a valid run that found nothing: it had nothing to assess, and
/// it is a reading of zero hits, not a gap. Only a run with findings and no verdicts is unassessed.</item>
/// </list></summary>
public sealed record GatePopulation(
    IReadOnlyList<GateRunRecord> Runs,
    IReadOnlyList<GateRunRecord> Attempts,
    IReadOnlyList<GateVerdict> Verdicts)
{
    public static GatePopulation Of(GateScope scope, Rubric rubric, GateReportInput input)
    {
        var attempts = input.Runs.Where(r => r.Scope == scope).ToList();

        return new GatePopulation(LatestAttempts(attempts), attempts, OnePerFinding(input.Verdicts.Where(v => v.Rubric == rubric)));
    }

    public static bool IsAssessed(GateRunRecord run, IReadOnlyCollection<GateVerdict> verdicts) =>
        verdicts.Count > 0 || run.Facts.Valid && run.FindingsCount == 0;

    private static IReadOnlyList<GateRunRecord> LatestAttempts(IReadOnlyList<GateRunRecord> attempts) =>
        [.. attempts.GroupBy(r => (r.Task, r.Reviewer, r.Repeat)).Select(cell => cell.MaxBy(r => r.Attempt)!)];

    private static IReadOnlyList<GateVerdict> OnePerFinding(IEnumerable<GateVerdict> verdicts) =>
        [.. verdicts.GroupBy(v => (v.RunId, v.FindingOrdinal))
            .Select(finding => finding
                .OrderByDescending(v => v.Reading.CountsInRates)
                .ThenBy(v => v.AssessorFamilyMatches)
                .ThenBy(v => v.Assessor.Value, StringComparer.Ordinal)
                .ThenBy(v => v.BatchId, StringComparer.Ordinal)
                .First())];
}
