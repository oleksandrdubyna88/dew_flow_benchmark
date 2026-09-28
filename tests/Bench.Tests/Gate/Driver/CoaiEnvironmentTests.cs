using System.Text;
using Bench.Domain.Gate;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Infrastructure.GateStoreFixtures;

namespace Bench.Tests.Gate.Driver;

/// <summary>S3.2 — one cell attempt's environment for the product: the parent's <c>COAI_*</c> dropped, every knob set,
/// the data directory from <see cref="CellPaths.DataDirFor(ArtifactScope)"/>, the caller session with its attempt, and
/// the secret joined LAST into a value that cannot print it.</summary>
public sealed class CoaiEnvironmentTests
{
    private const string Key = "sentinel-creds-key-9f31";
    private const string OtherKey = "another-creds-key-7c02";
    private const string Root = "/artifact-root";

    [Fact]
    public void The_snapshot_never_contains_the_keys_value_and_the_launch_environment_does()
    {
        var environment = Environment(Scope(GateStoreFixtures.Run(), attempt: 1));

        var child = environment.WithSecret(Secret(Key));

        child.Variables[CoaiEnvironment.CredsKeyVariable].Should().Be(Key, "the product reads the vault through this variable");
        environment.Snapshot.Values.Should().NotContain(v => v.Contains(Key, StringComparison.Ordinal));
        environment.Snapshot.Keys.Should().NotContain(CoaiEnvironment.CredsKeyVariable);
        environment.SnapshotJson.Should().NotContain(Key, "the snapshot is what an attempt stores");
        child.ToString().Should().NotContain(Key, "a launch environment logged by mistake names variables, never values");
        Secret(Key).ToString().Should().Be("[redacted]");
        environment.Snapshot.Should().NotContainKey(CoaiEnvironment.CredsKeyVariable, "joining the secret did not reach back into the snapshot");
        child.Scrub($"the child echoed {Key} to stderr").Should().Be("the child echoed [redacted] to stderr");
    }

    [Fact]
    public void The_settings_hash_is_stable_across_two_keys_two_cells_two_attempts_and_two_reviewers()
    {
        var run = GateStoreFixtures.Run();
        var first = Environment(Scope(run, attempt: 1));
        var second = Environment(Scope(run, attempt: 2) with { CellId = Guid.NewGuid() }, reviewer: Reviewer("qwen-medium", effort: "high"));

        first.WithSecret(Secret(Key));
        second.WithSecret(Secret(OtherKey));

        second.SettingsHash.Should().Be(first.SettingsHash,
            "the key is never an input, and the cell and the reviewer are axes of their own — a scope per cell would compare nothing");
        Environment(Scope(run, 1), settings: GateRunSettings.With(new Dictionary<string, string> { ["COAI_ON_EXHAUSTED"] = "continue" }).Ok())
            .SettingsHash.Should().NotBe(first.SettingsHash, "a run setting IS the scope");
        first.Snapshot["COAI_LOCAL_REASONING_EFFORT"].Should().Be("medium");
        second.Snapshot["COAI_LOCAL_REASONING_EFFORT"].Should().Be("high", "the transport is sent — and hashed where the reviewer is");
    }

