using System.Diagnostics;
using System.Reflection;
using Bench.Infrastructure.Process;
using Bench.Tests.Infrastructure;
using DotNet.Testcontainers.Configurations;
using FluentAssertions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bench.Tests.Leftovers;

/// <summary>One collection for every proof about the fixture's own container, so they run one at a time. No fixture is attached: each proof starts its own
/// <see cref="PostgresFixture"/> (a collection fixture that throws fails its tests and cannot skip them).</summary>
[CollectionDefinition("docker-real")]
public sealed class DockerRealCollection;

/// <summary>
/// The fixture's container (E6 S3): the reaper check, the reuse pin, the once-per-process pass and the names. The facts that need no daemon run everywhere; they run in CHILDREN of this
/// executable where a process-wide setting or the process-wide gate is under test, so the parent's own state is never touched.
/// </summary>
[Collection("docker-real")]
public sealed class FixtureContainerTests
{
    private const string ReaperStep = "BENCH_E6_REAPER_CHILD";
    private const string ReaperOut = "BENCH_E6_REAPER_OUT";
    private const string ReuseStep = "BENCH_E6_REUSE_CHILD";
    private const string ReuseOut = "BENCH_E6_REUSE_OUT";
    private const string GateStep = "BENCH_E6_GATE_CHILD";
    private const string GateOut = "BENCH_E6_GATE_OUT";
    private const string RyukOut = "BENCH_E6_RYUK_CHILD_OUT";
    private const string SwitchedOff = "The Testcontainers reaper (Ryuk) is switched off here";
    private static readonly TimeSpan ChildBudget = TimeSpan.FromMinutes(3);

    [Fact]
    public void A_fixture_refuses_to_start_when_the_environment_switches_the_reaper_off()
    {
        using var dir = TempDir.For("e6-reaper");
        var off = RunReaperChild(dir, "construct", "true");
        var on = RunReaperChild(dir, "construct", null);

        off.TimedOut.Should().BeFalse();
        off.ExitCode.Should().NotBe(0, "a fixture whose reaper is off refuses at construction");
        (off.StdOut + off.StdErr).Should().Contain(SwitchedOff);
        on.TimedOut.Should().BeFalse();
        on.ExitCode.Should().Be(0, "with the variable removed the same child passes the constructor: " + on.StdOut + on.StdErr);
        File.ReadAllText(dir.At("out.txt")).Should().Be("constructed", "the child stopped at the constructor");
    }

    [Fact]
    public void A_fixture_refuses_to_build_when_the_reaper_is_switched_off_after_construction()
    {
        using var dir = TempDir.For("e6-reaper");

        var child = RunReaperChild(dir, "initialize", null);

        child.TimedOut.Should().BeFalse();
        child.ExitCode.Should().NotBe(0);
        var output = child.StdOut + child.StdErr;
        output.Should().Contain(SwitchedOff, "the refusal comes from the second check");
        output.Should().NotContain("The durability tests need Docker", "the refusal came before Build(), so it is never reworded as a missing daemon");
    }

    /// <summary>The child of the two reaper facts. It returns at once when the parent did not name a step, so it is green in every normal run.</summary>
    [Fact]
    public async Task ReaperChild_runs_the_step_the_parent_names()
    {
        var step = Environment.GetEnvironmentVariable(ReaperStep);
        if (string.IsNullOrEmpty(step)) return;
        var fixture = new PostgresFixture();
        if (step == "initialize")
        {
            // Through the setter by reflection, never an assignment: the source test reads an assignment as a switch.
            typeof(TestcontainersSettings).GetProperty(nameof(TestcontainersSettings.ResourceReaperEnabled))!.SetValue(null, false);
            await fixture.InitializeAsync();
        }

        File.WriteAllText(Environment.GetEnvironmentVariable(ReaperOut)!, "constructed");
    }

