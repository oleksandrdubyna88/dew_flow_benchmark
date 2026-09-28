using Bench.Cli;
using Bench.Tests.Gate.Import;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Cli;

/// <summary><c>bench gate import</c> — the exit-code contract, driven through <see cref="Program.Run"/>: flags 4, a source
/// that is not there 3, and a real import of the redacted fixture that exits 0 and then 0 again with nothing new.</summary>
[Collection("postgres")]
public sealed class GateImportCommandTests(PostgresFixture postgres)
{
    [Fact]
    public void An_import_without_a_source_word_or_its_flags_is_a_configuration_refusal()
    {
        Run("gate", "import").Code.Should().Be(ExitCodes.Configuration);
        Run("gate", "import", "calib", "--db", "Host=x").Error.Should().Contain("--calib");
        Run("gate", "import", "coai-bench", "--db", "Host=x").Error.Should().Contain("--runs");
        Run("gate", "import", "summary", "--db", "Host=x", "--document", "d.md", "--section", "T").Error.Should().Contain("--gate");
    }

    [Fact]
    public void A_source_that_is_not_there_is_an_environment_refusal()
    {
        using var root = NewRoot();

        var (code, _, error) = Run("gate", "import", "calib", "--calib", Path.Combine(root.Path, "nowhere"), "--suite-file", "s.json",
            "--calib-models", "m.json", "--artifact-root", root.Path, "--db", "Host=x");

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("not a directory that exists");
    }

    [Fact]
    public async Task A_calibration_import_exits_zero_and_a_second_one_imports_nothing()
    {
        using var rig = await ImportRig.NewAsync(postgres);
        using var files = NewRoot();
        var suite = Path.Combine(files.Path, "suite.json");
        var models = Path.Combine(files.Path, "models.json");
        await File.WriteAllTextAsync(suite, ImportFixture.SuiteJson, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(models, ImportFixture.Root["models"]!.ToJsonString(), TestContext.Current.CancellationToken);
        string[] args = ["gate", "import", "calib", "--calib", rig.Source, "--suite-file", suite, "--calib-models", models,
            "--artifact-root", rig.Root, "--db", rig.Connection, "--assessor", "codex-astra", "--prompts", Path.Combine(Repository.Root, "prompts")];

        var first = Run(args);
        var second = Run(args);

        first.Code.Should().Be(ExitCodes.Pass, first.Error);
        first.Output.Should().Contain("imported       92 cell(s)").And.Contain("340 under strict-v1 (340 new");
        second.Code.Should().Be(ExitCodes.Pass, second.Error);
        second.Output.Should().Contain("0 cell(s), 92 already there unchanged").And.Contain("(0 new");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
