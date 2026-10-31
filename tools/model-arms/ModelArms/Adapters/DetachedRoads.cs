using System.Runtime.InteropServices;

namespace ModelArms.Adapters;

/// <summary>How a detached program can be started on this host (D13): <see cref="Setsid"/> — <c>posix_spawn</c> of the resolved <c>setsid</c> with the constants measured on Linux x86-64 (<see cref="PosixSpawn"/>); <see cref="Windows"/> — <c>Process.Start</c>, built and NOT observed (no Windows host here); <see cref="Unmeasured"/> — every other host, refused with the manual road.</summary>
internal enum DetachedRoad { Setsid, Windows, Unmeasured }

/// <summary>The explicit host → road table (<c>common.platform-limits</c>: a target → measured value mapping with NO catch-all; an unmeasured host fails closed, not inherits). Linux arm64's <c>O_NOFOLLOW</c> is <c>0x8000</c>, not <c>0x20000</c> (the <see cref="KernelFacts"/> precedent), and macOS and FreeBSD ship no util-linux <c>setsid</c>:
/// none of them is measured, so none starts an arm — <c>run-all</c> prints how to start one by hand (exit 3).</summary>
internal static class DetachedRoads
{
    public static DetachedRoad Current => For(OperatingSystem.IsLinux(), OperatingSystem.IsWindows(), RuntimeInformation.OSArchitecture);

    /// <summary>The table, with the host handed in so a test reads every row on any host.</summary>
    public static DetachedRoad For(bool linux, bool windows, Architecture architecture) =>
        (linux, windows, architecture) switch
        {
            (true, false, Architecture.X64) => DetachedRoad.Setsid,
            (false, true, _) => DetachedRoad.Windows,
            _ => DetachedRoad.Unmeasured,
        };
}
