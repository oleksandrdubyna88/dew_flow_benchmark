using System.Diagnostics;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Leftovers;

// Source: the order test is after dew_flow_connect_other_ais, src_mcp/tests/RunTempTests.cs at e060d77e (TheProducerAndTheSweep_LandTogether, TheSweep_EndedBeforeTheFirstTestStarted); the gate tests are new (E6 S2, brief § 6.4).
// This is benchmark's own file: it is not a manifest target.

/// <summary>
/// The once-only start-of-run gate and the order proof (E6 S2, G1-G4). G1 and G2 run on a gate of their own (the <see cref="StartOfRunSweep.Gate"/> seam), so they never move the end stamp or the counter that G3 reads
/// for the real process. G3 and G4 read the real process: the sweep ran once and ended before the first test began, and the assembly registers the fixture and the order proof.
/// </summary>
public sealed class StartOfRunSweepTests
{
    private const int Threads = 8;
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(60);

    /// <summary>What one dedicated thread saw: the result or the exception of its <c>Await()</c>, and the moment it returned.</summary>
    private sealed record Arrival(SweepResult? Result, Exception? Thrown, long ReturnedAt);

    /// <summary>
    /// Eight dedicated threads (not the xUnit pool: CI has fewer threads) meet at a barrier and call <see cref="StartOfRunSweep.Gate.Await"/> together. Every wait has a ceiling.
    /// </summary>
    private static List<Arrival> EightThreadsAwait(StartOfRunSweep.Gate gate)
    {
        var arrivals = new Arrival[Threads];
        using var barrier = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads).Select(index => new Thread(() =>
        {
            try
            {
                if (!barrier.SignalAndWait(Ceiling))
                {
                    arrivals[index] = new Arrival(null, new TimeoutException("the barrier did not fill"), Stopwatch.GetTimestamp());
                    return;
                }

                var result = gate.Await();
                arrivals[index] = new Arrival(result, null, Stopwatch.GetTimestamp());
            }
            catch (Exception ex)
            {
                arrivals[index] = new Arrival(null, ex, Stopwatch.GetTimestamp());
            }
        })
        { IsBackground = true }).ToList();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join(Ceiling).Should().BeTrue("a thread that waits on the gate must come back");
        }

        return [.. arrivals];
    }

    [Fact]
    public void TheGate_RunsTheSweepOnce_WhenEightThreadsArriveTogether()
    {
        var counter = 0;
        var made = SweepResult.Empty with { Listed = ["one sweep"] };
        var gate = new StartOfRunSweep.Gate(() =>
        {
            Interlocked.Increment(ref counter);
            Thread.Sleep(300);

            return made;
        });

        var arrivals = EightThreadsAwait(gate);

        Volatile.Read(ref counter).Should().Be(1, "eight threads at once: the sweep runs once");
        gate.Runs.Should().Be(1);
        arrivals.Should().OnlyContain(a => a.Thrown == null, "the gate never throws");
        arrivals.Should().OnlyContain(a => ReferenceEquals(a.Result, made), "every thread gets the SAME result object");
        gate.EndedAt.Should().NotBe(0);
        arrivals.Should().OnlyContain(a => a.ReturnedAt >= gate.EndedAt, "no thread returned before the sweep ended");
    }

    [Fact]
    public void TheGate_NeverThrows_ASweepThatThrowsIsOneErrorLine_OnEveryThread()
    {
        var gate = new StartOfRunSweep.Gate(() => throw new InvalidOperationException("boom\nsecond"));

        var arrivals = EightThreadsAwait(gate);

        arrivals.Should().OnlyContain(a => a.Thrown == null, "the gate never throws: a Lazy would cache the exception and rethrow it on every thread");
        arrivals.Should().OnlyContain(a => a.Result != null && a.Result.Errors.SequenceEqual(new[] { "sweep failed: boom" }), "the first line of the message, once, on every thread");
        gate.Runs.Should().Be(1);
        gate.EndedAt.Should().NotBe(0, "the end is stamped on the error path too");
    }

    /// <summary>The "many test classes at once" proof, in the real process: xUnit runs collections in parallel (8 threads here by xunit.runner.json, the vCPU count on CI), and still the sweep ran once.</summary>
    [Fact]
    public void TheRealSweep_RanOnceInThisProcess_AndEndedBeforeTheFirstTestStarted()
    {
        StartOfRunSweep.OrderViolations.Should().BeEmpty("the order proof never throws; it records each test that began before the sweep ended, by name");
        StartOfRunSweep.Runs.Should().Be(1, "every caller of the real sweep, on any thread, waited for the one sweep");
        StartOfRunSweep.EndedAt.Should().NotBe(0, "the sweep returned (a result or an error) and stamped its end");
        StartOfRunSweep.FirstTestStart.Should().NotBe(0, "the order proof's Before recorded a start");
        StartOfRunSweep.FirstTestStart.Should().BeGreaterThan(StartOfRunSweep.EndedAt, "no test started before the sweep ended");
        var margin = Stopwatch.GetElapsedTime(StartOfRunSweep.EndedAt, StartOfRunSweep.FirstTestStart);
        TestContext.Current.AddWarning($"E6 S2 order: the start-of-run sweep ended {margin.TotalMilliseconds:F1} ms before the first test started");
    }

    [Fact]
    public void TheAssembly_RegistersTheSweepFixtureAndTheOrderProof()
    {
        var assembly = typeof(StartOfRunSweepTests).Assembly;

        assembly.GetCustomAttributes<AssemblyFixtureAttribute>().Select(a => a.AssemblyFixtureType)
            .Should().Contain(typeof(LeftoversSweepFixture), "the start-of-run sweep is an assembly fixture of THIS suite");
        assembly.GetCustomAttributes<StartOfRunOrderAttribute>().Should().ContainSingle("the order proof runs on every test of THIS suite");
    }
}
