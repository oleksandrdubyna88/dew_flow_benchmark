using Bench.Domain.Gate;
using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2 — how an exit reads per CLI (a usage error is a launch refused by the build, anything else non-zero failed),
/// what the product's api probe printed, and the knob-less environment the api launch runs under.</summary>
public sealed class ProbeExitsTests
{
    [Theory]
    [InlineData(ProbeRuntime.Codex, 0, "", ProbeAttemptKind.Answered)]
    [InlineData(ProbeRuntime.Codex, 2, "error: unexpected argument '--search' found", ProbeAttemptKind.LaunchRefused)]
    [InlineData(ProbeRuntime.Codex, 1, "ERROR: stream disconnected", ProbeAttemptKind.Failed)]
    [InlineData(ProbeRuntime.Claude, 1, "error: unknown option '--add-dir'", ProbeAttemptKind.LaunchRefused)]
    [InlineData(ProbeRuntime.Claude, 1, "API Error: 500 internal server error", ProbeAttemptKind.Failed)]
    [InlineData(ProbeRuntime.Antigravity, 1, "Unknown argument: add-dir", ProbeAttemptKind.LaunchRefused)]
    [InlineData(ProbeRuntime.Antigravity, 1, "Error: model not found", ProbeAttemptKind.Failed)]
    [InlineData(ProbeRuntime.Api, 65, "", ProbeAttemptKind.LaunchRefused)]
    [InlineData(ProbeRuntime.Api, 1, "", ProbeAttemptKind.Failed)]
    public void A_usage_error_is_a_launch_refused_by_the_build_and_any_other_non_zero_exit_is_failed(ProbeRuntime runtime, int exit, string stderr, ProbeAttemptKind kind) =>
        ProbeExits.Classify(runtime, exit, stderr).Should().Be(kind);

    /// <summary>S2b, finding 4: the report is ONE JSON object (coai-mcp 0.40.3); the completion cases carry the statuses, the deliberate
    /// <c>wrong_key</c> case is excluded, and the account is out on a 401/402/403 or on the vendor's credits / spending-limit wording.</summary>
    [Fact]
    public void The_api_probes_report_is_json_its_completion_cases_carry_the_statuses_and_the_wrong_key_case_is_excluded()
    {
        var live = ProbeApiOutput.Read(ProbeVerdictsTests.Fixture("coai-mcp-0.40.3-probe-api-grok-403.json"));

        live.Captured.Should().BeTrue();
        live.Statuses.Should().HaveCount(9).And.OnlyContain(s => s == 403, "nine completion cases answered 403; the wrong_key case (400) is the product's own control");
        live.AccountMarker.Should().BeTrue("'…has either used all available credits or reached its monthly spending limit…'");
        ProbeApiOutput.AccountOut(live).Should().Be(ProbeFact.Yes);
        ProbeVerdicts.ApiReachable(0, live).Should().Match<ProbeFacts>(f => f.Reachable == ProbeFact.No && f.AccountOut == ProbeFact.Yes);

        var healthy = ProbeApiOutput.Read("""{"vendor":"grok","requests":[{"case":"json_schema","status":200,"error":""},{"case":"seed","status":400,"error":"seed is not supported"},{"case":"wrong_key","status":400,"error":"Incorrect API key provided."}]}""");
        healthy.Statuses.Should().Equal([200, 400]);
        ProbeApiOutput.AccountOut(healthy).Should().Be(ProbeFact.No, "a refused FIELD is not a refused account, and the wrong_key 400 is never counted");
        ProbeVerdicts.ApiReachable(0, healthy).Reachable.Should().Be(ProbeFact.Yes, "the endpoint answered and at least one completion case was a 200");
        ProbeVerdicts.ApiReachable(1, healthy).Reachable.Should().Be(ProbeFact.Yes, "the product's exit code does not decide whether the vendor was reached");

        ProbeApiOutput.AccountOut(ProbeApiOutput.Read("""{"requests":[{"case":"json_schema","status":200,"error":"Your quota for today is exhausted"}]}""")).Should().Be(ProbeFact.Yes, "the wording counts even beside a 200");
        ProbeApiOutput.AccountOut(ProbeApiOutput.Read("""{"requests":[{"case":"json_schema","status":402,"error":""}]}""")).Should().Be(ProbeFact.Yes, "payment required is a spent account");
        var none = ProbeApiOutput.Read("probe-api grok @ https://api.x.ai/v1 (xai)\nmodels: HTTP 200 (12 ids)\n");
        none.Should().Be(ProbeApiReport.NotCaptured, "text lines are not the report — the S2 guess is withdrawn");
        ProbeApiOutput.AccountOut(none).Should().Be(ProbeFact.NotCaptured, "statuses nobody printed are not a 'no'");
        ProbeVerdicts.ApiReachable(0, none).Reachable.Should().Be(ProbeFact.NotCaptured);
        ProbeVerdicts.ApiReachable(0, ProbeApiOutput.Read("""{"requests":[]}""")).Reachable.Should().Be(ProbeFact.No, "a report with no completion case reached nothing");
    }

