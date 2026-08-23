using System.Runtime.InteropServices;
using Bench.Application;
using Bench.Domain.Trace;
using Bench.Infrastructure.Process;
using Microsoft.Extensions.Logging;

namespace Bench.Infrastructure.Machine;

/// <summary>Reads this machine, once, at a run's start.
/// <para>
/// <b>Nothing here may fail a run.</b> Every read is wrapped and every failure becomes an empty field, because
/// the alternative is a benchmark that refuses to measure on a machine whose registry it could not parse.
/// <c>MachineFacts.NotRecorded</c> is a legitimate answer and the report knows how to print it.
/// </para>
/// <para>
/// <b>The Windows GPU read is PORTED, not invented</b> — mechanism, not repository, from
/// <c>dew_flow_rag_qln · src/Rag.Infrastructure/Gpu/GpuProbe.cs</c>, which learned the two things this script
/// depends on: WMI's <c>AdapterRAM</c> is a uint32 and saturates at 4 GiB, so the true size comes from the
/// display-class registry key and is matched back by driver description; and <c>ConfigManagerErrorCode</c> is
/// the field that exposes a card which has fallen off the bus. It is copied rather than referenced because a
/// benchmark that depended on one engine's repository for its own machine facts could not measure any other
/// engine — the reuse question is argued in <c>research/PLAN_hardware_sampler.md</c> §7.
/// </para></summary>
public sealed class MachineProbe(ILogger<MachineProbe> logger) : IMachineProbe
{
    /// <summary>Long enough for WMI on a cold machine, short enough that a run's start is never held hostage
    /// by a vendor tool that stopped answering. The same ceiling <c>GpuProbe</c> settled on.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public async Task<MachineFacts> ReadAsync(string volumePath, CancellationToken cancellationToken)
    {
        try
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? await WindowsAsync(volumePath, cancellationToken)
                : await LinuxAsync(volumePath, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberately broad, and deliberately not rethrown: a machine this cannot read is a machine
            // whose facts are unknown, which the shape can express. Losing a campaign to a registry quirk
            // would be a far worse trade than a run whose report says "machine not recorded".
            logger.LogWarning(ex, "The machine could not be read; the run records no facts about it");
            return MachineFacts.NotRecorded;
        }
    }

    private async Task<MachineFacts> WindowsAsync(string volumePath, CancellationToken cancellationToken)
    {
        var os = await PowerShellAsync(WindowsOsScript, cancellationToken);
        var wsl = await CaptureAsync("wsl.exe", ["--version"], cancellationToken);

        return new MachineFacts
        {
            Hostname = Environment.MachineName,
            MachineId = Field(os, "machineid"),
            Os = new OsFacts("windows", Field(os, "edition"), Field(os, "release"), Field(os, "build")),
            Wsl = MachineText.Wsl(wsl),
            Cpu = new CpuFacts(
                Field(os, "cpu"),
                Number(Field(os, "cores")),
                Number(Field(os, "threads")),
                Field(os, "power")),
            TotalRamBytes = Bytes(Field(os, "ram")),
            Adapters = [.. Adapters(os)],
            Volume = Volume(volumePath, Field(os, "cluster")),
        };
    }

    private async Task<MachineFacts> LinuxAsync(string volumePath, CancellationToken cancellationToken)
    {
        var release = await ReadFileAsync("/etc/os-release", cancellationToken);
        var (distro, codename) = MachineText.OsRelease(release);
        var kernel = (await ReadFileAsync("/proc/sys/kernel/osrelease", cancellationToken)).Trim();
        var (model, logical) = MachineText.CpuInfo(await ReadFileAsync("/proc/cpuinfo", cancellationToken));

        return new MachineFacts
        {
            Hostname = Environment.MachineName,
            MachineId = (await ReadFileAsync("/etc/machine-id", cancellationToken)).Trim(),
            // The family is the arm vocabulary, so an OS and a compute arm can be read against each other
            // without a mapping: a kernel Microsoft stamped is `wsl`, anything else is `linux`.
            Os = new OsFacts(
                kernel.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ? "wsl" : "linux",
                codename,
                distro,
                kernel),
            // No WSL layer read from inside the distro: wsl.exe lives on the Windows side, and the shim
            // versions it reports are a fact about the host. A daemon in here says nothing rather than
            // guessing at numbers it cannot see.
            Wsl = WslFacts.None,
            Cpu = new CpuFacts(model, 0, logical, await GovernorAsync(cancellationToken)),
            TotalRamBytes = MachineText.TotalMemoryBytes(await ReadFileAsync("/proc/meminfo", cancellationToken)),
            Adapters = [],
            Volume = Volume(volumePath, string.Empty),
        };
    }

