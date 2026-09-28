using System.Text.Json;
using Bench.Application.Gate;
using Bench.Cli;
using Bench.Contracts;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate.Import;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Cli;

/// <summary><c>bench gate report</c> and <c>bench gate suite record</c> (E6), through <see cref="Program.Run"/>: the report is
/// the SAME object the API answers (<c>--json</c>), a malformed ask is 4 naming what to pass, a scope whose suite was never
/// recorded is 3 naming the verb that records it, and the import records the suite it imports under.</summary>
[Collection("postgres")]
public sealed class GateReportCommandTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_calibration_import_records_its_suite_and_the_report_prints_the_object_the_api_answers()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        using var files = NewRoot();
        var suite = await SuiteFileAsync(files);

        var imported = Run(await ImportArgsAsync(rig, files, suite));
        var report = Run("gate", "report", "--gate", "feature", "--scope", ImportFixture.Suite.Stamp, "--rubric", "strict-v1", "--db", rig.Connection, "--json");

        imported.Code.Should().Be(ExitCodes.Pass, imported.Error);
        imported.Output.Should().Contain($"suite          {ImportFixture.Suite.Stamp} — 7 task(s) recorded");
        report.Code.Should().Be(ExitCodes.Pass, report.Error);
        var reads = new PostgresGateReads(rig.Db(), TimeProvider.System);
        var scope = ((GateAnswer<IReadOnlyList<GateScopeDto>>.Answered)await GateReportQuery.ScopesAsync(reads, "feature", Ct)).Value.Single();
        var api = ((GateAnswer<GateModelTableDto>.Answered)await GateReportQuery.ModelsAsync(reads, "feature", scope.Id, "strict-v1", Ct)).Value;
        report.Output.Trim().Should().Be(JsonSerializer.Serialize(api, Web), "one mapping, two surfaces — --json is the API's body byte for byte");
    }

    [Fact]
    public async Task The_text_report_puts_calibration_apart_and_says_every_withheld_figure_in_words()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        using var files = NewRoot();
        (await rig.ImportAsync(Ct)).Ok();
        Run("gate", "suite", "record", "--suite-file", await SuiteFileAsync(files), "--db", rig.Connection).Code.Should().Be(ExitCodes.Pass);

        var (code, output, error) = Run("gate", "report", "--gate", "feature", "--scope", ImportFixture.Suite.Stamp, "--rubric", "strict-v1", "--db", rig.Connection);

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("calibration tasks — reported apart").And.Contain("all tasks, calibration included");
        output.Should().Contain("not hand-checked", "a strict percentage nobody has checked is withheld in words, never printed");
        output.Should().Contain("imported from calib-py", "an imported scope says where its runs came from");
    }

    [Fact]
    public async Task A_report_asked_without_a_scope_or_a_rubric_is_refused_naming_what_there_is_to_choose()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        using var files = NewRoot();
        (await rig.ImportAsync(Ct)).Ok();
        var scope = await GateReportQuery.ScopesAsync(new PostgresGateReads(rig.Db(), TimeProvider.System), "feature", Ct);
        var id = ((GateAnswer<IReadOnlyList<GateScopeDto>>.Answered)scope).Value.Single().Id;

        var noScope = Run("gate", "report", "--gate", "feature", "--db", rig.Connection);
        var noRubric = Run("gate", "report", "--gate", "feature", "--scope", id, "--db", rig.Connection);
        var noGate = Run("gate", "report", "--gate", "7", "--scope", id, "--rubric", "strict-v1", "--db", rig.Connection);

        noScope.Code.Should().Be(ExitCodes.Configuration);
        noScope.Error.Should().Contain(id, "a refusal that names the scopes there are sends its reader to one of them");
        noRubric.Code.Should().Be(ExitCodes.Configuration);
        noRubric.Error.Should().Contain("strict-v1#");
        noGate.Code.Should().Be(ExitCodes.Configuration);
        noGate.Error.Should().Contain("plan, code or feature");
    }

    [Fact]
    public async Task A_scope_whose_suite_was_never_recorded_is_the_environment_until_suite_record_records_it_once()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        using var files = NewRoot();
        (await rig.ImportAsync(Ct)).Ok();
        var suite = await SuiteFileAsync(files);
        string[] report = ["gate", "report", "--gate", "feature", "--scope", ImportFixture.Suite.Stamp, "--rubric", "strict-v1", "--db", rig.Connection];

        var before = Run(report);
        var recorded = Run("gate", "suite", "record", "--suite-file", suite, "--db", rig.Connection);
        var again = Run("gate", "suite", "record", "--suite-file", suite, "--db", rig.Connection);
        var after = Run(report);

        before.Code.Should().Be(ExitCodes.Environment);
        before.Error.Should().Contain("bench gate suite record");
        recorded.Code.Should().Be(ExitCodes.Pass, recorded.Error);
        recorded.Output.Should().Contain($"suite          {ImportFixture.Suite.Stamp} — 7 task(s) recorded");
        again.Output.Should().Contain("0 task(s) recorded (already there)");
        after.Code.Should().Be(ExitCodes.Pass, after.Error);
    }

    [Fact]
    public void Suite_record_without_its_flags_is_a_configuration_refusal() =>
        Run("gate", "suite", "record", "--db", "Host=x").Should()
            .Match<(int Code, string Output, string Error)>(r => r.Code == ExitCodes.Configuration && r.Error.Contains("--suite-file"));

    private static async Task<string> SuiteFileAsync(TempRoot files)
    {
        var suite = Path.Combine(files.Path, "suite.json");
        await File.WriteAllTextAsync(suite, ImportFixture.SuiteJson, Ct);
        return suite;
    }

    private static async Task<string[]> ImportArgsAsync(ImportRig rig, TempRoot files, string suite)
    {
        var models = Path.Combine(files.Path, "models.json");
        await File.WriteAllTextAsync(models, ImportFixture.Root["models"]!.ToJsonString(), Ct);
        return ["gate", "import", "calib", "--calib", rig.Source, "--suite-file", suite, "--calib-models", models,
            "--artifact-root", rig.Root, "--db", rig.Connection, "--assessor", "codex-astra", "--prompts", Path.Combine(Repository.Root, "prompts")];
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
