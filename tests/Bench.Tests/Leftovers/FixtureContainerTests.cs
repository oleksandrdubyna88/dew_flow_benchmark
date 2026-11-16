using System.Reflection;
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
    public void Reuse_set_through_configuration_alone_stays_off_and_the_container_still_ends_with_the_fixture()
    {
        using var dir = TempDir.For("e6-reuse");
        File.WriteAllText(dir.At(".testcontainers.properties"), "testcontainers.reuse.enable=true\n");
        var env = new Dictionary<string, string?> { ["TESTCONTAINERS_REUSE_ENABLE"] = "true", ["HOME"] = dir.Path, ["USERPROFILE"] = dir.Path, [ReuseStep] = "1", [ReuseOut] = dir.At("out.txt") };

        var child = SuiteChild.Run(["--no-ansi", "--filter-method", "*ReuseChild_reads_the_builders_configured_reuse"], env, ChildBudget);

        child.TimedOut.Should().BeFalse();
        child.ExitCode.Should().Be(0, child.StdOut + child.StdErr);
        File.ReadAllLines(dir.At("out.txt")).Should().Contain("reuse=False", "Testcontainers 4.16.0 has no configuration path that turns reuse on; a dependency bump that adds one turns this pin red");
    }

    /// <summary>The child of the reuse pin: the builder's configured reuse, read by reflection from the non-public resource configuration.</summary>
    [Fact]
    public void ReuseChild_reads_the_builders_configured_reuse()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ReuseStep))) return;
        var lines = new List<string> { "reuse=" + ConfiguredReuse(new PostgresFixture().Builder()) };
        File.WriteAllLines(Environment.GetEnvironmentVariable(ReuseOut)!, lines);
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
        var gate = new ContainerPass.Gate(() =>
        {
            Interlocked.Increment(ref runs);
            Thread.Sleep(200);
            return new SweepResult([], [], ["counted"], []);
        });
        using var start = new ManualResetEventSlim();

        var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            start.Wait();
            return gate.Await();
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        runs.Should().Be(1, "every caller waits for the one pass");
        gate.Runs.Should().Be(1);
        results.Should().OnlyContain(result => ReferenceEquals(result, results[0]), "every caller got the same result");
    }

    [Fact]
    public void TheGate_caches_a_pass_that_throws_as_one_error_line_and_never_throws()
    {
        var gate = new ContainerPass.Gate(() => throw new InvalidOperationException("boom\nsecond line"));

        var first = gate.Await();
        var second = gate.Await();

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
