namespace Bench.Domain.Trace;

/// <summary>The operating system a run measured on — four values, because one is not enough.
/// <para>
/// <b>The build carries the PATCH.</b> <c>Win32_OperatingSystem.Version</c> stops at <c>10.0.26200</c>; the
/// UBR that moves on Patch Tuesday lives only in the registry, and a version without it cannot tell two runs
/// a month apart — which is precisely the question *"we updated and it got slower"* asks.
/// </para>
/// <para>
/// <b>And the product NAME is never read.</b> Windows reports <c>Windows 10 Pro</c> from a Windows 11
/// machine — Microsoft never updated that registry value — so a run labelled from it names the wrong
/// operating system in every row. <see cref="Release"/> and <see cref="Build"/> are the truth.
/// </para></summary>
/// <param name="Family">The arm vocabulary, shared with <c>ComputeBackend</c>: <c>windows</c>, <c>wsl</c>,
/// <c>linux</c>. One word, so an OS and an arm can be read against each other without a mapping.</param>
/// <param name="Edition">`Professional`, or a distro codename. Empty when unread.</param>
/// <param name="Release">`25H2`, `26.04`. The thing an operator upgrades on purpose.</param>
/// <param name="Build">`10.0.26200.8653`, `6.18.33.2-microsoft-standard-WSL2`. The thing that moves without
/// anybody deciding to.</param>
public sealed record OsFacts(string Family, string Edition, string Release, string Build)
{
    public static OsFacts Unknown { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);

    public string Canonical => $"os={Family}/{Edition}/{Release}/{Build}";

    public string Describe =>
        Family.Length == 0 ? "os unknown" : $"{Family} {Release} {Build}".Trim();
}

/// <summary>The WSL layer, which versions independently of both Windows and the distro.
/// <para>
/// <b><see cref="Direct3D"/> and <see cref="DxCore"/> are not decoration.</b> They are the GPU passthrough
/// shims, delivered by the Windows driver package into <c>/usr/lib/wsl/lib</c>, and they are the layer the
/// 155-second boundary finding lives in (<c>dew_flow_rag_qln · research/GPU_BACKEND_WSL_VS_WINDOWS.md</c>
/// §10). A WSL arm whose D3D shim changed is not the same arm, and no other field would show it.
/// </para>
/// <para>
/// WSLg and MSRDC come free from the same call and are deliberately NOT carried: they serve the graphical
/// session, no question here touches one, and a field nobody reads is a column that outlives its reason.
/// </para></summary>
public sealed record WslFacts(string Runtime, string Direct3D, string DxCore)
{
    /// <summary>Not under WSL, or a machine that could not be asked. Both are "no WSL layer here", and the
    /// difference is carried by <see cref="OsFacts.Family"/> rather than duplicated.</summary>
    public static WslFacts None { get; } = new(string.Empty, string.Empty, string.Empty);

    public string Canonical => $"wsl={Runtime}/{Direct3D}/{DxCore}";

    public string Describe =>
        Runtime.Length == 0 ? string.Empty : $"wsl {Runtime} · d3d {Direct3D} · dxcore {DxCore}";
}

/// <summary>One display adapter, its driver, and whether it is actually on the bus.
/// <para>
/// <b><see cref="HealthCode"/> is the field this record exists for.</b> Windows reports
/// <c>ConfigManagerErrorCode = 31</c> for a card that has fallen off the bus: still enumerated, everything
/// silently on the CPU, and the only symptom unexplained slowness. A campaign run in that state produces real
/// numbers that are CPU numbers wearing a GPU's label, and nothing else recorded here would say so.
/// </para></summary>
/// <param name="VramBytes">Read from the display-class registry key, never from WMI's <c>AdapterRAM</c> —
/// that one is a uint32 and saturates at 4 GiB, so a 32 GB card and a 4 GB integrated one report the same
/// number exactly where an operator is choosing between them.</param>
/// <param name="DriverVersion">Per ADAPTER rather than per machine, even when one driver serves several —
/// which is the case on this hardware. Under WSL this Windows driver IS the driver, materialised as the
/// shims in <see cref="WslFacts"/>.</param>
public sealed record AdapterFacts(
    string Name,
    long VramBytes,
    string DriverVersion,
    string DriverDate,
    int HealthCode)
{
    /// <summary>Windows uses 0 for "working properly". Any other value is a device the operator has to know
    /// about before reading a single number off this run.</summary>
    public bool IsHealthy => HealthCode == 0;

    public string Canonical => $"gpu={Name}/{VramBytes}/{DriverVersion}/{DriverDate}/{HealthCode}";

    public string Describe =>
        $"{Name} · {VramBytes / (1024d * 1024 * 1024):0.#} GiB · driver {DriverVersion}"
        + (IsHealthy ? string.Empty : $" · UNHEALTHY (code {HealthCode})");
}

