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

    [Fact]
    public void The_api_probes_statuses_are_read_off_its_report_and_a_refused_key_is_a_401_402_403_or_a_marker()
    {
        const string report = "probe-api grok @ https://api.x.ai/v1 (xai)\nmodels: HTTP 200 (12 ids)\nchat/completions: status 200, model grok-4.7, tokens 12/34\n";

        ProbeApiOutput.Statuses(report).Should().Equal([200, 200]);
        ProbeApiOutput.KeyRefused([200, 200], report).Should().Be(ProbeFact.No);
        ProbeApiOutput.KeyRefused([200, 403], report).Should().Be(ProbeFact.Yes, "the vendor refused the key");
        ProbeApiOutput.KeyRefused([402], report).Should().Be(ProbeFact.Yes, "payment required is a spent account");
        ProbeApiOutput.KeyRefused([200], report + "You've hit your usage limit.").Should().Be(ProbeFact.Yes, "a marker in the text counts as a CLI's would");
        ProbeApiOutput.KeyRefused([], "no rows").Should().Be(ProbeFact.NotCaptured, "statuses nobody printed are not a 'no'");
        ProbeApiOutput.Statuses("version 2.1.258 and 404 things").Should().BeEmpty("a bare number is not a status");
        ProbeVerdicts.ApiReachable(0, [200, 200], true, ProbeFact.No).Reachable.Should().Be(ProbeFact.Yes);
        ProbeVerdicts.ApiReachable(0, [200, 403], true, ProbeFact.Yes).Reachable.Should().Be(ProbeFact.No);
    }

    [Fact]
    public void The_bare_environment_drops_every_coai_variable_keeps_the_rest_and_takes_the_secret_last()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["COAI_VENDORS"] = "[{\"id\":\"inherited\"}]",
            ["coai_data_dir"] = "/somebody/elses/data",
            ["COAI_CREDS_KEY"] = "a-parent-key-1234",
            ["OPENAI_API_KEY"] = "sk-operators-own-key-5678",
        };

        var bare = CoaiEnvironment.Bare(parent);
        var child = bare.WithSecret(SecretValue.Of("sentinel-creds-key-rig-31", "rig").Ok());

        bare.Variables.Keys.Should().BeEquivalentTo(["PATH", "OPENAI_API_KEY"], "every COAI_* is dropped, whatever its case; the operator's other variables pass, as the editor passes them");
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
