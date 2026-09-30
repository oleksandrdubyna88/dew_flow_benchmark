using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Domain.Gate;
using Bench.Domain.Runs;
using Bench.Infrastructure.Persistence;
using Bench.Tests.Gate.Driver;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Cli;

/// <summary>S3.8 through <see cref="Program.Run"/>, the way an agent drives the binary: the exit-code contract of
/// <c>bench gate run | status | reviewers</c>, a real campaign against the fake product, and a moved product refused.</summary>
[Collection("postgres")]
public sealed class GateDriverCommandTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void A_run_with_no_reviewer_is_a_configuration_error()
    {
        Run("gate", "run", "--gate", "plan", "--db", "Host=x", "--suite-file", "s.json", "--coai-exe", "c.exe", "--artifact-root", "a")
            .Should().Match<(int Code, string Output, string Error)>(r => r.Code == ExitCodes.Configuration && r.Error.Contains("--reviewers"));
    }

    [Fact]
    public void A_suite_file_that_is_not_there_is_the_environment()
    {
        var (code, _, error) = Run("gate", "run", "--gate", "plan", "--reviewers", "rev-a", "--db", "Host=x", "--suite-file", Path.Combine(Path.GetTempPath(), "no-such-suite.json"),
            "--coai-exe", FakeCoai.Executable, "--artifact-root", Path.GetTempPath());

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("suite file").And.Contain("is not there");
    }

    [Fact]
    public async Task A_campaign_runs_end_to_end_through_the_cli_and_status_then_lists_it_without_claiming()
    {
        await using var setup = await CliSetup.StartAsync(postgres);

        var (code, output, error) = Run([.. setup.RunArgs("plan"), "--prediction", "every cell is valid"]);

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("campaign       2 cell(s) settled").And.Contain("footprint");
        output.Should().Contain("settled        cs2/rev-a/r1").And.Contain("settled        cs2/rev-a/r2",
            "a campaign that runs for hours says what it did as it does it, one line per cell");
        var runId = Guid.Parse(output.Split("gate run ")[1][..36]);
        File.ReadAllText(Path.Combine(setup.ArtifactRoot, "runs", runId.ToString("D"), GateRunCommand.PredictionFile)).Should().Be("every cell is valid");
        await using (var db = PostgresFixture.Context(setup.Connection))
        {
            (await new PostgresGateStore(db, TimeProvider.System).LoadAsync(runId, Ct)).Ok().Should().Match<GateRun>(r =>
                r.Status == GateRunStatus.Finished && r.PredictionHash.Length == 64, "the prediction's hash is on the run, its text in the artefact root");
            (await db.GateSuiteTasks.CountAsync(t => t.SuiteStamp == setup.SuiteStamp, Ct)).Should().BeGreaterThan(0,
                "a run records its suite's tasks, so its report can put the calibration tasks apart without the suite file (E6)");
        }

        var status = Run("gate", "status", "--run", runId.ToString(), "--db", setup.Connection);
        status.Code.Should().Be(ExitCodes.Pass);
        status.Output.Should().Contain("pending        0").And.Contain("settled        2").And.Contain("pins seen").And.Contain("isolated data directories");
    }

    /// <summary>T5 through the binary: a campaign whose reviewer's account is out is the ENVIRONMENT — exit 3, the run left
    /// running with its cells pending, and the resume named — never a pass over cells that measured nothing.</summary>
    [Fact]
    public async Task A_campaign_whose_reviewers_account_is_out_exits_as_the_environment_and_leaves_the_run_resumable()
    {
        await using var setup = await CliSetup.StartAsync(postgres);
        var script = Path.Combine(setup.ArtifactRoot, "..", "account-out.json");
        await File.WriteAllTextAsync(script, new JsonObject { ["accountOut"] = new JsonObject { ["rev-a"] = "exit 1: You've hit your monthly spend limit. (HTTP 429)" } }.ToJsonString(), Ct);
        Environment.SetEnvironmentVariable("FAKE_COAI_SCRIPT", script);

        try
        {
            var (code, output, error) = Run(setup.RunArgs("plan"));

            code.Should().Be(ExitCodes.Environment, output + error);
            error.Should().Contain("monthly spend limit").And.Contain("bench gate resume --run");
            output.Should().Contain("AccountOut").And.Contain("pending        rev-a 2");
            var runId = Guid.Parse(output.Split("gate run ")[1][..36]);
            await using var db = PostgresFixture.Context(setup.Connection);
            (await new PostgresGateStore(db, TimeProvider.System).LoadAsync(runId, Ct)).Ok().Status.Should().NotBe(GateRunStatus.Finished, "its cells are still to be measured");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAKE_COAI_SCRIPT", null);
        }
    }

    [Fact]
    public async Task Status_lists_claimed_cells_with_owner_and_age_and_abandoned_ones_with_their_cause_and_claims_nothing()
    {
        var connection = await postgres.NewDatabaseAsync($"gate_status_{Guid.NewGuid():N}");
        var store = new PostgresGateStore(PostgresFixture.Context(connection), TimeProvider.System);
        var (run, cells) = Planned(count: 3);
        await store.PlanAsync(run, cells, Ct);
        (await store.ClaimNextAsync(run.Id, WorkerIdentity.Here("lane-7"), Pin('c'), Ct)).Ok();

        var (code, output, _) = Run("gate", "status", "--run", run.Id.ToString(), "--db", connection);

        code.Should().Be(ExitCodes.Pass);
        output.Should().Contain("pending        2").And.Contain("claimed        cs2/grok-medium/r1 attempt 1 by lane-7").And.Contain("min ago").And.Contain(HashText.Short(new string('c', 64)));
        (await store.CellsAsync(run.Id, Ct)).Count(c => c.State == CellState.Claimed).Should().Be(1, "status claims nothing");
    }

    [Fact]
    public async Task A_product_that_moved_since_the_campaigns_earlier_run_is_refused_naming_both_shas()
    {
        await using var setup = await CliSetup.StartAsync(postgres);
        var store = new PostgresGateStore(PostgresFixture.Context(setup.Connection), TimeProvider.System);
        var earlier = GateRun.Planned(Guid.CreateVersion7(), GateKind.Plan, setup.SuiteStamp, DataDirMode.Isolated, Noon);
        var cell = GateCell.Pending(Guid.CreateVersion7(), earlier.Id, new GateMatrixCell(GateTaskId.Parse("cs2").Ok(), GateReviewerId.Parse("rev-a").Ok(), 1, 0, 0));
        await store.PlanAsync(earlier, [cell], Ct);
        (await store.ClaimNextAsync(earlier.Id, WorkerIdentity.Here("old"), Pin('d'), Ct)).Ok();

        var (code, _, error) = Run(setup.RunArgs("plan"));

        code.Should().Be(ExitCodes.Configuration);
        error.Should().Contain("the product moved").And.Contain(HashText.Short(new string('d', 64))).And.Contain("--allow-product-change");

        Run([.. setup.RunArgs("plan"), "--allow-product-change"]).Code.Should().Be(ExitCodes.Pass, "the change is allowed, and measured as a new scope");
    }

    /// <summary>A reviewer row hashes its reference NAMES; the cell stores a hash of what they RESOLVED to. A resume on a
    /// machine where the endpoint reference now points elsewhere is another configuration, refused by the reviewer's id —
    /// and it names no address.</summary>
    [Fact]
    public async Task A_resume_whose_endpoint_reference_now_resolves_elsewhere_is_refused()
    {
        await using var setup = await CliSetup.StartAsync(postgres);
        var variable = "BENCH_GATE_TEST_URL_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Environment.SetEnvironmentVariable(variable, "http://127.0.0.1:11434/v1");

        try
        {
            Run("gate", "reviewers", "add", "--id", "rev-local", "--runtime", "local", "--model", "qwen", "--endpoint", variable, "--gates", "plan", "--db", setup.Connection)
                .Code.Should().Be(ExitCodes.Pass);
            var args = setup.RunArgs("plan").Select(a => a == "rev-a" ? "rev-local" : a).ToArray();
            var first = Run(args);
            first.Code.Should().Be(ExitCodes.Pass, first.Error);
            var runId = Guid.Parse(first.Output.Split("gate run ")[1][..36]);
            await using (var db = PostgresFixture.Context(setup.Connection))
            {
                await db.GateRuns.Where(r => r.Id == runId).ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, GateRunStatus.Running), Ct);
            }

            Environment.SetEnvironmentVariable(variable, "http://127.0.0.1:22222/v1");
            var resumed = Run(["gate", "resume", "--run", runId.ToString(), .. args.Skip(2).Where((_, i) => true)]);

            resumed.Code.Should().Be(ExitCodes.Configuration);
            resumed.Error.Should().Contain("rev-local").And.Contain("resolves its references differently").And.NotContain("22222");
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    /// <summary>A dead environment is found BEFORE anything is planned: a reviewer whose creds-key reference is unset on
    /// this machine is the environment (3), named, and no run exists — rather than every cell failing one by one.</summary>
    [Fact]
    public async Task A_reviewer_whose_creds_key_is_unset_is_refused_before_anything_is_planned()
    {
        await using var setup = await CliSetup.StartAsync(postgres);
        var unset = "BENCH_GATE_TEST_UNSET_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Run("gate", "reviewers", "add", "--id", "rev-nokey", "--runtime", "api", "--model", "m", "--endpoint", "https://api.vendor-b.example.com/v1",
            "--creds-key-ref", unset, "--gates", "plan", "--db", setup.Connection).Code.Should().Be(ExitCodes.Pass);

        var (code, _, error) = Run(setup.RunArgs("plan").Select(a => a == "rev-a" ? "rev-nokey" : a).ToArray());

        code.Should().Be(ExitCodes.Environment);
        error.Should().Contain("rev-nokey").And.Contain(unset);
        await using var db = PostgresFixture.Context(setup.Connection);
        (await db.GateRuns.CountAsync(Ct)).Should().Be(0, "nothing is planned for an environment that cannot run it");
    }

    [Fact]
    public async Task A_resume_of_an_unknown_run_is_configuration_and_an_unreachable_database_is_the_environment()
    {
        await using var setup = await CliSetup.StartAsync(postgres);
        var unknown = Guid.NewGuid();

        var missing = Run(["gate", "resume", "--run", unknown.ToString(), .. setup.RunArgs("plan").Skip(2)]);
        missing.Code.Should().Be(ExitCodes.Configuration);
        missing.Error.Should().Contain($"no gate run {unknown}");

        var down = Run(["gate", "resume", "--run", unknown.ToString(), .. setup.RunArgs("plan").Skip(2).Select(a => a == setup.Connection ? "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2" : a)]);
        down.Code.Should().Be(ExitCodes.Environment);
        down.Error.Should().Contain("unreachable");
    }

    [Fact]
    public async Task Probe_lists_the_products_tools_and_providers_and_calls_no_model()
    {
        await using var setup = await CliSetup.StartAsync(postgres);

        var (code, output, error) = Run("gate", "probe", "--coai-exe", FakeCoai.Executable, "--reviewers", "rev-a", "--db", setup.Connection);

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("server         connect-other-ais").And.Contain("review_plan").And.Contain("no model was called");
        output.Should().NotContain(GateDriverRig.CredsKey);
        var probeDir = output.Split("probe dir      ")[1].Split('\n')[0].Trim();
        Directory.Exists(probeDir).Should().BeFalse("the probe's throwaway data directory is removed when it ends");
    }

    [Fact]
    public async Task Reviewers_are_added_listed_and_retired_and_never_edited()
    {
        await using var setup = await CliSetup.StartAsync(postgres);

        Run("gate", "reviewers", "list", "--db", setup.Connection).Output.Should().Contain("rev-a#");
        Run(setup.AddArgs("rev-a")).Error.Should().Contain("already exists").And.Contain("never edited");
        Run("gate", "reviewers", "retire", "--id", "rev-a", "--db", setup.Connection).Output.Should().Contain("retired");
        Run("gate", "reviewers", "list", "--db", setup.Connection).Output.Should().NotContain("rev-a#");
        Run("gate", "reviewers", "list", "--all", "--db", setup.Connection).Output.Should().Contain("rev-a#").And.Contain("retired");
        Run("gate", "reviewers", "add", "--id", "rev-bad", "--runtime", "api", "--model", "m", "--endpoint", "https://user:pw@api.vendor.example.com/v1", "--gates", "plan", "--db", setup.Connection)
            .Should().Match<(int Code, string Output, string Error)>(r => r.Code == ExitCodes.Configuration && r.Error.Contains("user-info") && !r.Error.Contains("pw@"));
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>A database with one reviewer added through the CLI, a git repository holding the seeded task, a suite
    /// file naming it, an artefact root and a checkout root — everything <c>bench gate run</c> is pointed at.</summary>
    private sealed class CliSetup : IAsyncDisposable
    {
        private readonly GateDriverRig _rig;
        private readonly string _credsVariable = "BENCH_GATE_TEST_CREDS_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        private CliSetup(GateDriverRig rig, string connection)
        {
            _rig = rig;
            Connection = connection;
        }

        public string Connection { get; }

        public string ArtifactRoot => _rig.Artifacts.Root;

        public string SuiteFile => Path.Combine(_rig.Root.Sibling("suite"), "suite.json");

        public string SuiteStamp { get; private set; } = string.Empty;

        public static async Task<CliSetup> StartAsync(PostgresFixture postgres)
        {
            var rig = await GateDriverRig.StartAsync(postgres);
            var setup = new CliSetup(rig, rig.Connection);
            Environment.SetEnvironmentVariable(setup._credsVariable, GateDriverRig.CredsKey);
            Run(setup.AddArgs("rev-a")).Code.Should().Be(ExitCodes.Pass);
            await setup.WriteSuiteAsync();
            return setup;
        }

        public string[] AddArgs(string id) =>
            ["gate", "reviewers", "add", "--id", id, "--runtime", "api", "--model", "model-a", "--endpoint", "https://api.vendor-a.example.com/v1",
             "--key-name", "vendor", "--creds-key-ref", _credsVariable, "--effort", "medium", "--gates", "plan,code,feature", "--db", Connection];

        public string[] RunArgs(string gate) =>
            ["gate", "run", "--gate", gate, "--suite-file", SuiteFile, "--reviewers", "rev-a", "--repeats", "2", "--coai-exe", FakeCoai.Executable,
             "--artifact-root", ArtifactRoot, "--checkout-root", _rig.Root.Sibling("cli-checkouts"), "--parallel", "1", "--db", Connection];

        private async Task WriteSuiteAsync()
        {
            var task = _rig.Task;
            var suite = new JsonObject
            {
                ["id"] = "cli-suite",
                ["privateNames"] = new JsonArray("contoso-orders"),
                ["tasks"] = new JsonArray(new JsonObject
                {
                    ["id"] = task.Id.Value,
                    ["language"] = task.Language,
                    ["gates"] = new JsonArray("plan", "code", "feature"),
                    ["repository"] = _rig.Repo.Root,
                    ["base"] = task.Case.Base.Value,
                    ["variantHead"] = task.Case.VariantHead.Value,
                    ["planPath"] = task.Case.PlanPath,
                    ["epics"] = JsonNode.Parse(task.Case.Epics),
                    ["lessons"] = JsonNode.Parse(task.Case.Lessons),
                    ["seeds"] = new JsonArray(new JsonObject
                    {
                        ["id"] = "cs2-S1",
                        ["file"] = "src/Orders.cs",
                        ["old"] = "a",
                        ["new"] = "b",
                        ["what"] = "w",
                        ["trigger"] = "t",
                        ["mechanism"] = "m",
                        ["consequence"] = "c",
                    }),
                }),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(SuiteFile)!);
            await File.WriteAllTextAsync(SuiteFile, suite.ToJsonString(), Ct);
            SuiteStamp = Bench.Application.Gate.GateSuiteFile.Parse(suite.ToJsonString()).Ok().Stamp;
        }

        public async ValueTask DisposeAsync()
        {
            Environment.SetEnvironmentVariable(_credsVariable, null);
            await _rig.DisposeAsync();
        }
    }
}
