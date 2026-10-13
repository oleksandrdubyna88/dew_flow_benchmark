using Bench.Application.Scouts;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Runs;
using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.13 / E2 S4: every PUBLIC writer takes the one projection (<see cref="ScoutPublication"/>), so a private task's repository url,
/// statement, ground truth, removals and KEY FILES reach none of them — the report object, its JSON and its text, <c>tasks.json</c>, the
/// hand-check Markdown, the <c>cells.jsonl</c> and RESULTS writers. The planted strings below are NOT on any deny-list: the scrubber would
/// not catch them, only the projection does. Written RED first against E1's writers, which published the url (<c>ScoutReport.cs:114</c>).</summary>
public sealed class ScoutPrivateProjectionTests
{
    private const string Url = "file:////wsl.localhost/Ubuntu/home/someone/git/zqvtharnish-repo";

    private const string Statement = "Where is the per-tenant rate limit decided for the Vrembolix gateway?";

    private static readonly string[] Truth = ["src/Zqvtharnish/Kleebor.php", "src/Zqvtharnish/Throttle.php", "tests/KleeborTest.php"];

    private static readonly string[] Planted = [Url, "zqvtharnish", "Kleebor", "Throttle.php", "Vrembolix", Statement];

    private static ScoutTask Private() =>
        ScoutTask.Frozen(ScoutTaskId.Parse("p03").Ok(), Url, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, Statement, Truth, [], ["todo/PLAN_kleebor.md"], new string('c', 64), ScoutTaskPrivacy.Private, "PHP").Ok()
        with
        { Commitments = new ScoutTaskCommitments(new string('1', 64), new string('2', 64)) };

    private static ScoutTask Public() => ScoutStoreFixtures.FrozenTask("t01");

    [Fact]
    public void A_private_task_freezes_with_its_label_language_and_privacy_and_a_named_id_or_no_language_is_refused()
    {
        var task = Private();

        (task.Privacy, task.Language, task.IsPrivate, task.Repository).Should().Be((ScoutTaskPrivacy.Private, "PHP", true, Url));
        Freeze("kleebor-task", "PHP").Reason().Should().Contain("task kleebor-task").And.Contain("label").And.NotContain(Statement);
        Freeze("p03", string.Empty).Reason().Should().Contain("names its language");
        Freeze("p03", "not a language!").Reason().Should().Contain("not a language");
        ScoutStoreFixtures.FrozenTask("t01").Privacy.Should().Be(ScoutTaskPrivacy.Public, "every task of E1 is public");
    }

    /// <summary>E2 S8: the screened private pool holds about 1 080 admissible candidates and every candidate's id IS its label, so a label reaches
    /// four digits — and still nothing but a 'p' and digits. Written RED first: <c>p1000</c> was refused under the two-or-three-digit rule.</summary>
    [Theory]
    [InlineData("p01", true)]
    [InlineData("p999", true)]
    [InlineData("p1000", true)]
    [InlineData("p9999", true)]
    [InlineData("p1", false)]
    [InlineData("p12345", false)]
    [InlineData("q100", false)]
    [InlineData("P100", false)]
    [InlineData("p10a", false)]
    public void A_private_label_is_a_p_and_two_to_four_digits(string id, bool legal)
    {
        ScoutTaskPrivacies.IsLabel(id).Should().Be(legal);
        ScoutTaskPrivacies.Refusal(ScoutTaskPrivacy.Private, id, "PHP").Should().Be(legal ? string.Empty : $"task {id}: {ScoutTaskPrivacies.LabelForms}");
    }

    [Fact]
    public void A_private_statement_that_leaks_is_refused_by_label_and_token_count_never_by_token()
    {
        var refusal = ScoutTask.Frozen(ScoutTaskId.Parse("p03").Ok(), Url, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, "Why does Kleebor.php throttle?", Truth, ["kleeborThrottle"], [], new string('c', 64), ScoutTaskPrivacy.Private, "PHP").Reason();

        refusal.Should().Be("task p03 (private): the statement leaks the answer — 3 token(s), withheld from every public writer");
        ScoutTask.Leaks("Why does Kleebor.php throttle?", Truth, ["kleeborThrottle"]).Select(l => l.Kind)
            .Should().Equal([LeakKind.Basename, LeakKind.Stem, LeakKind.Stem], "the basename, its stem, and Throttle.php's stem as a whole word");
    }

