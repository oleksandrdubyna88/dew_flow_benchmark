using Bench.Cli;
using Bench.Infrastructure.Engines;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench run` and `bench prune`, on the retrieval flags.
/// <para>
/// The refusals are what these tests are for. A run that names a variant with no engine to serve it, or an
/// engine named halfway, would measure the no-retrieval baseline while its operator believed otherwise — and
/// nothing in the report would say so, because every number would be internally consistent.
/// </para></summary>
[Collection("postgres")]
public sealed class RunRetrievalTests(PostgresFixture postgres)
{
    private const string Repo = "https://github.com/App-vNext/Polly.git";
    private const string Sha = "b7c4a3f2e1d0c9b8a7f6e5d4c3b2a1908f7e6d5c";
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    [Fact]
    public void A_variant_with_no_engine_to_serve_it_is_refused_before_anything_is_created()
    {
        using var suite = new TempSuiteFile();

        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--variants", "hybrid-rrf");

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("no engine serves them").And.Contain("--engine-url");
    }

    [Fact]
    public void An_engine_named_halfway_is_refused_rather_than_silently_measuring_the_baseline()
    {
        using var suite = new TempSuiteFile();

        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--engine-url", "http://127.0.0.1:5311");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("go together").And.Contain("looks configured for retrieval");
    }

    [Fact]
    public void A_variant_the_catalog_does_not_have_is_named_rather_than_skipped()
    {
        using var suite = new TempSuiteFile();

        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--engine-url", "http://127.0.0.1:5311", "--engine-project", Guid.NewGuid().ToString(),
            "--variants", "no-such-variant");

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("no-such-variant");
    }

    [Fact]
    public void A_run_with_no_engine_still_warns_that_its_lane_surfaces_nothing()
    {
        using var suite = new TempSuiteFile();

        var (_, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint);

        // The memorisation check is still the honest reading of a no-retrieval, no-tools run.
        output.Should().Contain("this lane surfaces nothing");
    }

    [Fact]
    public void A_run_that_retrieves_reports_its_engine_and_stops_claiming_the_lane_surfaces_nothing()
    {
        using var suite = new TempSuiteFile();
        var project = Guid.NewGuid();

        var (_, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--engine-url", "http://127.0.0.1:5311", "--engine-project", project.ToString());

        output.Should().Contain("engine   http://127.0.0.1:5311").And.Contain(project.ToString("D"));
        output.Should().NotContain(
            "this lane surfaces nothing",
            "left as a lane check alone, this line would have kept printing over a run whose every cell was "
            + "fed retrieved context, with anchor recall a real number underneath it");
    }

    [Fact]
    public void A_run_prunes_old_hit_snippets_at_startup_beside_its_sweep()
    {
        using var suite = new TempSuiteFile();

        var (_, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint);

        // Retention that only runs when somebody remembers to run it is the finding that left the crash
        // sweep unreachable for its first two weeks.
        output.Should().Contain("pruned").And.Contain("day(s)");
    }

    [Fact]
    public void Retention_can_be_switched_off_for_a_database_that_will_be_published_as_is()
    {
        using var suite = new TempSuiteFile();

        var (_, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path, "--no-checkout",
            "--db", postgres.ConnectionString, "--model", "qwen@local", "--model-url", DeadEndpoint,
            "--hit-retention-days", "0");

        output.Should().NotContain("pruned", "zero means keep everything, which is a legitimate choice");
    }

    [Fact]
    public void The_prune_verb_refuses_to_read_zero_as_drop_everything()
    {
        var (code, _, error) = Run("prune", "--db", postgres.ConnectionString, "--hit-retention-days", "0");

        // The same value means "keep everything" on a run. A destructive reading of a value whose other
        // reading is the opposite is not a reading to guess at.
        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("keep everything");
    }

    [Fact]
    public void The_prune_verb_reports_what_it_released_and_over_what_window()
    {
        var (code, output, _) = Run("prune", "--db", postgres.ConnectionString, "--hit-retention-days", "3650");

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("pruned").And.Contain("3650 day(s)");
    }

    [Fact]
    public void A_prune_without_a_store_is_refused_rather_than_pointed_at_a_default_database()
    {
        var (code, _, error) = Run("prune");

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("--db");
    }

    [Fact]
    public void The_help_text_names_the_retrieval_flags_and_the_prune_verb()
    {
        var (_, output, _) = Run("help");

        output.Should().Contain("--engine-url").And.Contain("--variants").And.Contain("--hit-retention-days");
        output.Should().Contain("bench prune");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TempSuiteFile : IDisposable
    {
        public TempSuiteFile()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"bench-retrieval-suite-{Guid.NewGuid():N}.json");

            File.WriteAllText(Path, """
            {
              "id": "demo",
              "questions": [
                { "id": "q1", "prompt": "how is the retry delay computed?",
                  "expectations": [ { "kind": "AnswerContains", "text": "Backoff" } ] }
              ]
            }
            """);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
