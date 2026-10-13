using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Contracts;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary><see cref="ScoutReport"/> from a hand-built run: every number the report prints is computed by hand beside the test; a preliminary
/// run says "preliminary" where a decision run gives its verdict; and the JSON is byte-identical across two computations, whatever order the
/// store hands the rows back in.</summary>
public sealed class ScoutReportTests
{
    private static readonly ScoutArmId[] AllSix = [ScoutArmId.A1, ScoutArmId.B1, ScoutArmId.A2, ScoutArmId.B2, ScoutArmId.A3, ScoutArmId.B3];

    /// <summary>The run: tasks t01 (org/repo-a) and t02 (org/repo-b), the given arms, one repeat; every cell scored with recall
    /// <paramref name="found"/>/2, one scout with 2 claims (1 wrong, 1 right), team cost $0.01, team time 5 000 ms, one attempt costing $0.03.</summary>
    private static (ScoutRun Run, IReadOnlyList<ScoutTask> Tasks, IReadOnlyList<ScoutCell> Cells, IReadOnlyList<ScoutCellRecord> Records,
        IReadOnlyList<ScoutAssessedClaim> Verdicts, IReadOnlyList<ScoutAttempt> Attempts) HandBuilt(ScoutRunKind kind, IReadOnlyList<ScoutArmId> arms, Func<ScoutArmId, long> found)
    {
        IReadOnlyList<ScoutTask> tasks = [Task("t01", "org/repo-a"), Task("t02", "org/repo-b")];
        var run = ScoutRun.Planned(Guid.Parse("01900000-0000-7000-8000-000000000001"), ScoutStoreFixtures.Noon, ScoutStoreFixtures.Pins(), 7, 1, arms,
            [.. tasks.Select(t => t.Id)], 10m).Ok() with
        { Kind = kind };
        var schedule = ScoutSchedule.Plan([.. tasks.Select(t => t.Id)], arms, [], 1, 7).Ok();
        var cells = schedule.Select((c, i) => Settled(ScoutCell.Pending(Guid.Parse($"01900000-0000-7000-8000-{i + 100:000000000000}"), run.Id, c), found(c.Arm))).ToList();
        var records = cells.Select(c => new ScoutCellRecord(c.Id, ScoutStoreFixtures.Scout(1) with { ClaimsParsed = 2, KeyFiles = ["src/Store.cs"] })).ToList();
        var verdicts = cells.SelectMany(c => new[] { Claim(run.Id, c.Id, 1, ScoutClaimVerdict.Wrong), Claim(run.Id, c.Id, 2, ScoutClaimVerdict.Right) }).ToList();
        var attempts = cells.Select(c => new ScoutAttempt(Guid.CreateVersion7(), run.Id, c.Id, 1, 1.2m, ScoutAttemptState.Settled, 0.03m, ScoutStoreFixtures.Noon)).ToList();

        return (run, tasks, cells, records, verdicts, attempts);
    }

    private static ScoutRunReportDto Report(ScoutRunKind kind, IReadOnlyList<ScoutArmId> arms, Func<ScoutArmId, long> found)
    {
        var (run, tasks, cells, records, verdicts, attempts) = HandBuilt(kind, arms, found);
        return ScoutReport.Of(run, tasks, cells, records, verdicts, attempts, BootstrapSettings.Default);
    }

