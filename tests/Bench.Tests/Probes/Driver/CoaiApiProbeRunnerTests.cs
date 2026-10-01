using System.Collections;
using System.Text.Json.Nodes;
using Bench.Application.Probes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Runs;
using Bench.Infrastructure.Probes;
using Bench.Tests.Gate.Driver;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bench.Tests.Probes.Driver;

/// <summary>S2 acceptance 5 — the api subject through the fake product: the argv D6 spells; a planted sentinel key reaching the
/// child's environment and appearing in no artefact, no log line and no stderr capture; the facts read off the product's report;
/// the product's own exits.</summary>
public sealed class CoaiApiProbeRunnerTests : IDisposable
{
    private const string Key = "sentinel-creds-key-probe-77";

    private readonly TempRoot _root = GateStoreFixtures.NewRoot();
    private readonly List<FakeCoai> _fakes = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ProbeSubject Grok = ProbeSubject.Parse("grok-api", "api", "grok-4.7", "BENCH_GATE_COAI_EXE", string.Empty, "grok", "https://api.x.ai/v1", "xai").Ok();

    [Fact]
    public async Task The_product_is_launched_with_D6s_argv_the_key_reaches_its_environment_and_appears_in_no_artefact()
    {
        var (runner, fake) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["echoCredsKey"] = true, ["echoVariable"] = "COAI_VENDORS" } }, parentExtras: new() { ["COAI_VENDORS"] = "[{\"id\":\"inherited\"}]" });
        var run = Run();

        var result = await runner.RunAsync(run, Grok, Claimed(run), Ct);

        var settlement = result.Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement;
        settlement.Facts.Kind.Should().Be(ProbeAttemptKind.Answered);
        settlement.Facts.Reachable.Should().Be(ProbeFact.Yes, "exit 0 and the one completion row answered 200");
        settlement.Facts.AccountOut.Should().Be(ProbeFact.No);
        fake.Events().Select(e => e.Text).Should().ContainSingle(t => t.StartsWith("probe-api ", StringComparison.Ordinal)).Which
            .Should().Be("probe-api --probe-api --vendor grok --model grok-4.7 --endpoint https://api.x.ai/v1 --dialect xai --timeout-seconds 60");
        var stderr = settlement.Artifacts.Single(a => a.Kind == ProbeArtifactKind.Stderr);
        var text = await File.ReadAllTextAsync(Path.Combine([_root.Path, .. stderr.Path.Segments]), Ct);
        text.Should().Contain("COAI_CREDS_KEY=[redacted]", "the child saw the key — and every text written has it scrubbed");
        text.Should().Contain("COAI_VENDORS=<unset>", "the parent's COAI_* never reaches the product");
        text.Should().NotContain(Key);
        Directory.EnumerateFiles(_root.Path, "*", SearchOption.AllDirectories).Should().OnlyContain(f => !File.ReadAllText(f).Contains(Key, StringComparison.Ordinal), "the key is in no artefact");
    }

    [Fact]
    public async Task A_refused_key_at_the_vendor_is_the_measurement_account_out_yes_reachable_no()
    {
        var (runner, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["stdout"] = ProbeVerdictsTests.Fixture("coai-mcp-0.40.3-probe-api-grok-403.json") } });
        var run = Run();

        var settlement = (await runner.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement;

        settlement.Facts.Kind.Should().Be(ProbeAttemptKind.Answered);
        settlement.Facts.AccountOut.Should().Be(ProbeFact.Yes, "Q5 asks what the account says right now — a refused key is the answer, not an interruption (the live grok report of 2026-10-01: every case 403, 'used all available credits or reached its monthly spending limit')");
        settlement.Facts.Reachable.Should().Be(ProbeFact.No, "the endpoint answered, but no completion case was a 200");
        settlement.Artifacts.Select(a => a.Kind).Should().BeEquivalentTo([ProbeArtifactKind.Stdout, ProbeArtifactKind.Stderr, ProbeArtifactKind.Argv], "the raw evidence and the exact argv (S2b)");
        var argv = await File.ReadAllTextAsync(Path.Combine([_root.Path, .. settlement.Artifacts.Single(a => a.Kind == ProbeArtifactKind.Argv).Path.Segments]), Ct);
        argv.Should().Contain("\"--probe-api\"").And.Contain("\"grok-4.7\"").And.NotContain(Key);
    }

    [Theory]
    [InlineData(65, ProbeAttemptKind.LaunchRefused)]
    [InlineData(3, ProbeAttemptKind.Failed)]
    public async Task The_products_own_exits_read_as_the_plan_says(int exit, ProbeAttemptKind kind)
    {
        var (runner, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["exitCode"] = exit, ["stdout"] = "usage: coai-mcp --probe-api ..." } });
        var run = Run();

        var facts = (await runner.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;

        facts.Kind.Should().Be(kind);
        facts.ExitCode.Value.Should().Be(exit);
        facts.Reachable.Should().Be(ProbeFact.NotCaptured, "no report was printed");
        facts.AccountOut.Should().Be(ProbeFact.NotCaptured);
    }

    [Fact]
    public async Task No_vault_or_no_key_hands_the_attempt_back_unmeasured_whether_the_product_or_this_machine_says_so()
    {
        var (noVault, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["exitCode"] = 78 } });
        var (noKey, _) = Rig(new JsonObject(), secrets: new ScriptedSecrets(Outcome<SecretValue>.Failure("COAI_CREDS_KEY in the coai settings file resolved to nothing")));
        var run = Run();

        (await noVault.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Unmeasured>().Subject.Reason.Should().Be(ProbeReason.AccountOut, "exit 78: nothing about the vendor can be measured until the vault is there");
        (await noKey.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Unmeasured>().Subject.Reason.Should().Be(ProbeReason.AccountOut);
        _fakes[1].Events().Should().BeEmpty("without a key the product is never launched");
    }

    /// <summary>S2c, review finding 6 — the product runner has the same rule as the CLI runner: raw evidence that cannot be written means
    /// an attempt handed back unmeasured, never a settled cell with no stdout on disk.</summary>
    [Fact]
    public async Task A_raw_artefact_that_cannot_be_committed_hands_the_attempt_back_unmeasured_and_never_settles()
    {
        var (runner, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject() }, artifacts: new RefusingArtifacts());
        var run = Run();

        var result = await runner.RunAsync(run, Grok, Claimed(run), Ct);

        result.Should().BeOfType<ProbeAttemptResult.Unmeasured>("a cell with no stdout on disk must not settle").Subject.Reason.Should().Be(ProbeReason.ArtifactsNotCommitted);
    }

    /// <summary>S2c, review finding 3: the product launch, too, inherits nothing the harness owns — no <c>BENCH_*</c> (the database url
    /// carries a password) and no secret-named variable of the operator's shell; the vault key alone joins, under the product's name.</summary>
    [Fact]
    public async Task The_product_inherits_no_bench_variable_and_no_secret_named_variable_of_the_harness()
    {
        var (runner, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["echoVariable"] = "BENCH_DB" } }, parentExtras: new() { ["BENCH_DB"] = "Host=db;Password=hunter2-planted", ["OPENAI_API_KEY"] = "sk-planted-operator-key-77" });
        var run = Run();

        var settlement = (await runner.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement;

        var stderr = await File.ReadAllTextAsync(Path.Combine([_root.Path, .. settlement.Artifacts.Single(a => a.Kind == ProbeArtifactKind.Stderr).Path.Segments]), Ct);
        stderr.Should().Contain("BENCH_DB=<unset>", "the bench's own variables never reach the product");
        var argv = await File.ReadAllTextAsync(Path.Combine([_root.Path, .. settlement.Artifacts.Single(a => a.Kind == ProbeArtifactKind.Argv).Path.Segments]), Ct);
        argv.Should().Contain("\"environment\"").And.Contain("COAI_CREDS_KEY").And.NotContain("OPENAI_API_KEY").And.NotContain("BENCH_DB").And.NotContain("hunter2", "the names the product was launched with, and never a value");
    }

    private sealed class RefusingArtifacts : IProbeArtifacts
    {
        public Task<Outcome<ProbeArtifact>> CommitAsync(ProbeAttemptScope scope, ProbeArtifactKind kind, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            Task.FromResult(Outcome<ProbeArtifact>.Failure("the artefact root refused the write: disk full"));
    }

    private (CoaiApiProbeRunner Runner, FakeCoai Fake) Rig(JsonObject script, Dictionary<string, string>? parentExtras = null, IProbeSecrets? secrets = null, IProbeArtifacts? artifacts = null)
    {
        var fake = new FakeCoai(script);
        _fakes.Add(fake);
        var parent = System.Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty, StringComparer.Ordinal);
        parent["FAKE_COAI_SCRIPT"] = fake.ScriptPath;
        parent["FAKE_COAI_EVENTS"] = fake.EventsDir;
        foreach (var (name, value) in parentExtras ?? [])
        {
            parent[name] = value;
        }

        var runner = new CoaiApiProbeRunner(
            artifacts ?? new ProbeArtifacts(_root.Path), secrets ?? new ScriptedSecrets(SecretValue.Of(Key, "rig")),
            new CoaiApiProbeSettings(FakeCoai.Executable, parent, TimeSpan.FromSeconds(60)), NullLogger<CoaiApiProbeRunner>.Instance);

        return (runner, fake);
    }

    private static ProbeRun Run() => ProbeRun.Planned(Guid.CreateVersion7(), ProbeStoreFixtures.Oracle(), [Grok], 1, DateTimeOffset.UtcNow).Ok();

    private static ProbeCell Claimed(ProbeRun run) =>
        ProbeCellLifecycle.Claim(
            ProbeCell.Pending(Guid.CreateVersion7(), run.Id, new ProbeMatrixCell(ProbeKind.ApiReachable, Grok.Id, 1, 0, 0)), WorkerIdentity.Here("test"), DateTimeOffset.UtcNow, ProbeStoreFixtures.Pin()).Ok();

    private sealed class ScriptedSecrets(Outcome<SecretValue> key) : IProbeSecrets
    {
        public Outcome<SecretValue> CredsKey() => key;
    }

    public void Dispose()
    {
        foreach (var fake in _fakes)
        {
            fake.Dispose();
        }

        _root.Dispose();
    }
}
