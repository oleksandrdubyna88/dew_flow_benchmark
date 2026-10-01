using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Contracts;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>S3 acceptance 2–4 through the verbs, against the fake CLI and a real database: a quota stop exits 3 naming the resume
/// command and <c>resume</c> finishes exactly the rest; <c>rerun --cell</c> appends generation 2 beside generation 1; every verb
/// that claims runs the entry step first (a stranded fixture is gone, a dead owner's claim is handed back); <c>prune</c> refuses
/// an open run and, on a settled one, deletes the artefacts and says the verdicts are no longer auditable.</summary>
[Collection("postgres")]
public sealed class ProbesDriverCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task A_quota_stop_exits_3_with_the_pending_count_per_subject_and_the_resume_line_and_resume_finishes_exactly_the_rest()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        var codex = setup.AddSubject("codex-fake", "codex", new JsonObject { ["quotaFrom"] = 2 });
        var claude = setup.AddSubject("claude-fake", "claude");

        var (code, output, error) = await setup.PlanAndRunAsync("read-inside,read-outside-bare", 2);

        code.Should().Be(ExitCodes.Environment, output + error);
        var runId = ProbesCliSetup.RunIdOf(output);
        output.Should().Contain("pending        codex-fake 3").And.Contain($"resume         bench probes resume --run {runId}");
        error.Should().Contain("codex-fake").And.Contain($"bench probes resume --run {runId}");
        var stopped = await setup.NewStore().CellsAsync(runId, Ct);
        stopped.Where(c => c.Subject.Value == "claude-fake").Should().OnlyContain(c => c.State == CellState.Settled, "the other lane went on");
        stopped.Count(c => c.State == CellState.Pending).Should().Be(3);

        codex.Rewrite(new JsonObject()); // the account is back
        var resumed = await setup.RunAsync("resume", "--run", runId.ToString());

        resumed.Code.Should().Be(ExitCodes.Pass, resumed.Output + resumed.Error);
        resumed.Output.Should().Contain("3 cell(s) settled");
        var after = await setup.NewStore().CellsAsync(runId, Ct);
        after.Should().HaveCount(8).And.OnlyContain(c => c.State == CellState.Settled && c.Generation == 1);
        codex.Calls().Should().HaveCount(5, "two calls before the stop, three after — exactly the rest");
        claude.Calls().Should().HaveCount(4, "the resume measured nothing that was already settled");
    }

    [Fact]
    public async Task Rerun_cell_appends_generation_2_that_report_reads_while_status_still_lists_generation_1()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var run = await setup.PlanAndRunAsync("read-inside,read-outside-bare", 1);
        run.Code.Should().Be(ExitCodes.Pass, run.Output + run.Error);
        var runId = ProbesCliSetup.RunIdOf(run.Output);
        var first = (await setup.NewStore().CellsAsync(runId, Ct)).Single(c => c.Probe == ProbeKind.ReadOutsideBare);

        var rerun = await setup.RunAsync("rerun", "--cell", first.Id.ToString());

        rerun.Code.Should().Be(ExitCodes.Pass, rerun.Output + rerun.Error);
        var lineage = (await setup.NewStore().CellsAsync(runId, Ct)).Where(c => c.Probe == ProbeKind.ReadOutsideBare).OrderBy(c => c.Generation).ToList();
        lineage.Select(c => (c.Generation, c.State)).Should().Equal([(1, CellState.Settled), (2, CellState.Settled)], "a re-run appends; it never reopens (D2)");
        lineage[1].Artifacts.Should().OnlyContain(a => a.Path.Value.Contains("/g2/a1/"), "generation 2's evidence is its own");
        (await setup.NewStore().CellsAsync(runId, Ct)).Should().HaveCount(3, "only the named cell was re-measured");

        var report = JsonSerializer.Deserialize<ProbeRunReportDto>((await setup.RunAsync("report", "--run", runId.ToString(), "--json")).Output, Web)!;
        report.Cells.Single(c => c.Probe == "read-outside-bare").Should().Match<ProbeCellReportDto>(c => c.Generation == 2 && c.CellId == lineage[1].Id && c.LatestGeneration == 2);
        var status = await setup.RunAsync("status", "--run", runId.ToString());
        status.Code.Should().Be(ExitCodes.Pass);
        status.Output.Should().Contain("read-outside-bare/claude-fake/r1 g1").And.Contain("read-outside-bare/claude-fake/r1 g2", "the history stays");
    }

    [Fact]
    public async Task Rerun_of_a_subject_slice_appends_one_generation_per_lineage_and_refuses_while_its_cells_are_still_pending()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var codex = setup.AddSubject("codex-fake", "codex", new JsonObject { ["quotaFrom"] = 1 });
        var run = await setup.PlanAndRunAsync("read-inside,read-outside-bare", 1);
        var runId = ProbesCliSetup.RunIdOf(run.Output);

        var refused = await setup.RunAsync("rerun", "--run", runId.ToString(), "--subject", "codex-fake");
        refused.Code.Should().Be(ExitCodes.Configuration);
        refused.Error.Should().Contain("Pending").And.Contain($"bench probes resume --run {runId}");

        var slice = await setup.RunAsync("rerun", "--run", runId.ToString(), "--subject", "claude-fake", "--probe", "read-inside");
        slice.Code.Should().Be(ExitCodes.Pass, slice.Output + slice.Error);
        var cells = await setup.NewStore().CellsAsync(runId, Ct);
        cells.Where(c => c.Subject.Value == "claude-fake" && c.Probe == ProbeKind.ReadInside).Select(c => c.Generation).Should().BeEquivalentTo([1, 2]);
        cells.Where(c => c.Subject.Value == "claude-fake" && c.Probe == ProbeKind.ReadOutsideBare).Select(c => c.Generation).Should().Equal([1]);
        codex.Calls().Should().HaveCount(1, "the slice never reached the subject it did not name");
    }

    [Fact]
    public async Task Run_prepares_on_entry_handing_back_a_dead_owners_claim_before_it_claims()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var (earlier, cells) = ProbeStoreFixtures.Planned(count: 1);
        var store = setup.NewStore();
        (await store.PlanAsync(earlier, cells, Ct)).Ok();
        (await store.ClaimNextAsync(earlier.Id, earlier.Subjects[0].Id, TestWorkers.Dead("killed-bench"), ProbeStoreFixtures.Pin(), Ct)).Ok();

        var (code, output, error) = await setup.PlanAndRunAsync("read-inside", 1);

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain("prepared       1 claim(s) handed back");
        (await setup.NewStore().CellAsync(cells[0].Id, Ct)).Ok().State.Should().Be(CellState.Pending, "the sweep half of the entry step ran");
    }

    /// <summary>The bench was killed while its CLI hung: the cell is left Claimed by a process that is gone, with its fixture on disk.
    /// <c>resume</c>'s entry step hands the claim back and deletes the fixture, and the cell is measured in a fresh attempt — the
    /// hang is "left for resume", and never read as a verdict.</summary>
    [Fact]
    public async Task Resume_hands_back_a_cell_a_killed_bench_left_claimed_deletes_its_fixture_and_measures_it_in_a_fresh_attempt()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var (runId, cell) = await StrandedRunAsync(setup);
        var fixture = setup.PlantFixture(runId, cell);

        var (code, output, error) = await setup.RunAsync("resume", "--run", runId.ToString());

        code.Should().Be(ExitCodes.Pass, output + error);
        Directory.Exists(fixture).Should().BeFalse("a fixture a kill stranded is deleted on the next verb's entry (finding 3)");
        var resumed = (await setup.NewStore().CellAsync(cell, Ct)).Ok();
        resumed.State.Should().Be(CellState.Settled);
        resumed.Attempts.Should().Be(2, "the killed attempt stays counted; the measurement is attempt 2, in a directory of its own");
        resumed.Facts.Kind.Should().Be(ProbeAttemptKind.Answered);
    }

    [Fact]
    public async Task Rerun_prepares_on_entry_deleting_a_stranded_fixture_before_it_claims()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var run = await setup.PlanAndRunAsync("read-inside,read-outside-bare", 1);
        var runId = ProbesCliSetup.RunIdOf(run.Output);
        var cells = await setup.NewStore().CellsAsync(runId, Ct);
        var fixture = setup.PlantFixture(runId, cells[1].Id);

        var rerun = await setup.RunAsync("rerun", "--cell", cells[0].Id.ToString());

        rerun.Code.Should().Be(ExitCodes.Pass, rerun.Output + rerun.Error);
        Directory.Exists(fixture).Should().BeFalse();
    }

    [Fact]
    public async Task Sweep_runs_the_entry_step_alone_handing_back_a_dead_claim_and_deleting_its_fixture_and_claims_nothing()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        var fake = setup.AddSubject("claude-fake", "claude");
        var (runId, cell) = await StrandedRunAsync(setup);
        var fixture = setup.PlantFixture(runId, cell);
        var launched = fake.Calls().Count;

        var (code, output, error) = await setup.RunAsync("sweep", "--run", runId.ToString());

        code.Should().Be(ExitCodes.Pass, output + error);
        output.Should().Contain("1 claim(s) handed back").And.Contain("1 stranded fixture folder(s) deleted");
        Directory.Exists(fixture).Should().BeFalse();
        (await setup.NewStore().CellAsync(cell, Ct)).Ok().State.Should().Be(CellState.Pending, "handed back — and nothing was claimed again");
        fake.Calls().Should().HaveCount(launched, "sweep launches nothing");
    }

    [Fact]
    public async Task Prune_refuses_a_run_with_a_pending_or_claimed_cell_naming_that_state_and_deletes_nothing()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        var (run, cells) = ProbeStoreFixtures.Planned(count: 2);
        var store = setup.NewStore();
        (await store.PlanAsync(run, cells, Ct)).Ok();
        (await store.ClaimNextAsync(run.Id, run.Subjects[0].Id, WorkerIdentity.Here("live-lane"), ProbeStoreFixtures.Pin(), Ct)).Ok();
        var evidence = Path.Combine(setup.ArtifactRoot, ProbePaths.Folder, run.Id.ToString("D"), cells[0].Id.ToString("D"), "g1", "a1");
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "answer.txt"), "an answer", Ct);

        var (code, _, error) = await setup.RunAsync("prune", "--run", run.Id.ToString());

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("1 Pending and 1 Claimed");
        File.Exists(Path.Combine(evidence, "answer.txt")).Should().BeTrue("nothing is deleted while the run is open (finding 5)");
        (await setup.NewStore().LoadAsync(run.Id, Ct)).Ok().ArtifactsPruned.Should().BeFalse();
    }

    [Fact]
    public async Task Prune_of_a_settled_run_deletes_its_artefacts_flags_the_run_and_report_says_the_verdicts_are_no_longer_auditable()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var run = await setup.PlanAndRunAsync("read-inside", 1);
        var runId = ProbesCliSetup.RunIdOf(run.Output);
        var runFolder = Path.Combine(setup.ArtifactRoot, ProbePaths.Folder, runId.ToString("D"));
        var other = Path.Combine(setup.ArtifactRoot, ProbePaths.Folder, Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(other);
        Directory.EnumerateFiles(runFolder, "*", SearchOption.AllDirectories).Should().NotBeEmpty();

        var (code, output, error) = await setup.RunAsync("prune", "--run", runId.ToString());

        code.Should().Be(ExitCodes.Pass, output + error);
        Directory.Exists(runFolder).Should().BeFalse();
        Directory.Exists(other).Should().BeTrue("another run's artefacts are never touched");
        (await setup.NewStore().LoadAsync(runId, Ct)).Ok().ArtifactsPruned.Should().BeTrue();
        (await setup.RunAsync("report", "--run", runId.ToString())).Output.Should().Contain("no longer auditable from disk");
        JsonSerializer.Deserialize<ProbeRunReportDto>((await setup.RunAsync("report", "--run", runId.ToString(), "--json")).Output, Web)!
            .Should().Match<ProbeRunReportDto>(r => r.ArtifactsPruned && !r.Auditable);
        (await setup.RunAsync("rerun", "--run", runId.ToString(), "--subject", "claude-fake")).Should().Match<(int Code, string Output, string Error)>(r =>
            r.Code == ExitCodes.Configuration && r.Error.Contains("pruned"), "a pruned run measures nothing more");
    }

    /// <summary>Gate code round 2, finding 4: <c>prune</c> flags the run BEFORE it deletes, so a deletion the filesystem half-refuses
    /// leaves a run flagged pruned with artefacts still on disk. The guarantee is that this is recoverable — a prune of an already-pruned
    /// run retries whatever remains and exits 0 once nothing does, and a prune with nothing left is a 0, not a refusal. The first
    /// deletion is made to fail partway with a REAL filesystem refusal: a file held open without sharing on Windows, its folder made
    /// read-only on Unix (where an open handle does not stop an unlink).</summary>
    [Fact]
    public async Task A_prune_the_filesystem_half_refused_is_finished_by_a_second_prune_which_exits_0_and_a_third_with_nothing_left_still_exits_0()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var run = await setup.PlanAndRunAsync("read-inside", 2);
        run.Code.Should().Be(ExitCodes.Pass, run.Output + run.Error);
        var runId = ProbesCliSetup.RunIdOf(run.Output);
        var runFolder = Path.Combine(setup.ArtifactRoot, ProbePaths.Folder, runId.ToString("D"));
        var cellFolders = Directory.EnumerateDirectories(runFolder).Order(StringComparer.Ordinal).ToList();
        cellFolders.Should().HaveCount(2);
        var held = Directory.EnumerateFiles(cellFolders[1], "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).First();

        (int Code, string Output, string Error) first;
        using (HoldAgainstDeletion(held))
        {
            first = await setup.RunAsync("prune", "--run", runId.ToString());
        }

        first.Code.Should().Be(ExitCodes.Environment, first.Output + first.Error);
        first.Error.Should().Contain("flagged pruned").And.Contain("prune again");
        Directory.Exists(cellFolders[0]).Should().BeFalse("the deletion went partway — the folder nothing held is gone");
        File.Exists(held).Should().BeTrue("the folder the filesystem refused is still on disk");
        (await setup.NewStore().LoadAsync(runId, Ct)).Ok().ArtifactsPruned.Should().BeTrue();

        var second = await setup.RunAsync("prune", "--run", runId.ToString());

        second.Code.Should().Be(ExitCodes.Pass, "a prune of an already-pruned run retries what remains — " + second.Output + second.Error);
        second.Output.Should().Contain("1 cell folder(s) of artefacts deleted");
        Directory.Exists(runFolder).Should().BeFalse("the retry finished the cleanup");

        var third = await setup.RunAsync("prune", "--run", runId.ToString());

        third.Code.Should().Be(ExitCodes.Pass, "nothing left to delete is an answer, not a refusal — " + third.Output + third.Error);
        third.Output.Should().Contain("0 cell folder(s) of artefacts deleted");
    }

    /// <summary>A real refusal to delete <paramref name="file"/>, released on dispose: no-share open on Windows; a read-only parent
    /// folder on Unix — skipped when this user can write there anyway (root ignores the mode).</summary>
    private static IDisposable HoldAgainstDeletion(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
        }

        return ReadOnlyFolder.Hold(Path.GetDirectoryName(file)!);
    }

    private static bool CanWriteIn(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, ".write-probe");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A folder made read-only until disposed — an unlink inside it is refused for any user but root.</summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private sealed class ReadOnlyFolder(string folder, UnixFileMode mode) : IDisposable
    {
        public static ReadOnlyFolder Hold(string folder)
        {
            var held = new ReadOnlyFolder(folder, File.GetUnixFileMode(folder));
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            if (CanWriteIn(folder))
            {
                held.Dispose();
                Assert.Skip("this user writes through a read-only folder (root) — no filesystem refusal can be planted");
            }

            return held;
        }

        public void Dispose() => File.SetUnixFileMode(folder, mode);
    }

    /// <summary>A run of two read-inside cells for the setup's subject: the first settled through the verb, the second left Claimed by
    /// a bench process that is gone — the state a kill mid-attempt leaves.</summary>
    private static async Task<(Guid RunId, Guid Cell)> StrandedRunAsync(ProbesCliSetup setup)
    {
        var run = await setup.PlanAndRunAsync("read-inside", 2);
        run.Code.Should().Be(ExitCodes.Pass, run.Output + run.Error);
        var runId = ProbesCliSetup.RunIdOf(run.Output);
        var cell = (await setup.NewStore().CellsAsync(runId, Ct)).OrderBy(c => c.Slot).Last();

        await using var db = PostgresFixture.Context(setup.Connection);
        var dead = TestWorkers.Dead("probe-lane-killed");
        await db.ProbeCells.Where(c => c.Id == cell.Id).ExecuteUpdateAsync(u => u
            .SetProperty(c => c.State, CellState.Claimed)
            .SetProperty(c => c.Owner, dead.Label)
            .SetProperty(c => c.OwnerHost, dead.Host)
            .SetProperty(c => c.OwnerPid, dead.Pid)
            .SetProperty(c => c.ClaimedAt, DateTimeOffset.UtcNow), Ct);

        return (runId, cell.Id);
    }
}
