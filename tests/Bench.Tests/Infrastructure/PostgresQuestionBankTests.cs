using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The question bank against a real Postgres.
/// <para>
/// Every rule worth testing here is a rule about CONCURRENCY or about a unique index — one mark per
/// reviewer per question, one suite-facing id in the whole bank, a snapshot written once. A fake store
/// would only prove that the fake agrees with what we assumed the database does, which is the assumption
/// under test.
/// </para></summary>
[Collection("postgres")]
public sealed class PostgresQuestionBankTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly CommitSha Commit = CommitSha.Parse(new string('d', 40)).Ok();

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_imported_file_lands_its_groups_its_reviewers_its_questions_and_their_marks()
    {
        var bank = Bank();
        var suffix = Unique();

        var report = await ImportAsync(bank, File(suffix));

        report.Refusals.Should().BeEmpty();
        report.Groups.Should().Be(1);
        report.Reviewers.Should().Be(2);
        report.Questions.Should().Be(2);
        report.Reviews.Should().Be(3);

        var listed = (await bank.QuestionsAsync(new BankQuery($"lookup-{suffix}"), Ct)).Ok();
        listed.Should().HaveCount(2);
        listed[0].Question.Question.Expectations.Should().ContainSingle(
            "the expectations survive the round trip through the same wire shape a suite file uses");
        listed[0].Question.Seed.At.Should().Be(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            "the seed's date is the memorisation check's only input — it is not the import date");
    }

    [Fact]
    public async Task A_question_id_already_in_the_bank_is_refused_and_the_rest_of_the_import_still_lands()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));

        // The operator re-runs a command they were not sure took — the normal case, not an exotic one.
        var second = await ImportAsync(bank, File(suffix));

        second.Questions.Should().Be(0);
        second.Refusals.Should().HaveCountGreaterThan(0);
        second.Refusals.Should().Contain(r => r.Contains("already in the bank"));
        (await bank.QuestionsAsync(new BankQuery($"lookup-{suffix}"), Ct)).Ok()
            .Should().HaveCount(2, "an import that refuses a row must not duplicate the ones it took before");
    }

    [Fact]
    public async Task A_question_naming_a_group_nobody_created_costs_that_question_and_nothing_more()
    {
        var bank = Bank();
        var suffix = Unique();
        var file = File(suffix) with
        {
            Questions = [.. File(suffix).Questions, Question($"orphan-{suffix}", "no-such-group", 9, "A.Orphan")],
        };

        var report = await ImportAsync(bank, file);

        report.Questions.Should().Be(2, "an import of two hundred questions must not be lost over one bad row");
        report.Refusals.Should().Contain(r => r.Contains("not in the bank"));
    }

    [Fact]
    public async Task One_reviewer_marks_a_question_once_and_changing_their_mind_replaces_that_mark()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));

        await bank.ReviewAsync($"q1-{suffix}", $"claude-{suffix}", ReviewVerdict.Rejected, "on reflection, ambiguous", Noon, Ct);

        var question = (await bank.QuestionsAsync(new BankQuery($"lookup-{suffix}"), Ct)).Ok()
            .Single(e => e.Question.Question.Id == $"q1-{suffix}");
        var reviews = (await bank.ReviewsAsync([question.Question.Id], Ct)).Ok();

        reviews.Should().HaveCount(2, "the other reviewer's mark is untouched — 'two of three approved' has to stay representable");
        reviews.Should().ContainSingle(r => r.Verdict == ReviewVerdict.Rejected)
            .Which.Note.Should().Be("on reflection, ambiguous");
    }

    [Fact]
    public async Task A_mark_by_a_reviewer_who_is_not_in_the_bank_is_refused_by_name()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));

        var refused = await bank.ReviewAsync($"q1-{suffix}", "nobody", ReviewVerdict.Approved, "", Noon, Ct);

        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("no reviewer").And.Contain("a reviewer is a row");
    }

    [Fact]
    public async Task Only_accepted_questions_are_selectable()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));

        var all = (await bank.QuestionsAsync(new BankQuery($"lookup-{suffix}"), Ct)).Ok();
        var selectable = (await bank.QuestionsAsync(BankQuery.Selection($"lookup-{suffix}", 0, 0), Ct)).Ok();

        all.Should().HaveCount(2);
        selectable.Should().ContainSingle().Which.Question.Question.Id.Should().Be($"q1-{suffix}");

        await bank.SetStateAsync($"q2-{suffix}", CandidateState.Accepted, Ct);
        (await bank.QuestionsAsync(BankQuery.Selection($"lookup-{suffix}", 0, 0), Ct)).Ok()
            .Should().HaveCount(2, "accepting a question is what puts it in reach of a test");
    }

    [Fact]
    public async Task A_selection_reads_the_group_and_the_ordinal_range_an_operator_quotes()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));
        await bank.SetStateAsync($"q2-{suffix}", CandidateState.Accepted, Ct);

        var first = (await bank.QuestionsAsync(BankQuery.Selection($"lookup-{suffix}", 1, 1), Ct)).Ok();

        first.Should().ContainSingle().Which.Question.Ordinal.Should().Be(1);
    }

    [Fact]
    public async Task A_moved_question_keeps_its_history_so_a_finished_report_can_explain_itself()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));
        (await bank.AddGroupAsync(QuestionGroup.Create($"adversarial-{suffix}", "Adversarial", 2).Ok(), Ct))
            .Failed().Should().BeFalse();

        var moved = await bank.MoveAsync($"q1-{suffix}", $"adversarial-{suffix}", "it is a trap question, not a lookup", Noon, Ct);

        moved.Ok().From.Value.Should().Be($"lookup-{suffix}");
        moved.Ok().To.Value.Should().Be($"adversarial-{suffix}");
        (await bank.MovesAsync($"q1-{suffix}", Ct)).Ok().Should().ContainSingle()
            .Which.Reason.Should().Contain("trap question");
        (await bank.QuestionsAsync(new BankQuery($"adversarial-{suffix}"), Ct)).Ok().Should().ContainSingle();
    }

    [Fact]
    public async Task A_tests_selection_is_snapshotted_once_and_read_back_with_its_groups()
    {
        var bank = Bank();
        var suffix = Unique();
        await ImportAsync(bank, File(suffix));

        var entries = (await bank.QuestionsAsync(BankQuery.Selection($"lookup-{suffix}", 0, 0), Ct)).Ok();
        var frozen = BankFreeze.Freeze($"bank-{suffix}", entries).Ok();
        var runId = await SeedRunAsync(frozen.Stamp);

        var store = new PostgresRunQuestionStore(postgres.NewContext());
        (await store.SaveAsync(runId, frozen.Questions, Ct)).Ok().Should().Be(1);

        var again = await store.SaveAsync(runId, frozen.Questions, Ct);
        again.Failed().Should().BeTrue();
        again.Reason().Should().Contain("written once", "a snapshot that could be rewritten would answer with today's opinion");

        var read = (await store.ForRunAsync(runId, Ct)).Ok();
        read.Should().ContainSingle();
        read[0].Group.Value.Should().Be($"lookup-{suffix}");
        read[0].QuestionId.Should().Be($"q1-{suffix}");
    }

    [Fact]
    public async Task A_run_that_selected_nothing_is_refused_rather_than_snapshotted_empty()
    {
        var store = new PostgresRunQuestionStore(postgres.NewContext());

        var refused = await store.SaveAsync(await SeedRunAsync("empty@v1#abc"), [], Ct);

        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("measures nothing");
    }

    private PostgresQuestionBank Bank() => new(postgres.NewContext());

    /// <summary>A per-test suffix, because this store is SHARED across the suite: a bank key is unique in
    /// the database, so two tests naming one group would fail each other rather than themselves — the
    /// shared-store lesson the sweep tests already paid for once.
    /// <para>
    /// <b>Not <c>Guid.CreateVersion7</c>.</b> A v7 guid opens with a millisecond timestamp, so its first
    /// eight hex characters are IDENTICAL for every test that starts in the same moment — which is what
    /// tests in one class do. Six of these tests failed against each other's data before this line said
    /// <c>NewGuid</c>: a "unique" suffix that is a truncated clock is a shared suffix.
    /// </para></summary>
    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private Task<BankImportReport> ImportAsync(IQuestionBank bank, BankFile file) =>
        BankImport.ApplyAsync(bank, file, new TestClock(Noon), Ct);

    private async Task<Guid> SeedRunAsync(string stamp)
    {
        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/x.git").Ok(), Commit);
        var run = BenchRun.Planned("bank", target, EngineRef.Filesystem(), stamp, Noon);

        var cells = Matrix.Plan(
            [new Question("q1", "p", [Expectation.File(SourceAnchor.File("src/A.cs", Commit))], "")],
            repeats: 1,
            [new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1))],
            [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        await postgres.NewStore(new TestClock(Noon)).CreateAsync(run, cells, Ct);

        return run.Id;
    }

    private static BankFile File(string suffix) => new()
    {
        TargetRepo = "https://github.com/App-vNext/Polly.git",
        AuthoredAtCommit = new string('d', 40),
        Groups = [new GroupFile { Key = $"lookup-{suffix}", Title = "Code lookup", Ordinal = 1 }],
        Reviewers =
        [
            new ReviewerFile { Key = $"claude-{suffix}", DisplayName = "Claude", Ordinal = 1 },
            new ReviewerFile { Key = $"codex-{suffix}", DisplayName = "Codex", Ordinal = 2 },
        ],
        Questions =
        [
            Question($"q1-{suffix}", $"lookup-{suffix}", 1, "RetryHelper.Delay") with
            {
                State = "Accepted",
                Reviews =
                [
                    new ReviewFile { Reviewer = $"claude-{suffix}", Verdict = "Approved" },
                    new ReviewFile { Reviewer = $"codex-{suffix}", Verdict = "Approved", Note = "clear" },
                ],
            },
            Question($"q2-{suffix}", $"lookup-{suffix}", 2, "RetryHelper.Jitter") with
            {
                Reviews = [new ReviewFile { Reviewer = $"claude-{suffix}", Verdict = "Rejected", Note = "two questions in one" }],
            },
        ],
    };

    private static BankQuestionFile Question(string id, string group, int ordinal, string member) => new()
    {
        Group = group,
        Ordinal = ordinal,
        Kind = "Reading",
        Source = "RepositoryHistory",
        AuthorModel = "opus-5",
        Seed = new SeedFile { Kind = "pull-request", Reference = "#1234", At = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero) },
        Id = id,
        Prompt = $"where is {member} computed?",
        ReferenceAnswer = "in RetryHelper",
        Expectations =
        [
            new ExpectationFile { Kind = "Member", File = "src/Polly.Core/Retry/RetryHelper.cs", Member = member, Start = 75, End = 111 },
        ],
    };
}
