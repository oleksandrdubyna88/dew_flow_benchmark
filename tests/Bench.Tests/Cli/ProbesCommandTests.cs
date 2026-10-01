using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Application.Probes;
using Bench.Cli;
using Bench.Domain;
using Bench.Domain.Probes;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>S3 acceptance 1 and 5 through the verbs: the web oracle is read BEFORE anything is planned (D7) and never again;
/// every refusal is the exit code §5 names, in the order a person fixes things; <c>report --json</c> is the object the read
/// query answers, byte for byte — what S4's API will serve.</summary>
[Collection("postgres")]
public sealed class ProbesCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_reads_the_oracle_before_anything_is_planned_and_a_failed_read_exits_3_naming_the_cause_with_no_run_left()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        setup.Oracle.Answer = Outcome<ProbeOracle>.Failure("the npm registry answered HTTP 503 for https://registry.npmjs.org/@openai/codex/latest");

        var (code, output, error) = await setup.PlanAndRunAsync("read-inside", 1);

        code.Should().Be(ExitCodes.Environment, output + error);
        error.Should().Contain("HTTP 503").And.Contain("--oracle-version", "the refusal says how to pin the oracle by hand instead");
        setup.Oracle.Calls.Should().Be(1);
        await using var db = PostgresFixture.Context(setup.Connection);
        (await db.ProbeRuns.CountAsync(Ct)).Should().Be(0, "an oracle that cannot be read stops the run before anything is planned (D7)");
        (await db.ProbeCells.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task An_oracle_version_given_by_hand_is_stored_as_manual_and_the_registry_is_never_asked()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");

        var (code, output, error) = await setup.PlanAndRunAsync("read-inside", 1, "--oracle-version", "0.60.1");

        code.Should().Be(ExitCodes.Pass, output + error);
        setup.Oracle.Calls.Should().Be(0, "--oracle-version pins the oracle; the registry is not read");
        var run = (await setup.NewStore().LoadAsync(ProbesCliSetup.RunIdOf(output), Ct)).Ok();
        run.Oracle.Version.Should().Be("0.60.1");
        run.Oracle.Source.Should().Be(OracleSource.Manual);
        output.Should().Contain("0.60.1 (manual)");
    }

    [Fact]
    public async Task Resume_and_rerun_read_the_oracle_from_the_run_and_never_call_it()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var first = await setup.PlanAndRunAsync("read-inside,web-search", 1);
        first.Code.Should().Be(ExitCodes.Pass, first.Output + first.Error);
        var runId = ProbesCliSetup.RunIdOf(first.Output);
        var cell = (await setup.NewStore().CellsAsync(runId, Ct)).First(c => c.Probe == ProbeKind.WebSearch);
        setup.Oracle.Answer = Outcome<ProbeOracle>.Failure("the registry must not be read again");

        var rerun = await setup.RunAsync("rerun", "--cell", cell.Id.ToString());
        var resume = await setup.RunAsync("resume", "--run", runId.ToString());

        rerun.Code.Should().Be(ExitCodes.Pass, rerun.Output + rerun.Error);
        resume.Code.Should().Be(ExitCodes.NoReport, "nothing was pending, so nothing was produced — and the oracle was still not read");
        setup.Oracle.Calls.Should().Be(1, "run read it once; resume and rerun read it from the run (D7)");
        var again = (await setup.NewStore().CellsAsync(runId, Ct)).Single(c => c.Probe == ProbeKind.WebSearch && c.Generation == 2);
        again.Facts.AnswerCurrent.Should().Be(ProbeFact.Yes, "generation 2 is compared with the version frozen on the run");
    }

    [Fact]
    public async Task Configuration_refusals_exit_4_each_naming_what_to_fix()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        var gitRepo = setup.Root.Sibling("repo");
        Directory.CreateDirectory(Path.Combine(gitRepo, ".git"));

        try
        {
            (await setup.RunAsync("run", "--probes", "read-inside")).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("--subjects-file"));
            (await setup.PlanAndRunAsync("read-everything", 1)).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("read-everything") && r.Error.Contains("read-inside"));
            (await setup.PlanAndRunAsync("read-inside", 1, "--oracle-version", "latest")).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("not a version"));
            (await setup.PlanAndRunAsync("read-inside", 0)).Code.Should().Be(ExitCodes.Configuration);
            (await setup.PlanAndRunAsync("read-inside", 1, "--work-root", setup.ArtifactRoot)).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("overlap"));
            (await setup.PlanAndRunAsync("read-inside", 1, "--work-root", Path.Combine(gitRepo, "work"))).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("git checkout"));
            (await setup.RunAsync("resume")).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("--run"));
            (await setup.RunAsync("rerun", "--run", Guid.NewGuid().ToString())).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("--subject"));
            (await setup.RunAsync("resume", "--run", Guid.NewGuid().ToString())).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("no probe run"));
            (await setup.RunAsync("frobnicate")).Should().Match<(int Code, string Output, string Error)>(r =>
                r.Code == ExitCodes.Configuration && r.Error.Contains("frobnicate") && r.Error.Contains("rerun"));
            setup.Oracle.Calls.Should().Be(0, "every refusal came before the oracle");
            await using var db = PostgresFixture.Context(setup.Connection);
            (await db.ProbeRuns.CountAsync(Ct)).Should().Be(0);
        }
        finally
        {
            Directory.Delete(gitRepo, recursive: true);
        }
    }

    [Fact]
    public void A_probes_verb_with_no_database_is_refused_through_the_binarys_own_dispatch()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = Program.Run(["probes", "status", "--run", Guid.NewGuid().ToString(), "--db", string.Empty], output, error, Ct);

        code.Should().Be(ExitCodes.Configuration, error.ToString());
        error.ToString().Should().Contain("--db");
    }

    [Fact]
    public async Task A_subjects_file_that_is_not_there_is_the_environment_and_a_malformed_one_is_configuration()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);

        var missing = await setup.RunAsync("run", "--subjects-file", Path.Combine(setup.Root.Sibling("nowhere"), "subjects.json"), "--probes", "read-inside");
        missing.Code.Should().Be(ExitCodes.Environment);
        missing.Error.Should().Contain("is not there");

        Directory.CreateDirectory(Path.GetDirectoryName(setup.SubjectsFile)!);
        await File.WriteAllTextAsync(setup.SubjectsFile, new JsonObject { ["subjects"] = new JsonArray(new JsonObject { ["id"] = "claude-x", ["runtime"] = "claude", ["model"] = "m", ["executableRef"] = "C:\\tools\\claude.exe" }) }.ToJsonString(), Ct);
        var malformed = await setup.PlanAndRunAsync("read-inside", 1);
        malformed.Code.Should().Be(ExitCodes.Configuration);
        malformed.Error.Should().Contain("PATH", "a path where a variable NAME belongs is refused by name (D4)");
    }

    [Fact]
    public async Task An_unresolved_executable_reference_exits_4_naming_the_variable_before_the_oracle_and_plans_nothing()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        setup.AddSubject("codex-fake", "codex", resolvable: false);

        var (code, output, error) = await setup.PlanAndRunAsync("read-inside", 1);

        code.Should().Be(ExitCodes.Configuration, output + error);
        error.Should().Contain("codex-fake").And.Contain("BENCH_TEST_CODEX_FAKE");
        setup.Oracle.Calls.Should().Be(0, "a subject that cannot run here is refused before the registry is read");
        await using var db = PostgresFixture.Context(setup.Connection);
        (await db.ProbeRuns.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task A_codex_config_that_cannot_be_read_is_a_configuration_refusal_before_anything_is_planned()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("codex-fake", "codex");
        setup.CodexServers = () => Outcome<IReadOnlyList<string>>.Failure("the codex config config.toml exists and could not be read (IOException) — its MCP servers cannot be switched off");

        var (code, _, error) = await setup.PlanAndRunAsync("read-inside", 1);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("codex-fake").And.Contain("MCP servers");
        await using var db = PostgresFixture.Context(setup.Connection);
        (await db.ProbeRuns.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task An_unreachable_database_is_the_environment()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");

        var (code, _, error) = await setup.PlanAndRunAsync("read-inside", 1, "--db", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("unreachable");
    }

    [Fact]
    public async Task Report_json_is_the_read_querys_object_byte_for_byte_and_the_text_names_the_build_and_the_rerun_command()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);
        setup.AddSubject("claude-fake", "claude");
        setup.AddSubject("codex-fake", "codex", new JsonObject { ["mode"] = "refuse" });
        var run = await setup.PlanAndRunAsync("read-inside,read-outside-bare", 1);
        run.Code.Should().Be(ExitCodes.Pass, run.Output + run.Error);
        var runId = ProbesCliSetup.RunIdOf(run.Output);

        var json = await setup.RunAsync("report", "--run", runId.ToString(), "--json");
        var text = await setup.RunAsync("report", "--run", runId.ToString());

        json.Code.Should().Be(ExitCodes.Pass, json.Error);
        await using var db = PostgresFixture.Context(setup.Connection);
        var expected = (await ProbeReport.ReadAsync(new PostgresProbeReads(db), runId, Ct)).Ok();
        json.Output.TrimEnd().Should().Be(JsonSerializer.Serialize(expected, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "the CLI prints the object the API will answer — one shape, two surfaces");
        expected.Cells.Should().HaveCount(4);
        var refused = expected.Cells.Where(c => c.Subject == "codex-fake").ToList();
        refused.Should().OnlyContain(c => c.Kind == "launch-refused" && c.Exit.Captured && c.Exit.Code == 2 && c.Facts.CanaryRead == "not-captured");
        text.Code.Should().Be(ExitCodes.Pass);
        text.Output.Should().Contain("launch-refused (exit 2)").And.Contain("fake-cli 1.0.0-fake").And.Contain($"bench probes rerun --cell {refused[0].CellId}");
    }

    [Fact]
    public async Task Report_of_an_unknown_run_is_configuration()
    {
        await using var setup = await ProbesCliSetup.StartAsync(postgres);

        var (code, _, error) = await setup.RunAsync("report", "--run", Guid.NewGuid().ToString());

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("no probe run");
    }
}