    [Fact]
    public async Task Reuse_set_through_configuration_alone_stays_off_and_the_container_still_ends_with_the_fixture()
    {
        using var dir = TempDir.For("e6-reuse");
        File.WriteAllText(dir.At(".testcontainers.properties"), "testcontainers.reuse.enable=true\n");
        var env = new Dictionary<string, string?> { ["TESTCONTAINERS_REUSE_ENABLE"] = "true", ["HOME"] = dir.Path, ["USERPROFILE"] = dir.Path, [ReuseStep] = "1", [ReuseOut] = dir.At("out.txt") };

        var child = SuiteChild.Run(["--no-ansi", "--filter-method", "*ReuseChild_reads_the_builders_configured_reuse"], env, ChildBudget);

        child.TimedOut.Should().BeFalse();
        child.ExitCode.Should().Be(0, child.StdOut + child.StdErr);
        var lines = File.ReadAllLines(dir.At("out.txt"));
        lines.Should().Contain("reuse=False", "Testcontainers 4.16.0 has no configuration path that turns reuse on; a dependency bump that adds one turns this pin red");
        await ContainerHalfAsync(lines);
    }

    /// <summary>Where a daemon answered, the child also started the fixture and disposed it: neither its container nor its volumes may be there, and its reuse-hash label was empty.</summary>
    private static async Task ContainerHalfAsync(string[] lines)
    {
        if (DockerGate.Decide(DockerGate.NoDaemonReason, DockerGate.OnGitHubActions) is { Kind: DockerGate.Kind.Skip } skipped)
        {
            TestContext.Current.AddWarning("the container half of the reuse pin did not run: " + skipped.Reason);
            return;
        }

        DockerGate.Require();
        var id = lines.Single(line => line.StartsWith("id=", StringComparison.Ordinal))[3..];
        var volumes = lines.Where(line => line.StartsWith("volume=", StringComparison.Ordinal)).Select(line => line[7..]).ToList();
        lines.Should().Contain("reusehash=", "the fixture's container carries no reuse-hash label");
        try
        {
            (await DockerCli.Survivors(id, volumes, TimeSpan.FromSeconds(30))).Should().BeEmpty("with reuse set through configuration alone the container still ends with the fixture, and so do its volumes");
        }
        finally
        {
            await DockerCli.CleanUp(id, volumes, null);
        }
    }

