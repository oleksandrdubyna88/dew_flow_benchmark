using Bench.Cli;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>`bench run` puts the target's tree on disk before it measures anything.
/// <para>
/// The provider was written and tested from the first commits and <b>nothing called it</b>: every run
/// printed "the target was not checked out, so its commit is recorded but unverified" and went on to
/// measure against a sha nobody had confirmed exists. That is the audit's own worst pattern — work that
/// is implemented, tested, and never triggered — and these tests are the trigger.
/// </para>
/// <para>
/// Against a real git repository on disk, over a <c>file://</c> url: no network, and the guarantee under
/// test is git's behaviour rather than a fake's agreement with our idea of it.
/// </para></summary>
[Collection("postgres")]
public sealed class RunCheckoutTests(PostgresFixture postgres) : IAsyncLifetime
{
    /// <summary>Port 1 answers nothing anywhere, so every leg fails on transport in milliseconds.</summary>
    private const string DeadEndpoint = "http://127.0.0.1:1/v1";

    private readonly string _cacheRoot = Path.Combine(Path.GetTempPath(), $"bench-run-cache-{Guid.NewGuid():N}");
    private TempGitRepo _source = null!;

    public async ValueTask InitializeAsync() => _source = await TempGitRepo.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _source.Dispose();
        TempGitRepo.DeleteTree(_cacheRoot);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void A_run_checks_the_target_out_at_its_pinned_commit_before_it_measures()
    {
        using var suite = new TempSuite();

        var (code, output, _) = Run(
            "run", "--repo", _source.Url, "--commit", _source.FirstCommit, "--suite-file", suite.Path,
            "--checkout-root", _cacheRoot, "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint);

        code.Should().Be(ExitCodes.NoReport, "the endpoint is dead — the checkout is what this test is about");
        output.Should().Contain("checkout ").And.Contain(_source.FirstCommit);
        output.Should().NotContain("UNVERIFIED", "the commit was verified, so nothing may say otherwise");

        var worktree = Directory.GetDirectories(Path.Combine(_cacheRoot, "worktrees"), "*", SearchOption.AllDirectories)
            .Single(d => Path.GetFileName(d) == _source.FirstCommit);
        File.ReadAllText(Path.Combine(worktree, "content.txt")).Trim().Should().Be(
            "one", "the tree on disk is the tree at THAT commit, which is the whole point of pinning one");
    }

    [Fact]
    public void A_commit_the_repository_does_not_have_ends_the_run_before_a_single_cell_exists()
    {
        using var suite = new TempSuite();

        var (code, _, error) = Run(
            "run", "--repo", _source.Url, "--commit", new string('f', 40), "--suite-file", suite.Path,
            "--checkout-root", _cacheRoot, "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint);

        // Before this, a run against an unpushed or garbage-collected sha produced a whole campaign of
        // results labelled with a tree nobody had ever seen.
        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("could not be checked out").And.Contain("unpushed, on a fork, or garbage-collected");
    }

    [Fact]
    public async Task The_source_repository_is_left_exactly_as_it_was()
    {
        using var suite = new TempSuite();
        var headBefore = await _source.HeadAsync();

        Run("run", "--repo", _source.Url, "--commit", _source.FirstCommit, "--suite-file", suite.Path,
            "--checkout-root", _cacheRoot, "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint);

        // The component this replaces ran `git checkout` in place on a configured path — which for a
        // benchmark means rewriting whatever a developer had open to a commit they never asked for.
        (await _source.HeadAsync()).Should().Be(headBefore);
        (await _source.StatusAsync()).Should().BeEmpty();
    }

    [Fact]
    public void No_checkout_is_allowed_and_says_plainly_that_the_commit_is_unverified()
    {
        using var suite = new TempSuite();

        var (_, output, _) = Run(
            "run", "--repo", _source.Url, "--commit", new string('f', 40), "--suite-file", suite.Path,
            "--no-checkout", "--db", postgres.ConnectionString,
            "--model", "qwen@local", "--model-url", DeadEndpoint);

        // The escape hatch stays for a target this machine cannot clone — and it keeps the warning, because
        // with it the sha really is unverified. A sha that does not exist gets that far and no further.
        output.Should().Contain("UNVERIFIED").And.NotContain("checkout ");
    }

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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bench-checkout-suite-{Guid.NewGuid():N}.json");
            File.WriteAllText(Path, """
            {
              "id": "demo",
              "questions": [
                { "id": "q1", "prompt": "what does content.txt say?",
                  "expectations": [ { "kind": "AnswerContains", "text": "one" } ] }
              ]
            }
            """);
        }

        public string Path { get; }

        public void Dispose() => File.Delete(Path);
    }
}
