using System.Text.Json;
using System.Text.Json.Serialization;
using Bench.Domain.Trace;

namespace Bench.Application;

/// <summary>Storing <see cref="LegLoad"/> as JSON, through an explicit wire record.
/// <para>
/// The <c>ResponseMetaJson</c> rule, for the same reason: the domain type is free to change shape while a
/// stored row must stay readable for years. Every member is nullable and an absent one reads as
/// <em>not sampled</em>, so a row written before this column existed — <c>{}</c>, the default the migration
/// gives it — is a leg nobody watched rather than a leg on an idle machine.
/// </para>
/// <para>
/// The attribution travels as a NAME. It is the field that decides whether a VRAM figure may be read as this
/// leg's, and an ordinal changes meaning the day somebody inserts an enum member — in an object published
/// beside the results.
/// </para></summary>
public static class LegLoadJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write(LegLoad load) =>
        JsonSerializer.Serialize(
            new LoadWire
            {
                Cpu = Wire(load.CpuPercent),
                Ram = Wire(load.RamBytesUsed),
                Vram = Wire(load.Vram.Bytes),
                Attribution = load.Vram.Attribution,
                SharedWith = load.Vram.SharedWith,
                WindowSeconds = load.Window.TotalSeconds,
            },
            Options);

    public static LegLoad Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LegLoad.NotSampled("this leg carries no sampling record");
        }

        try
        {
            return Materialise(JsonSerializer.Deserialize<LoadWire>(json, Options));
        }
        catch (JsonException)
        {
            // One unreadable row must not take out a report about every other leg.
            return LegLoad.NotSampled("this leg's sampling record could not be read");
        }
    }

    private static LegLoad Materialise(LoadWire? wire) =>
        wire is null
            ? LegLoad.NotSampled("this leg carries no sampling record")
            : new LegLoad(
                Summary(wire.Cpu),
                Summary(wire.Ram),
                new VramReading(Summary(wire.Vram), wire.Attribution, wire.SharedWith ?? string.Empty),
                TimeSpan.FromSeconds(wire.WindowSeconds));

    private static SummaryWire? Wire(SampleSummary summary) =>
        summary.Sampled
            ? new SummaryWire(summary.Minimum, summary.Maximum, summary.Mean, summary.Count, null)
            // Absent rather than a row of zeroes: the reason is the only useful thing an unsampled field can
            // carry, and it is what an operator reads to learn whether to fix a probe or accept a short leg.
            : new SummaryWire(null, null, null, 0, summary.Reason);

    private static SampleSummary Summary(SummaryWire? wire) =>
        wire is { Minimum: { } min, Maximum: { } max, Mean: { } mean } && wire.Count > 0
            ? new SampleSummary(true, min, max, mean, wire.Count, string.Empty)
            : SampleSummary.Nothing(wire?.Reason ?? "this leg carries no reading for that stream");

    private sealed record LoadWire
    {
        public SummaryWire? Cpu { get; init; }

        public SummaryWire? Ram { get; init; }

        public SummaryWire? Vram { get; init; }

        public VramAttribution Attribution { get; init; }

        public string? SharedWith { get; init; }

        public double WindowSeconds { get; init; }
    }

    private sealed record SummaryWire(double? Minimum, double? Maximum, double? Mean, int Count, string? Reason);
}
