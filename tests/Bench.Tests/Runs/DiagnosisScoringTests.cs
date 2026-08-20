using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Runs;

/// <summary>The diagnosis instrument (todo/PLAN_investigate_vs_implement.md §3.3): mechanical readings of
/// an Investigate phase's structured output against the reference fix's touched members.
/// <para>
/// The pair recall × precision is the load-bearing part — recall alone teaches a model to name every file
/// it saw, and only the pair says which subject actually knew. Malformed and wrong are DISTINCT states:
/// a diagnosis that cannot be read scores no anchor numbers at all, never zeros.
/// </para></summary>
public sealed class DiagnosisScoringTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('d', 40)).Ok();

    private static readonly SourceAnchor Causal =
        SourceAnchor.Member("src/Retry/Policy.cs", "Policy.NextDelay", new LineSpan(120, 141), Commit);

    private static readonly SourceAnchor Symptom =
        SourceAnchor.Member("src/Http/Client.cs", "Client.SendAsync", new LineSpan(40, 60), Commit);

    [Fact]
    public void Naming_the_causal_member_scores_full_recall_and_full_precision()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(Anchor("src/Retry/Policy.cs", "Policy.NextDelay")), [Causal], []);

        Value(metrics, DiagnosisScoring.Parses).Should().Be("true");
        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be("1");
        Value(metrics, DiagnosisScoring.Precision).Should().Be("1");
    }

    [Fact]
    public void A_shotgun_diagnosis_keeps_its_recall_and_loses_its_precision()
    {
        var anchors = new[] { Anchor("src/Retry/Policy.cs", "Policy.NextDelay") }
            .Concat(Enumerable.Range(1, 9).Select(i => Anchor($"src/Noise/F{i}.cs", $"T{i}.M{i}")))
            .ToArray();

        var metrics = DiagnosisScoring.Score(Parsed(anchors), [Causal], []);

        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be("1");
        Value(metrics, DiagnosisScoring.Precision).Should().Be(
            "0.1", "naming everything is not a diagnosis, and only the pair can say so");
    }

    [Fact]
    public void Naming_only_the_symptom_fires_the_trap()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(Anchor("src/Http/Client.cs", "Client.SendAsync")), [Causal], [Symptom]);

        Value(metrics, DiagnosisScoring.SymptomOnly).Should().Be("true");
        Failed(metrics, DiagnosisScoring.SymptomOnly).Should().BeTrue(
            "pointing at where the defect manifests instead of where it is caused is the failure the arm exists to catch");
        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be("0");
    }

    [Fact]
    public void Reaching_the_cause_does_not_fire_the_trap_even_when_the_symptom_is_also_named()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(
                Anchor("src/Http/Client.cs", "Client.SendAsync"),
                Anchor("src/Retry/Policy.cs", "Policy.NextDelay")),
            [Causal], [Symptom]);

        Value(metrics, DiagnosisScoring.SymptomOnly).Should().Be("false");
    }

    [Fact]
    public void The_trap_is_not_emitted_for_a_task_that_authored_no_symptom_anchors()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(Anchor("src/Retry/Policy.cs", "Policy.NextDelay")), [Causal], []);

        metrics.Should().NotContain(m => m.Name == DiagnosisScoring.SymptomOnly);
    }

    [Fact]
    public void Overlapping_lines_reach_the_cause_without_naming_the_member()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(new DiagnosisAnchor("src/Retry/Policy.cs", string.Empty, new LineSpan(130, 135))),
            [Causal], []);

        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be(
            "1", "a diagnosis pointing into the touched span has found the place, whatever it called it");
    }

    [Fact]
    public void A_bare_file_claim_does_not_answer_for_a_member_level_truth()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(new DiagnosisAnchor("src/Retry/Policy.cs", string.Empty, LineSpan.Whole)),
            [Causal], []);

        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be(
            "0", "naming the file alone would inflate recall on every member-level truth in it");
    }

    [Fact]
    public void A_span_only_truth_is_reached_by_lines_and_never_by_a_bare_file_claim()
    {
        // The FixDiff shape: derived ground truth carries a path and a span, no member name.
        var truth = SourceAnchor.Member("src/F.cs", string.Empty, new LineSpan(50, 60), Commit);

        var byFileAlone = DiagnosisScoring.Score(
            Parsed(new DiagnosisAnchor("src/F.cs", string.Empty, LineSpan.Whole)), [truth], []);
        var byLines = DiagnosisScoring.Score(
            Parsed(new DiagnosisAnchor("src/F.cs", string.Empty, new LineSpan(55, 58))), [truth], []);

        Value(byFileAlone, DiagnosisScoring.AnchorRecall).Should().Be(
            "0", "a truth with a line claim is not a whole-file truth, and naming the file alone must not reach it");
        Value(byLines, DiagnosisScoring.AnchorRecall).Should().Be("1");
    }

    [Fact]
    public void A_member_is_matched_whole_never_by_suffix()
    {
        var truth = SourceAnchor.Member("src/R.cs", "Retry", LineSpan.Whole, Commit);

        var metrics = DiagnosisScoring.Score(Parsed(Anchor("src/R.cs", "NoRetry")), [truth], []);

        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be("0");
    }

    [Fact]
    public void A_malformed_diagnosis_scores_no_anchor_numbers_at_all()
    {
        var metrics = DiagnosisScoring.Score(
            new DiagnosisReading.Malformed("mechanism is empty", "{ \"anchors\": [] }"), [Causal], [Symptom]);

        Value(metrics, DiagnosisScoring.Parses).Should().Be("false");
        Failed(metrics, DiagnosisScoring.Parses).Should().BeTrue();
        metrics.Should().HaveCount(1, "malformed and wrong are different facts, and zeros would merge them");
    }

    [Fact]
    public void An_absent_diagnosis_says_so()
    {
        var metrics = DiagnosisScoring.Score(new DiagnosisReading.Absent(), [Causal], []);

        Value(metrics, DiagnosisScoring.Parses).Should().Be("false");
        Reason(metrics, DiagnosisScoring.Parses).Should().Contain("no JSON object");
        metrics.Should().HaveCount(1);
    }

    [Fact]
    public void A_truth_with_no_causal_anchors_reads_nothing_to_find_rather_than_zero()
    {
        var metrics = DiagnosisScoring.Score(
            Parsed(Anchor("src/Retry/Policy.cs", "Policy.NextDelay")), [], []);

        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be("nothing to find");
        Failed(metrics, DiagnosisScoring.AnchorRecall).Should().BeFalse();
    }

    [Fact]
    public void A_diagnosis_naming_no_anchors_fails_precision_by_name_rather_than_by_arithmetic()
    {
        var metrics = DiagnosisScoring.Score(Parsed(), [Causal], []);

        Value(metrics, DiagnosisScoring.AnchorRecall).Should().Be("0");
        Value(metrics, DiagnosisScoring.Precision).Should().Be("no anchors named");
        Failed(metrics, DiagnosisScoring.Precision).Should().BeTrue();
    }

    private static DiagnosisReading.Parsed Parsed(params DiagnosisAnchor[] anchors) =>
        new(new Diagnosis([.. anchors], "the delay is recomputed without carrying state", string.Empty), string.Empty);

    private static DiagnosisAnchor Anchor(string path, string member) =>
        new(path, member, LineSpan.Whole);

    private static string Value(IReadOnlyList<StoredMetric> metrics, string name) =>
        metrics.Single(m => m.Name == name).Value;

    private static bool Failed(IReadOnlyList<StoredMetric> metrics, string name) =>
        metrics.Single(m => m.Name == name).Failed;

    private static string Reason(IReadOnlyList<StoredMetric> metrics, string name) =>
        metrics.Single(m => m.Name == name).Reason;
}