    [Fact]
    public void The_report_names_a_private_task_by_its_label_and_language_and_its_json_and_text_carry_nothing_planted()
    {
        var (run, tasks, cells, records, verdicts, attempts) = Run(Private(), Public());

        var report = ScoutReport.Of(run, tasks, cells, records, verdicts, attempts, BootstrapSettings.Default);

        var p03 = report.Tasks.Single(t => t.Task == "p03");
        (p03.Repository, p03.Privacy, p03.Language, p03.GroundTruth).Should().Be(("p03", "private", "PHP", 3));
        report.Tasks.Single(t => t.Task == "t01").Should().Match<Contracts.ScoutTaskReportDto>(t => t.Repository == "example-org/example-repo" && t.Privacy == "public");
        AssertClean(ScoutsReadCommand.Json(report));
        AssertClean(ScoutReportText.Of(report));
        ScoutReportText.Of(report).Should().Contain("task p03 (private · PHP");
    }

    [Fact]
    public void Tasks_json_carries_a_private_task_as_label_language_sizes_and_commitments_and_the_private_file_carries_the_rest()
    {
        var frozen = new ScoutFiles.FrozenTask(Private(), ["kleeborThrottle"]);

        var json = ScoutFiles.WriteTasks([frozen, new ScoutFiles.FrozenTask(Public(), [])], "scouts-preliminary-2026-10-12", "cutoffs.json");

        AssertClean(json);
        json.Should().Contain("\"privacy\": \"private\"").And.Contain("\"language\": \"PHP\"").And.Contain("\"groundTruthCount\": 3")
            .And.Contain($"\"statementCommitment\": \"{new string('1', 64)}\"").And.Contain("\"campaign\": \"scouts-preliminary-2026-10-12\"")
            .And.Contain("example-org/example-repo", "a public task keeps its repository");
        var reread = ScoutFiles.Tasks(json);
        reread.Reason().Should().Contain("p03").And.Contain("--private-tasks-file", "the public file alone cannot freeze a private task");

        var privateJson = ScoutFiles.WritePrivateTasks([frozen]);
        privateJson.Should().Contain(Statement).And.Contain(Url).And.Contain("kleeborThrottle");
        ScoutFiles.PrivateTasks(privateJson).Ok().Should().ContainSingle().Which.Task.Statement.Should().Be(Statement);
    }

    [Fact]
    public void The_hand_check_markdown_shows_a_private_item_by_label_and_commitment_and_the_companion_holds_the_plaintext()
    {
        var cell = Guid.Parse("01900000-0000-7000-8000-000000000050");
        var pairs = ScoutHandcheck.PairsOf(cell, "p03", Truth, ["src/Zqvtharnish/Kleebor.php", "README.md"], Truth, "C:/work/tree");
        var claim = new HandcheckClaim(new ScoutClaimKey(cell, 1, 1), "p03", ScoutOutput.Claims("CLAIMS\n- src/Zqvtharnish/Kleebor.php:3 — defines the Kleebor throttle").Claims[0], ScoutClaimVerdict.Wrong);
        var draw = ScoutHandcheck.Draw(pairs, [claim], 3, 5);
        var committer = new ScoutCommitter(Infrastructure.GateStoreFixtures.Key, "scouts-preliminary-2026-10-12");

        var markdown = ScoutHandcheck.Markdown(Guid.Empty, draw, new ScoutHandcheckPrivacy(new HashSet<string>(StringComparer.Ordinal) { "p03" }, committer));
        var companion = ScoutHandcheck.PrivateCompanion(Guid.Empty, draw, new HashSet<string>(StringComparer.Ordinal) { "p03" });

        AssertClean(markdown);
        markdown.Should().Contain("Task `p03` (private)").And.Contain(committer.GroundTruthFile("p03", "src/Zqvtharnish/Kleebor.php"))
            .And.Contain(committer.Claim("p03", "defines the Kleebor throttle")).And.Contain("- [ ] agree");
        companion.Should().Contain("src/Zqvtharnish/Kleebor.php").And.Contain("defines the Kleebor throttle").And.Contain("### F01");
    }

    [Fact]
    public void Cells_jsonl_and_the_results_skeleton_carry_a_private_task_without_its_paths_and_a_public_one_with_them()
    {
        var (run, tasks, cells, records, verdicts, attempts) = Run(Private(), Public());
        var report = ScoutReport.Of(run, tasks, cells, records, verdicts, attempts, BootstrapSettings.Default);

        var jsonl = ScoutRecordFiles.CellsJsonl(run, tasks, cells, records, verdicts, ScrubDenyList.None).Ok();
        var skeleton = ScoutRecordFiles.ResultsSkeleton(report);

        AssertClean(jsonl);
        AssertClean(skeleton);
        jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(cells.Count, "one line per cell");
        jsonl.Should().Contain("\"task\":\"p03\"").And.Contain("\"keyFiles\":[]").And.Contain("src/Store.cs", "a public task's KEY FILES are repository-relative paths");
        skeleton.Should().Contain("preliminary — not a verdict").And.Contain("| p03 | p03 (private · PHP) |").And.Contain("| t01 | example-org/example-repo |");
    }

