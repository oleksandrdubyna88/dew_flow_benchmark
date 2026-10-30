using System.Runtime.InteropServices;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What THIS host is, as the plain facts <see cref="PlatformRoads.Of"/> decides over (E3.S0): the operating system by <c>OperatingSystem.Is*</c>, the architecture the process runs as, whether <c>statx</c> answers
/// (<see cref="KernelFacts.StatxProbe"/>) and the runtime's description. The decision is Core's and pure; this only reports. The road is computed ONCE per process and handed to every adapter as a parameter — an adapter reads
/// <see cref="CurrentRoad"/> only where it is the verb's default, never inside a decision, so a test hands another host's road on this one.</summary>
internal static class PlatformFactsReader
{
    /// <summary>The host's roads, computed once.</summary>
    public static PlatformRoad CurrentRoad => Road.Value;

    private static readonly Lazy<PlatformRoad> Road = new(() => PlatformRoads.Of(Current()));

    /// <summary>The facts of this host. When <c>statx</c> does not answer on Linux, the words of the failure are kept in the description — they are what the road's printed line shows.</summary>
    public static PlatformFacts Current()
    {
        var statx = KernelFacts.StatxProbe();
        var linux = OperatingSystem.IsLinux();
        var words = linux ? statx.Match(_ => string.Empty, why => $" (statx unavailable: {why})") : string.Empty;
        return new PlatformFacts(
            OperatingSystem.IsWindows(),
            linux,
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsFreeBSD(),
            RuntimeInformation.ProcessArchitecture == Architecture.X64,
            statx.Match(available => available, _ => false),
            RuntimeInformation.OSDescription + words);
    }
}