    /// <summary>The child of the reuse pin: the builder's configured reuse, read by reflection from the non-public resource configuration.</summary>
    [Fact]
    public async Task ReuseChild_reads_the_builders_configured_reuse()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ReuseStep))) return;
        var lines = new List<string> { "reuse=" + ConfiguredReuse(new PostgresFixture().Builder()) };
        if (DockerGate.NoDaemonReason is null) lines.AddRange(await StartAndDisposeAsync());
        File.WriteAllLines(Environment.GetEnvironmentVariable(ReuseOut)!, lines);
    }

    private static async Task<IEnumerable<string>> StartAndDisposeAsync()
    {
        var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        var id = fixture.ContainerId;
        var volumes = await DockerCli.VolumesOf(id);
        var hash = await DockerCli.Format(id, "{{index .Config.Labels \"org.testcontainers.reuse-hash\"}}");
        await fixture.DisposeAsync();
        return [$"id={id}", .. volumes.Select(volume => "volume=" + volume), "reusehash=" + hash];
    }

    [Fact]
    public void The_container_pass_runs_once_for_two_fixture_starts_in_one_process()
    {
        using var dir = TempDir.For("e6-gate");
        var env = new Dictionary<string, string?> { [GateStep] = "1", [GateOut] = dir.At("out.txt") };

        var child = SuiteChild.Run(["--no-ansi", "--filter-method", "*GateChild_starts_two_fixtures"], env, ChildBudget);

        child.TimedOut.Should().BeFalse();
        child.ExitCode.Should().Be(0, child.StdOut + child.StdErr);
        File.ReadAllLines(dir.At("out.txt")).Should().Contain("passes=1", "no other test shares that process, so the only passes are the gate's");
    }

    /// <summary>The child of the gate fact: two fixtures started one after the other in a process that runs nothing else. Where no daemon answers each start fails after the pass.</summary>
    [Fact]
    public async Task GateChild_starts_two_fixtures()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(GateStep))) return;
        var lines = new List<string>();
        foreach (var index in new[] { 0, 1 })
        {
            var fixture = new PostgresFixture();
            try
            {
                await fixture.InitializeAsync();
                lines.Add($"start{index}=started");
            }
            catch (InvalidOperationException ex)
            {
                lines.Add($"start{index}=failed: {ex.Message.Split('\n')[0]}");
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }

        lines.Add($"passes={ContainerPass.Runs}");
        File.WriteAllLines(Environment.GetEnvironmentVariable(GateOut)!, lines);
    }

    [Fact]
    public async Task TheGate_runs_its_pass_once_for_eight_callers()
    {
        var runs = 0;
        var gate = new ContainerPass.Gate(async () =>
        {
            Interlocked.Increment(ref runs);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            return new SweepResult([], [], ["counted"], []);
        });
        using var start = new ManualResetEventSlim();

        var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            start.Wait();
            return await gate.AwaitAsync();
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        runs.Should().Be(1, "every caller waits for the one pass");
        gate.Runs.Should().Be(1);
        results.Should().OnlyContain(result => ReferenceEquals(result, results[0]), "every caller got the same result");
    }

    [Fact]
    public async Task TheGate_caches_a_pass_that_throws_as_one_error_line_and_never_throws()
    {
        var gate = new ContainerPass.Gate(() => throw new InvalidOperationException("boom\nsecond line"));

        var first = await gate.AwaitAsync();
        var second = await gate.AwaitAsync();

        first.Errors.Should().Equal("containers: failed: boom");
        second.Should().BeSameAs(first, "a cached exception would fail every later fixture of the process");
        gate.Runs.Should().Be(1);
    }

    [Fact]
    public void TwoFixtures_get_two_names()
    {
        var first = new PostgresFixture().ContainerName;
        var second = new PostgresFixture().ContainerName;

        first.Should().NotBe(second);
        new[] { first, second }.Should().OnlyContain(name => Owned.FullName().IsMatch(name) && name.Length <= Owned.MaxName, "an owner-form name of at most 63 characters, valid for Docker");
    }

    [Fact(Timeout = 900_000)]
    public async Task Ryuk_removes_the_container_and_its_volumes_of_a_child_test_process_killed_with_SIGKILL()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the proof kills the child with SIGKILL: there is no such signal on Windows");
        DockerGate.Require();
        using var dir = TempDir.For("e6-ryuk");
        var env = new Dictionary<string, string?> { [RyukOut] = dir.At("child.txt") };
        using var child = Process.Start(SuiteChild.StartInfoFor(Environment.ProcessPath!, ["--no-ansi", "--filter-method", "*RyukChild_starts_a_fixture_and_waits_to_be_killed"], env))!;
        var drained = Task.WhenAll(child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken), child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken));
        string? id = null;
        IReadOnlyList<string> volumes = [];
        Exception? failed = null;
        try
        {
            id = await WaitForChildContainerAsync(dir.At("child.txt"), child);
            (await DockerCli.ContainerIds(id)).Should().ContainSingle("the child's container is running before the kill");
            volumes = await DockerCli.VolumesOf(id);
            volumes.Should().NotBeEmpty("the image declares its data directory a volume; none means this fixture no longer proves that Ryuk removes volumes");

            child.Kill();
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
            child.ExitCode.Should().Be(137, "SIGKILL: the child had no chance to dispose anything");

            var gone = await DockerCli.Poll(async () => (await DockerCli.ContainerIds(id)).Count == 0, TimeSpan.FromSeconds(120));
            gone.Should().NotBeNull($"Ryuk must remove container {id} within 120 s of the kill");
            TestContext.Current.AddWarning($"Ryuk removed the killed child's container after {gone!.Value.TotalSeconds:0} s");
            var volumesGone = await DockerCli.Poll(async () => !(await DockerCli.ListedVolumes()).Intersect(volumes).Any(), TimeSpan.FromSeconds(120));
            volumesGone.Should().NotBeNull($"Ryuk must remove the volumes {string.Join(", ", volumes)} of the killed child's container within 120 s");
            TestContext.Current.AddWarning($"the killed child's volumes were gone after {volumesGone!.Value.TotalSeconds:0} s");
        }
        catch (Exception ex)
        {
            failed = ex;
            throw;
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            try
            {
                await drained.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                // The killed child's pipes are not read to their end; nothing depends on them.
            }

            if (id is not null) await DockerCli.CleanUp(id, volumes, failed);
        }
    }

    /// <summary>The child of the Ryuk proof: starts the fixture, reports its container (a temp file, then a move) and waits to be killed.</summary>
    [Fact(Timeout = 900_000)]
    public async Task RyukChild_starts_a_fixture_and_waits_to_be_killed()
    {
        var report = Environment.GetEnvironmentVariable(RyukOut);
        if (string.IsNullOrEmpty(report)) return;
        var fixture = new PostgresFixture();
        await fixture.InitializeAsync();
        File.WriteAllText(report + ".part", fixture.ContainerId + "\n" + fixture.ContainerName + "\n");
        File.Move(report + ".part", report);
        await Task.Delay(TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TheFixturesContainer_has_the_owner_form_name_and_the_runs_record_as_its_label()
    {
        DockerGate.Require();
        var fixture = new PostgresFixture();
        string? id = null;
        Exception? failed = null;
        try
        {
            await fixture.InitializeAsync();
            id = fixture.ContainerId;
            // The last field is a sentinel: the runner trims its output, and an empty reuse-hash value at the very end would lose its tab.
            var fields = (await DockerCli.Format(id, "{{.Name}}\t{{index .Config.Labels \"" + ContainerPass.LabelKey + "\"}}\t{{index .Config.Labels \"org.testcontainers.reuse-hash\"}}\tend")).Split('\t');

            fields.Should().HaveCount(4);
            fields[3].Should().Be("end");
            fields[0].TrimStart('/').Should().Be(fixture.ContainerName, "the container carries the name the fixture chose before it started");
            Owned.FullName().IsMatch(fields[0].TrimStart('/')).Should().BeTrue("an owner-form name of this user and this repository");
            fields[1].Should().Be(Owner.RunRecord().Format(), "the label is the owner record the container pass reads");
            fields[2].Should().BeEmpty("reuse is never set: Testcontainers writes its reuse-hash label only for a reused container");
        }
        catch (Exception ex)
        {
            failed = ex;
            throw;
        }
        finally
        {
            await fixture.DisposeAsync();
            await DockerCli.CleanUp(id ?? fixture.ContainerName, [], failed);
        }
    }

    [Fact]
    public async Task ThePass_on_a_real_daemon_removes_a_dead_owners_container_by_its_id()
    {
        DockerGate.Require();
        var pid = UnmatchablePid.Next();
        UnmatchablePid.AssertNoSuchProcess(pid);
        var made = new List<string>();
        Exception? failed = null;
        try
        {
            var dead = await DockerCli.Create(Owned.NameFor("pass-dead"), (Owner.This with { Pid = pid }).Format());
            made.Add(dead);
            var live = await DockerCli.Create(Owned.NameFor("pass-live"), Owner.This.Format());
            made.Add(live);
            var bare = await DockerCli.Create(Owned.NameFor("pass-bare"), null);
            made.Add(bare);

            var result = ContainerPass.Run(ContainerPass.Real(DateTimeOffset.UtcNow) with { MinAge = TimeSpan.Zero, Consider = made.Contains });

            result.Errors.Should().BeEmpty();
            result.Removed.Should().ContainSingle().Which.Should().EndWith($"({dead[..12]})");
            (await DockerCli.ContainerIds(dead)).Should().BeEmpty("the dead owner's container is removed by its captured id");
            var kept = result.Preserved.Should().ContainSingle().Subject;
            kept.Path.Should().EndWith($"({live[..12]})");
            kept.Reason.Should().Be("live");
            (await DockerCli.ContainerIds(live)).Should().ContainSingle("a live owner's container is kept");
            string.Join('\n', result.Listed.Concat(result.Preserved.Select(kept => kept.Path)).Concat(result.Removed)).Should().NotContain(bare[..12], "a container with no owner label is never listed by the label filter");
        }
        catch (Exception ex)
        {
            failed = ex;
            throw;
        }
        finally
        {
            foreach (var id in made) await DockerCli.CleanUp(id, [], failed);
        }
    }

    [Fact]
    public async Task A_reused_container_outlives_disposal_so_the_disposal_check_catches_reuse()
    {
        DockerGate.Require();
        var fixture = new PostgresFixture(builder => builder.WithReuse(true));
        string? id = null;
        IReadOnlyList<string> volumes = [];
        Exception? failed = null;
        try
        {
            await fixture.InitializeAsync();
            id = fixture.ContainerId;
            volumes = await DockerCli.VolumesOf(id);
            await fixture.DisposeAsync();

            var survivors = await DockerCli.Survivors(id, volumes, TimeSpan.FromSeconds(10));

            survivors.Should().NotBeEmpty("a container started with reuse is not removed by disposal (Testcontainers: enabling reuse disables the reaper), so the disposal check of the next test sees it: reuse cannot hide");
        }
        catch (Exception ex)
        {
            failed = ex;
            throw;
        }
        finally
        {
            // A reused container is not tracked by Ryuk: it is removed here by its id, or by its name when the start failed after the container existed.
            await DockerCli.CleanUp(id ?? fixture.ContainerName, volumes, failed);
        }
    }

    [Fact]
    public async Task TheDisposedFixturesContainer_is_gone_by_its_id_and_with_it_its_databases_and_its_volumes()
    {
        DockerGate.Require();
        var fixture = new PostgresFixture();
        string? id = null;
        IReadOnlyList<string> volumes = [];
        Exception? failed = null;
        try
        {
            await fixture.InitializeAsync();
            id = fixture.ContainerId;
            await fixture.NewEmptyDatabaseAsync("e6s3_" + Guid.NewGuid().ToString("N")[..12]);
            (await DockerCli.ContainerIds(id)).Should().ContainSingle("the container is there before disposal");
            volumes = await DockerCli.VolumesOf(id);
            volumes.Should().NotBeEmpty("the image declares its data directory a volume; none means this proof no longer proves the volume ends with the container");
            TestContext.Current.AddWarning($"the fixture's container size after one database: {await DockerCli.SizeOf(id)}");

            await fixture.DisposeAsync();

            (await DockerCli.Survivors(id, volumes, TimeSpan.FromSeconds(30))).Should().BeEmpty("disposal removes the container, its databases and its volumes");
        }
        catch (Exception ex)
        {
            failed = ex;
            throw;
        }
        finally
        {
            await DockerCli.CleanUp(id ?? fixture.ContainerName, volumes, failed);
        }
    }

    [Theory]
    [InlineData(null, true, DockerGate.Kind.Run)]
    [InlineData(null, false, DockerGate.Kind.Run)]
    [InlineData("'docker' is not on PATH", true, DockerGate.Kind.Fail)]
    [InlineData("'docker' is not on PATH", false, DockerGate.Kind.Skip)]
    public void TheDockerGate_runs_where_a_daemon_answered_fails_on_GitHub_Actions_without_one_and_skips_elsewhere(string? noDaemonReason, bool githubActions, DockerGate.Kind expected)
    {
        var decision = DockerGate.Decide(noDaemonReason, githubActions);

        decision.Kind.Should().Be(expected);
        if (noDaemonReason is not null) decision.Reason.Should().Contain(noDaemonReason, "a skip or a failure names why no daemon answered");
    }

    private static async Task<string> WaitForChildContainerAsync(string report, Process child)
    {
        var clock = Stopwatch.StartNew();
        while (!File.Exists(report))
        {
            if (child.HasExited) Assert.Fail($"the child exited ({child.ExitCode}) before it started its container");
            if (clock.Elapsed > TimeSpan.FromMinutes(5)) Assert.Fail("the child did not start its container within 5 minutes");
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }

        return File.ReadAllLines(report)[0];
    }

    private static SuiteChild.Result RunReaperChild(TempDir dir, string step, string? ryukOff)
    {
        var env = new Dictionary<string, string?> { [ContainerPass.RyukVariable] = ryukOff, [ReaperStep] = step, [ReaperOut] = dir.At("out.txt") };
        return SuiteChild.Run(["--no-ansi", "--filter-method", "*ReaperChild_runs_the_step_the_parent_names"], env, ChildBudget);
    }

    /// <summary>Reads <c>Reuse</c> from the builder's non-public <c>DockerResourceConfiguration</c>, walking the type chain for the member.</summary>
    private static string ConfiguredReuse(PostgreSqlBuilder builder)
    {
        var configuration = Member(builder, "DockerResourceConfiguration") ?? throw new InvalidOperationException("the builder has no DockerResourceConfiguration: Testcontainers changed shape");
        return (Member(configuration, "Reuse") ?? "null").ToString()!;
    }

    private static object? Member(object owner, string name)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (var type = owner.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetProperty(name, all) is { } property) return property.GetValue(owner);
        }

        return null;
    }
}

