using Bench.Application;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Models;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Domain.Trace;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench solve` — the attended implement leg, end to end over a real repository: the task
/// read from the bank, the reference diagnosis handed in the prompt, the subject's diff extracted and
/// scored by the real signals. The subject here is a fake that answers with the reference fix itself —
/// the ceiling case, which must pass every signal or the instrument is broken.</summary>
[Collection("postgres")]
public sealed class SolveCommandTests(PostgresFixture postgres) : IDisposable
{
    private readonly DatedGitRepo _repo = new(TestContext.Current.CancellationToken);

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_reference_fix_as_the_answer_passes_every_signal()
    {
        var (questionId, solverDiff) = await LandTaskAsync();

        var (code, output, error) = await RunAsync(
            new ScriptedSubject($"Here is the fix.\n\n```diff\n{solverDiff}\n```"),
            "solve", "--question", questionId,
            "--model", "fake-solver", "--model-url", "http://127.0.0.1:1/v1",
            "--gate-build", "git;--version",
            "--gate-test", "git;grep;-q;FIXED;--;src/Policy.cs");

        error.Should().BeEmpty();
        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("ATTENDED").And.Contain("Hidden tests green");
    }

    [Fact]
    public async Task A_reading_question_cannot_be_solved_and_says_so()
    {
        var groupKey = $"solve-{Guid.NewGuid():N}"[..14];
        var bank = new PostgresQuestionBank(postgres.NewContext());
        var group = QuestionGroup.Create(groupKey, "Reading", 905).Ok();
        (await bank.AddGroupAsync(group, Ct)).Ok();

        var commit = CommitSha.Parse(new string('a', 40)).Ok();
        var reading = BankQuestion.Create(
            group.Id, 1, TaskKind.Reading,
            new Question($"read-{groupKey}", "What colour is it?",
                [Expectation.File(SourceAnchor.File("src/F.cs", commit))], "blue"),
            string.Empty, AuthoringSource.Human, string.Empty,
            QuestionSeed.Written("human", "op", DateTimeOffset.UnixEpoch),
            RepoUrl.Parse("https://example.invalid/x.git").Ok(), commit, DateTimeOffset.UtcNow).Ok();
        (await bank.AddAsync(reading, Ct)).Ok();

        var (code, _, error) = await RunAsync(
            new ScriptedSubject("irrelevant"),
            "solve", "--question", $"read-{groupKey}",
            "--model", "fake", "--model-url", "http://127.0.0.1:1/v1",
            "--gate-build", "git;--version", "--gate-test", "git;--version");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("Reading");
    }

    /// <summary>A real base+fix pair in the temp repo, landed in the bank the way harvest lands it —
    /// and the solver's "answer" is the fix's own src diff, captured from git.</summary>
    private async Task<(string QuestionId, string SolverDiff)> LandTaskAsync()
    {
        var baseSha = await _repo.InitAsync(
            ("src/Policy.cs", "class Policy { /* OLD */ }\n"), ("seed", "2026-05-01T12:00:00"));
        var fixSha = await _repo.CommitManyAsync(
            [
                ("src/Policy.cs", "class Policy { /* FIXED */ }\n"),
                ("tests/PolicyTests.cs", "class PolicyTests { }\n"),
            ],
            ("fix: the marker", "2026-08-11T09:30:00"));
        var referenceDiff = await _repo.GitAsync("diff", baseSha, fixSha);
        var solverDiff = await _repo.GitAsync("diff", baseSha, fixSha, "--", "src");

        var groupKey = $"solve-{Guid.NewGuid():N}"[..14];
        var bank = new PostgresQuestionBank(postgres.NewContext());
        var group = QuestionGroup.Create(groupKey, "Code", 906).Ok();
        (await bank.AddGroupAsync(group, Ct)).Ok();

        var baseCommit = CommitSha.Parse(baseSha).Ok();
        var task = CodeTask.Harvested(
            baseCommit, CommitSha.Parse(fixSha).Ok(), referenceDiff, gatesRan: false, "test fixture").Ok();
        var questionId = $"fix-{groupKey}";
        var landed = BankQuestion.Create(
            group.Id, 1, TaskKind.Fix,
            new Question(questionId, "The marker is wrong. Fix it.",
                [new Expectation(ExpectationKind.Member, SourceAnchor.Member("src/Policy.cs", string.Empty, new LineSpan(1, 1), baseCommit), string.Empty, true)],
                string.Empty),
            CodeTaskCodec.Write(task), AuthoringSource.BugsAndTests, "fixture",
            QuestionSeed.Written("commit", fixSha, DateTimeOffset.UnixEpoch),
            RepoUrl.Parse("https://example.invalid/x.git").Ok(), baseCommit, DateTimeOffset.UtcNow).Ok();
        (await bank.AddAsync(landed, Ct)).Ok();

        return (questionId, solverDiff);
    }

    private async Task<(int Code, string Output, string Error)> RunAsync(IModelRuntime subject, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await SolveCommand.RunAsync(
            CommandLine.Parse(args),
            new PostgresQuestionBank(postgres.NewContext()),
            new RepoAsCheckout(_repo.Root),
            subject,
            output, error, Ct);

        return (code, output.ToString(), error.ToString());
    }

    public void Dispose() => _repo.Dispose();

    private sealed class RepoAsCheckout(string tree) : ICheckoutProvider
    {
        public Task<Outcome<string>> EnsureAsync(MeasurementTarget target, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success(tree));
    }

    private sealed class ScriptedSubject(string answer) : IModelRuntime
    {
        public ModelHosting Hosting => ModelHosting.Local;

        public Task<Outcome<string>> AcceptBudgetAsync(Budget budget, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<string>.Success("fake"));

        public Task<Outcome<ModelAnswer>> AskAsync(ModelRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<ModelAnswer>.Success(new ModelAnswer(
                Captured.Text(answer), CapturedCount.Number(10), CapturedCount.Number(10),
                TimeSpan.FromMilliseconds(50), SamplingAsSent.From(request.Sampling, "request-body"),
                StopReason.Completed, "stop")));
    }
}
