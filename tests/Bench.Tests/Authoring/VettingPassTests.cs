using Bench.Application;
using Bench.Application.Bank;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Bank;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>The vetting pass: reviewer slots marking proposed questions, and what the marks do to a question's
/// own state.
/// <para>
/// Every test takes its OWN database. The promotion rule reads every reviewer row in the bank — that is what
/// "every configured reviewer approved" means — so a shared database would have each test's decision depend on
/// how many slots another test happened to create.
/// </para>
/// <para>
/// The agent is a fake, deliberately: what a real reviewer's judgement is worth is a question this class cannot
/// answer, and what it CAN pin down is that a verdict is stored, a self-review is refused, an unreadable answer
/// never reads as approval, and a question moves only under the strict rule.
/// </para></summary>
[Collection("postgres")]
public sealed class VettingPassTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoUrl Target = RepoUrl.Parse("https://github.com/dotnet/aspnetcore.git").Ok();
    private static readonly CommitSha Commit = CommitSha.Parse(new string('b', 40)).Ok();
    private static readonly string Prompts = Path.Combine(Repository.Root, "prompts");

    private const string AuthorModel = "claude-sonnet-4-6";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_bound_slot_approving_makes_the_question_SELECTABLE()
    {
        var bank = await BankAsync("all_approve", slots: 2);

        var report = await VetAsync(bank, Approved("checked both anchors"), reviewerModel: "gpt-5");

        report.Accepted.Should().Be(1, string.Join(" | ", report.Refusals.Concat(report.Questions.SelectMany(q => q.Skipped))));
        (await StateAsync(bank)).Should().Be(CandidateState.Accepted);
    }

    [Fact]
    public async Task ONE_slot_rejecting_rejects_the_question_however_many_approved()
    {
        var bank = await BankAsync("one_rejects", slots: 2);

        // Both slots answer the same way here, so the guarantee under test is the rule rather than the mixture:
        // a rejection outranks approvals because it names a specific defect, and no count answers a defect.
        var report = await VetAsync(bank, Rejected("the member is at 41-49, not 45-49"), reviewerModel: "gpt-5");

        report.Rejected.Should().Be(1);
        (await StateAsync(bank)).Should().Be(CandidateState.Rejected);
    }

    [Fact]
    public async Task A_slot_that_has_not_marked_yet_leaves_the_question_WAITING()
    {
        // Three slots configured, one bound: the classic half-reviewed bank. Two unbound slots are people, and
        // a machine pass cannot speak for them.
        var bank = await BankAsync("waiting", slots: 3, bind: 1);

        var report = await VetAsync(bank, Approved("looks measurable"), reviewerModel: "gpt-5");

        report.Waiting.Should().Be(1);
        report.Questions.Single().Decision.Reason.Should().Contain("reviewer-2").And.Contain("reviewer-3");
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed, "a question two reviewers have not seen is not vouched for");
    }

    [Fact]
    public async Task A_model_reviewing_its_OWN_question_is_skipped_and_the_question_stays_proposed()
    {
        var bank = await BankAsync("self", slots: 1);

        var report = await VetAsync(bank, Approved("mine, and excellent"), reviewerModel: AuthorModel);

        // The whole reason the flag exists: with one verified CLI author, every slot bound to it would otherwise
        // approve its own work and the bank would look reviewed.
        report.Questions.Single().Skipped.Should().ContainSingle().Which.Should().Contain("its own");
        report.Questions.Single().Marks.Should().BeEmpty();
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed);
    }

    [Fact]
    public async Task Allowing_self_review_takes_the_mark_and_REPORTS_what_it_cost()
    {
        var bank = await BankAsync("self_allowed", slots: 1);

        var report = await VetAsync(bank, Approved("verified at the pinned commit"), reviewerModel: AuthorModel, allowSelf: true);

        report.Accepted.Should().Be(1);
        report.Cost.Should().Contain("one opinion sampled three times",
            "an escape hatch that is silent is one nobody remembers taking");
    }

    [Fact]
    public async Task An_answer_with_NO_verdict_field_is_not_an_approval()
    {
        var bank = await BankAsync("no_verdict", slots: 1);

        var report = await VetAsync(bank, """{ "note": "this one seems fine to me" }""", reviewerModel: "gpt-5");

        // The failure mode that looks like success, and the reason this pass reads its own wire shape instead of
        // the import's `ReviewFile` — that one defaults its verdict to Approved, which is right for a file a
        // person wrote and catastrophic for an agent whose answer lost a field.
        report.Questions.Single().Skipped.Should().ContainSingle().Which.Should().Contain("not a verdict");
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed);
    }

    [Fact]
    public async Task A_rejection_with_no_reason_is_REFUSED_rather_than_stored()
    {
        var bank = await BankAsync("no_reason", slots: 1);

        var report = await VetAsync(bank, """{ "verdict": "rejected", "note": "  " }""", reviewerModel: "gpt-5");

        // A rejection is the only record of what an author gets wrong. Stored empty it would remove a question
        // from the bank and teach the prompt nothing.
        report.Questions.Single().Skipped.Should().ContainSingle().Which.Should().Contain("only record");
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed);
    }

    [Fact]
    public async Task A_verdict_prefaced_with_PROSE_is_still_read()
    {
        var bank = await BankAsync("prose", slots: 1);

        var prefaced = "I checked the file at the pinned commit. Here is my verdict:\n"
            + """{ "verdict": "approved", "note": "the member resolves" }""";

        var report = await VetAsync(bank, prefaced, reviewerModel: "gpt-5");

        // Measured on the authoring side and expected to repeat here: prose before JSON is the agent's instinct,
        // not a prompt defect that one more sentence fixes.
        report.Accepted.Should().Be(1, string.Join(" | ", report.Questions.SelectMany(q => q.Skipped)));
    }

    [Fact]
    public async Task An_agent_that_could_not_be_reached_leaves_the_question_untouched()
    {
        var bank = await BankAsync("unreachable", slots: 1);

        var report = await VetAsync(bank, new RefusingAgent("claude is not installed on this machine"), "gpt-5", allowSelf: false);

        report.Questions.Single().Skipped.Should().ContainSingle().Which.Should().Contain("not installed");
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed);
    }

    [Fact]
    public async Task A_bank_with_NO_bound_slot_refuses_the_pass_and_says_how_to_fix_it()
    {
        var bank = await BankAsync("unbound", slots: 3, bind: 0);

        var report = await VettingPass.RunAsync(
            new EchoingAgent(Approved("never asked")), Bank(bank), Prompts, Request(Group(bank), allowSelf: false), [], Noon, Ct);

        report.Refusals.Should().ContainSingle().Which.Should().Contain("bench questions bind");
        report.Questions.Should().BeEmpty("a pass with nothing to ask must not report questions as vetted");
    }

    [Fact]
    public async Task An_already_ACCEPTED_question_is_not_re_vetted()
    {
        var bank = await BankAsync("decided", slots: 1);
        await Bank(bank).SetStateAsync(QuestionId, CandidateState.Accepted, Ct);

        var report = await VetAsync(bank, Rejected("I would have said no"), reviewerModel: "gpt-5");

        // Re-vetting a decided question would let a machine overwrite a person's mark, and the person is the
        // one whose judgement this bank is built to keep.
        report.Questions.Should().BeEmpty();
        (await StateAsync(bank)).Should().Be(CandidateState.Accepted);
    }

    [Fact]
    public async Task A_mark_records_the_MODEL_that_made_it_not_only_the_slot()
    {
        var bank = await BankAsync("provenance", slots: 1);

        await VettingPass.RunAsync(
            new EchoingAgent(Approved("checked it")),
            Bank(bank),
            Prompts,
            Request(Group(bank), allowSelf: true),
            Panel(bank, ["gpt-5.6-terra"]),
            Noon,
            Ct);

        // The slot is not provenance, and this cost a real ambiguity: `reviewer-2` held claude-sonnet-4-6 for
        // thirty-three marks and gpt-5.6-terra for the next seven, so "reviewer-2 rejected 7" needed a timestamp
        // filter to be honest. A slot says which COLUMN a mark is in; only this says who made it.
        var stored = (await Bank(bank).QuestionsAsync(new BankQuery("code-lookup"), Ct)).Ok().Single();
        var marks = (await Bank(bank).ReviewsAsync([stored.Question.Id], Ct)).Ok();

        marks.Should().ContainSingle().Which.ModelId.Should().Be("gpt-5.6-terra");
        marks.Single().Unattributed.Should().BeFalse();
    }

    [Fact]
    public async Task The_author_is_out_of_its_OWN_panel_and_the_other_two_are_enough()
    {
        // The operator's one-third design, end to end: three slots, one of them the model that wrote this
        // question. Without eligibility the strict rule would wait forever on a mark the self-review refusal
        // will never allow.
        var bank = await BankAsync("thirds", slots: 3);
        var agent = new EchoingAgent(Approved("checked it"));
        var slots = Panel(bank, [AuthorModel, "gpt-5", "gemini-3-pro"]);

        var report = await VettingPass.RunAsync(
            agent, Bank(bank), Prompts, Request(Group(bank), allowSelf: false), slots, Noon, Ct);

        agent.Launches.Should().Be(2, "the author's own slot must not be asked, and the other two must");
        report.Accepted.Should().Be(1, string.Join(" | ", report.Questions.SelectMany(q => q.Skipped)));
        report.Questions.Single().Decision.Reason.Should().Contain("2 eligible");
        (await StateAsync(bank)).Should().Be(CandidateState.Accepted);
    }

    [Fact]
    public async Task When_EVERY_slot_is_the_authors_model_nothing_is_eligible_and_nothing_is_accepted()
    {
        var bank = await BankAsync("all_author", slots: 3);
        var agent = new EchoingAgent(Approved("mine, and excellent"));
        var slots = Panel(bank, [AuthorModel, AuthorModel, AuthorModel]);

        var report = await VettingPass.RunAsync(
            agent, Bank(bank), Prompts, Request(Group(bank), allowSelf: false), slots, Noon, Ct);

        // The bank's actual state on 2026-08-18, asserted so it cannot come back silently: three slots, one
        // model, that model authored. Nobody may vouch, nothing is launched, nothing is promoted.
        agent.Launches.Should().Be(0);
        report.Questions.Single().Decision.Reason.Should().Contain("every configured slot is the model that wrote it");
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed);
    }

    /// <summary>The reviewer slots of this bank, each bound to the model at its ordinal position.</summary>
    private IReadOnlyList<ReviewerSlot> Panel(string connection, IReadOnlyList<string> models) =>
        [.. Bank(connection).ReviewersAsync(Ct).GetAwaiter().GetResult().Ok()
            .Where(r => !r.IsHuman)
            .Select((reviewer, index) => new ReviewerSlot(reviewer, Model(models[index]), "reviewer-exe"))];

    [Fact]
    public async Task A_question_whose_ANCHOR_does_not_resolve_costs_ZERO_launches()
    {
        // The whole point of the pre-gate, and the only assertion that can prove it: the tree here is an empty
        // temporary directory, so the question's anchor points at nothing.
        var bank = await BankAsync("broken_anchor", slots: 3);
        var agent = new EchoingAgent(Approved("never asked"));

        var report = await VettingPass.RunAsync(
            agent, Bank(bank), Prompts, Request(Group(bank), allowSelf: true) with { Worktree = Empty() }, Slots(bank, "gpt-5"), Noon, Ct);

        agent.Launches.Should().Be(0, "three agents were being paid to discover arithmetic");
        report.Broken.Should().Be(1);
        report.LaunchesSaved.Should().Be(3);
        report.Questions.Single().Defects.Should().ContainSingle().Which.Should().Contain("no such file");
        (await StateAsync(bank)).Should().Be(CandidateState.Proposed, "a broken anchor is a defect to fix, not a verdict");
    }

    [Fact]
    public async Task A_question_whose_anchor_RESOLVES_is_still_asked_of_every_slot()
    {
        // The other half: the gate must not become a reason nothing is ever reviewed. Same pass, same question,
        // a tree that actually contains the anchored member.
        var bank = await BankAsync("good_anchor", slots: 1);
        var agent = new EchoingAgent(Approved("checked the span"));

        var report = await VettingPass.RunAsync(
            agent, Bank(bank), Prompts, Request(Group(bank), allowSelf: true) with { Worktree = Anchored() }, Slots(bank, "gpt-5"), Noon, Ct);

        agent.Launches.Should().Be(1, string.Join(" | ", report.Questions.SelectMany(q => q.Defects)));
        report.Broken.Should().Be(0);
        report.Accepted.Should().Be(1);
    }

    /// <summary>A tree with nothing in it — every anchor points at nothing.</summary>
    private string Empty() => Directory.CreateTempSubdirectory("bench-empty-tree").FullName;

    private string _anchored = string.Empty;

    /// <summary>A tree holding the one file this class's question is anchored at, with the member's name on the
    /// claimed line. Built once per test.</summary>
    private string Anchored()
    {
        if (_anchored.Length > 0)
        {
            return _anchored;
        }

        var tree = Directory.CreateTempSubdirectory("bench-anchored-tree").FullName;
        var file = Path.Combine(tree, "src", "A.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file, [.. Enumerable.Range(1, 44).Select(n => $"// line {n}"), "    public static StoreKind KindOf() => default;", .. Enumerable.Range(46, 10).Select(n => $"// line {n}")]);
        _anchored = tree;

        return tree;
    }

    private IReadOnlyList<ReviewerSlot> Slots(string connection, string reviewerModel) =>
        [.. Bank(connection).ReviewersAsync(Ct).GetAwaiter().GetResult().Ok()
            .Where(r => !r.IsHuman)
            .Select(r => new ReviewerSlot(r, Model(reviewerModel), "reviewer-exe"))];

    private const string QuestionId = "vetting-subject";

    private static string Approved(string note) => $$"""{ "verdict": "approved", "note": "{{note}}" }""";

    private static string Rejected(string note) => $$"""{ "verdict": "rejected", "note": "{{note}}" }""";

    private Task<VettingReport> VetAsync(string connection, string answer, string reviewerModel, bool allowSelf = false) =>
        VetAsync(connection, new EchoingAgent(answer), reviewerModel, allowSelf);

    private async Task<VettingReport> VetAsync(
        string connection, ICliAgentRuntime agent, string reviewerModel, bool allowSelf)
    {
        var reviewers = (await Bank(connection).ReviewersAsync(Ct)).Ok().Where(r => !r.IsHuman).ToList();

        return await VettingPass.RunAsync(
            agent,
            Bank(connection),
            Prompts,
            Request(Group(connection), allowSelf),
            [.. reviewers.Select(r => new ReviewerSlot(r, Model(reviewerModel), "reviewer-exe"))],
            Noon,
            Ct);
    }

    /// <summary>The worktree every test but the broken-anchor one uses: a tree that actually contains the file and
    /// the member this class's question is anchored at.
    /// <para>
    /// It used to be <c>Path.GetTempPath()</c>, which stopped being good enough the moment the pass grew a
    /// mechanical anchor gate — nine tests about MARKS went red because their fixture's ground truth pointed at
    /// nothing. Correct of the gate, and a fixture that was always lying about the tree.
    /// </para></summary>
    private VettingRequest Request(QuestionGroup group, bool allowSelf) =>
        new(group, Target, Commit, TimeSpan.FromMinutes(2), Anchored(), allowSelf, Limit: 10);

    /// <summary>A database of this test's own, holding the real <c>code-lookup</c> group, <paramref name="slots"/>
    /// reviewer rows of which <paramref name="bind"/> name a model, and one proposed question to mark.</summary>
    private async Task<string> BankAsync(string name, int slots, int bind = int.MaxValue)
    {
        var connection = await postgres.NewDatabaseAsync($"bench_vet_{name}_{Guid.NewGuid():N}");
        var bank = Bank(connection);
        var group = QuestionGroup.Create("code-lookup", "Code lookup", 1).Ok();

        await bank.AddGroupAsync(group, Ct);

        foreach (var ordinal in Enumerable.Range(1, slots))
        {
            var model = ordinal <= bind ? "reviewer-model" : string.Empty;
            await bank.AddReviewerAsync(Reviewer.Create($"reviewer-{ordinal}", $"Reviewer {ordinal}", ordinal, model).Ok(), Ct);
        }

        await bank.AddAsync(Question(group), Ct);

        return connection;
    }

    private static BankQuestion Question(QuestionGroup group) =>
        BankQuestion.Create(
            group.Id,
            1,
            TaskKind.Reading,
            new Domain.Suites.Question(
                QuestionId,
                "where is the store kind decided?",
                [new Expectation(
                    ExpectationKind.Member,
                    SourceAnchor.Member("src/A.cs", "StoreNaming.KindOf", new LineSpan(45, 49), Commit),
                    string.Empty,
                    true)],
                "in a naming helper"),
            codeTaskJson: string.Empty,
            AuthoringSource.Synthetic,
            AuthorModel,
            QuestionSeed.Member("StoreNaming.KindOf", Noon),
            Target,
            Commit,
            Noon).Ok();

    private static RegisteredModel Model(string modelId) =>
        RegisteredModel.Create(
            $"rev-{Guid.NewGuid():N}"[..18],
            "the reviewer",
            ModelRuntimeKind.CliClaude,
            ModelHosting.Cloud,
            ModelConfig.Parse(modelId, "BENCH_CLAUDE_EXE", string.Empty, "BENCH_CLAUDE_EXE", Sampling.Deterministic(1), 0, 0).Ok(),
            Noon).Ok();

    private QuestionGroup Group(string connection) =>
        Bank(connection).GroupsAsync(Ct).GetAwaiter().GetResult().Ok().Single();

    private async Task<CandidateState> StateAsync(string connection) =>
        (await Bank(connection).QuestionsAsync(new BankQuery("code-lookup"), Ct)).Ok().Single().Question.State;

    private IQuestionBank Bank(string connection) =>
        new PostgresQuestionBank(new BenchDbContext(
            new DbContextOptionsBuilder<BenchDbContext>().UseNpgsql(connection).Options));

    private sealed class EchoingAgent(string answer) : ICliAgentRuntime
    {
        public int Launches { get; private set; }

        public Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken)
        {
            Launches++;
            return Task.FromResult(Outcome<AgentAnswer>.Success(new AgentAnswer(answer, TimeSpan.FromSeconds(3), answer.Length)));
        }
    }

    private sealed class RefusingAgent(string reason) : ICliAgentRuntime
    {
        public Task<Outcome<AgentAnswer>> AskAsync(AgentAsk ask, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<AgentAnswer>.Failure(reason));
    }
}
