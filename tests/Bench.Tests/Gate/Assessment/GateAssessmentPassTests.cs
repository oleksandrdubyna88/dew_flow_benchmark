using Bench.Application;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.Assessment.AssessmentFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>S4.2–S4.4 and S4.7 end to end — a real store and artefact root, a scripted assessor. The guarantees: the
/// assessor is never told which model, run or reviewer wrote a finding; verdicts are persisted per batch, so a pass killed
/// part-way keeps what it finished and a re-run skips it; a batch that fails twice is a failure VERDICT for each of its
/// findings; a later pass re-asks exactly those and supersedes them; a missing id is asked once more and then left
/// unassessed; a verdict read by the reviewer's own family is flagged.</summary>
[Collection("postgres")]
public sealed class GateAssessmentPassTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_finding_gets_a_blinded_verdict_and_nothing_the_assessor_is_sent_names_a_model_a_run_or_a_reviewer()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 2, findings: 2, ct: Ct);
        var assessor = ScriptedAssessor.AllSupported();

        var report = (await rig.Pass(assessor).RunAsync(rig.Request(), _ => { }, Ct)).Ok();

        report.Assessed.Should().Be(4);
        report.Unassessed.Should().Be(0);
        report.Exported.Should().Be(4);
        (await rig.VerdictsAsync()).Should().HaveCount(4).And.OnlyContain(v => v.Reading is Verdict.Strict && v.Assessor.Value == "codex-astra");

        var sent = string.Join('\n', assessor.Prompts.Concat(assessor.Asks.Select(a => a.WorkingDirectory)).Concat(assessor.Asks.SelectMany(a => new[] { a.Options.OutputSchemaFile, a.Options.LastMessageFile })));
        sent.Should().NotContain("grok").And.NotContain(rig.Campaign.ToString()).And.NotContain(rig.Reviewer.Definition.Model);
        rig.Cells.Should().OnlyContain(cell => !sent.Contains(cell.ToString()), "no cell id — the run a finding came from — reaches the assessor");
        sent.Replace('\\', '/').Should().NotContain(rig.Root.Replace('\\', '/'),
            "no path handed to the assessor — its folder, the schema, the answer file, the seed list — lies in the artefact root, where the key and every run's findings are one directory away");
        assessor.Prompts[0].Should().Contain(AssessRig.NeutralCheckout).And.Contain("PRIOR CLUSTER KEYS\n(none)").And.Contain("INPUT ROWS (4 findings, task cs2)");
    }

    [Fact]
    public async Task A_pass_killed_in_its_second_batch_keeps_the_first_and_a_rerun_asks_only_what_is_left()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 3, ct: Ct);
        var crashing = new ScriptedAssessor((ids, call) => call == 1
            ? Answer([.. ids.Select(id => AnswerRow(id))])
            : throw new OperationCanceledException("the operator pressed Ctrl+C"));

        var killed = () => rig.Pass(crashing).RunAsync(rig.Request(batchSize: 2), _ => { }, Ct);
        await killed.Should().ThrowAsync<OperationCanceledException>();
        (await rig.VerdictsAsync()).Should().HaveCount(2, "the first batch was persisted before the second was asked");

        var healthy = ScriptedAssessor.AllSupported();
        var rerun = (await rig.Pass(healthy).RunAsync(rig.Request(batchSize: 2), _ => { }, Ct)).Ok();

        healthy.Prompts.Should().ContainSingle().Which.Should().Contain("INPUT ROWS (1 findings, task cs2)");
        rerun.Exported.Should().Be(0, "the key already held every finding");
        (await rig.VerdictsAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task A_batch_answering_with_an_extra_id_is_retried_once_and_failing_again_every_finding_is_an_UnknownIds_verdict()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 3, ct: Ct);
        var extra = new ScriptedAssessor((ids, _) => Answer([.. ids.Select(id => AnswerRow(id)), AnswerRow("ffffffff")]));

        var report = (await rig.Pass(extra).RunAsync(rig.Request(), _ => { }, Ct)).Ok();

        extra.Prompts.Should().HaveCount(2, "one retry of the whole batch, and no more");
        report.Failed.Should().Be(3);
        (await rig.VerdictsAsync()).Should().HaveCount(3).And.OnlyContain(v => v.Reading == new Verdict.AssessmentFailure(AssessmentFailureCause.UnknownIds));

        var row = (await rig.ReportAsync()).Rows.Single();
        row.AssessmentFailed.Should().Be(3, "failures are their own column");
        row.Assessed.Should().Be(0);
        row.SupportedPct.Should().Be(Figure.Unassessed, "a failure is never a refuted finding and never in supported %");
    }

    [Fact]
    public async Task A_later_pass_re_asks_exactly_the_failed_findings_and_its_verdicts_supersede_the_failures()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 3, ct: Ct);
        await rig.Pass(new ScriptedAssessor((_, _) => "not json at all")).RunAsync(rig.Request(), _ => { }, Ct);

        var healthy = ScriptedAssessor.AllSupported();
        var second = (await rig.Pass(healthy).RunAsync(rig.Request(), _ => { }, Ct)).Ok();

        healthy.Prompts.Should().ContainSingle().Which.Should().Contain("INPUT ROWS (3 findings");
        second.Assessed.Should().Be(3);

        var row = (await rig.ReportAsync()).Rows.Single();
        row.AssessmentFailed.Should().Be(0, "a real reading supersedes the failure it replaced");
        row.Supported.Should().Be(3);
        row.SupportedPct.Should().Be(Figure.NotHandChecked, "the verdicts are real now, and still nobody has hand-checked them");
    }

    [Fact]
    public async Task A_missing_id_is_asked_once_more_on_its_own_and_then_left_unassessed_with_no_row()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 3, ct: Ct);
        string? omitted = null;
        var forgetful = new ScriptedAssessor((ids, _) =>
        {
            omitted ??= ids[0];
            return Answer([.. ids.Where(id => id != omitted).Select(id => AnswerRow(id))]);
        });

        var report = (await rig.Pass(forgetful).RunAsync(rig.Request(), _ => { }, Ct)).Ok();

        forgetful.Prompts.Should().HaveCount(2);
        ScriptedAssessor.IdsIn(forgetful.Prompts[1]).Should().Equal([omitted!]);
        report.Unassessed.Should().Be(1);
        report.Assessed.Should().Be(2);
        (await rig.VerdictsAsync()).Should().HaveCount(2, "a finding left unassessed has no row — it is not an assessment failure");
    }

    [Fact]
    public async Task A_verdict_read_by_the_reviewers_own_family_is_flagged_and_counted_apart()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, reviewerModel: "gpt-5.6-terra", ct: Ct);

        var report = (await rig.Pass(ScriptedAssessor.AllSupported()).RunAsync(rig.Request(), _ => { }, Ct)).Ok();

        report.FamilyMatched.Should().Be(2);
        (await rig.VerdictsAsync()).Should().OnlyContain(v => v.AssessorFamilyMatches);
        (await rig.ReportAsync()).Rows.Single().AssessorFamilyMatched.Should().Be(2);
    }

    [Fact]
    public async Task Prior_cluster_keys_of_the_task_are_carried_into_the_next_batch()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 3, ct: Ct);
        var assessor = ScriptedAssessor.AllSupported();

        await rig.Pass(assessor).RunAsync(rig.Request(batchSize: 2), _ => { }, Ct);

        assessor.Prompts[0].Should().Contain("PRIOR CLUSTER KEYS\n(none)");
        assessor.Prompts[1].Should().Contain("PRIOR CLUSTER KEYS\n- cs2:null-export", "the first batch's key is offered to the second so one issue keeps one key");
    }

    /// <summary>Prior cluster keys come from THIS assessor's COMMITTED readings: an orphan log line (its batch never reached
    /// the database) is not a reading, and another assessor's keys would steer the second opinion toward the first.</summary>
    [Fact]
    public async Task Prior_cluster_keys_are_this_assessors_committed_ones_only()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, ct: Ct);
        var other = GateReviewer.Create(GateReviewerId.Parse("claude-opus").Ok(),
            GateReviewerTests.Definition(model: "claude-opus-5", runtime: ReviewerRuntime.Claude, endpoint: string.Empty, credsKeyRef: string.Empty, keyName: string.Empty).Ok(), DateTimeOffset.UtcNow);
        await rig.Pass(ScriptedAssessor.AllSupported()).RunAsync(rig.Request(assessor: other), _ => { }, Ct);
        var key = (await rig.Files.ReadKeyAsync(Ct)).Ok();
        await rig.Files.AppendVerdictLinesAsync(AssessRig.Assessor().Id,
            [new VerdictLine(key[0].Id.Value, "cs2", "codex-astra", "orphan-batch", rig.Strict.Rubric.Hash, "supported", "high", "yes", "yes", "cs2:orphan-key", "none", "n", string.Empty, DateTimeOffset.UtcNow)], Ct);

        var codex = ScriptedAssessor.AllSupported();
        await rig.Pass(codex).RunAsync(rig.Request(), _ => { }, Ct);

        codex.Prompts[0].Should().Contain("PRIOR CLUSTER KEYS\n(none)", "neither the other assessor's committed keys nor this one's orphan line are this assessor's readings");
    }

    /// <summary>A batch can take many minutes; the operator is told when it is SENT, not only when it comes back.</summary>
    [Fact]
    public async Task A_batch_is_announced_before_the_assessor_is_asked()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, ct: Ct);
        var events = new List<string>();
        var assessor = new ScriptedAssessor((ids, _) =>
        {
            events.Add("asked");
            return Answer([.. ids.Select(id => AnswerRow(id))]);
        });

        await rig.Pass(assessor).RunAsync(rig.Request(), new AssessmentProgress(started => events.Add($"sent {started.Asked}"), done => events.Add("done")), Ct);

        events.Should().Equal(["sent 2", "asked", "done"]);
    }

    /// <summary>The database is the commit point: a batch it refused was not assessed, whatever the assessor answered.</summary>
    [Fact]
    public async Task A_batch_the_verdict_store_refuses_is_left_unassessed_never_reported_as_read()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, reviewerModel: "gpt-5.6-terra", ct: Ct);

        var report = (await rig.Pass(ScriptedAssessor.AllSupported(), verdicts: new RefusingVerdicts(rig.NewVerdicts())).RunAsync(rig.Request(), _ => { }, Ct)).Ok();

        report.Assessed.Should().Be(0, "nothing reached the database");
        report.Unassessed.Should().Be(2, "the next pass must ask them again, and the exit code must say so");
        report.FamilyMatched.Should().Be(0);
        report.Refusals.Should().Contain(r => r.Contains("the database refused the batch"));
    }

    /// <summary>Line n of findings.jsonl is taken as ordinal n — and checked: its text must hash to the finding the database
    /// stored for that ordinal, or a reordered or edited file would have one finding judged under another's identity.</summary>
    [Fact]
    public async Task A_findings_file_whose_line_is_not_the_stored_finding_is_refused_rather_than_assessed_under_another_identity()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, ct: Ct, fileLine: i => FindingJson(1 - i));
        var assessor = ScriptedAssessor.AllSupported();

        var refused = await rig.Pass(assessor).RunAsync(rig.Request(), _ => { }, Ct);

        refused.Reason().Should().Contain("does not hash to the finding the database stored");
        assessor.Prompts.Should().BeEmpty("nothing is shown to an assessor under an identity that is not its own");
    }

    [Fact]
    public async Task A_second_pass_of_the_same_assessor_while_one_runs_is_refused()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 1, ct: Ct);
        await using var held = (await rig.Files.LockAssessorAsync(AssessRig.Assessor().Id, Ct)).Ok();

        (await rig.Pass(ScriptedAssessor.AllSupported()).RunAsync(rig.Request(), _ => { }, Ct)).Reason().Should().Contain("another assessment pass");
    }
}
