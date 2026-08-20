using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench run --task-kind fix [--arm investigate-only]`
/// (todo/PLAN_investigate_vs_implement.md step 5): the front door for the investigate arm — and the
/// refusals for the arms that cannot be planned, because a cell the runner can only block must not be
/// creatable.</summary>
[Collection("postgres")]
public sealed class FixRunPlanningTests(PostgresFixture postgres)
{
    private const string Repo = "https://github.com/App-vNext/Polly.git";
    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    /// <summary>Port 1 answers nothing anywhere, so every leg fails on transport in milliseconds —
    /// which is what makes an end-to-end `bench run` affordable as a test.</summary>
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_diff_producing_arm_cannot_be_planned_and_the_refusal_names_the_sandbox()
    {
        var (code, _, error) = Run(
            "run", "--db", postgres.ConnectionString, "--bank-group", "x", "--task-kind", "fix", "--arm", "full");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("sandbox").And.Contain("investigate-only");
    }

    [Fact]
    public void An_arm_on_a_reading_run_is_a_named_refusal()
    {
        var (code, _, error) = Run(
            "run", "--db", postgres.ConnectionString, "--bank-group", "x", "--arm", "investigate-only");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--task-kind fix");
    }

    [Fact]
    public void An_unknown_task_kind_is_refused_by_name()
    {
        var (code, _, error) = Run(
            "run", "--db", postgres.ConnectionString, "--bank-group", "x", "--task-kind", "vibes");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("'vibes'").And.Contain("'fix'");
    }

    [Fact]
    public async Task A_fix_run_plans_investigate_only_cells_and_their_legs_carry_the_arm()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var file = new TempFixBank(suffix);
        Run("questions", "import", "--file", file.Path, "--db", postgres.ConnectionString);

        var (code, _, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--no-checkout",
            "--bank-group", $"codeplan-{suffix}", "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--task-kind", "fix");

        code.Should().Be(ExitCodes.NoReport, "the dead endpoint answers nothing, so the run measured nothing — and that is not a pass");

        await using var db = postgres.NewContext();
        var cell = db.Cells.Single(c => c.QuestionId == $"fixq-{suffix}");
        cell.Arm.Should().Be(FixArm.InvestigateOnly, "a fix run defaults to the one arm that can be honoured");
        cell.Leg.Should().EndWith("!investigate-only");
        var row = db.Runs.Single(r => r.Id == cell.RunId);
        row.Kind.Should().Be(TaskKind.Fix, "the run's kind is what a later judge pass reads to frame its verdicts");
        row.Status.Should().Be(RunStatus.Completed,
            "every cell reached a terminal state, and a listing that still said Planned would tell the operator the opposite of the truth");

        db.LegPhases.Count(p => p.CellId == cell.Id).Should().Be(
            2, "even a leg that died on transport records its phase attempt — Investigate closed, Judge stopped");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>One accepted fix-kind question — the minimum a fix run can freeze a selection from.</summary>
    private sealed class TempFixBank : IDisposable
    {
        public TempFixBank(string suffix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-fixbank-{suffix}.json");
            File.WriteAllText(Path, $$"""
            {
              "targetRepo": "https://github.com/App-vNext/Polly.git",
              "authoredAtCommit": "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9",
              "groups": [ { "key": "codeplan-{{suffix}}", "title": "Code tasks", "ordinal": 1 } ],
              "reviewers": [],
              "questions": [
                {
                  "group": "codeplan-{{suffix}}", "ordinal": 1, "kind": "Fix", "state": "Accepted",
                  "source": "BugsAndTests", "authorModel": "harvest",
                  "seed": { "kind": "commit", "reference": "abc", "at": "2026-08-11T00:00:00Z" },
                  "id": "fixq-{{suffix}}",
                  "prompt": "Retries stop honouring the cap after the first attempt. Investigate.",
                  "expectations": [
                    { "kind": "Member", "file": "src/Polly.Core/Retry/RetryHelper.cs",
                      "member": "RetryHelper.DecorrelatedJitterBackoffV2", "start": 75, "end": 111 }
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
