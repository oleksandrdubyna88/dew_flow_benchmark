using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Reading a machine's own reports.
/// <para>
/// Pure, and separate from whatever launched the process, for the reason every codec here is: these are the
/// parts with judgement in them — which line is the version, what a missing field means — and none of them
/// needs an operating system to be checked. Each parser below is fed real captured output in the tests, not
/// an idealised shape.
/// </para></summary>
public static class MachineText
{
    /// <summary>The WSL stack out of <c>wsl.exe --version</c>.
    /// <para>
    /// <b>The input arrives UTF-16.</b> <c>wsl.exe</c> writes its version block as UTF-16LE, so a reader that
    /// decoded it as UTF-8 hands this method <c>W\0S\0L\0</c> — every character followed by a NUL. Verified
    /// on this machine 2026-08-19: the first bytes are <c>87,0,83,0,76,0</c>. The proper fix is at the read,
    /// and it is done there too; the NUL strip stays here because whether the mangling survives depends on
    /// who did the reading, and a parser correct for both is cheaper than a rule every caller must remember.
    /// </para>
    /// <para>
    /// Labels are matched case-insensitively on their prefix rather than by exact string, because this output
    /// is localised: a German host prints <c>WSL-Version</c>. Matching the ASCII stem keeps the parse working
    /// where an equality check would silently return nothing.
    /// </para></summary>
    public static WslFacts Wsl(string output)
    {
        var lines = Lines(output);

        return lines.Count == 0
            ? WslFacts.None
            : new WslFacts(
                Value(lines, "WSL"),
                Value(lines, "Direct3D"),
                Value(lines, "DXCore"));
    }

    /// <summary>Distro release and codename out of <c>/etc/os-release</c>.
    /// <para>
    /// <c>VERSION_ID</c> rather than <c>PRETTY_NAME</c>: the pretty name is prose that has changed format
    /// between releases, while the id is the value the distro promises to keep parseable. Values are quoted
    /// in that file and the quotes are not part of them.
    /// </para></summary>
    public static (string Release, string Codename) OsRelease(string content)
    {
        var fields = Lines(content)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim().Trim('"'), StringComparer.Ordinal);

        return (fields.GetValueOrDefault("VERSION_ID", string.Empty),
                fields.GetValueOrDefault("VERSION_CODENAME", string.Empty));
    }

    /// <summary>Total memory out of <c>/proc/meminfo</c>, in BYTES.
    /// <para>
    /// <b>Under WSL this is the VM's allocation, not the machine's.</b> Measured here: the distro reports
    /// 47 066 772 kB where the host has 96 GB. Both numbers are true and they answer different questions —
    /// the one that matters for a leg is what the process could actually allocate, which is this one. A
    /// reader that treated it as the machine's RAM would report a 96 GB box as a 45 GB box, or worse, would
    /// call the same machine two different sizes depending on which side the daemon ran.
    /// </para>
    /// <para>
    /// The file states its own unit and it is always <c>kB</c> — meaning KiB, which is the kernel's usage
    /// rather than the SI one. Multiplying by 1024 is correct and by 1000 is a 2.4 % error nobody would
    /// notice.
    /// </para></summary>
    public static long TotalMemoryBytes(string meminfo) =>
        Lines(meminfo)
            .Where(line => line.StartsWith("MemTotal:", StringComparison.Ordinal))
            .Select(line => line.Split(':', 2)[1].Replace("kB", string.Empty, StringComparison.OrdinalIgnoreCase).Trim())
            .Select(value => long.TryParse(value, out var kib) ? kib * 1024 : 0L)
            .FirstOrDefault();

    /// <summary>Processor model and logical count out of <c>/proc/cpuinfo</c>.
    /// <para>
    /// The physical core count is deliberately NOT derived here: <c>cpu cores</c> is per socket and absent
    /// under some hypervisors, and a wrong core count is worse than an unknown one when it is the denominator
    /// the CPU control arms are read against. The caller supplies it from a source that knows, or leaves it 0.
    /// </para></summary>
    public static (string Model, int Logical) CpuInfo(string cpuinfo)
    {
        var lines = Lines(cpuinfo);

        var model = lines
            .Where(line => line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Split(':', 2) is [_, var value] ? value.Trim() : string.Empty)
            .FirstOrDefault(string.Empty);

        return (model, lines.Count(line => line.StartsWith("processor", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Non-empty lines, with the UTF-16 mangling of <see cref="Wsl"/> removed.</summary>
    private static IReadOnlyList<string> Lines(string text) =>
        [.. text.Replace("\0", string.Empty, StringComparison.Ordinal)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>The value of the first line whose label starts with <paramref name="stem"/>.</summary>
    private static string Value(IReadOnlyList<string> lines, string stem) =>
        lines
            .Where(line => line.StartsWith(stem, StringComparison.OrdinalIgnoreCase) && line.Contains(':', StringComparison.Ordinal))
            .Select(line => line.Split(':', 2)[1].Trim())
            .FirstOrDefault(string.Empty);
}
