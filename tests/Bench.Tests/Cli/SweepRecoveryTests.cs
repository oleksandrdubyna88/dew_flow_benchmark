using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>Crash recovery, from the operator's side.
/// <para>
/// The store could hand back a dead host's claims from its first commit, and until 2026-08-16
/// <b>nothing ever called it</b>: there was no verb for it and <c>bench run</c> could only add cells.
/// A killed run therefore left its claimed cells claimed forever — invisible work that no later run
/// would ever pick up. These two tests are the trigger the sweep was missing.
/// </para></summary>
[Collection("postgres")]
public sealed class SweepRecoveryTests(PostgresFixture postgres)
{
    private const string Repo = "https://github.com/App-vNext/Polly.git";
    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    /// <summary>Port 1 answers nothing anywhere, so every leg fails on transport in milliseconds — which
    /// is what makes an end-to-end `bench run` affordable as a test.</summary>
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_run_sweeps_cells_a_crash_left_claimed_before_it_drains()
    {
        var stranded = await StrandAsync("host-killed-before-the-run");
        using var suite = new TempSuite();

        var (code, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint);

        (await StateAsync(stranded)).Should().Be(CellState.Pending,
            "a cell a killed host left Claimed is nobody's, and no later run would ever have claimed it again");
        output.Should().Contain("swept", "recovery that happens silently is recovery an operator cannot trust");
        code.Should().Be(ExitCodes.NoReport,
            "the endpoint was dead, so the run measured nothing — which is neither a pass nor the subject's fault");
    }

    [Fact]
    public async Task The_sweep_verb_hands_back_a_dead_hosts_claims_without_starting_a_run()
    {
        var stranded = await StrandAsync("host-killed-with-no-successor");

        var (code, output, _) = Run("sweep", "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.Pass);
        (await StateAsync(stranded)).Should().Be(CellState.Pending);
        output.Should().Contain("requeued").And.Contain("30 min",
            "the window is part of the report: 'swept nothing' means something different at five minutes");
    }

    [Fact]
    public async Task A_sweep_without_a_store_is_refused_rather_than_pointed_at_a_default_database()
    {
        var (code, _, error) = Run("sweep");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--db");
    }

    /// <summary>A run whose host was killed two hours ago, holding one claim.</summary>
    private async Task<Guid> StrandAsync(string owner)
    {
        var store = postgres.NewStore(new TestClock(DateTimeOffset.UtcNow.AddHours(-2)));
        var commit = CommitSha.Parse(new string('b', 40)).Ok();
        var target = MeasurementTarget.At(RepoUrl.Parse("https://example.invalid/stranded.git").Ok(), commit);
        var run = BenchRun.Planned("killed", target, EngineRef.Filesystem(), "suite@v1#abc", DateTimeOffset.UtcNow);

        var cells = Matrix.Plan(
            [new Question("q1", "prompt", [Expectation.File(SourceAnchor.File("src/F.cs", commit))], string.Empty)],
            repeats: 1,
            [new Subject(ModelRef.Parse("m", ModelHosting.Local).Ok(), Sampling.Deterministic(1))],
            [Lane.Named("no-tools")]).Ok()
            .Select(c => RunCell.Pending(run.Id, c)).ToList();

        await store.CreateAsync(run, cells, Ct);

        return (await store.ClaimNextAsync(run.Id, owner, Ct)).Ok().Id;
    }

    private async Task<CellState> StateAsync(Guid cellId)
    {
        await using var db = postgres.NewContext();
        return (await db.Cells.AsNoTracking().FirstAsync(c => c.Id == cellId, Ct)).State;
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TempSuite : IDisposable
    {
        public TempSuite()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-sweep-suite-{Guid.NewGuid():N}.json");
            File.WriteAllText(Path, """
            {
              "id": "demo",
              "questions": [
                { "id": "q1", "prompt": "where is the order total computed?",
                  "expectations": [ { "kind": "AnswerContains", "text": "Total" } ] }
              ]
            }
            """);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
