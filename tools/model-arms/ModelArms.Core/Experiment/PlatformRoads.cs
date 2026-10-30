namespace ModelArms.Core.Experiment;

/// <summary>How the trust facts of a root are read: owner, mode and identity by <c>statx</c> (<see cref="Kernel"/>); mode, kind and a witness by the runtime (<see cref="Portable"/>); not at all (<see cref="NotPerformed"/>, Windows, unchanged).</summary>
public enum TrustRoad { Kernel, Portable, NotPerformed }

/// <summary>How a model-written file is read: <c>KernelFacts.ReadSmall</c> (Linux x86-64 with a working <c>statx</c>) or the runtime's open-and-read under a ceiling.</summary>
public enum BoundedRoad { Kernel, Portable }

/// <summary>How a lock holder is believed: <c>/proc</c> field 22 (Linux), <c>Process.StartTime</c> within two seconds (the other POSIX hosts), or never (Windows, unchanged).</summary>
public enum HolderRoad { ProcTicks, ProcessStartTime, None }

/// <summary>What the host said about itself, as plain facts so a test can hand in another host's.</summary>
public sealed record PlatformFacts(bool Windows, bool Linux, bool MacOS, bool FreeBSD, bool X64, bool StatxAvailable, string Description);

/// <summary>The roads one host takes, and what it did NOT verify because of them. <see cref="Line"/> is the one sentence a verb prints about it — empty when nothing was left unverified.</summary>
public sealed record PlatformRoad(string Platform, TrustRoad Trust, BoundedRoad Bounded, HolderRoad Holder, bool LibcMkdir, IReadOnlyList<string> NotVerified)
{
    public string Line =>
        NotVerified.Count == 0 ? string.Empty : $"trust check on {Platform}: NOT verified — {string.Join("; ", NotVerified)} (D17; the roads: E3.S0)";
}

/// <summary>C18, pure: the table of the host's roads (E3.S0). Every host is a row and none is refused — a check the host can perform is performed, one it cannot is named in <see cref="PlatformRoad.Line"/>,
/// and nothing is refused for want of a kernel reader (<c>common.platform-limits</c>: no catch-all that silently inherits another platform's number — a POSIX host not named takes the portable roads and SAYS so).
/// <para>
/// Linux reads <c>statx</c> (one 256-byte struct for every architecture), so its trust road is <see cref="TrustRoad.Kernel"/> wherever <c>statx</c> works; its bounded read needs the open flags probed on x86-64 as well.
/// A host with no probed layout (macOS, FreeBSD, any other) takes the runtime's own facts: <see cref="TrustRoad.Portable"/>.
/// </para></summary>
public static class PlatformRoads
{
    private const string BoundedClause =
        "the bounded read of model-written files is the runtime's: a FIFO or a device is not told from a file (each read runs under a 5 s ceiling), and a link swapped in between the path's check and the open is followed";

    private const string RegularClause = "whether each is a regular file (a FIFO or a device is not told from a file)";

    private const string WitnessClause = "the inode and change time between the trust check and the read (the length and write time were compared instead)";

    private const string HeldFileClause = "the gate lock's held file against its path after the open";

    private const string StartTimeClause = "a lock holder's start is believed within 2 s of its note, not by the kernel's tick";

    public static PlatformRoad Of(PlatformFacts facts) =>
        facts.Windows ? new(facts.Description, TrustRoad.NotPerformed, BoundedRoad.Portable, HolderRoad.None, LibcMkdir: false, [])
        : facts.Linux ? OfLinux(facts)
        : OfOtherPosix(facts);

    private static PlatformRoad OfLinux(PlatformFacts facts) =>
        (facts.StatxAvailable, facts.X64) switch
        {
            (true, true) => new(facts.Description, TrustRoad.Kernel, BoundedRoad.Kernel, HolderRoad.ProcTicks, LibcMkdir: true, []),
            (true, false) => new(facts.Description, TrustRoad.Kernel, BoundedRoad.Portable, HolderRoad.ProcTicks, LibcMkdir: true, [BoundedClause]),
            (false, _) => new(facts.Description, TrustRoad.Portable, BoundedRoad.Portable, HolderRoad.ProcTicks, LibcMkdir: true, [.. TrustClauses(facts.Description), BoundedClause]),
        };

    private static PlatformRoad OfOtherPosix(PlatformFacts facts) =>
        new(facts.Description, TrustRoad.Portable, BoundedRoad.Portable, HolderRoad.ProcessStartTime, LibcMkdir: true, [.. TrustClauses(facts.Description), BoundedClause, StartTimeClause]);

    /// <summary>What the portable trust road leaves unchecked: the owner (its mode IS judged), the kind of file, the inode, and the held lock file's identity — <c>ReadOpen</c> asks <c>statx</c> too.</summary>
    private static IEnumerable<string> TrustClauses(string platform) =>
    [
        $"the owner of the root, experiment.json and scenarios.json (no probed kernel reader on {platform}; their mode WAS judged)",
        RegularClause,
        WitnessClause,
        HeldFileClause,
    ];
}
