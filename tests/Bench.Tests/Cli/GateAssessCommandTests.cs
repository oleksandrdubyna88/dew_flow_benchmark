using Bench.Cli;
using Bench.Domain.Gate;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate;
using Bench.Tests.Gate.Assessment;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>S4.6 through <see cref="Program.Run"/>: <c>bench gate assess</c> and <c>bench gate hand-check</c>, refused in the
/// order a person fixes things.</summary>
[Collection("postgres")]
public sealed class GateAssessCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("--assessor", "")]
    [InlineData("--batch-size", "25")]
    [InlineData("--scope", "both")]
    [InlineData("--run", "")]
    public void A_bad_invocation_is_a_configuration_error_before_anything_is_opened(string flag, string value)
    {
        var args = new List<string> { "gate", "assess", "--db", "Host=x", "--artifact-root", "a", "--suite-file", "s.json", "--assessor", "codex-astra", "--run", Guid.NewGuid().ToString() };
        Apply(args, flag, value);

        var (code, _, error) = Run([.. args]);

        code.Should().Be(ExitCodes.Configuration, error);
        error.Should().Contain(flag switch
        {
            "--assessor" => "--assessor",
            "--batch-size" => "1 to 24",
            "--scope" => "name the same thing twice",
            _ => "--run <id>",
        });
    }

    [Fact]
    public void A_suite_file_that_is_not_there_is_the_environment()
    {
        var (code, _, error) = Run("gate", "assess", "--db", "Host=x", "--artifact-root", "a", "--assessor", "codex-astra", "--run", Guid.NewGuid().ToString(),
            "--suite-file", Path.Combine(Path.GetTempPath(), $"no-such-suite-{Guid.NewGuid():N}.json"));

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("is not there");
    }

    [Fact]
    public async Task A_run_of_another_suite_an_unknown_assessor_and_an_api_assessor_are_each_refused_by_name()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 1, ct: Ct);
        var suite = Path.Combine(rig.Root, "suite.json");
        await File.WriteAllTextAsync(suite, AssessRig.SuiteJson, Ct);
        var apiId = $"api-{Guid.NewGuid():N}"[..12];
        await using (var db = postgres.NewContext())
        {
            (await new PostgresGateReviewerCatalog(db).AddAsync(
                GateReviewer.Create(GateReviewerId.Parse(apiId).Ok(), GateReviewerTests.Definition().Ok(), DateTimeOffset.UtcNow), Ct)).Ok();
        }

        string[] Args(string assessor, params string[] more) =>
            ["gate", "assess", "--db", postgres.ConnectionString, "--artifact-root", rig.Root, "--suite-file", suite, "--assessor", assessor,
                "--prompts", Path.Combine(Repository.Root, "prompts"), .. more];

        var other = Run(Args("codex-astra", "--scope", "another-suite#000000000000"));
        other.Code.Should().Be(ExitCodes.Configuration);
        other.Error.Should().Contain("is not the suite file's stamp");

        var unknown = Run(Args("nobody-here", "--run", rig.Campaign.ToString()));
        unknown.Code.Should().Be(ExitCodes.Configuration);
        unknown.Error.Should().Contain("nobody-here");

        var api = Run(Args(apiId, "--run", rig.Campaign.ToString()));
        api.Code.Should().Be(ExitCodes.Configuration, api.Error);
        api.Error.Should().Contain("a codex or claude row");
    }

    [Fact]
    public async Task A_hand_check_sample_over_too_few_verdicts_is_refused_and_record_needs_its_file()
    {
        using var rig = await AssessRig.SettledAsync(postgres, cells: 1, findings: 2, ct: Ct);
        string[] Args(params string[] verb) =>
            ["gate", "hand-check", .. verb, "--db", postgres.ConnectionString, "--artifact-root", rig.Root, "--assessor", "codex-astra",
                "--run", rig.Campaign.ToString(), "--prompts", Path.Combine(Repository.Root, "prompts")];

        var sample = Run(Args("sample"));
        sample.Code.Should().Be(ExitCodes.Configuration);
        sample.Error.Should().Contain("reads at least 20");

        Run(Args("record")).Error.Should().Contain("--file");
        Run(Args("tally")).Error.Should().Contain("sample or record");
    }

    private static void Apply(List<string> args, string flag, string value)
    {
        var at = args.IndexOf(flag);
        switch (flag)
        {
            case "--scope":
                args.AddRange(["--scope", "gate-assess-test#000000000000"]);
                break;
            case "--batch-size":
                args.AddRange(["--batch-size", value]);
                break;
            default:
                args.RemoveRange(at, 2);
                break;
        }
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
