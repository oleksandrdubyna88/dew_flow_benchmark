using System.Diagnostics;
using System.Reflection;
using Xunit;
using Xunit.v3;

[assembly: Bench.Tests.Leftovers.StartOfRunOrder]

namespace Bench.Tests.Leftovers;

// Source: dew_flow_connect_other_ais, src_mcp/tests_leftovers/StartOfRunOrderAttribute.cs at e060d77e (E5 S2-B; the assembly-level declaration is the form of src_mcp/tests/RunTempStart.cs). Changed here (E6 S2): the
// attribute first waits on the start-of-run gate, and it never throws: a violation is recorded and a test asserts there is none. This is benchmark's own file: it is not a manifest target.

/// <summary>
/// The proof that the start-of-run sweep ended before ANY test began (E6 S2). It is an ASSEMBLY-level before/after attribute, so xUnit runs its <see cref="Before"/> for every test of every collection of the
/// assembly, on every run. <see cref="Before"/> FIRST waits on the gate of <see cref="StartOfRunSweep"/>, so no test body on any thread starts before the sweep ended, even if a future xUnit builds the
/// assembly fixture later. It then checks the order, and it NEVER throws: the sweep never fails the run, and a throw here would fail a test. A violation is recorded in
/// <see cref="StartOfRunSweep.OrderViolations"/>, and <c>StartOfRunSweepTests</c> asserts that list is empty.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class StartOfRunOrderAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        StartOfRunSweep.Run();
        var now = Stopwatch.GetTimestamp();
        var ended = StartOfRunSweep.EndedAt;
        if (ended == 0 || ended > now)
        {
            StartOfRunSweep.RecordOrderViolation(
                $"{test.TestDisplayName} started before the start-of-run sweep ended: every test must wait on StartOfRunSweep.Run() first; " +
                "check that [assembly: Bench.Tests.Leftovers.StartOfRunOrder] is declared and that no fixture builds a test before the gate (E6 S2)");
        }

        StartOfRunSweep.RecordTestStart(now);
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
    }
}
