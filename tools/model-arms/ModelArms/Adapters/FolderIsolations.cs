using ModelArms.Core.Experiment;
using ModelArms.Core.Ports;

namespace ModelArms.Adapters;

/// <summary>Which <see cref="IFolderIsolation"/> a host gets (E5.S1 question 9), chosen by the PLATFORM ROAD ALONE: Windows has no POSIX modes (<see cref="NoIsolation"/>, exit 0 — <c>validate</c> refuses the block there, <c>lock</c> says the same without running validate); the kernel road (<c>Kernel</c>/<c>Kernel</c>: Linux x86-64 with <c>statx</c>) takes
/// <see cref="PosixFolderIsolation"/>; every other POSIX road takes <see cref="RuntimeFolderIsolation"/>. The PLAN decides nothing about the adapter: an empty plan is the verb's own answer, and <c>unlock</c> never computes a plan — a standing snapshot is restored whatever <c>experiment.json</c> says now.</summary>
internal static class FolderIsolations
{
    public static IFolderIsolation For(PlatformRoad road, IsolationHands? hands, List<string>? events) =>
        road.Trust == TrustRoad.NotPerformed || OperatingSystem.IsWindows() ? new NoIsolation("isolation by file modes is POSIX only — nothing to lock on Windows")
        : road is { Trust: TrustRoad.Kernel, Bounded: BoundedRoad.Kernel } ? new PosixFolderIsolation(hands, events)
        : new RuntimeFolderIsolation(road, hands, events);
}