/// <summary>Whether the Docker proofs run here. A daemon that answered runs them; none, on GitHub Actions, FAILS them (a durability suite that quietly does not run reports green while guarding nothing); none elsewhere skips them with its reason.</summary>
public static class DockerGate
{
    public enum Kind
    {
        Run,
        Fail,
        Skip,
    }

    public sealed record Decision(Kind Kind, string Reason);

    private static readonly Lazy<string?> NoDaemon = new(AskTheDaemon);

    /// <summary>Null when a daemon answered <c>docker version</c>; otherwise why none did.</summary>
    internal static string? NoDaemonReason => NoDaemon.Value;

    internal static bool OnGitHubActions => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    /// <summary>Pure: <paramref name="noDaemonReason"/> is null when a daemon answered.</summary>
    public static Decision Decide(string? noDaemonReason, bool githubActions) =>
        noDaemonReason is null ? new Decision(Kind.Run, "a daemon answered")
        : githubActions ? new Decision(Kind.Fail, "no Docker daemon answered on GitHub Actions: " + noDaemonReason)
        : new Decision(Kind.Skip, "no Docker daemon answered here: " + noDaemonReason);

    /// <summary>Runs at the top of a Docker proof: returns when a daemon answered, fails under GitHub Actions without one, skips elsewhere.</summary>
    internal static void Require()
    {
        var decision = Decide(NoDaemonReason, OnGitHubActions);
        if (decision.Kind == Kind.Fail) Assert.Fail(decision.Reason);
        if (decision.Kind == Kind.Skip) Assert.Skip(decision.Reason);
    }

