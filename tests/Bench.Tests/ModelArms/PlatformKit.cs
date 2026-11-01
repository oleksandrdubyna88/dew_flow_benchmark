using System.Runtime.InteropServices;
using ModelArms.Core.Experiment;
using ModelArmsApp = ModelArms;
using RootFacts = ModelArms.Adapters.RootFacts;

namespace Bench.Tests.ModelArms;

/// <summary>The roads of hosts this suite does not run on, handed in through the seams (E3.S0): every test that uses one says so in its header, because a Mac's road composed on a real Mac is unobserved (E3 §8 item 25).</summary>
internal static class PlatformKit
{
    /// <summary>macOS 15 on arm64: the portable roads everywhere, the holder believed by its start second.</summary>
    public static PlatformRoad MacRoad { get; } = PlatformRoads.Of(new PlatformFacts(false, false, true, false, false, false, "macOS 15"));

    /// <summary>Windows: no POSIX modes, so no folder isolation (the trust check is not performed).</summary>
    public static PlatformRoad WindowsRoad { get; } = PlatformRoads.Of(new PlatformFacts(true, false, false, false, true, false, "Windows test host"));

    /// <summary>Linux x86-64 with a working <c>statx</c>: the road this suite's host takes.</summary>

    public static PlatformRoad LinuxRoad { get; } = PlatformRoads.Of(new PlatformFacts(false, true, false, false, true, true, "Linux test host"));

    /// <summary>The gate's seams with the Mac's road handed in (<paramref name="from"/> or the real ones, edited): the portable roads everywhere, and the facts read by that road. Shared by the two classes that drive the gate on the portable road.</summary>
    public static ModelArmsApp.GateSeams MacSeams(ModelArmsApp.GateSeams? from = null) =>
        (from ?? ModelArmsApp.GateSeams.Real) with { Road = MacRoad, ReadFacts = (path, kind) => RootFacts.ReadFacts(path, kind, MacRoad) };

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int fd);

    /// <summary>Frees a thread blocked in a FIFO's open (a Portable bounded read that its ceiling gave up on): <c>open(O_RDWR | O_NONBLOCK)</c> answers at once whether or not anyone is waiting (the runtime's own <c>FileStream</c> on a FIFO may block), and the close ends it. Linux, where the asm-generic flags are 2 and 0x800.</summary>
    public static void ReleaseFifo(string fifo)
    {
        var fd = Open(fifo, 0x802);
        if (fd >= 0)
        {
            Close(fd);
        }
    }
}