/// <param name="PowerPlan">Windows power scheme or the Linux CPU governor. `Balanced` against
/// `High performance` is a classic silent confound, and it is the denominator the CPU control arms —
/// <c>windows/cpu/—</c> against <c>wsl/cpu/—</c> — are read against.</param>
public sealed record CpuFacts(string Model, int PhysicalCores, int LogicalCores, string PowerPlan)
{
    public static CpuFacts Unknown { get; } = new(string.Empty, 0, 0, string.Empty);

    public string Canonical => $"cpu={Model}/{PhysicalCores}/{LogicalCores}/{PowerPlan}";

    public string Describe =>
        Model.Length == 0 ? "cpu unknown" : $"{Model} · {PhysicalCores}c/{LogicalCores}t · {PowerPlan}";
}

/// <summary>The volume a run's checkout and corpus live on.
/// <para>
/// <b>The filesystem matters as much as the cluster size.</b> A path on DrvFs (<c>/mnt/d</c>) and one on ext4
/// are the same disk through two filesystems, which is the 155-second finding's cousin — and
/// <see cref="FreeBytes"/> is here because a disk that fills reports "no space" during an unrelated run
/// (<c>todo/PLAN_corpus_litter.md</c>: 24.38 GB of which 22 GB was leaked corpora).
/// </para></summary>
/// <param name="FreeBytes">Deliberately NOT part of the fingerprint — it changes by the minute, and a
/// fingerprint that moved with it would make every run its own machine.</param>
public sealed record VolumeFacts(
    string Path,
    string FileSystem,
    int ClusterBytes,
    long FreeBytes,
    long TotalBytes)
{
    public static VolumeFacts Unknown { get; } = new(string.Empty, string.Empty, 0, 0, 0);

    /// <summary>Without <see cref="FreeBytes"/>. See the parameter's own note.</summary>
    public string Canonical => $"vol={Path}/{FileSystem}/{ClusterBytes}/{TotalBytes}";

    public string Describe =>
        Path.Length == 0
            ? "volume unknown"
            : $"{Path} · {FileSystem} · {ClusterBytes} B clusters · {FreeBytes / (1024d * 1024 * 1024):0.#} GiB free";
}

/// <summary>The machine a run measured on, read once at its start.
/// <para>
/// <b>It is a FINGERPRINT, not a gate.</b> Two runs on different drivers are not refused — hardware changes,
/// and refusing would make a benchmark unable to span a driver update — but a report that puts them side by
/// side says so. Three states, the shape <c>IndexCommit</c> already uses: same machine · different machine ·
/// not recorded.
/// </para>
/// <para>
/// Nothing in this system records which machine produced a row today, so two machines' results merge
/// silently. That is the gap this closes, and it is why <see cref="Hostname"/> and <see cref="MachineId"/>
/// are here beside the versions.
/// </para></summary>
public sealed record MachineFacts
{
    /// <summary>No probe ran. Distinct from a probe that ran and could read nothing: this one says the
    /// question was never asked, which is what every run stored before this existed is.</summary>
    public static MachineFacts NotRecorded { get; } = new();

    public string Hostname { get; init; } = string.Empty;

    /// <summary>A stable per-machine id, so two hosts that happen to share a name are still two machines.</summary>
    public string MachineId { get; init; } = string.Empty;

    public OsFacts Os { get; init; } = OsFacts.Unknown;

    public WslFacts Wsl { get; init; } = WslFacts.None;

    public CpuFacts Cpu { get; init; } = CpuFacts.Unknown;

    public long TotalRamBytes { get; init; }

    public IReadOnlyList<AdapterFacts> Adapters { get; init; } = [];

    public VolumeFacts Volume { get; init; } = VolumeFacts.Unknown;

    /// <summary>Whether anything was read at all.</summary>
    public bool Recorded => Hostname.Length > 0 || Os.Family.Length > 0;

    /// <summary>Every adapter that is not working properly. Empty is the ordinary case and the one an
    /// operator never has to think about; non-empty means the numbers in this run need a sentence.</summary>
    public IReadOnlyList<AdapterFacts> UnhealthyAdapters => [.. Adapters.Where(a => !a.IsHealthy)];

    /// <summary>What two runs are compared by.
    /// <para>
    /// Over the STABLE identity only: the free space on a volume changes by the minute, and folding it in
    /// would give every run its own fingerprint and make the comparison useless — the failure this property
    /// exists to enable would be the first casualty of writing it carelessly.
    /// </para></summary>
    public string Canonical => string.Join(
        '|',
        [
            $"host={Hostname}/{MachineId}",
            Os.Canonical,
            Wsl.Canonical,
            Cpu.Canonical,
            $"ram={TotalRamBytes}",
            .. Adapters.Select(a => a.Canonical),
            Volume.Canonical,
        ]);

    public string Fingerprint => Recorded ? StableHash.Of(Canonical) : string.Empty;
}
