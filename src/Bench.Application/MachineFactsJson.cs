using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Storing <see cref="MachineFacts"/> as JSON, through an explicit wire record.
/// <para>
/// Explicit rather than serialising the domain type, for the reason <c>ResponseMetaJson</c> and
/// <c>VariantJson</c> already give: the domain type is free to change shape, while a stored row must stay
/// readable for years. This one is published with the results, so it is camelCase — the same rule the
/// variant catalog follows for the same reason.
/// </para>
/// <para>
/// Every wire member is nullable and every read defaults to the "unknown" value of its type, so a row
/// written before a field existed reads as <em>nobody recorded this</em> rather than as a machine that
/// answered an empty string. An unreadable row is <see cref="MachineFacts.NotRecorded"/> — never an
/// exception, because a report that cannot render one run's machine must still render the other's.
/// </para></summary>
public static class MachineFactsJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(MachineFacts facts) =>
        JsonSerializer.Serialize(
            new MachineWire
            {
                Hostname = facts.Hostname,
                MachineId = facts.MachineId,
                Os = new OsWire(facts.Os.Family, facts.Os.Edition, facts.Os.Release, facts.Os.Build),
                Wsl = facts.Wsl == WslFacts.None
                    ? null
                    : new WslWire(facts.Wsl.Runtime, facts.Wsl.Direct3D, facts.Wsl.DxCore),
                Cpu = new CpuWire(facts.Cpu.Model, facts.Cpu.PhysicalCores, facts.Cpu.LogicalCores, facts.Cpu.PowerPlan),
                TotalRamBytes = facts.TotalRamBytes,
                Adapters = [.. facts.Adapters.Select(a =>
                    new AdapterWire(a.Name, a.VramBytes, a.DriverVersion, a.DriverDate, a.HealthCode))],
                Volume = new VolumeWire(
                    facts.Volume.Path, facts.Volume.FileSystem, facts.Volume.ClusterBytes,
                    facts.Volume.FreeBytes, facts.Volume.TotalBytes),
            },
            Options);

    public static MachineFacts Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return MachineFacts.NotRecorded;
        }

        try
        {
            return Materialise(JsonSerializer.Deserialize<MachineWire>(json, Options));
        }
        catch (JsonException)
        {
            // A row this build cannot read is a machine it does not know about — which is exactly the state
            // NotRecorded expresses. Throwing here would make one unreadable row take out a whole report.
            return MachineFacts.NotRecorded;
        }
    }

    private static MachineFacts Materialise(MachineWire? wire) =>
        wire is null
            ? MachineFacts.NotRecorded
            : new MachineFacts
            {
                Hostname = wire.Hostname ?? string.Empty,
                MachineId = wire.MachineId ?? string.Empty,
                Os = wire.Os is { } os
                    ? new OsFacts(os.Family ?? string.Empty, os.Edition ?? string.Empty, os.Release ?? string.Empty, os.Build ?? string.Empty)
                    : OsFacts.Unknown,
                Wsl = wire.Wsl is { } wsl
                    ? new WslFacts(wsl.Runtime ?? string.Empty, wsl.Direct3D ?? string.Empty, wsl.DxCore ?? string.Empty)
                    : WslFacts.None,
                Cpu = wire.Cpu is { } cpu
                    ? new CpuFacts(cpu.Model ?? string.Empty, cpu.PhysicalCores, cpu.LogicalCores, cpu.PowerPlan ?? string.Empty)
                    : CpuFacts.Unknown,
                TotalRamBytes = wire.TotalRamBytes,
                Adapters = [.. (wire.Adapters ?? []).Select(a =>
                    new AdapterFacts(a.Name ?? string.Empty, a.VramBytes, a.DriverVersion ?? string.Empty, a.DriverDate ?? string.Empty, a.HealthCode))],
                Volume = wire.Volume is { } volume
                    ? new VolumeFacts(volume.Path ?? string.Empty, volume.FileSystem ?? string.Empty, volume.ClusterBytes, volume.FreeBytes, volume.TotalBytes)
                    : VolumeFacts.Unknown,
            };

    private sealed record MachineWire
    {
        public string? Hostname { get; init; }

        public string? MachineId { get; init; }

        public OsWire? Os { get; init; }

        /// <summary>Absent rather than empty when there is no WSL layer — a Windows-native host and one whose
        /// shims could not be read are different facts, and an object full of empty strings says the second
        /// when the first is true.</summary>
        public WslWire? Wsl { get; init; }

        public CpuWire? Cpu { get; init; }

        public long TotalRamBytes { get; init; }

        public IReadOnlyList<AdapterWire>? Adapters { get; init; }

        public VolumeWire? Volume { get; init; }
    }

    private sealed record OsWire(string? Family, string? Edition, string? Release, string? Build);

    private sealed record WslWire(string? Runtime, string? Direct3D, string? DxCore);

    private sealed record CpuWire(string? Model, int PhysicalCores, int LogicalCores, string? PowerPlan);

    private sealed record AdapterWire(string? Name, long VramBytes, string? DriverVersion, string? DriverDate, int HealthCode);

    private sealed record VolumeWire(string? Path, string? FileSystem, int ClusterBytes, long FreeBytes, long TotalBytes);
}
