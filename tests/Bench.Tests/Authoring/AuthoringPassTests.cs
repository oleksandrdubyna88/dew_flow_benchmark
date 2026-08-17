using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Registry;
using Bench.Domain.Retrieval;
using Bench.Domain.Runs;
using Bench.Domain.Targets;
using Bench.Tests.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>The authoring pass: an agent's answer through the bank's own admission rules.
/// <para>
/// Against real Postgres, because the guarantee that matters is what ends up STORED — a pass whose candidates
/// are refused by the bank after it reported them proposed is a pass that lies in its own report.
/// </para>
/// <para>
/// The agent is a fake here on purpose: what a real one writes is a question about quality, and this class is
/// about admission. Whether the real CLI answers at all is <c>CliAgentLiveTests</c>' job.
/// </para></summary>
[Collection("postgres")]
public sealed class AuthoringPassTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoUrl Target = RepoUrl.Parse("https://github.com/dotnet/aspnetcore.git").Ok();
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();
    private static readonly string Prompts = Path.Combine(Repository.Root, "prompts");

    /// <summary>A tag unique to this test instance, appended to every question id it writes. Question ids are
    /// unique across the whole bank, so this is the only scope an assertion can trust in a shared database.</summary>
    private readonly string _tag = Guid.NewGuid().ToString("N")[..10];

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_well_formed_answer_lands_in_the_bank_with_its_author_and_its_prompt_hash()
    {
        var group = await GroupAsync();

        var report = await RunAsync(group, Answer(One("cancellation", "src/A.cs", "A.Cancel", 10, 20)));

        report.Proposed.Should().Be(1, string.Join(" | ", report.Rejected));
        report.Rejected.Should().BeEmpty();
        report.PromptHash.Should().NotBeEmpty("'these questions came from that prompt' has to be a stored fact");

        var stored = (await MineAsync(group)).Should().ContainSingle().Subject;
        stored.Question.AuthorModel.Should().Be("claude-sonnet");
        stored.Question.Source.Should().Be(AuthoringSource.Synthetic);
    }

    [Fact]
    public async Task A_MALFORMED_answer_is_a_rejection_carrying_the_parse_error_and_NOTHING_is_repaired()
    {
        var group = await GroupAsync();

        var report = await RunAsync(group, "here are your questions: [ {\"id\": ");

        // A pass that fixes its author's JSON produces questions that are partly the pass's, and "which model
        // wrote this set" stops being answerable — the one property the founding plan insists on, because a
        // set's ceiling becomes its author's ceiling.
        report.Proposed.Should().Be(0);
        report.Rejected.Should().ContainSingle().Which.Should().Contain("not the shape the bank reads");
        (await MineAsync(group)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_answer_wrapped_in_a_code_FENCE_is_unwrapped_because_that_is_a_habit_not_a_defect()
    {
        var group = await GroupAsync();

        var fenced = "```json\n" + Answer(One("fenced", "src/B.cs", "B.M", 1, 9)) + "\n```";

        var report = await RunAsync(group, fenced);

        // The only repair this pass performs, and the line is drawn there deliberately: unwrapping a fence
        // changes no question, while anything further would start editing them.
        report.Proposed.Should().Be(1, string.Join(" | ", report.Rejected));
    }

    [Fact]
    public async Task A_question_with_nothing_to_FIND_is_refused_by_the_rule_that_already_existed()
    {
        var group = await GroupAsync();

        var report = await RunAsync(group, $$"""
            [{ "id": "no-anchor-{{_tag}}", "prompt": "how does this work?", "referenceAnswer": "somehow",
               "expectations": [ { "kind": "AnswerContains", "text": "Token" } ] }]
            """);

        // QuestionCandidate.Propose, not a second admission rule written here: two rules would drift, and the
        // drifted one would be the unread one.
        report.Proposed.Should().Be(0);
        report.Rejected.Should().ContainSingle().Which.Should().Contain("no retrieval expectation");
    }

    [Fact]
    public async Task A_seed_with_no_DATE_is_stored_as_unstated_rather_than_as_today()
    {
        var group = await GroupAsync();

        var report = await RunAsync(group, $$"""
            [{ "id": "dateless-{{_tag}}", "prompt": "where is the retry delay computed?", "referenceAnswer": "in a helper",
               "seed": { "kind": "member", "reference": "RetryHelper.Backoff" },
               "expectations": [ { "kind": "Member", "file": "src/A.cs", "member": "A.M", "start": 1, "end": 9 } ] }]
            """);

        report.Proposed.Should().Be(1);

        // The whole memorisation check reads this date. Stamping "now" on a seed the author could not date
        // would certify every question as clear of every subject's cutoff — the one lie the check rests on.
        var stored = (await MineAsync(group)).Should().ContainSingle().Subject;
        stored.Question.Seed.Kind.Should().Be("unstated");
        stored.Question.Seed.At.Should().Be(default);
    }

    [Fact]
    public async Task The_same_question_twice_in_one_batch_is_stored_ONCE()
    {
        var group = await GroupAsync();

        var twice = $"[{One("first", "src/A.cs", "A.Same", 10, 20)},{One("second", "src/A.cs", "A.Same", 10, 20)}]";

        var report = await RunAsync(group, twice);

        // Expected rather than alarming: three authors on one group independently write the question about the
        // most obvious member in the repository.
        report.Duplicates.Should().Be(1);
        report.Proposed.Should().Be(1);
        report.Describe.Should().Contain("duplicate");
    }

    [Fact]
    public async Task An_agent_that_could_not_be_reached_is_a_report_rather_than_an_exception()
    {
        var group = await GroupAsync();

        var report = await RunAsync(group, new RefusingAgent("claude is not installed on this machine"));

        // One author's bad afternoon must not end a run over six groups of a hundred questions.
        report.Proposed.Should().Be(0);
        report.Rejected.Should().ContainSingle().Which.Should().Contain("not installed");
        report.PromptHash.Should().NotBeEmpty("the prompt was rendered before the agent failed, and that is a fact");
    }

    [Fact]
    public async Task Ordinals_continue_from_where_the_operator_said_rather_than_from_one()
    {
        var group = await GroupAsync();

        await RunAsync(group, Answer(One("q41", "src/A.cs", "A.M", 1, 9)), ordinal: 41);

        // The operator quotes ordinals — "group 1, questions 1–10" — so they are assigned, never generated.
        (await MineAsync(group)).Should().ContainSingle().Which.Question.Ordinal.Should().Be(41);
    }

    private Task<AuthoringReport> RunAsync(QuestionGroup group, string answer, int ordinal = 1) =>
        RunAsync(group, new EchoingAgent(answer), ordinal);

    private async Task<AuthoringReport> RunAsync(QuestionGroup group, ICliAgentRuntime agent, int ordinal = 1) =>
        await AuthoringPass.RunAsync(
            agent,
            Bank(),
            Prompts,
            new AuthoringRequest(group, Target, Commit, Count: 3, ordinal, TimeSpan.FromMinutes(2), Path.GetTempPath()),
            Author(),
            "claude",
            Noon,
            Ct);

    private static RegisteredModel Author() =>
        RegisteredModel.Create(
            $"author-{Guid.NewGuid():N}"[..20],
            "the author",
            ModelRuntimeKind.CliClaude,
            ModelHosting.Cloud,
            ModelConfig.Parse("claude-sonnet", "BENCH_CLAUDE_EXE", string.Empty, "BENCH_CLAUDE_EXE", Sampling.Deterministic(1), 0, 0).Ok(),
            Noon).Ok();

    /// <summary>The REAL <c>code-lookup</c> group, created once and reused.
    /// <para>
    /// It cannot be a per-test key: the prompt catalog briefs five named groups and refuses anything else, so a
    /// unique key would fail at the render rather than at the thing under test. The bank holds one row per key,
    /// so this is idempotent, and every assertion below is scoped by QUESTION ID instead — which is unique
    /// across the whole bank and therefore the only scope that holds in a shared database.
    /// </para></summary>
    private async Task<QuestionGroup> GroupAsync()
    {
        var group = QuestionGroup.Create("code-lookup", "Code lookup", 1).Ok();
        var added = await Bank().AddGroupAsync(group, Ct);

        return added.Match(row => row, _ => Existing());
    }

    private QuestionGroup Existing() =>
        Bank().GroupsAsync(Ct).GetAwaiter().GetResult().Ok().First(g => g.Key.Value == "code-lookup");

    /// <summary>Questions this test wrote, found by the ids it chose. `ContainSingle` over a group would count
    /// every other test's rows.</summary>
    private async Task<IReadOnlyList<BankEntry>> MineAsync(QuestionGroup group, string tag)
    {
        // NOT BankQuery.Selection: that one is accepted-only, deliberately, because a test may not measure a
        // question nobody vouched for. What this pass produces is PROPOSED, which is the point of it.
        var all = (await Bank().QuestionsAsync(new BankQuery(group.Key.Value), Ct)).Ok();

        return [.. all.Where(e => e.Question.Question.Id.Contains(tag, StringComparison.Ordinal))];
    }

    private IQuestionBank Bank() => new Bench.Infrastructure.Persistence.PostgresQuestionBank(postgres.NewContext());

    private static string Answer(string question) => $"[{question}]";

    private Task<IReadOnlyList<BankEntry>> MineAsync(QuestionGroup group) => MineAsync(group, _tag);

    private string One(string id, string file, string member, int start, int end) => $$"""
        { "id": "{{id}}-{{_tag}}", "prompt": "where is the delay computed?", "referenceAnswer": "in a helper",
          "seed": { "kind": "member", "reference": "{{member}}", "at": "2026-05-14" },
          "expectations": [ { "kind": "Member", "file": "{{file}}", "member": "{{member}}", "start": {{start}}, "end": {{end}} } ] }
        """;

    /// <summary>An agent that answers with a fixed string. What a real one WRITES is a question about quality;
    /// this class is about admission.</summary>
    private sealed class EchoingAgent(string answer) : ICliAgentRuntime
    {
        public Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<AgentAnswer>.Success(
                new AgentAnswer(answer, TimeSpan.FromSeconds(4), answer.Length)));
    }

    private sealed class RefusingAgent(string reason) : ICliAgentRuntime
    {
        public Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<AgentAnswer>.Failure(reason));
    }
}
