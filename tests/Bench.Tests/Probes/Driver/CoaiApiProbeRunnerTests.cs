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

    private static readonly ProbeSubject Grok = ProbeSubject.Parse("grok-api", "api", "grok-4.7", "BENCH_GATE_COAI_EXE", "grok", "https://api.x.ai/v1", "xai").Ok();

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
        var (runner, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["lines"] = new JsonArray("models: HTTP 403 forbidden", "chat/completions: HTTP 403 forbidden") } });
        var run = Run();

        var facts = (await runner.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;

        facts.AccountOut.Should().Be(ProbeFact.Yes, "Q5 asks what the account says right now — a refused key is the answer, not an interruption");
        facts.Reachable.Should().Be(ProbeFact.No);
    }

    [Theory]
    [InlineData(65, ProbeAttemptKind.LaunchRefused)]
    [InlineData(3, ProbeAttemptKind.Failed)]
    public async Task The_products_own_exits_read_as_the_plan_says(int exit, ProbeAttemptKind kind)
    {
        var (runner, _) = Rig(new JsonObject { ["probeApi"] = new JsonObject { ["exitCode"] = exit, ["lines"] = new JsonArray() } });
        var run = Run();

        var facts = (await runner.RunAsync(run, Grok, Claimed(run), Ct)).Should().BeOfType<ProbeAttemptResult.Settled>().Subject.Settlement.Facts;

        facts.Kind.Should().Be(kind);
        facts.ExitCode.Value.Should().Be(exit);
        facts.Reachable.Should().Be(ProbeFact.NotCaptured, "no status was printed");
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

    private (CoaiApiProbeRunner Runner, FakeCoai Fake) Rig(JsonObject script, Dictionary<string, string>? parentExtras = null, IProbeSecrets? secrets = null)
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
            new ProbeArtifacts(_root.Path), secrets ?? new ScriptedSecrets(SecretValue.Of(Key, "rig")),
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
