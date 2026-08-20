using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Authoring;
using Bench.Domain.Targets;

namespace Bench.Application;

/// <summary>`CodeTask` to and from the JSON a bank row stores.
/// <para>
/// Unknown members are REFUSED, not dropped — the `VariantJson` discipline, and for the same reason:
/// this is a stored configuration our own writer produces, and a stray field silently surviving here
/// would be an axis nobody can resolve later. camelCase, because the row is published with the
/// results. Contrast `DiagnosisJson`, which reads an AGENT's answer and tolerates extras — a payload
/// and a configuration are different trust shapes, and each codec says which it is.
/// </para></summary>
public static class CodeTaskCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    public static string Write(CodeTask task) =>
        JsonSerializer.Serialize(
            new CodeTaskWire(
                task.Kind,
                task.BaseCommit.Value,
                task.FixCommit.Value,
                task.ReferenceDiff,
                task.Mechanism,
                task.GatesRan,
                task.GateDetail),
            Options);

    /// <summary>Reads a stored payload back, or refuses by name. An EMPTY payload is a refusal too —
    /// a reading question legitimately stores nothing here, and the caller decides that by the row's
    /// `Kind` before ever asking this codec.</summary>
    public static Outcome<CodeTask> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Outcome<CodeTask>.Failure(
                "the code-task payload is empty — a reading question has none, and asking for it is the caller's routing defect");
        }

        try
        {
            var wire = JsonSerializer.Deserialize<CodeTaskWire>(json, Options);

            return wire is null
                ? Outcome<CodeTask>.Failure("the code-task payload read as null")
                : ToDomain(wire);
        }
        catch (JsonException error)
        {
            return Outcome<CodeTask>.Failure($"the code-task payload could not be read: {error.Message}");
        }
    }

    private static Outcome<CodeTask> ToDomain(CodeTaskWire wire) =>
        CommitSha.Parse(wire.BaseCommit).Match(
            baseCommit => CommitSha.Parse(wire.FixCommit).Match(
                fixCommit => Outcome<CodeTask>.Success(new CodeTask(
                    wire.Kind,
                    baseCommit,
                    fixCommit,
                    wire.ReferenceDiff,
                    wire.Mechanism,
                    wire.GatesRan,
                    wire.GateDetail)),
                Outcome<CodeTask>.Failure),
            Outcome<CodeTask>.Failure);

    private sealed record CodeTaskWire(
        string Kind,
        string BaseCommit,
        string FixCommit,
        string ReferenceDiff,
        string Mechanism,
        bool GatesRan,
        string GateDetail);
}