    /// <summary>The volume a run's checkout lives on. Cluster size arrives from the caller because only
    /// Windows states it plainly; on Linux it is the filesystem's block size and needs its own probe, which is
    /// left unread rather than filled with a plausible 4096.</summary>
    private VolumeFacts Volume(string path, string cluster)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));

            if (string.IsNullOrEmpty(root))
            {
                return VolumeFacts.Unknown;
            }

            var drive = new DriveInfo(root);

            return new VolumeFacts(root, drive.DriveFormat, Number(cluster), drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The volume behind {Path} could not be read", path);
            return VolumeFacts.Unknown;
        }
    }

    private static IEnumerable<AdapterFacts> Adapters(IReadOnlyDictionary<string, string> os) =>
        Field(os, "gpus")
            .Split('~', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split('|'))
            .Where(parts => parts.Length == 5)
            .Select(parts => new AdapterFacts(parts[0], Bytes(parts[1]), parts[2], parts[3], Number(parts[4])));

    private async Task<string> GovernorAsync(CancellationToken cancellationToken)
    {
        // Absent under WSL — the VM has no cpufreq — which is an honest unknown rather than a default. A
        // governor invented here would be a claim about how the CPU was scheduled during a measurement.
        var governor = await ReadFileAsync("/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor", cancellationToken);

        return governor.Trim();
    }

    private async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "{Path} could not be read", path);
            return string.Empty;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> PowerShellAsync(string script, CancellationToken cancellationToken)
    {
        var output = await CaptureAsync(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script],
            cancellationToken);

        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string> CaptureAsync(string exe, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var attempt = await ProcessRunner.RunAsync(exe, arguments, Environment.CurrentDirectory, Timeout, cancellationToken);

        return attempt is ProcessAttempt.Completed { Result.Ok: true } done ? done.Result.StandardOutput : string.Empty;
    }

    private static string Field(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.GetValueOrDefault(key, string.Empty);

    private static int Number(string value) => int.TryParse(value, out var parsed) ? parsed : 0;

    private static long Bytes(string value) => long.TryParse(value, out var parsed) ? parsed : 0;

    /// <summary>One PowerShell round trip for everything Windows will only say through WMI or the registry.
    /// <para>
    /// One launch rather than six: each is ~200 ms of process start, and this runs before a campaign that may
    /// then take hours — but it also runs in tests and on an operator's machine while they wait.
    /// </para>
    /// <para>
    /// <b><c>ProductName</c> is deliberately not read.</b> The registry reports <c>Windows 10 Pro</c> from a
    /// Windows 11 machine — Microsoft never updated that value — so a run labelled from it would name the
    /// wrong operating system in every row. <c>DisplayVersion</c> and the build carry the truth, and the
    /// <c>UBR</c> is the patch, which <c>Win32_OperatingSystem.Version</c> does not include at all.
    /// </para></summary>
    private const string WindowsOsScript = """
        $ErrorActionPreference = 'SilentlyContinue'
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
        "release=$($cv.DisplayVersion)"
        "edition=$($cv.EditionID)"
        "build=10.0.$($cv.CurrentBuild).$($cv.UBR)"
        "machineid=$((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Cryptography').MachineGuid)"
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        "cpu=$($cpu.Name)"
        "cores=$($cpu.NumberOfCores)"
        "threads=$($cpu.NumberOfLogicalProcessors)"
        "ram=$((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory)"
        "power=$((powercfg /getactivescheme) -replace '.*\(', '' -replace '\)', '')"
        $vram = @{}
        Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}' |
          ForEach-Object {
            $p = Get-ItemProperty $_.PSPath
            if ($p.'HardwareInformation.qwMemorySize' -and $p.DriverDesc) { $vram[$p.DriverDesc] = $p.'HardwareInformation.qwMemorySize' }
          }
        $gpus = Get-CimInstance Win32_VideoController | ForEach-Object {
          $size = $vram[$_.Name]
          if (-not $size) { $size = 0 }
          $date = if ($_.DriverDate) { (Get-Date $_.DriverDate -Format 'yyyy-MM-dd') } else { '' }
          "$($_.Name)|$size|$($_.DriverVersion)|$date|$([int]$_.ConfigManagerErrorCode)"
        }
        "gpus=$($gpus -join '~')"
        "cluster=$((Get-CimInstance -ClassName Win32_Volume | Where-Object { $_.DriveLetter -eq (Get-Location).Drive.Name + ':' } | Select-Object -First 1).BlockSize)"
        """;
}