    [Fact]
    public void The_parents_coai_variables_and_the_variable_holding_the_creds_key_never_reach_the_child()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["COAI_VENDORS"] = "[{\"id\":\"inherited\"}]",
            ["coai_data_dir"] = "/somebody/elses/data",
            ["COAI_CREDS_KEY"] = "a-parent-key",
            ["BENCH_COAI_CREDS_KEY"] = Key,
        };

        var environment = Environment(Scope(GateStoreFixtures.Run(), 1), parent: parent);

        environment.Variables.Should().ContainKey("PATH");
        environment.Variables.Keys.Should().NotContain(k => k.Equals("coai_data_dir", StringComparison.Ordinal));
        environment.Variables.Should().NotContainKey("BENCH_COAI_CREDS_KEY", "the operator's copy of the key stays with the operator");
        environment.Variables.Values.Should().NotContain(v => v.Contains("inherited", StringComparison.Ordinal) || v == "a-parent-key");
        environment.Variables[CoaiEnvironment.DataDirVariable].Should().EndWith(Path.Combine("attempt-1", "data"));
    }

    [Theory]
    [InlineData("COAI_API_KEY")]
    [InlineData("coai_vendor_token")]
    [InlineData("COAI_Creds_Key")]
    [InlineData("COAI_DATA_DIR")]
    [InlineData("COAI_VENDORS")]
    [InlineData("PATH")]
    public void An_operator_setting_cannot_carry_a_secret_or_override_what_the_harness_sets_per_cell(string name)
    {
        GateRunSettings.With(new Dictionary<string, string> { [name] = "x" }).Reason().Should().Contain(name);
    }

    [Fact]
    public void Two_cells_resolved_at_once_never_share_a_data_directory_unless_the_run_is_shared()
    {
        var isolated = GateStoreFixtures.Run(DataDirMode.Isolated);
        var shared = GateStoreFixtures.Run(DataDirMode.Shared);

        var dirs = Enumerable.Range(0, 16).AsParallel()
            .Select(_ => Environment(new ArtifactScope(isolated, Guid.NewGuid(), 1)).DataDir)
            .ToList();
        var sharedDirs = Enumerable.Range(0, 4).AsParallel()
            .Select(_ => Environment(new ArtifactScope(shared, Guid.NewGuid(), 1)).DataDir)
            .Distinct()
            .ToList();

        dirs.Should().OnlyHaveUniqueItems("an isolated cell's product store is its own");
        sharedDirs.Should().ContainSingle().Which.Should().EndWith(CellPaths.SharedDataFolder);
    }

    [Theory]
    [InlineData(DataDirMode.Isolated, RequestedDataDir.Shared)]
    [InlineData(DataDirMode.Shared, RequestedDataDir.Isolated)]
    public void A_run_is_resumed_only_with_the_data_directory_mode_it_was_planned_with(DataDirMode stored, RequestedDataDir requested)
    {
        var run = GateStoreFixtures.Run(stored);

        GateRunResume.Resume(run, requested).Reason().Should().Contain($"planned with {stored.ToString().ToLowerInvariant()}");
        GateRunResume.Resume(run, RequestedDataDir.AsStored).Ok().Mode.Should().Be(stored);
    }

    [Fact]
    public async Task Attempt_two_gets_its_own_data_directory_and_caller_session_and_attempt_one_is_left_as_it_was()
    {
        using var temp = NewRoot();
        var store = Store(temp);
        var run = GateStoreFixtures.Run();
        var cell = Guid.NewGuid();
        var first = new ArtifactScope(run, cell, 1);
        var second = new ArtifactScope(run, cell, 2);
        (await store.BeginAttemptAsync(first, TestContext.Current.CancellationToken)).Ok();
        var one = Environment(first, root: store.Root);
        Directory.CreateDirectory(one.DataDir);
        var ledger = Path.Combine(one.DataDir, "usage.jsonl");
        await File.WriteAllTextAsync(ledger, "{\"outcome\":\"ok\"}\n", Encoding.UTF8, TestContext.Current.CancellationToken);

        (await store.BeginAttemptAsync(second, TestContext.Current.CancellationToken)).Ok();
        var two = Environment(second, root: store.Root);

        two.DataDir.Should().NotBe(one.DataDir);
        two.CallerSession.Should().NotBe(one.CallerSession).And.EndWith("-a2");
        one.CallerSession.Should().EndWith("-a1");
        (await File.ReadAllTextAsync(ledger, TestContext.Current.CancellationToken)).Should().Be("{\"outcome\":\"ok\"}\n", "the interrupted attempt's store is kept, never continued");
        Directory.Exists(two.DataDir).Should().BeTrue("the next attempt begins in a directory of its own");
        Directory.EnumerateFileSystemEntries(two.DataDir).Should().BeEmpty();
    }

    private static ArtifactScope Scope(GateRun run, int attempt) => new(run, Guid.NewGuid(), attempt);

    internal static GateReviewer Reviewer(string id = "grok-medium", string effort = "medium") =>
        GateReviewer.Create(GateReviewerId.Parse(id).Ok(), GateReviewerTests.Definition(effort: effort, credsKeyRef: "BENCH_COAI_CREDS_KEY").Ok(), Noon);

    private static CoaiEnvironment Environment(
        ArtifactScope scope,
        GateReviewer? reviewer = null,
        GateRunSettings? settings = null,
        IReadOnlyDictionary<string, string>? parent = null,
        string root = Root)
    {
        var chosen = reviewer ?? Reviewer();

        return CoaiEnvironment.For(new CoaiEnvironmentInputs(
            scope,
            chosen,
            GateTaskId.Parse("cs2").Ok(),
            Repeat: 1,
            settings ?? GateRunSettings.With(new Dictionary<string, string>()).Ok(),
            parent ?? new Dictionary<string, string> { ["PATH"] = "/usr/bin" },
            root),
            CoaiVendorsSetting.From([chosen], GateKind.Feature, ResolvedReferences.Empty).Ok());
    }

    private static SecretValue Secret(string value) => SecretValue.Of(value, "the creds key").Ok();
}
