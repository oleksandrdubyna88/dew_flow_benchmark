using Bench.Application;
using Bench.Application.Bank;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench questions harvest --statement-file …` — the landing half
/// (todo/PLAN_investigate_vs_implement.md §3.5): the candidate reaches the bank as `Proposed`, its
/// payload round-trips through the codec, and a FAILED gate refuses the landing with the gate's own
/// verdict rather than banking a task nobody proved.</summary>
[Collection("postgres")]
public sealed class HarvestLandingTests(PostgresFixture postgres) : IDisposable
{
    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_gated_fix_lands_as_a_Proposed_code_task_with_its_payload_whole()
    {
        var groupKey = $"code-{Unique()}";
        var bank = new PostgresQuestionBank(postgres.NewContext());
        (await bank.AddGroupAsync(QuestionGroup.Create(groupKey, "Code", 900).Ok(), Ct)).Ok();

        var (baseSha, fixSha) = await FixtureAsync();
        using var statement = new TempStatement("Retries stop honouring the cap after the first attempt.");

        var (code, output, error) = await RunAsync(bank,
            "questions", "harvest", "--repo", "https://example.invalid/x.git", "--commit", fixSha,
            "--statement-file", statement.Path, "--statement-author", "operator",
            "--group", groupKey,
            "--gate-build", "git;--version", "--gate-test", "git;grep;-q;FIXED;--;src/Policy.cs");

        error.Should().BeEmpty();
        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("gates    passed").And.Contain("landed");

        var entry = (await bank.QuestionsAsync(new BankQuery(groupKey), Ct)).Ok()
            .Should().ContainSingle().Subject;
        entry.Question.Kind.Should().Be(TaskKind.Fix);
        entry.Question.State.Should().Be(CandidateState.Proposed, "harvest proposes; only the panel vouches");
        entry.Question.Question.Prompt.Should().Contain("Retries stop honouring the cap");
        entry.Question.Question.Expectations.Should().Contain(e => e.Anchor.FilePath == "src/Policy.cs");

        var task = CodeTaskCodec.Read(entry.Question.CodeTaskJson).Ok();
        task.BaseCommit.Value.Should().Be(baseSha);
        task.FixCommit.Value.Should().Be(fixSha);
        task.GatesRan.Should().BeTrue();
        task.Mechanism.Should().BeEmpty("the mechanism is authored later, never invented at harvest");
        entry.Question.Question.ReferenceAnswer.Should().Contain("--- a/src/Policy.cs",
            "the reference is the reference FIX itself — what the diagnosis judge reads");
    }

    [Fact]
    public async Task A_failed_gate_refuses_the_landing_and_the_bank_stays_empty()
    {
        var groupKey = $"code-{Unique()}";
        var bank = new PostgresQuestionBank(postgres.NewContext());
        (await bank.AddGroupAsync(QuestionGroup.Create(groupKey, "Code", 901).Ok(), Ct)).Ok();

        var (_, fixSha) = await FixtureAsync();
        using var statement = new TempStatement("A symptom.");

        var (code, _, error) = await RunAsync(bank,
            "questions", "harvest", "--repo", "https://example.invalid/x.git", "--commit", fixSha,
            "--statement-file", statement.Path, "--statement-author", "operator",
            "--group", groupKey,
            // OLD is present at BASE, so the "test" passes on the buggy tree — the red gate must refuse.
            "--gate-build", "git;--version", "--gate-test", "git;grep;-q;OLD;--;src/Policy.cs");

        code.Should().Be(ExitCodes.Regression);
        error.Should().Contain("Nothing landed");
        (await bank.QuestionsAsync(new BankQuery(groupKey), Ct)).Ok().Should().BeEmpty();
    }

    [Fact]
    public async Task Landing_without_an_author_is_refused_before_anything_runs()
    {
        var bank = new PostgresQuestionBank(postgres.NewContext());
        var (_, fixSha) = await FixtureAsync();
        using var statement = new TempStatement("A symptom.");

        var (code, _, error) = await RunAsync(bank,
            "questions", "harvest", "--repo", "https://example.invalid/x.git", "--commit", fixSha,
            "--statement-file", statement.Path, "--no-gates");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--statement-author");
    }

    [Fact]
    public async Task An_ungated_landing_says_so_in_the_stored_payload()
    {
        var groupKey = $"code-{Unique()}";
        var bank = new PostgresQuestionBank(postgres.NewContext());
        (await bank.AddGroupAsync(QuestionGroup.Create(groupKey, "Code", 902).Ok(), Ct)).Ok();

        var (_, fixSha) = await FixtureAsync();
        using var statement = new TempStatement("A symptom.");

        var (code, output, _) = await RunAsync(bank,
            "questions", "harvest", "--repo", "https://example.invalid/x.git", "--commit", fixSha,
            "--statement-file", statement.Path, "--statement-author", "operator",
            "--group", groupKey, "--no-gates");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("SKIPPED");

        var entry = (await bank.QuestionsAsync(new BankQuery(groupKey), Ct)).Ok().Single();
        var task = CodeTaskCodec.Read(entry.Question.CodeTaskJson).Ok();
        task.GatesRan.Should().BeFalse();
        task.GateDetail.Should().Contain("no-gates", "an ungated candidate must carry its own warning label");
    }

    private async Task<(string BaseSha, string FixSha)> FixtureAsync()
    {
        var baseSha = await _repo.InitAsync(
            ("src/Policy.cs", "class Policy { /* OLD */ }\n"), ("seed", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitManyAsync(
            [
                ("src/Policy.cs", "class Policy { /* FIXED */ }\n"),
                ("tests/PolicyTests.cs", "class PolicyTests { }\n"),
            ],
            ("fix: the marker", "2026-08-11T09:30:00"));

        return (baseSha, fixSha);
    }

    private async Task<(int Code, string Output, string Error)> RunAsync(IQuestionBank bank, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await HarvestCommand.RunAsync(
            CommandLine.Parse(args), new RepoAsCheckout(_repo.Root), bank, TimeProvider.System, output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private sealed class RepoAsCheckout(string tree) : ICheckoutProvider
    {
        public Task<Outcome<string>> EnsureAsync(MeasurementTarget target, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success(tree));
    }

    private sealed class TempStatement : IDisposable
    {
        public TempStatement(string text)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-stmt-{Guid.NewGuid():N}.md");
            File.WriteAllText(Path, text);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }

    public void Dispose() => _repo.Dispose();
}
