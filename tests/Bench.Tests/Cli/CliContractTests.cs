using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The CLI's contract, tested because its first consumer is an agent.
/// <para>
/// The load-bearing distinction is exit 1 against exit 3/4: "the measurement ran and the answer is
/// bad" must never be confused with "the harness could not run". An agent that cannot tell them apart
/// reports a regression when the real news is a missing file — confidently, and repeatedly.
/// </para></summary>
public sealed class CliContractTests
{
    [Fact]
    public void A_missing_suite_file_is_an_environment_problem_not_a_regression()
    {
        var (code, _, error) = Run("plan", "--repo", "https://example.invalid/x.git",
            "--commit", new string('e', 40), "--suite-file", "does-not-exist.json", "--subjects", "m@local");

        code.Should().Be(ExitCodes.Environment);
        code.Should().NotBe(ExitCodes.Regression, "a harness that cannot start has measured nothing");
        error.Should().Contain("suite file not found");
    }

    [Fact]
    public void A_malformed_commit_is_a_configuration_problem()
    {
        using var suite = new TempSuite();

        var (code, _, error) = Run("plan", "--repo", "https://example.invalid/x.git",
            "--commit", "abc123", "--suite-file", suite.Path, "--subjects", "m@local");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("abbreviations are ambiguous");
    }

    [Fact]
    public void An_unset_subject_is_refused_rather_than_defaulted()
    {
        using var suite = new TempSuite();

        var (code, _, error) = Run("plan", "--repo", "https://example.invalid/x.git",
            "--commit", new string('e', 40), "--suite-file", suite.Path);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("at least one subject");
    }

    [Fact]
    public void An_unknown_verb_is_a_configuration_problem_and_says_so()
    {
        var (code, _, error) = Run("mesure");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("unknown command 'mesure'");
    }

    [Fact]
    public void Help_lists_the_exit_code_contract()
    {
        var (code, output, _) = Run("help");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("0 pass").And.Contain("1 regression").And.Contain("3 environment");
    }

    [Fact]
    public void A_well_formed_plan_reports_the_split_the_order_and_its_warnings()
    {
        using var suite = new TempSuite();

        var (code, output, _) = Run("plan", "--repo", "https://example.invalid/x.git",
            "--commit", new string('e', 40), "--suite-file", suite.Path,
            "--subjects", "qwen@local,opus@cloud", "--lanes", "native,retrieval", "--repeats", "3");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("matrix   2 x 3 repeat(s) x 4 leg(s) = 24 cell(s)");
        output.Should().Contain("warn").And.Contain("no exclusions");
        output.Should().Contain("opus is a billable cloud model");
        output.Should().NotContain("repeats = 1", "three repeats can support a ranking");
    }

    [Fact]
    public void A_single_repeat_is_allowed_and_warned_about()
    {
        using var suite = new TempSuite();

        var (_, output, _) = Run("plan", "--repo", "https://example.invalid/x.git",
            "--commit", new string('e', 40), "--suite-file", suite.Path, "--subjects", "qwen@local");

        output.Should().Contain("repeats = 1").And.Contain("cannot support a ranking");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Bench.Cli.Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TempSuite : IDisposable
    {
        public TempSuite()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-suite-{Guid.NewGuid():N}.json");
            File.WriteAllText(Path, """
            {
              "id": "demo",
              "questions": [
                { "id": "q1", "prompt": "where is the order total computed?",
                  "expectations": [ { "kind": "Member", "file": "src/Orders.cs", "member": "OrderService.Total", "start": 10, "end": 24 } ] },
                { "id": "q2", "prompt": "what invalidates the cache?",
                  "expectations": [ { "kind": "File", "file": "src/Cache.cs" } ] }
              ]
            }
            """);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
