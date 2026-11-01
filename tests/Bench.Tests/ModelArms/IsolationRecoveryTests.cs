using System.Globalization;
using System.Text.RegularExpressions;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using ModelArms.Core.Isolation;
using ModelArms.Core.Steps;
using ModelArms.Verbs;
using Xunit;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>Who may change a mode, and what a run does under the isolation (D11, F0): only <c>lock</c> and <c>unlock</c> ever set a mode or restore a snapshot — every other verb reads the two files, prints the one line and goes on by name; <c>unlock</c> is refused beside a live arm unless forced;
/// <c>lock</c> then <c>run-arm</c> leaves the experiment LOCKED (the re-review's defect was a runner that restored on entry); a runner whose run has an isolation block that is not applied says so once and runs on.
/// <para>
/// What this does NOT prove: <c>run-all</c> and <c>chain</c> beyond the admission each ends at here (the rows are derived from the dispatch; the chain's own stages are <c>ChainVerbTests</c>); that no process BESIDE the module changes a mode (a same-user <c>chmod</c>, parent §9).
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class IsolationRecoveryTests
{
    private static readonly string[] States = ["none", "applying", "applied", "inconsistent"];

    private static readonly string[] NotChangers = [.. ModelArmsCommandLine.Verbs.Except(["init", "version", "help", "gate-lock", "lock", "unlock"])];

    public static TheoryData<string, string> Rows
    {
        get
        {
            var rows = new TheoryData<string, string>();
            foreach (var verb in NotChangers)
            {
                foreach (var state in States)
                {
                    rows.Add(verb, state);
                }
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task No_verb_but_lock_and_unlock_changes_a_mode_or_restores_a_snapshot(string verb, string state)
    {
        using var kit = new IsolationKit();
        kit.Root.UsePath(kit.Root.Scratch("empty-path"));
        var modes = $$"""[{"path":"{{kit.Pids}}","kind":"unlisted","mode":"755"}]""";
        var snapshot = $$"""{"run":"part6","digest":"d1","since":"2026-10-31T10:00:00Z","modes":{{modes}}}""";
        switch (state)
        {
            case "applying": File.WriteAllText(kit.Layout.LockedModes, snapshot); break;
            case "applied":
                File.WriteAllText(kit.Layout.LockedModes, snapshot);
                File.WriteAllText(kit.Layout.LockedModesApplied, """{"run":"part6","digest":"d1","verifiedAt":"2026-10-31T10:00:05Z"}""");
                break;
            case "inconsistent": File.WriteAllText(kit.Layout.LockedModesApplied, """{"run":"part6","digest":"d1","verifiedAt":"2026-10-31T10:00:05Z"}"""); break;
        }

        File.SetUnixFileMode(kit.Pids, (UnixFileMode)0xC0);
        var before = (Snapshot: Bytes(kit.Layout.LockedModes), Applied: Bytes(kit.Layout.LockedModesApplied));
        var arm = verb is "run-arm" or "run-all" or "checks" ? new[] { "C1" } : verb is "chain" ? ["part6"] : [];
        var output = new StringWriter();

        var code = await ModelArmsApp.Program.RunAsync([verb, .. arm, "--experiment", kit.Root.Root], output, new StringWriter(), kit.Root.Environment, TestContext.Current.CancellationToken);

        new[] { 0, 3, 4 }.Should().Contain(code, "every verb ends at its admission (status, which only reads, prints and returns 0)");
        File.GetUnixFileMode(kit.Pids).Should().Be((UnixFileMode)0xC0, $"{verb} must not change a mode");
        Bytes(kit.Layout.LockedModes).Should().Equal(before.Snapshot);
        Bytes(kit.Layout.LockedModesApplied).Should().Equal(before.Applied);
        kit.Files().Where(name => name.Contains("restored", StringComparison.Ordinal)).Should().BeEmpty($"{verb} must not move a snapshot aside");
        var line = IsolationLine.Of(IsolationFiles.State(kit.Layout));
        Assert.True(!output.ToString().Contains("isolation", StringComparison.Ordinal) || output.ToString().Contains(line, StringComparison.Ordinal), $"{verb} printed isolation text that is not the one line: {output}");
    }

    private static byte[] Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : [];

    [Fact]
    public void The_only_sites_that_set_a_mode_or_move_a_snapshot_are_the_isolation_adapters_and_the_named_builders()
    {
        var spellings = new Regex(@"SetUnixFileMode|UnixFileMode \|=|UnixFileMode =|""chmod""|/proc/self/fd|LockedModesRestored\(|LockedModesAppliedRestored\(", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        var found = Directory.EnumerateFiles(Path.Combine(Cli.Repository.Root, "tools", "model-arms"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Where(path => spellings.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        found.Should().Contain("PosixFolderIsolation.cs", "a scan that matches nothing passes forever");
        found.Should().Equal(
            ["ArmMaker.cs", "ArmPaths.cs", "BaseCopy.cs", "Init.cs", "PosixFolderIsolation.cs", "RootFolders.cs", "RuntimeFolderIsolation.cs", "SnapshotAside.cs"],
            "only the two isolation adapters, the one aside helper, the folder helper and the legitimate builders (the arm's own folders, the base copy, init) set a mode or move a snapshot");
    }

    [Fact]
    public async Task Unlock_is_refused_while_an_arm_of_the_map_is_alive_unless_forced()
    {
        using var kit = new IsolationKit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        (await kit.LockAsync()).Code.Should().Be(0);
        await using var holder = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.RunnerLock("C1"), FakeCli.Executable);
        await holder.LineAsync("held ", TimeSpan.FromSeconds(30));
        var pid = int.Parse(holder.Printed().Split('|')[0].Trim().Split(' ')[1], CultureInfo.InvariantCulture);
        var locked = kit.Modes();

        var refused = await kit.UnlockAsync(clock: TimeProvider.System);

        refused.Code.Should().Be(4, refused.Output + refused.Error);
        refused.Error.Should().Contain($"refused — C1 is alive (no pid file yet — the lock is held by pid {pid} since").And.Contain("unlock once no arm is alive, or --force");
        kit.Modes().Should().Equal(locked);
        kit.Snapshot().Should().BeOfType<LockState.Applied>();

        await holder.KillAsync();
        File.WriteAllText(kit.Layout.PidFile("C1"), "2147483646\n");
        var stale = await kit.UnlockAsync(clock: TimeProvider.System);
        stale.Code.Should().Be(0, "a stale pid beside a free lock is not alive: " + stale.Output + stale.Error);

        (await kit.LockAsync()).Code.Should().Be(0);
        await using var again = ModelArmsBinary.StartHolder(kit.Root, kit.Layout.RunnerLock("C1"), FakeCli.Executable);
        await again.LineAsync("held ", TimeSpan.FromSeconds(30));
        var forced = await kit.UnlockAsync(force: true, clock: TimeProvider.System);
        forced.Code.Should().Be(0, forced.Output + forced.Error);
        kit.Modes().Should().Equal("755", "700", "755", "755", "755", "755", "755", "755");
    }

    [Fact]
    public async Task Lock_then_run_arm_leaves_the_experiment_locked()
    {
        using var kit = new IsolationKit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        RunArmKit.Replay(fake, "result-success.jsonl");
        (await kit.LockAsync()).Code.Should().Be(0);
        var state = kit.Snapshot();
        var modes = kit.Modes();

        var run = await RunArmKit.RunAsync(kit.Root, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        run.Error.Should().NotContain("is not applied", "the isolation is applied for the run");
        kit.Root.ArmDones("C1").Should().ContainSingle();
        kit.Snapshot().Should().Be(state, "run-arm restores nothing and re-locks nothing: the same digest and verifiedAt");
        kit.Modes().Should().Equal(modes).And.Equal("0", "300", "300", "300", "300", "300", "300", "300");
        File.ReadAllText(kit.Layout.PidFile("C1")).Should().Be(Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "\n");
        var probe = FileGateLock.Create(TimeProvider.System, TimeSpan.FromMilliseconds(50)).Ok();
        probe.Take(kit.Layout.RunnerLock("C1"), create: false, note: false).Should().BeOfType<GateLockAnswer.Held>("the runner's lock is free once it ended");
        probe.Take(kit.Layout.IsolationLock, create: false, note: false).Should().BeOfType<GateLockAnswer.Held>("so is the isolation lock");
        var paths = kit.Root.PathsOf("C1");
        var session = Guid.Parse(File.ReadAllText(paths.Session("1-code")).Trim());
        File.Exists(SessionTranscripts.PathOf(SessionTranscripts.ClaudeHome(kit.Root.Environment), paths.CloneFolder, session)).Should().BeTrue("the transcript the fake left is reached by name under the 300 ~/.claude/projects");
    }

    [Fact]
    public async Task A_runner_warns_once_when_its_runs_isolation_is_not_applied_and_still_runs()
    {
        using var kit = new IsolationKit();
        using var fake = new FakeCli();
        await kit.AddArmAsync("C1", fake, hang: false);
        RunArmKit.Replay(fake, "result-success.jsonl");

        var bare = await RunArmKit.RunAsync(kit.Root, "C1");

        bare.Code.Should().Be(0, bare.Error + bare.Output);
        kit.Root.ArmDones("C1").Should().ContainSingle();
        var warnings = bare.Error.Split('\n').Where(line => line.Contains("is not applied", StringComparison.Ordinal)).ToList();
        warnings.Should().ContainSingle().Which.Should().Be("model-arms run-arm C1: run part6 has an isolation block and its isolation is not applied (no isolation snapshot) — the arms can list each other's sessions; run `model-arms lock part6`");

        kit.Root.Edit(top =>
        {
            top["runs"]!["part7"]!["closedArms"] = new System.Text.Json.Nodes.JsonArray();
            top["runs"]!["part7"]!["closed"] = new System.Text.Json.Nodes.JsonArray("<root>/chain/part6");
        });
        (await kit.LockAsync("part7")).Code.Should().Be(0);
        var other = await RunArmKit.RunAsync(kit.Root, "C1");
        other.Code.Should().Be(0, other.Error + other.Output);
        other.Error.Should().Contain("run part6 has an isolation block and its isolation is not applied (isolation applied for part7 since");

        (await kit.UnlockAsync()).Code.Should().Be(0);
        (await kit.LockAsync()).Code.Should().Be(0);
        var applied = await RunArmKit.RunAsync(kit.Root, "C1");
        applied.Code.Should().Be(0, applied.Error + applied.Output);
        applied.Error.Should().NotContain("is not applied");

        using var plain = new ExperimentRoot();
        using var plainFake = new FakeCli();
        await plain.MakeArmAsync("C1", plainFake);
        RunArmKit.Replay(plainFake, "result-success.jsonl");
        var without = await RunArmKit.RunAsync(plain, "C1");
        without.Code.Should().Be(0, without.Error + without.Output);
        without.Error.Should().NotContain("is not applied", "the example's isolation block is removed by default: the line never enters the scheme tests' stderr");
    }
}