    private static string? AskTheDaemon()
    {
        var attempt = Task.Run(() => ProcessRunner.RunAsync("docker", ["version", "--format", "{{.Server.Version}}"], AppContext.BaseDirectory, TimeSpan.FromSeconds(30), CancellationToken.None)).GetAwaiter().GetResult();
        return attempt is ProcessAttempt.Completed { Result: { ExitCode: 0, StandardOutput: { Length: > 0 } } } ? null : attempt.Describe + (attempt is ProcessAttempt.Completed done ? ": " + done.Result.StandardError.Trim() : string.Empty);
    }
}

/// <summary>The Docker questions of the proofs. Every query must SUCCEED to count: a failed one or a timeout fails the test, never passes as "absent".</summary>
internal static class DockerCli
{
    private static readonly TimeSpan Call = TimeSpan.FromSeconds(60);

    internal static async Task<ProcessResult> Run(params string[] args)
    {
        var attempt = await ProcessRunner.RunAsync("docker", args, AppContext.BaseDirectory, Call, TestContext.Current.CancellationToken);
        if (attempt is ProcessAttempt.Completed { Result.ExitCode: 0 } done) return done.Result;
        throw new InvalidOperationException($"docker {args[0]} did not succeed: {attempt.Describe}" + (attempt is ProcessAttempt.Completed failed ? ": " + failed.Result.StandardError.Trim() : string.Empty));
    }

