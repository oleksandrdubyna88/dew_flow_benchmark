using System.Collections.Concurrent;
using System.Diagnostics;

namespace Bench.Tests.Leftovers;

// Source: dew_flow_connect_other_ais, src_mcp/tests_leftovers/StartOfRunSweep.cs at e060d77e (E5 S2-B): the ledger (its end stamp), RecordTestStart and the sweep call. Changed here (E6 S2, plan D5,
// brief B5): coai's list-only switch (its D11) is NOT carried, so the sweep collects (rule §4); the once-only Gate is new. This is benchmark's own file: it is not a manifest target.

/// <summary>
/// The start-of-run sweep of this test project (E6 S2): the copied <see cref="LeftoversSweep"/> over the MACHINE's temp root, behind a gate that runs it ONCE per process and that every thread waits for,
/// with the end of the sweep stamped so a test can prove it ended before the first test began.
/// </summary>
/// <remarks>
/// <para>The copied <see cref="LeftoversSweepFixture"/> calls <see cref="Run"/> from its constructor (manifest operation on <c>RealSweep</c>). xUnit v3 builds every assembly fixture before it starts any test
/// collection, so the sweep ends first; <see cref="StartOfRunOrderAttribute"/> calls <see cref="Run"/> too, on every test of every run, so even a future xUnit that builds the fixture later cannot start a test
/// before the sweep ended: that call waits on the gate.</para>
/// <para>The gate NEVER throws. A <see cref="Lazy{T}"/> caches an exception and would then throw on every test's <c>Before</c>, which would fail the run (rule §4: a sweep never fails the run). The exception becomes
/// one <see cref="SweepResult.Errors"/> line, and the fixture prints it as <c>error: sweep failed: …</c>.</para>
/// </remarks>
public static class StartOfRunSweep
{
    private static readonly Gate Process = new(RealSweep);
    private static readonly ConcurrentQueue<string> Violations = new();
    private static long _firstTestStart;

    /// <summary>
    /// The once-only gate. A seam: a test makes its own gate over its own sweep, so it can never move the end or the counter the order proof and the real-run test read. <see cref="Await"/> blocks every caller,
    /// on any thread, until the one sweep ended, and gives all of them the SAME result object.
    /// </summary>
    internal sealed class Gate
    {
        private readonly Lazy<SweepResult> _once;
        private long _endedAt;
        private int _runs;

        internal Gate(Func<SweepResult> sweep)
        {
            _once = new Lazy<SweepResult>(() => Once(sweep), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>The first caller runs the sweep; every other caller waits for it. Never throws, and <see cref="EndedAt"/> is set when this returns, on every path.</summary>
        internal SweepResult Await() => _once.Value;

        /// <summary><see cref="Stopwatch.GetTimestamp"/> taken when the sweep ended (a result, or the error line of an exception); 0 until then.</summary>
        internal long EndedAt => Volatile.Read(ref _endedAt);

        /// <summary>How many times the sweep was started: 1 once the gate was used, never more.</summary>
        internal int Runs => Volatile.Read(ref _runs);

        private SweepResult Once(Func<SweepResult> run)
        {
            Interlocked.Increment(ref _runs);
            try
            {
                return run();
            }
            catch (Exception ex)
            {
                return new SweepResult([], [], [], [$"sweep failed: {FirstLine(ex.Message)}"]);
            }
            finally
            {
                Volatile.Write(ref _endedAt, Stopwatch.GetTimestamp());
            }
        }
    }

    /// <summary>The real entry: the sweep of this process, run once. Every caller gets the same result, after it ended.</summary>
    public static SweepResult Run() => Process.Await();

    /// <summary><see cref="Stopwatch.GetTimestamp"/> taken when the sweep of this process ended; 0 until then. Read by the order proof.</summary>
    public static long EndedAt => Process.EndedAt;

    /// <summary>How many times the sweep of this process started: 1 in a real run, whatever the number of threads.</summary>
    public static int Runs => Process.Runs;

    /// <summary>The earliest <c>Before</c> timestamp of any test of this process (an interlocked minimum); 0 until the first test.</summary>
    public static long FirstTestStart => Volatile.Read(ref _firstTestStart);

    /// <summary>
    /// One line per test whose <c>Before</c> found the sweep not ended: <see cref="StartOfRunOrderAttribute"/> NEVER throws (a throw would fail that test and the sweep must never fail the run), so the order
    /// proof is a test (<c>StartOfRunSweepTests</c>) that asserts this list is empty.
    /// </summary>
    public static IReadOnlyList<string> OrderViolations => [.. Violations];

    internal static void RecordOrderViolation(string line) => Violations.Enqueue(line);

    /// <summary>Notes the start of a test: keeps the earliest timestamp (0 means none yet).</summary>
    internal static void RecordTestStart(long timestamp)
    {
        long seen;
        do
        {
            seen = Volatile.Read(ref _firstTestStart);
            if (seen != 0 && seen <= timestamp)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _firstTestStart, timestamp, seen) != seen);
    }

    private static string FirstLine(string message) => message.Split('\n')[0].TrimEnd('\r');

    /// <summary>
    /// The template's sweep call over the REAL temp root (<see cref="Owned.TempRoot"/>, the helper's one door), behind the macOS layout requirement: a requirement that throws means no sweep runs, and the
    /// gate turns the exception into one error line.
    /// </summary>
    private static SweepResult RealSweep() =>
        LeftoversSweepFixture.GuardedSweep(DarwinLayout.Require, () =>
            LeftoversSweep.Run(new SweepOptions(Owned.TempRoot(), DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess, Scope.IsMine)));
}
