using Bench.Application;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Targets;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench questions harvest` — the report-only first form
/// (todo/PLAN_investigate_vs_implement.md §3.5): point it at a fix commit, read back the derived
/// candidate material. Nothing lands and no gate runs yet, and the printout says so — a verb that
/// looked like it had banked a task would be worse than no verb.</summary>
public sealed class HarvestCommandTests : IDisposable
{
    private const string Repo = "https://example.invalid/x.git";

    private readonly DatedGitRepo _repo;

    public HarvestCommandTests() => _repo = new DatedGitRepo(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_fix_is_harvested_and_printed_with_its_derived_material()
    {
        await _repo.InitAsync(
            ("src/Policy.cs", "class Policy { int A() => 1; }\n"),
            commit: ("seed", "2026-05-01T12:00:00"));
        await _repo.CommitAsync(
            ("tests/PolicyTests.cs", "class PolicyTests { }\n"),
            commit: ("tests scaffold", "2026-05-02T12:00:00"));
        var fixSha = await _repo.CommitAsync(
            ("src/Policy.cs", "class Policy { int A() => 2; }\n"),
            commit: ("fix: wrong constant", "2026-08-11T09:30:00"));

        var (code, output, _) = await Run(fixSha, new ScriptedCheckouts(_repo.Root, fixSha));

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain(fixSha[..12]).And.Contain("fix: wrong constant");
        output.Should().Contain("2026-08-11", "the seed rides the printout — derived, never typed");
        output.Should().Contain("src/Policy.cs", "the causal anchor is the whole point of the harvest");
        output.Should().Contain("printed only", "nothing landed and no gate ran, and the verb must say so");
    }

    [Fact]
    public async Task A_fix_that_only_touched_tests_is_no_candidate_and_exits_NoReport()
    {
        await _repo.InitAsync(("tests/T.cs", "class T { }\n"), commit: ("seed", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitAsync(
            ("tests/T.cs", "class T { int X; }\n"), commit: ("fix: test only", "2026-06-01T12:00:00"));

        var (code, output, _) = await Run(fixSha, new ScriptedCheckouts(_repo.Root, fixSha));

        code.Should().Be(ExitCodes.NoReport, "a fix with no causal anchor gives an investigate arm nothing to score");
        output.Should().Contain("causal   none");
    }

    [Fact]
    public async Task A_merge_commit_is_the_operators_problem_not_the_machines()
    {
        await _repo.InitAsync(("a.txt", "one\n"), commit: ("first", "2026-05-01T12:00:00"));
        await _repo.GitAsync("checkout", "-q", "-b", "side");
        await _repo.CommitAsync(("b.txt", "side\n"), commit: ("side", "2026-05-02T12:00:00"));
        await _repo.GitAsync("checkout", "-q", "-");
        await _repo.CommitAsync(("c.txt", "main\n"), commit: ("main", "2026-05-03T12:00:00"));
        await _repo.GitAsync("merge", "--no-ff", "-q", "-m", "merge side", "side");
        var mergeSha = (await _repo.GitAsync("rev-parse", "HEAD")).Trim();

        var (code, _, error) = await Run(mergeSha, new ScriptedCheckouts(_repo.Root, mergeSha));

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("merge");
    }

    [Fact]
    public async Task An_unset_or_short_commit_is_refused_before_any_checkout()
    {
        var untouched = new ScriptedCheckouts(_repo.Root, expectedSha: string.Empty);

        (await Run(fixSha: string.Empty, untouched)).Code.Should().Be(ExitCodes.Configuration);
        (await Run(fixSha: "abc123", untouched)).Code.Should().Be(ExitCodes.Configuration,
            "the record pins the full sha, so the verb asks for all forty characters");
        untouched.Calls.Should().Be(0, "a refused invocation must not cost a clone");
    }

    [Fact]
    public async Task A_failed_checkout_is_the_environments_answer()
    {
        var (code, _, error) = await Run(new string('a', 40), new ScriptedCheckouts(failure: "network is a rumour"));

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("network is a rumour");
    }

    private async Task<(int Code, string Output, string Error)> Run(string fixSha, ScriptedCheckouts checkouts)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var args = new List<string> { "questions", "harvest", "--repo", Repo };

        if (fixSha.Length > 0)
        {
            args.AddRange(["--commit", fixSha]);
        }

        var code = await HarvestCommand.RunAsync(
            CommandLine.Parse([.. args]), checkouts, new UntouchedBank(), TimeProvider.System, output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }

    public void Dispose() => _repo.Dispose();

    /// <summary>Report-only harvests never reach the bank, and a double that quietly answered would hide
    /// a print-only path that had started landing things.</summary>
    private sealed class UntouchedBank : Bench.Application.Bank.IQuestionBank
    {
        private static NotSupportedException Nope => new("a report-only harvest does not touch the bank");

        public Task<Outcome<Bench.Domain.Bank.QuestionGroup>> AddGroupAsync(Bench.Domain.Bank.QuestionGroup group, CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.Reviewer>> AddReviewerAsync(Bench.Domain.Bank.Reviewer reviewer, CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.BankQuestion>> AddAsync(Bench.Domain.Bank.BankQuestion question, CancellationToken ct) => throw Nope;

        public Task<Outcome<IReadOnlyList<Bench.Domain.Bank.QuestionGroup>>> GroupsAsync(CancellationToken ct) => throw Nope;

        public Task<Outcome<IReadOnlyList<Bench.Domain.Bank.Reviewer>>> ReviewersAsync(CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.Reviewer>> BindReviewerAsync(string reviewerKey, string modelKey, CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.Reviewer>> SetReviewerEnabledAsync(string reviewerKey, bool enabled, CancellationToken ct) => throw Nope;

        public Task<Outcome<IReadOnlyList<Bench.Domain.Bank.BankEntry>>> QuestionsAsync(Bench.Application.Bank.BankQuery query, CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.QuestionReview>> ReviewAsync(string questionId, string reviewerKey, Bench.Domain.Bank.ReviewVerdict verdict, string note, DateTimeOffset at, string modelId, CancellationToken ct) => throw Nope;

        public Task<Outcome<IReadOnlyList<Bench.Domain.Bank.QuestionReview>>> ReviewsAsync(IReadOnlyList<Guid> questionIds, CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.BankQuestion>> SetStateAsync(string questionId, Bench.Domain.Authoring.CandidateState state, CancellationToken ct) => throw Nope;

        public Task<Outcome<Bench.Domain.Bank.GroupMove>> MoveAsync(string questionId, string toGroupKey, string reason, DateTimeOffset at, CancellationToken ct) => throw Nope;

        public Task<Outcome<IReadOnlyList<Bench.Domain.Bank.GroupMove>>> MovesAsync(string questionId, CancellationToken ct) => throw Nope;
    }

    /// <summary>A checkout that hands back the temp repository itself — the verb needs a directory where
    /// git resolves the sha, and the temp repo IS one.</summary>
    private sealed class ScriptedCheckouts(
        string tree = "", string expectedSha = "", string failure = "") : ICheckoutProvider
    {
        public int Calls { get; private set; }

        public Task<Outcome<string>> EnsureAsync(MeasurementTarget target, CancellationToken cancellationToken)
        {
            Calls++;

            if (failure.Length > 0)
            {
                return Task.FromResult(Outcome<string>.Failure(failure));
            }

            if (expectedSha.Length > 0)
            {
                target.Commit.Value.Should().Be(expectedSha, "the verb must check out the FIX commit it was asked about");
            }

            return Task.FromResult(Outcome<string>.Success(tree));
        }
    }
}