    /// <summary>Review finding (E2 code round): a cell whose task the run does not hold was written as a line marked <i>private</i> — the missing
    /// task read as the safe default, so a defect in the join became a committed record. It is a typed failure naming the task, and nothing is
    /// written.</summary>
    [Fact]
    public void A_cell_whose_task_the_run_does_not_hold_is_a_typed_failure_naming_the_task_never_a_line_marked_private()
    {
        var (run, _, cells, records, verdicts, _) = Run(Private(), Public());

        var jsonl = ScoutRecordFiles.CellsJsonl(run, [Private()], cells, records, verdicts, ScrubDenyList.None);

        (jsonl is Outcome<string>.Ok ok ? ok.Value : string.Empty).Should().NotContain("\"task\":\"t01\",\"privacy\":\"private\"", "a missing task is never a private line");
        jsonl.Reason().Should().Contain("t01").And.Contain("not among the run's 1 task(s)").And.Contain("nothing was written");
    }

    private static void AssertClean(string text)
    {
        foreach (var planted in Planted)
        {
            text.Should().NotContain(planted, "a private task's values reach no public writer");
        }
    }

    private static Bench.Domain.Outcome<ScoutTask> Freeze(string id, string language) =>
        ScoutTask.Frozen(ScoutTaskId.Parse(id).Ok(), Url, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, Statement, Truth, [], [], new string('c', 64), ScoutTaskPrivacy.Private, language);

    /// <summary>A settled run over the given tasks × A1, every cell scored, one scout naming its task's first G file and <c>README.md</c>.</summary>
    private static (ScoutRun Run, IReadOnlyList<ScoutTask> Tasks, IReadOnlyList<ScoutCell> Cells, IReadOnlyList<ScoutCellRecord> Records,
        IReadOnlyList<ScoutAssessedClaim> Verdicts, IReadOnlyList<ScoutAttempt> Attempts) Run(params ScoutTask[] tasks)
    {
        var ids = tasks.Select(t => t.Id).ToList();
        var run = ScoutRun.Planned(Guid.Parse("01900000-0000-7000-8000-000000000001"), ScoutStoreFixtures.Noon, ScoutStoreFixtures.Pins(), 7, 1, [ScoutArmId.A1], ids, 10m).Ok()
            with
        { Campaign = "scouts-preliminary-2026-10-12" };
        var schedule = ScoutSchedule.Plan(ids, [ScoutArmId.A1], [], 1, 7).Ok();
        var cells = schedule.Select((c, i) => ScoutCell.Pending(Guid.Parse($"01900000-0000-7000-8000-{i + 100:000000000000}"), run.Id, c) with
        {
            Claim = Claimable.Stored(CellState.Settled, 1, WorkerIdentity.Nobody, ScoutStoreFixtures.Noon),
            Pin = ScoutStoreFixtures.Pin(),
            Outcome = ScoutCellOutcome.Scored,
            Facts = new ScoutCellFacts(CapturedCount.Number(1), CapturedCount.Number(3), CapturedCount.Number(5_000), CapturedCount.Number(20_000), CapturedUsd.Amount(0.03m), CapturedUsd.Amount(0.03m))
            { TeamUsd = CapturedUsd.Amount(0.01m), TeamTokens = CapturedTokens.Of(new Domain.Probes.TokenUsage(4, 50, 0, 200, 0)) },
        }).ToList();
        var records = cells.Select(c => new ScoutCellRecord(c.Id, ScoutStoreFixtures.Scout(1) with
        {
            KeyFiles = [tasks.Single(t => t.Id.Value == c.Task.Value).GroundTruth[0], "README.md"],
        })).ToList();
        var verdicts = cells.Select(c => new ScoutAssessedClaim(new ScoutClaimKey(c.Id, 1, 1), run.Id, tasks.Single(t => t.Id.Value == c.Task.Value).GroundTruth[0], CapturedCount.Number(3),
            new string('e', 64), ScoutClaimVerdict.Wrong, "gpt-test", ScoutAssessment.RubricSha256, ScoutStoreFixtures.Noon)).ToList();
        var attempts = cells.Select(c => new ScoutAttempt(Guid.CreateVersion7(), run.Id, c.Id, 1, 1.2m, ScoutAttemptState.Settled, 0.03m, ScoutStoreFixtures.Noon)).ToList();

        return (run, tasks, cells, records, verdicts, attempts);
    }
}
