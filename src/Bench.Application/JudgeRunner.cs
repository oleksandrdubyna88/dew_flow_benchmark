using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Microsoft.Extensions.Logging;

namespace Bench.Application;

/// <param name="Judged">Legs this pass gave a verdict about — reached, read, and stored.</param>
/// <param name="NotJudgeable">Legs whose question carries no reference answer. A gap in the SUITE, and
/// reported separately so it never reads as an arbiter that disagreed.</param>
/// <param name="Silent">Legs where the arbiter was unreachable or unreadable. Recorded as "not judged",
/// never as a failing verdict.</param>
/// <param name="SelfJudged">Legs where the arbiter and the subject are the same model. Not refused —
/// it is a legitimate reading — but counted, because it is not the SAME reading, and a report that cannot
/// say how many of its verdicts were self-issued is a report that will be over-read.</param>
public readonly record struct JudgeReport(int Judged, int NotJudgeable, int Silent, int SelfJudged)
{
    public int Total => Judged + NotJudgeable + Silent;

    public string Describe =>
        $"{Judged} judged · {NotJudgeable} not judgeable · {Silent} arbiter silent" +
        (SelfJudged > 0 ? $" · {SelfJudged} SELF-JUDGED" : string.Empty);
}

/// <summary>Re-scoring a finished run with an arbiter, without re-running a single leg.
/// <para>
/// This is the property the whole port exists for, and it is worth naming plainly: the expensive artefact
/// is the subject's answer, and it is already stored. Changing the arbiter, or adding a second one that
/// disagrees, must therefore cost nothing but the arbiter's own inference — otherwise every question about
/// the judgement is one you have to buy the entire run again to ask.
/// </para>
/// <para>
/// Interruptible for the same reason the telemetry ingest is: work is selected by what carries no verdict
/// yet, so a crash at leg 400 of 500 resumes at 400 and never re-judges the first 399.
/// </para></summary>
public sealed class JudgeRunner(IResultStore results, ILogger<JudgeRunner> logger)
{
    public async Task<Outcome<JudgeReport>> JudgeRunAsync(
        Guid runId, Suite suite, IJudge judge, CancellationToken cancellationToken)
    {
        var name = JudgeScoring.MetricName(judge.Model.Id);
        var pending = await results.WithoutMetricAsync(runId, name, cancellationToken);

        if (pending.Count == 0)
        {
            return Outcome<JudgeReport>.Failure(
                $"run {runId} has nothing for '{judge.Model.Id}' to judge — either it holds no results, or this arbiter has already read them all");
        }

        var questions = suite.Questions.ToDictionary(q => q.Id, StringComparer.Ordinal);
        var stranger = pending.FirstOrDefault(l => !questions.ContainsKey(l.QuestionId));

        if (stranger.QuestionId is { Length: > 0 })
        {
            // The wrong suite was handed in. Refused whole rather than judged leg by leg: a verdict issued
            // against a reference answer from a DIFFERENT suite is the one kind of wrong result this system
            // could not later detect, because it would look exactly like a normal one.
            return Outcome<JudgeReport>.Failure(
                $"suite '{suite.Stamp}' has no question '{stranger.QuestionId}' — this is not the suite that produced run {runId}");
        }

        var report = new JudgeReport();

        foreach (var leg in pending)
        {
            report = await JudgeLegAsync(leg, questions[leg.QuestionId], judge, report, cancellationToken);
        }

        logger.LogInformation("judged run {Run} with {Judge}: {Report}", runId, judge.Model.Id, report.Describe);

        return Outcome<JudgeReport>.Success(report);
    }

    private async Task<JudgeReport> JudgeLegAsync(
        JudgeableLeg leg, Question question, IJudge judge, JudgeReport report, CancellationToken cancellationToken)
    {
        var reading = question.ReferenceAnswer.Length == 0
            ? JudgeReading.Silent("no reference answer")
            : await AskAsync(judge, question, leg, cancellationToken);

        var metric = JudgeScoring.Score(question, reading, judge.Model.Id, leg.SubjectModelId);
        var stored = await results.AppendMetricsAsync(leg.ResultId, [metric], cancellationToken);

        if (stored is Outcome<int>.Fail failed)
        {
            logger.LogWarning("could not store a verdict for result {Result}: {Reason}", leg.ResultId, failed.Reason);
            return report;
        }

        return Count(report, question, reading, leg, judge);
    }

    private static async Task<JudgeReading> AskAsync(
        IJudge judge, Question question, JudgeableLeg leg, CancellationToken cancellationToken)
    {
        var verdict = await judge.JudgeAsync(question.Prompt, leg.Answer, question.ReferenceAnswer, cancellationToken);

        return verdict.Match(v => JudgeReading.Verdict(v.Passed, v.Reason), JudgeReading.Silent);
    }

    /// <summary>Counted from what was KNOWN, not from the stored metric's text. Reading the tally back out
    /// of the value it just wrote would make the report agree with the metric by construction — including
    /// when both are wrong.</summary>
    private static JudgeReport Count(
        JudgeReport report, Question question, JudgeReading reading, JudgeableLeg leg, IJudge judge) =>
        report with
        {
            NotJudgeable = report.NotJudgeable + (question.ReferenceAnswer.Length == 0 ? 1 : 0),
            Silent = report.Silent + (question.ReferenceAnswer.Length > 0 && !reading.Reached ? 1 : 0),
            Judged = report.Judged + (question.ReferenceAnswer.Length > 0 && reading.Reached ? 1 : 0),
            SelfJudged = report.SelfJudged
                + (string.Equals(judge.Model.Id, leg.SubjectModelId, StringComparison.OrdinalIgnoreCase) ? 1 : 0),
        };
}
