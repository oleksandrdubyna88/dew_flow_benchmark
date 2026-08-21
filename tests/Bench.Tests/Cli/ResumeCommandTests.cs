using Bench.Cli;
using Bench.Domain.Runs;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>
/// `bench resume --run &lt;id&gt;` — finishing a campaign something interrupted.
///
/// <para>The verb exists because the harness claimed to be resumable and was not: the sweep hands back cells
/// a dead host CLAIMED inside a running drain, and nothing could pick a run up from a cold start. A campaign
/// killed part-way left its cells <c>Pending</c> in a run stuck at <c>Running</c> forever. Found by losing 6
/// of 45 legs to a shell timeout, which on a multi-hour campaign is not a hypothetical.</para>
///
/// <para>What these pin is mostly the boundary between what a run STORES and what an operator re-declares.
/// Getting that wrong in either direction is silent: re-declaring too much lets the second half of a
/// campaign measure a different subject under the first half's label, and re-declaring too little makes a
/// resume impossible for want of an endpoint nobody recorded.</para>
/// </summary>
[Collection("postgres")]
public sealed class ResumeCommandTests(PostgresFixture postgres)
{
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_resume_without_a_run_id_is_refused_because_it_continues_ONE_named_run()
    {
        var (code, _, error) = Run("resume", "--db", postgres.ConnectionString, "--model-url", DeadEndpoint);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--run");
    }

    [Fact]
    public void A_resume_without_a_model_url_is_refused_and_the_reason_says_why_it_is_not_stored()
    {
        // The boundary, stated in the refusal itself: an endpoint is where a model was reachable that day,
        // not a property of the measurement — so a run deliberately does not record it, and a resume must
        // ask. A message that only said "required" would leave a reader hunting for the field.
        var (code, _, error) = Run("resume", "--db", postgres.ConnectionString, "--run", Guid.NewGuid().ToString());

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--model-url").And.Contain("property of the measurement");
    }

    [Fact]
    public async Task An_INTERRUPTED_run_is_finished_from_a_cold_start_and_its_status_advances()
    {
        var (runId, bank) = await PlannedAsync();
        using var _ = bank;
        await ReopenAsync(runId);

        var (code, output, error) = Run(
            "resume", "--run", runId.ToString(), "--db", postgres.ConnectionString,
            "--model-url", DeadEndpoint, "--stale-after-minutes", "0");

        // The dead endpoint answers nothing, so the reopened leg is abandoned rather than scored — which is
        // the point: what is being pinned is that the cell was REACHED at all, from a process that knew
        // nothing but the run's id.
        code.Should().NotBe(ExitCodes.Configuration, error);
        output.Should().Contain("rebuilt from the bank").And.Contain("stamp verified");

        await using var db = postgres.NewContext();
        db.Cells.Count(c => c.RunId == runId && c.State == CellState.Pending)
            .Should().Be(0, "a resumed run leaves nothing pending, which is what makes the status honest");
        db.Runs.Single(r => r.Id == runId).Status
            .Should().Be(RunStatus.Completed, "a status nothing can advance out of is the stuck in-flight state rule 8 forbids");
    }

    [Fact]
    public async Task A_finished_run_resumes_as_a_NO_OP_that_says_so_rather_than_draining_nothing()
    {
        var (runId, bank) = await PlannedAsync();
        using var _ = bank;

        var (_, output, _) = Run(
            "resume", "--run", runId.ToString(), "--db", postgres.ConnectionString, "--model-url", DeadEndpoint);

        // Saying so plainly beats a second drain that measures nothing and prints a summary as though it had.
        output.Should().Contain("already finished");
    }

    [Fact]
    public async Task A_model_that_is_not_a_SUBJECT_of_the_run_is_refused_naming_the_ones_that_are()
    {
        // The silent failure this prevents: the second half of a campaign measured on a different model and
        // labelled with the first half's identity. Nothing downstream could tell.
        var (runId, bank) = await PlannedAsync();
        using var _ = bank;
        await ReopenAsync(runId);

        var (code, _, error) = Run(
            "resume", "--run", runId.ToString(), "--db", postgres.ConnectionString,
            "--model-url", DeadEndpoint, "--model", "some-other-model");

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("some-other-model").And.Contain("may not change who is measured");
    }

    [Fact]
    public async Task A_lane_RETIRED_since_the_run_started_still_resumes()
    {
        // Exactly what retired rows stay listable for. Refusing here would strand the campaigns that ran
        // long enough for somebody to tidy the catalog underneath them — the ones most worth finishing.
        var lane = $"lane-{Guid.NewGuid():N}"[..18];
        Run("lanes", "add", "--name", lane, "--presentation", "none", "--db", postgres.ConnectionString);

        var (runId, bank) = await PlannedAsync(lane);
        using var _ = bank;
        await ReopenAsync(runId);
        Run("lanes", "retire", "--name", lane, "--db", postgres.ConnectionString);

        var (code, output, error) = Run(
            "resume", "--run", runId.ToString(), "--db", postgres.ConnectionString,
            "--model-url", DeadEndpoint, "--stale-after-minutes", "0");

        code.Should().NotBe(ExitCodes.Configuration, error);
        output.Should().Contain(lane);
        error.Should().NotContain("retired");
    }

    /// <summary>A real run against a dead endpoint: every cell settles, which is the state a resume has to
    /// tell apart from an interrupted one.</summary>
    private async Task<(Guid RunId, TempBank Bank)> PlannedAsync(string lane = "")
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var bank = new TempBank(suffix, "https://example.invalid/x.git", Sha);
        Run("questions", "import", "--file", bank.Path, "--db", postgres.ConnectionString);

        string[] lanes = lane.Length > 0 ? ["--lanes", lane] : [];
        var (code, _, error) = Run([
            "run", "--repo", "https://example.invalid/x.git", "--commit", Sha, "--no-checkout",
            "--bank-group", bank.Group, "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint, "--label", $"resume-{suffix}",
            .. lanes]);

        code.Should().Be(ExitCodes.NoReport, error);

        await using var db = postgres.NewContext();
        return (db.Runs.Single(r => r.Label == $"resume-{suffix}").Id, bank);
    }

    /// <summary>What an interruption leaves: a settled cell handed back to Pending, its owner cleared.</summary>
    private async Task ReopenAsync(Guid runId)
    {
        await using var db = postgres.NewContext();
        var cell = db.Cells.First(c => c.RunId == runId);
        cell.State = CellState.Pending;
        cell.Owner = string.Empty;
        cell.OwnerHost = string.Empty;
        cell.OwnerPid = 0;
        await db.SaveChangesAsync(Ct);
    }

    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }
}
