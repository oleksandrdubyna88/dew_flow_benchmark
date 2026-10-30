using ModelArms.Core.Experiment;

namespace Bench.Tests.ModelArms;

/// <summary>The roads of hosts this suite does not run on, handed in through the seams (E3.S0): every test that uses one says so in its header, because a Mac's road composed on a real Mac is unobserved (E3 §8 item 25).</summary>
internal static class PlatformKit
{
    /// <summary>macOS 15 on arm64: the portable roads everywhere, the holder believed by its start second.</summary>
    public static PlatformRoad MacRoad { get; } = PlatformRoads.Of(new PlatformFacts(false, false, true, false, false, false, "macOS 15"));

    /// <summary>Linux x86-64 with a working <c>statx</c>: the road this suite's host takes.</summary>
    public static PlatformRoad LinuxRoad { get; } = PlatformRoads.Of(new PlatformFacts(false, true, false, false, true, true, "Linux test host"));
}
