using System.Runtime.InteropServices;

namespace ModelArms.Adapters;

/// <summary>How a detached program can be started on this host (D13): <see cref="Setsid"/> — <c>posix_spawn</c> of the resolved <c>setsid</c> with the constants measured on Linux x86-64 with glibc (<see cref="PosixSpawn"/>); <see cref="Windows"/> — <c>Process.Start</c>, built and NOT observed (no Windows host here); <see cref="Unmeasured"/> — every other host, refused with the manual road.</summary>
internal enum DetachedRoad { Setsid, Windows, Unmeasured }

/// <summary>The explicit host → road table (<c>common.platform-limits</c>: a target → measured value mapping with NO catch-all; an unmeasured host fails closed, not inherits). The key is the operating system, the CPU AND the C library: this runtime's identifier is the distribution's (<c>ubuntu.26.04-x64</c> here, measured), so it cannot say glibc from musl; the C library
/// is asked directly — <c>gnu_get_libc_version</c> exists in glibc alone (<see cref="PosixSpawn.GlibcVersion"/>; empty on musl). Only Linux x86-64 with glibc 2.34 or later is measured (and <c>posix_spawn_file_actions_addclosefrom_np</c>, which arrived in 2.34, must be present: without it the runner would inherit every descriptor that is not close-on-exec). Linux arm64's <c>O_NOFOLLOW</c> is <c>0x8000</c>, not <c>0x20000</c> (the
/// <see cref="KernelFacts"/> precedent); musl, macOS and FreeBSD were not measured, and the last two ship no util-linux <c>setsid</c>. None of them starts an arm — <c>run-all</c> prints how to start one by hand (exit 3), in a sentence that names what was measured.</summary>
internal static class DetachedRoads
{
    /// <summary>The words every refusal of the road carries: what <c>Setsid</c> was measured on, so the operator reads why this host is not it.</summary>
    public const string Measured = "measured on Linux x86-64 with glibc 2.34 or later (posix_spawn_file_actions_addclosefrom_np present) and built for Windows only";

    private const int GlibcMajor = 2;
    private const int GlibcMinor = 34;

    public static DetachedRoad Current
    {
        get
        {
            var road = For(OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), RuntimeInformation.OSArchitecture, PosixSpawn.GlibcVersion);
            return road == DetachedRoad.Setsid && !PosixSpawn.CloseFromAvailable ? DetachedRoad.Unmeasured : road;
        }
    }

    /// <summary>The table, with the host handed in so a test reads every row on any host. <paramref name="glibc"/> is glibc's version text (<c>2.43</c>), empty when the C library is not glibc.</summary>
    public static DetachedRoad For(bool windows, bool linux, Architecture architecture, string glibc) =>
        (windows, linux, architecture, AtLeast(glibc)) switch
        {
            (false, true, Architecture.X64, true) => DetachedRoad.Setsid,
            (true, false, _, _) => DetachedRoad.Windows,
            _ => DetachedRoad.Unmeasured,
        };

    private static bool AtLeast(string glibc) =>
        glibc.Split('.') is [var major, var minor, ..] && int.TryParse(major, out var first) && int.TryParse(minor, out var second) && (first, second).CompareTo((GlibcMajor, GlibcMinor)) >= 0;
}
