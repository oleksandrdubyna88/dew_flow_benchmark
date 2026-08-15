using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench run` — the refusals, which are the half an agent actually depends on.
/// <para>
/// The execution half is proved by <c>LegRunnerTests</c> against real Postgres; what is left here is
/// the boundary: every way an invocation can be wrong must come back as a DIFFERENT code from "the
/// subject answered badly". A run that cannot start has measured nothing, and an agent that reads
/// that as a regression will report one.
/// </para></summary>
public sealed class RunCommandTests
{
    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    [Fact]
    public void An_unset_suite_file_is_the_callers_mistake_and_a_missing_one_is_the_machines()
    {
        Run("run", "--repo", Repo, "--commit", Sha).Code
            .Should().Be(ExitCodes.Configuration, "an unset flag is an invocation error");

        var (code, _, error) = Run("run", "--repo", Repo, "--commit", Sha, "--suite-file", "nowhere.json");

        code.Should().Be(ExitCodes.Environment, "a named file that is not there is the environment");
        error.Should().Contain("suite file not found");
    }

    [Fact]
    public void A_run_without_a_store_is_refused_before_anything_is_spent()
    {
        using var suite = new TempRunSuite();

        var (code, _, error) = Run("run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--model", "qwen@local", "--model-url", "http://127.0.0.1:11434/v1");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("not durable is not a run");
    }

    [Fact]
    public void An_unset_model_is_refused_rather_than_defaulted()
    {
        using var suite = new TempRunSuite();

        var (code, _, error) = Run("run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--db", Connection, "--model-url", "http://127.0.0.1:11434/v1");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("the model id is unset");
    }

    [Fact]
    public void An_unset_model_url_is_refused_rather_than_pointed_at_a_default_port()
    {
        using var suite = new TempRunSuite();

        var (code, _, error) = Run("run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--db", Connection, "--model", "qwen@local");

        code.Should().Be(ExitCodes.Configuration, "guessing localhost:11434 is how an arm gets measured against the wrong endpoint");
        error.Should().Contain("has no base url");
    }

    [Fact]
    public void An_abbreviated_commit_is_refused_before_the_store_is_touched()
    {
        using var suite = new TempRunSuite();

        var (code, _, error) = Run("run", "--repo", Repo, "--commit", "a603169", "--suite-file", suite.Path,
            "--db", Connection, "--model", "qwen@local", "--model-url", "http://127.0.0.1:11434/v1");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("abbreviations are ambiguous");
    }

    [Fact]
    public void An_unreachable_store_is_an_environment_problem_not_a_regression()
    {
        using var suite = new TempRunSuite();

        var (code, _, error) = Run("run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--db", "Host=127.0.0.1;Port=1;Database=bench;Username=postgres;Password=x;Timeout=2",
            "--model", "qwen@local", "--model-url", "http://127.0.0.1:11434/v1");

        code.Should().Be(ExitCodes.Environment);
        code.Should().NotBe(ExitCodes.Regression, "nothing was measured, so nothing regressed");
        error.Should().Contain("store is not reachable");
    }

    [Fact]
    public void The_help_text_documents_the_run_verb()
    {
        var (_, output, _) = Run("help");

        output.Should().Contain("bench run").And.Contain("--model-url").And.Contain("--db");
    }

    private const string Repo = "https://github.com/App-vNext/Polly.git";
    private const string Connection = "Host=127.0.0.1;Port=1;Database=bench;Username=postgres;Password=x";

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TempRunSuite : IDisposable
    {
        public TempRunSuite()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-run-suite-{Guid.NewGuid():N}.json");
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
