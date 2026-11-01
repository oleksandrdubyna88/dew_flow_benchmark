using ModelArms.Adapters;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>A hook that stands in for a process that dies half-way: it throws, and the test sees where the lock stopped.</summary>
internal sealed class TestCrash : Exception;


/// <summary>What the isolation test classes share (one place, so a class can stay under the file limit): the modes of the eight entries before and after a lock, the kit on the kernel road, a hook that dies half-way, a stand-in for the chmod and the line a failure prints.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class IsolationSupport
{
    internal static readonly string[] Original = ["755", "700", "755", "755", "755", "755", "755", "755"];
    internal static readonly string[] Locked = ["0", "300", "300", "300", "300", "300", "300", "300"];

    internal static IsolationKit Kit()
    {
        var kit = new IsolationKit();
        var road = PlatformFactsReader.CurrentRoad;
        Assert.SkipUnless(road is { Trust: TrustRoad.Kernel, Bounded: BoundedRoad.Kernel }, $"the kernel road is Linux x86-64 with statx; this host's road is: {road.Platform}");
        return kit;
    }

    internal static IsolationHands Crash(int after) => new(null, null, (index, _) => ThrowAfter(index, after), null);

    internal static void ThrowAfter(int index, int after)
    {
        if (index == after)
        {
            throw new TestCrash();
        }
    }

    internal static int Chmod(string path, int mode)
    {
        File.SetUnixFileMode(path, (UnixFileMode)mode);
        return 0;
    }

    internal static string Ctx(IsolationRun run) => $"stdout: {run.Output} stderr: {run.Error}";

}
