using System.Text;
using Bench.Cli;
using Bench.Domain.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Cli;

/// <summary><c>bench gate export</c> and <c>bench gate prune</c> — the exit-code contract and the words, driven
/// through <see cref="Program.Run"/> exactly as an agent drives the binary.</summary>
[Collection("postgres")]
public sealed class CoaiGateCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Export_writes_the_public_export_only()
    {
        Run("gate", "export", "--db", "Host=x", "--out", "x.json", "--suite-file", "s.json")
            .Should().Match<(int Code, string Output, string Error)>(r => r.Code == ExitCodes.Configuration && r.Error.Contains("--public"));
    }

    [Fact]
    public void Export_without_the_suites_private_names_is_refused()
    {
        var (code, _, error) = Run("gate", "export", "--public", "--db", "Host=x", "--out", "x.json");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("private names").And.Contain("cannot prove it carries none");
    }

    [Fact]
    public async Task Export_of_a_clean_database_writes_rows_and_exits_zero_and_a_dirty_one_writes_nothing()
    {
        var connection = await postgres.NewDatabaseAsync($"gate_cli_{Guid.NewGuid():N}");
        var store = new PostgresGateStore(PostgresFixture.Context(connection), new TestClock(Noon));
        var (run, cells) = Planned(count: 1);
        await store.PlanAsync(run, cells, Ct);
        using var temp = NewRoot();
        var suite = Path.Combine(temp.Path, "suite.json");
        await File.WriteAllTextAsync(suite, "{\"privateNames\":[\"contoso-orders\"]}", Ct);
        var clean = Path.Combine(temp.Path, "clean.json");

        var (code, output, _) = Run("gate", "export", "--public", "--db", connection, "--suite-file", suite, "--out", clean);

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("through the publication guard");
        (await File.ReadAllTextAsync(clean, Ct)).Should().Contain(run.Id.ToString()).And.Contain("\"gate_cells\"");

        await using (var db = PostgresFixture.Context(connection))
        {
            db.GateReviewers.Add(new GateReviewerRow { Id = "host-planted", Hash = new string('4', 64), Model = "m", RemoteVendor = Environment.MachineName, AddedAt = Noon });
            await db.SaveChangesAsync(Ct);
        }

        var hosted = Run("gate", "export", "--public", "--db", connection, "--suite-file", suite, "--out", Path.Combine(temp.Path, "hosted.json"));
        hosted.Code.Should().Be(ExitCodes.NoReport, "the CLI hands the guard this machine's name");
        hosted.Error.Should().Contain(PublicationGuard.HostRule);
        await using (var db = PostgresFixture.Context(connection))
        {
            await db.GateReviewers.Where(r => r.Id == "host-planted").ExecuteDeleteAsync(Ct);
        }

        await using (var db = PostgresFixture.Context(connection))
        {
            await db.GateRuns.Where(r => r.Id == run.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.SuiteStamp, "contoso-orders#abc"), Ct);
        }

        var dirty = Path.Combine(temp.Path, "dirty.json");
        var refused = Run("gate", "export", "--public", "--db", connection, "--suite-file", suite, "--out", dirty);

        refused.Code.Should().Be(ExitCodes.NoReport, "nothing was exported — never a pass");
        refused.Error.Should().Contain("gate_runs.SuiteStamp").And.NotContain("contoso");
        File.Exists(dirty).Should().BeFalse("one dirty value refuses the whole export");
    }

    [Fact]
    public async Task An_export_that_cannot_be_written_is_an_environment_failure_and_leaves_no_partial_file()
    {
        var connection = await postgres.NewDatabaseAsync($"gate_cli_{Guid.NewGuid():N}");
        using var temp = NewRoot();
        var suite = Path.Combine(temp.Path, "suite.json");
        await File.WriteAllTextAsync(suite, "{\"privateNames\":[]}", Ct);
        var target = Path.Combine(temp.Path, "no-such-directory", "export.json");

        var (code, _, error) = Run("gate", "export", "--public", "--db", connection, "--suite-file", suite, "--out", target);

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("could not be written");
        Directory.Exists(Path.GetDirectoryName(target)).Should().BeFalse();
        Run("gate", "export", "--public", "--db", connection, "--suite-file", Path.Combine(temp.Path, "missing.json"), "--out", target)
            .Error.Should().Contain("Could not find", "the refusal says what went wrong, not only an exception's type name");
    }

    [Fact]
    public async Task Prune_defaults_to_thirty_days_and_a_dry_run_lists_and_deletes_nothing()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var scope = new ArtifactScope(GateStoreFixtures.Run(), Guid.CreateVersion7(), 1);
        await store.BeginAttemptAsync(scope, Ct);
        var body = CellPaths.AttemptRoot(scope).Then("tap/call-01.request.json").Ok();
        await store.WriteAsync(scope, ArtifactClass.TapRequest, body, Encoding.UTF8.GetBytes("{}"), Ct);
        await store.WriteAsync(scope, ArtifactClass.TapFacts, CellPaths.AttemptRoot(scope).Then("tap/call-01.json").Ok(), Encoding.UTF8.GetBytes("{}"), Ct);
        await store.WriteAsync(scope, ArtifactClass.RunRecord, CellPaths.AttemptRoot(scope).Then(CellPaths.RunRecordFile).Ok(), Encoding.UTF8.GetBytes("{}"), Ct);
        var bodyFile = Path.Combine([store.Root, .. body.Segments]);
        File.SetLastWriteTimeUtc(bodyFile, DateTime.UtcNow.AddDays(-(CoaiGateCommand.DefaultTapRetentionDays + 1)));

        var (code, output, _) = Run("gate", "prune", "--artifact-root", temp.Path, "--dry-run");

        code.Should().Be(ExitCodes.Pass);
        CoaiGateCommand.DefaultTapRetentionDays.Should().Be(30);
        output.Should().Contain("would release").And.Contain("past 30 day(s) WOULD be released").And.Contain($"footprint      run {scope.Run.Id}: ");
        File.Exists(bodyFile).Should().BeTrue("a dry run deletes nothing");

        var pruned = Run("gate", "prune", "--artifact-root", temp.Path);

        pruned.Output.Should().Contain("pruned         1 tap body file(s)");
        File.Exists(bodyFile).Should().BeFalse();
    }

    [Fact]
    public void Prune_refuses_an_artefact_root_inside_a_git_checkout_and_a_non_positive_window()
    {
        using var temp = NewRoot();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".git"));

        Run("gate", "prune", "--artifact-root", Path.Combine(temp.Path, "a")).Error.Should().Contain("inside the git checkout");
        Run("gate", "prune", "--artifact-root", temp.Path, "--tap-retention-days", "0").Code.Should().Be(ExitCodes.Configuration);
    }

    [Fact]
    public void An_unknown_gate_sub_verb_is_a_configuration_error()
    {
        Run("gate", "frobnicate").Should().Match<(int Code, string Output, string Error)>(r => r.Code == ExitCodes.Configuration && r.Error.Contains("export or prune") && r.Error.Contains("run, resume, status, sweep"));
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
