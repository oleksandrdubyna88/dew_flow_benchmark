using Bench.Cli;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The bank from the outside: import a file, then measure what it holds.
/// <para>
/// The load-bearing test is the last one — a run created with <c>--bank-group</c> freezes the selection
/// through the SAME suite machinery a file uses and writes the per-test snapshot. Without the snapshot, a
/// question re-filed next month would move a finished report's numbers into a different column, silently.
/// </para></summary>
[Collection("postgres")]
public sealed class QuestionsCommandTests(PostgresFixture postgres)
{
    private const string Repo = "https://github.com/App-vNext/Polly.git";
    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    /// <summary>Port 1 answers nothing anywhere, so every leg fails on transport in milliseconds — which
    /// is what makes an end-to-end `bench run` affordable as a test.</summary>
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void An_import_lands_the_bank_and_the_listing_shows_what_may_enter_a_test()
    {
        var suffix = Unique();
        using var file = new TempBank(suffix);

        var (imported, importOutput, _) = Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);
        var (listed, listOutput, _) = Run(
            "questions", "list", "--group", $"lookup-{suffix}", "--db", postgres.ConnectionString);

        imported.Should().Be(ExitCodes.Pass);
        importOutput.Should().Contain("2 question(s)");
        listed.Should().Be(ExitCodes.Pass);
        listOutput.Should().Contain("Accepted").And.Contain("Proposed",
            "a listing that hid the state would make a selection look larger than it is");
    }

    [Fact]
    public void An_import_that_refused_a_row_does_not_exit_green()
    {
        var suffix = Unique();
        using var file = new TempBank(suffix);
        Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);

        var (code, output, _) = Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);

        // An import that quietly took 190 of 200 questions and exited 0 is how a test ends up measuring a
        // set nobody agreed to. Nothing was measured, so it is not a regression of a subject either.
        code.Should().Be(ExitCodes.Regression);
        output.Should().Contain("refused").And.Contain("already in the bank");
    }

    [Fact]
    public void A_missing_import_file_is_the_machines_problem_and_an_unset_one_is_the_callers()
    {
        Run("questions", "import", "--db", postgres.ConnectionString).Code.Should().Be(ExitCodes.Configuration);

        var (code, _, error) = Run("questions", "import", "--file", "nowhere.json", "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("import file not found");
    }

    [Fact]
    public void A_move_without_a_reason_is_refused_because_the_reason_is_the_whole_record()
    {
        var suffix = Unique();
        using var file = new TempBank(suffix);
        Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);

        var (code, _, error) = Run(
            "questions", "move", "--question", $"q1-{suffix}", "--to", $"lookup-{suffix}", "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--reason is required");
    }

    [Fact]
    public async Task A_run_created_from_the_bank_freezes_its_selection_and_snapshots_the_groups()
    {
        var suffix = Unique();
        using var file = new TempBank(suffix);
        Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);

        var (code, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--no-checkout",
            "--bank-group", $"lookup-{suffix}", "--suite-id", $"bank-{suffix}",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint);

        // The endpoint is dead, so nothing is measured — that is exit 5 and it is not what this test is
        // about. What it is about is that the selection became a frozen, hashed suite and left a snapshot.
        code.Should().Be(ExitCodes.NoReport);
        output.Should().Contain($"bank-{suffix}@v1#").And.Contain("1 question(s)");
        output.Should().Contain("question(s) frozen with their groups");

        await using var db = postgres.NewContext();
        var snapshot = await db.RunQuestions.AsNoTracking().ToListAsync(Ct);

        snapshot.Should().ContainSingle(q => q.QuestionId == $"q1-{suffix}")
            .Which.GroupKey.Should().Be($"lookup-{suffix}",
                "a report reads the group the test FROZE, so re-filing the question later cannot move its numbers");
    }

    [Fact]
    public void A_bank_selection_with_nothing_accepted_refuses_the_run_rather_than_measuring_nothing()
    {
        var suffix = Unique();
        using var file = new TempBank(suffix);
        Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);
        Run("questions", "reject", "--question", $"q1-{suffix}", "--db", postgres.ConnectionString);

        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--no-checkout", "--bank-group", $"lookup-{suffix}",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint);

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("nothing nobody vouched for");
    }

    [Fact]
    public void A_run_that_names_neither_a_suite_file_nor_a_bank_group_says_so()
    {
        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--no-checkout", "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--suite-file or --bank-group is required");
    }

    /// <summary>Random, not a v7 guid: a v7 opens with a millisecond timestamp, so truncating one gives
    /// every test in a class the same "unique" suffix — which this suite has already been bitten by.</summary>
    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>One group, two reviewers, two questions — one accepted, one not. The shape an authoring
    /// pass actually produces, in miniature.</summary>
    private sealed class TempBank : IDisposable
    {
        public TempBank(string suffix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-bank-{suffix}.json");
            File.WriteAllText(Path, $$"""
            {
              "targetRepo": "https://github.com/App-vNext/Polly.git",
              "authoredAtCommit": "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9",
              "groups": [ { "key": "lookup-{{suffix}}", "title": "Code lookup", "ordinal": 1 } ],
              "reviewers": [
                { "key": "claude-{{suffix}}", "displayName": "Claude", "ordinal": 1 },
                { "key": "codex-{{suffix}}", "displayName": "Codex", "ordinal": 2 }
              ],
              "questions": [
                {
                  "group": "lookup-{{suffix}}", "ordinal": 1, "kind": "Reading", "state": "Accepted",
                  "source": "RepositoryHistory", "authorModel": "opus-5",
                  "seed": { "kind": "pull-request", "reference": "#1234", "at": "2026-05-01T00:00:00Z" },
                  "id": "q1-{{suffix}}",
                  "prompt": "How does Polly compute the delay for an exponential retry with jitter?",
                  "referenceAnswer": "RetryHelper.DecorrelatedJitterBackoffV2",
                  "expectations": [
                    { "kind": "Member", "file": "src/Polly.Core/Retry/RetryHelper.cs",
                      "member": "RetryHelper.DecorrelatedJitterBackoffV2", "start": 75, "end": 111 }
                  ],
                  "reviews": [ { "reviewer": "claude-{{suffix}}", "verdict": "Approved" } ]
                },
                {
                  "group": "lookup-{{suffix}}", "ordinal": 2, "kind": "Reading", "state": "Proposed",
                  "source": "Synthetic", "authorModel": "qwen3-coder",
                  "id": "q2-{{suffix}}",
                  "prompt": "Which type caps the retry delay?",
                  "expectations": [
                    { "kind": "Member", "file": "src/Polly.Core/Retry/RetryConstants.cs",
                      "member": "RetryConstants.MaxDelay", "start": 5, "end": 9 }
                  ]
                }
              ]
            }
            """);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
