using Bench.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>The registry from the outside, and the run that draws on it.
/// <para>
/// Two properties carry this step. First, what an operator types is a REFERENCE: pasting the endpoint
/// itself is refused, because this database is published unedited. Second, a test composed from registry
/// keys measures every one of them — a two-subject run plans twice the cells and records both choices on
/// the test, so a registry edit next month cannot change what it says it measured.
/// </para></summary>
[Collection("postgres")]
public sealed class ModelsCommandTests(PostgresFixture postgres) : IDisposable
{
    private const string Repo = "https://github.com/App-vNext/Polly.git";
    private const string Sha = "a603169f3f8b40b3c4b9e2d1a0c7e5f6d8b2a4c9";

    /// <summary>Port 1 answers nothing anywhere, so every leg fails on transport in milliseconds.</summary>
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    private readonly List<string> _variables = [];

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var name in _variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void A_model_is_added_by_reference_and_the_listing_says_whether_it_resolves_here()
    {
        var key = Unique("qwen");
        var reference = Reference("BENCH_TEST_URL", DeadEndpoint);

        var (added, addOutput, _) = Run(
            "models", "add", "--key", key, "--model-id", "qwen3-coder:latest",
            "--base-url-ref", reference, "--db", postgres.ConnectionString);
        var (listed, listOutput, _) = Run("models", "list", "--db", postgres.ConnectionString);

        added.Should().Be(ExitCodes.Pass);
        addOutput.Should().Contain(reference).And.Contain("references, resolved on this machine at use");
        listed.Should().Be(ExitCodes.Pass);
        listOutput.Should().Contain($"{reference} ✓",
            "whether a row's references resolve HERE is what decides if a test can use it");
    }

    [Fact]
    public void An_endpoint_pasted_where_a_reference_belongs_is_refused_with_the_reason()
    {
        var (code, _, error) = Run(
            "models", "add", "--key", Unique("pasted"), "--model-id", "qwen3-coder:latest",
            "--base-url-ref", "http://127.0.0.1:11434/v1", "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("VALUE").And.Contain("published unedited");
    }

    [Fact]
    public void A_model_that_does_not_resolve_here_is_still_listed_and_marked_unset()
    {
        var key = Unique("absent");
        Run("models", "add", "--key", key, "--model-id", "m:latest",
            "--base-url-ref", "BENCH_TEST_NEVER_SET", "--db", postgres.ConnectionString);

        var (_, output, _) = Run("models", "list", "--db", postgres.ConnectionString);

        output.Should().Contain("BENCH_TEST_NEVER_SET ✗ unset",
            "a row that cannot be used is shown as such rather than hidden — the operator has to fix it, not hunt it");
    }

    [Fact]
    public async Task A_run_composed_from_registry_keys_measures_every_one_of_them()
    {
        var first = Unique("first");
        var second = Unique("second");
        var reference = Reference("BENCH_TEST_URL", DeadEndpoint);
        Add(first, "qwen3-coder:latest", reference);
        Add(second, "gemma3:latest", reference);

        using var suite = new TempSuite();
        var (code, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--subjects", $"{first},{second}", "--judges", first,
            "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.NoReport, "the endpoint is dead, so nothing was measured — that is not this test's subject");
        output.Should().Contain("2 cell(s)", "two subjects over one question is two legs, not one");
        output.Should().Contain("qwen3-coder:latest, gemma3:latest",
            "the resolved model ids are printed, not the keys — a result carries the second");

        await using var db = postgres.NewContext();
        var subjects = await db.RunSubjects.AsNoTracking().Select(s => s.ModelKey).ToListAsync(Ct);
        var judges = await db.RunJudges.AsNoTracking().Where(j => j.ModelKey == first).ToListAsync(Ct);

        subjects.Should().Contain([first, second], "the test records what it chose, so the registry can change afterwards");
        judges.Should().ContainSingle().Which.Ordinal.Should().Be(0, "the first arbiter named is the primary");
    }

    [Fact]
    public void A_run_naming_a_disabled_model_is_refused_before_a_single_cell_exists()
    {
        var key = Unique("disabled");
        Add(key, "qwen3-coder:latest", Reference("BENCH_TEST_URL", DeadEndpoint));
        Run("models", "disable", "--key", key, "--db", postgres.ConnectionString);

        using var suite = new TempSuite();
        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--subjects", key, "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("disabled").And.Contain("by name");
    }

    [Fact]
    public void A_run_whose_reference_is_unset_on_this_machine_is_refused_naming_the_variable()
    {
        var key = Unique("unset");
        Add(key, "qwen3-coder:latest", "BENCH_TEST_NEVER_SET");

        using var suite = new TempSuite();
        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--subjects", key, "--db", postgres.ConnectionString);

        // Three hours into a sweep this reads as a wall of identical transport failures. Here it names the
        // variable, before anything was created.
        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("BENCH_TEST_NEVER_SET").And.Contain("unset on this machine");
    }

    [Fact]
    public void A_run_naming_a_key_that_is_not_in_the_registry_says_where_to_add_it()
    {
        using var suite = new TempSuite();

        var (code, _, error) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--subjects", "no-such-model", "--db", postgres.ConnectionString);

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("no model 'no-such-model' in the registry").And.Contain("every role draws from that one list");
    }

    [Fact]
    public void The_ad_hoc_model_pair_still_works_and_records_no_roles()
    {
        using var suite = new TempSuite();

        var (code, output, _) = Run(
            "run", "--repo", Repo, "--commit", Sha, "--suite-file", suite.Path,
            "--model", "qwen@local", "--model-url", DeadEndpoint, "--db", postgres.ConnectionString);

        // Pointing the harness at something once, without registering it, stays possible — and records no
        // roles, because a role names a REGISTRY key and this run named none.
        code.Should().Be(ExitCodes.NoReport);
        output.Should().Contain("subjects qwen").And.NotContain("arbiters");
    }

    private void Add(string key, string modelId, string reference) =>
        Run("models", "add", "--key", key, "--model-id", modelId,
            "--base-url-ref", reference, "--db", postgres.ConnectionString)
            .Code.Should().Be(ExitCodes.Pass);

    /// <summary>Sets a real environment variable for the duration of the test, and takes it back down —
    /// the registry resolves references from the environment, so this is the only honest way to exercise
    /// resolution through the CLI.</summary>
    private string Reference(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _variables.Add(name);
        return name;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16].TrimEnd('-');

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class TempSuite : IDisposable
    {
        public TempSuite()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-models-suite-{Guid.NewGuid():N}.json");
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