    private static async Task<List<string>> Lines(params string[] args) =>
        [.. (await Run(args)).StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    internal static Task<List<string>> ContainerIds(string id) => Lines("ps", "-a", "-q", "--no-trunc", "--filter", "id=" + id);

    internal static Task<List<string>> ListedVolumes() => Lines("volume", "ls", "-q");

    internal static async Task<List<string>> VolumesOf(string id) =>
        await Lines("inspect", "--type", "container", "--format", "{{range .Mounts}}{{if eq .Type \"volume\"}}{{.Name}}{{println}}{{end}}{{end}}", id);

    internal static async Task<string> Format(string id, string template) =>
        (await Run("inspect", "--type", "container", "--format", template, id)).StandardOutput.TrimEnd('\n');

    internal static async Task<string> SizeOf(string id) =>
        (await Run("ps", "-a", "-s", "--no-trunc", "--filter", "id=" + id, "--format", "{{.Size}}")).StandardOutput.Trim();

    /// <summary>A stopped container by <c>docker create</c> (never started): the name and the owner label given, or no label at all.</summary>
    internal static async Task<string> Create(string name, string? record)
    {
        var label = record is null ? [] : new[] { "--label", ContainerPass.LabelKey + "=" + record };
        return (await Run(["create", "--name", name, .. label, "postgres:17-alpine"])).StandardOutput.Trim();
    }

    /// <summary>The disposal check: what is still there after <paramref name="ceiling"/> — the container by its id, the volumes it had. Empty is clean.</summary>
    internal static async Task<List<string>> Survivors(string id, IReadOnlyList<string> volumes, TimeSpan ceiling)
    {
        var left = new List<string>();
        if (await Poll(async () => (await ContainerIds(id)).Count == 0, ceiling) is null) left.Add("container " + id);
        if (await Poll(async () => !(await ListedVolumes()).Intersect(volumes).Any(), ceiling) is null) left.AddRange((await ListedVolumes()).Intersect(volumes).Select(volume => "volume " + volume));
        return left;
    }

    /// <summary>Polls every two seconds; the seconds it took, or null when <paramref name="ceiling"/> passed first.</summary>
    internal static async Task<TimeSpan?> Poll(Func<Task<bool>> done, TimeSpan ceiling)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (await done()) return clock.Elapsed;
            if (clock.Elapsed >= ceiling) return null;
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Removes by the captured id (or the container's name, when the start failed before an id was known) and the captured volume names. A resource that is already gone is clean; any other
    /// non-zero exit, a timeout or a missing docker is a cleanup failure naming the resource: a warning, and a thrown error unless the proof itself already failed (<paramref name="failed"/>), whose exception must not be replaced.</summary>
    internal static async Task CleanUp(string reference, IReadOnlyList<string> volumes, Exception? failed)
    {
        var left = new List<string>();
        if (await Try("container " + reference, "rm", "-f", "-v", reference) is { } container) left.Add(container);
        foreach (var volume in volumes)
        {
            if (await Try("volume " + volume, "volume", "rm", volume) is { } survivor) left.Add(survivor);
        }

        foreach (var line in left) TestContext.Current.AddWarning("CLEANUP FAILED: " + line);
        if (left.Count > 0 && failed is null) throw new InvalidOperationException("cleanup failed, the daemon may still hold: " + string.Join("; ", left));
    }

    /// <summary>Null when the call removed the resource or it was already gone; otherwise the one line that names the resource and what went wrong.</summary>
    private static async Task<string?> Try(string resource, params string[] args)
    {
        ProcessAttempt attempt;
        try
        {
            attempt = await ProcessRunner.RunAsync("docker", args, AppContext.BaseDirectory, Call, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return $"{resource}: docker {args[0]} threw {ex.Message}";
        }

        return attempt switch
        {
            ProcessAttempt.Completed { Result.ExitCode: 0 } => null,
            ProcessAttempt.Completed gone when gone.Result.StandardError.Contains("No such ", StringComparison.Ordinal) => null,
            ProcessAttempt.Completed failed => $"{resource}: docker {args[0]} exited {failed.Result.ExitCode}: {failed.Result.StandardError.Trim().Split('\n')[0]}",
            _ => $"{resource}: docker {args[0]} {attempt.Describe}",
        };
    }
}