    /// <summary>S2c, review finding 3 widened the bare environment: the product never inherits a <c>BENCH_*</c> (the database url carries a
    /// password) or a secret-named variable of the operator's shell either — it authenticates through the vault, by the one key joined
    /// LAST. The system variables still pass, as the live launch of 2026-10-01 needed them.</summary>
    [Fact]
    public void The_bare_environment_drops_every_coai_bench_and_secret_named_variable_keeps_the_rest_and_takes_the_secret_last()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["COAI_VENDORS"] = "[{\"id\":\"inherited\"}]",
            ["coai_data_dir"] = "/somebody/elses/data",
            ["COAI_CREDS_KEY"] = "a-parent-key-1234",
            ["OPENAI_API_KEY"] = "sk-operators-own-key-5678",
            ["BENCH_DB"] = "Host=db;Password=hunter2-planted",
        };

        var bare = CoaiEnvironment.Bare(parent);
        var child = bare.WithSecret(SecretValue.Of("sentinel-creds-key-rig-31", "rig").Ok());

        bare.Variables.Keys.Should().BeEquivalentTo(["PATH"], "every COAI_*, every BENCH_* and every secret-named variable is dropped, whatever its case; the system variables pass");
        child.Scrub("BENCH_DB=Host=db;Password=hunter2-planted").Should().Be("BENCH_DB=[redacted]", "a bench variable's value is scrubbed like a secret's — the database url carries a password");
        bare.Snapshot.Should().BeEmpty("there is no session to snapshot");
        child.Variables[CoaiEnvironment.CredsKeyVariable].Should().Be("sentinel-creds-key-rig-31");
        child.Variables.Values.Should().NotContain("a-parent-key-1234", "the parent's key never reaches the product");
        child.Scrub("key sentinel-creds-key-rig-31 and sk-operators-own-key-5678 echoed").Should().Be("key [redacted] and [redacted] echoed", "the vault key and the operator's inherited secrets are scrubbed from every text written");
        child.ToString().Should().NotContain("sentinel");
    }

    [Fact]
    public void The_attempts_layout_is_built_from_ids_alone_and_a_cell_folder_name_reads_back_as_its_id()
    {
        var (run, cell) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var scope = new ProbeAttemptScope(run, cell, Generation: 2, Attempt: 3);

        ProbePaths.AttemptRoot(scope).Value.Should().Be($"probes/{run:D}/{cell:D}/g2/a3");
        ProbePaths.Artifact(scope, ProbeArtifactKind.Stdout).Value.Should().Be($"probes/{run:D}/{cell:D}/g2/a3/stdout.txt");
        ProbePaths.RunRoot(run).Value.Should().Be($"probes/{run:D}");
        ProbePaths.CellOf(cell.ToString("D")).Ok().Should().Be(cell);
        ProbePaths.CellOf("g1").Reason().Should().Contain("not a cell folder");
        ProbePaths.Layout(ProbeKind.ReadInside).Should().Be(new ProbeFixtureLayout(true, true));
        ProbePaths.Layout(ProbeKind.WebSearch).Should().Be(new ProbeFixtureLayout(false, false), "an EMPTY cwd and no canary anywhere");
        ProbePaths.Layout(ProbeKind.WebConfined).Should().Be(new ProbeFixtureLayout(false, true));
        ProbePaths.Layout(ProbeKind.ReadDenied).Should().Be(new ProbeFixtureLayout(false, true));
    }

    [Fact]
    public void Only_a_claimed_cell_has_an_attempt_scope()
    {
        var (run, cells) = Bench.Tests.Infrastructure.ProbeStoreFixtures.Planned(count: 1);

        ProbeAttemptScope.Of(cells[0]).Reason().Should().Contain("Pending at attempt 0");
        var claimed = ProbeCellLifecycle.Claim(cells[0], Bench.Domain.Runs.WorkerIdentity.Here("t"), DateTimeOffset.UtcNow, Bench.Tests.Infrastructure.ProbeStoreFixtures.Pin()).Ok();
        ProbeAttemptScope.Of(claimed).Ok().Should().Be(new ProbeAttemptScope(run.Id, claimed.Id, 1, 1));
    }
}