    [Fact]
    public void The_report_of_a_hand_built_preliminary_run_carries_the_hand_computed_numbers_and_no_verdict()
    {
        // Hand table — arms A1 and B1 over t01 (repo-a) and t02 (repo-b), every cell: recall found/2, 1 wrong of 2 decided, team $0.01:
        //   A1: found 1 → recall 0.5;  wrong per 10 = 1/2 × 10 = 5;  $ per right file = 0.01 / 1 = 0.01
        //   B1: found 2 → recall 1.0;  wrong per 10 = 5;             $ per right file = 0.01 / 2 = 0.005
        //   W1: Δrecall = +0.5 on both tasks → point +0.5, two repositories, every resample +0.5 → [+0.5, +0.5]; Δwrong = 0
        //   W2, W3: not in the run → workload-missing.   Ledger: 4 attempts × $0.03 settled.
        var report = Report(ScoutRunKind.Preliminary, [ScoutArmId.A1, ScoutArmId.B1], arm => arm == ScoutArmId.A1 ? 1 : 2);

        report.Kind.Should().Be("preliminary");
        report.Verdict.Should().Be("preliminary");
        var w1 = report.Workloads.Single(w => w.Workload == "W1");
        w1.Should().Match<ScoutWorkloadDto>(w => w.ArmA == "A1" && w.ArmB == "B1" && w.Outcome == "preliminary" && w.Reasons.Count == 0);
        w1.Recall.Should().Be(new ScoutIntervalDto(true, 0.5, 0.5, 0.5, 2, 2, true));
        w1.WrongPerTen.Should().Be(new ScoutIntervalDto(true, 0, 0, 0, 2, 2, true));
        report.Workloads.Where(w => w.Workload != "W1").Should().OnlyContain(w => w.Reasons.SequenceEqual(new[] { "workload-missing" }));

        var (a1, b1) = (report.Arms.Single(a => a.Arm == "A1"), report.Arms.Single(a => a.Arm == "B1"));
        (a1.Recall.Value, a1.WrongPerTen.Value, a1.UsdPerRightFile.Value, a1.TeamUsd.Value).Should().Be((0.5, 5.0, 0.01, 0.01));
        (b1.Recall.Value, b1.WrongPerTen.Value, b1.UsdPerRightFile.Value).Should().Be((1.0, 5.0, 0.005));
        a1.Counts.Should().Be(new ScoutCountsDto(2, 2, 0, 0, 0, 0, 0, 2, 2, 4, 0, 0, 0), "the fixture scout carries one unparseable claim line per cell");
        a1.WrongGap.Should().Be("none");
        report.Ledger.Should().Be(new ScoutLedgerDto(10m, 0m, 0m, 0m, 0m, 10m, 4, 0), "the hand-built run row carries an untouched ledger; the attempts are counted beside it");
        report.Tasks.Select(t => (t.Task, t.Repository, t.GroundTruth)).Should().Equal(("t01", "org/repo-a", 2), ("t02", "org/repo-b", 2));
        ScoutReportText.Of(report).Should().Contain("verdict        preliminary — not a verdict").And.Contain("Δrecall +0.5 [+0.5, +0.5] over 2 task(s) in 2 repo(s)");
    }

    [Fact]
    public void A_decision_run_whose_three_workloads_pass_shows_the_rule_change_and_the_same_numbers_as_a_preliminary_run_do_not()
    {
        var decision = Report(ScoutRunKind.Decision, AllSix, _ => 2);
        var preliminary = Report(ScoutRunKind.Preliminary, AllSix, _ => 2);

        decision.Verdict.Should().Be("rule-change-shown");
        decision.Workloads.Should().OnlyContain(w => w.Outcome == "pass");
        preliminary.Verdict.Should().Be("preliminary");
        preliminary.Workloads.Select(w => w.Recall).Should().Equal(decision.Workloads.Select(w => w.Recall), "a preliminary run still shows every interval");
        ScoutReportText.Of(decision).Should().Contain("RULE CHANGE SHOWN");
    }

    [Fact]
    public void The_json_is_byte_identical_across_two_computations_and_whatever_order_the_rows_come_back_in()
    {
        var (run, tasks, cells, records, verdicts, attempts) = HandBuilt(ScoutRunKind.Preliminary, AllSix, arm => arm.ToString().StartsWith('A') ? 1 : 2);

        var first = ScoutsReadCommand.Json(ScoutReport.Of(run, tasks, cells, records, verdicts, attempts, BootstrapSettings.Default));
        var second = ScoutsReadCommand.Json(ScoutReport.Of(run, [.. tasks.Reverse()], [.. cells.Reverse()], [.. records.Reverse()], [.. verdicts.Reverse()], [.. attempts.Reverse()], BootstrapSettings.Default));

        second.Should().Be(first);
        first.Should().Contain("\"verdict\":\"preliminary\"");
    }

    private static ScoutTask Task(string id, string repository) =>
        ScoutTask.Frozen(ScoutTaskId.Parse(id).Ok(), repository, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.HowEndToEnd, "How does a cell reach its settlement after a crash?", ["src/Store.cs", "src/Sweep.cs"], [], [], new string('c', 64)).Ok();

    private static ScoutCell Settled(ScoutCell cell, long found) => cell with
    {
        Claim = Claimable.Stored(CellState.Settled, 1, WorkerIdentity.Nobody, ScoutStoreFixtures.Noon),
        Pin = ScoutStoreFixtures.Pin(),
        Outcome = ScoutCellOutcome.Scored,
        Facts = new ScoutCellFacts(CapturedCount.Number(found), CapturedCount.Number(2), CapturedCount.Number(5_000), CapturedCount.Number(20_000),
            CapturedUsd.Amount(0.03m), CapturedUsd.Amount(0.03m))
        { TeamUsd = CapturedUsd.Amount(0.01m), TeamTokens = CapturedTokens.Of(new Domain.Probes.TokenUsage(4, 50, 0, 200, 0)) },
    };

    private static ScoutAssessedClaim Claim(Guid runId, Guid cellId, int index, ScoutClaimVerdict verdict) =>
        new(new ScoutClaimKey(cellId, 1, index), runId, "src/Store.cs", CapturedCount.Number(3), new string('e', 64), verdict, "gpt-test", ScoutAssessment.RubricSha256, ScoutStoreFixtures.Noon);
}
