using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The files a finished attempt commits through the store, beside the ones it adopts. Everything here lives in
/// the artefact root, outside git and outside the database: the snapshot (it names the data directory), the tool
/// arguments (they carry the plan and the epics), every reply and resolve, the findings' text, the ledger slice (its rows
/// carry an e-mail on a Team server), the shim's prompt and answer files, the settings check, and — LAST — the run
/// record, whose presence says the attempt was written out whole.</summary>
public static class GateAttemptRecord
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static IReadOnlyList<PendingArtifact> Files(GateCellWork work, ArtifactPath root, Evidence evidence, GateSettlement settlement)
    {
        var files = new List<PendingArtifact>();

        Add(files, root, "settings.json", ArtifactClass.Other, evidence.SnapshotJson);
        Add(files, root, "request.json", ArtifactClass.Request, Requests(evidence.Run));

        foreach (var stage in evidence.Run.Stages)
        {
            Add(files, root, $"stage-{stage.Name}.reply.json", ArtifactClass.Reply, stage.Reply);
            Add(files, root, $"stage-{stage.Name}.resolve.json", ArtifactClass.Reply, stage.Resolve);
        }

        Add(files, root, "reply.json", ArtifactClass.Reply, evidence.Run.Measured.Reply);
        Add(files, root, "findings.jsonl", ArtifactClass.Findings, string.Concat(evidence.Reply.Findings.Select(f => f.Json + "\n")));
        Add(files, root, "ledger.jsonl", ArtifactClass.Ledger, evidence.LedgerText);
        Add(files, root, "settings-check.json", ArtifactClass.Other, evidence.Settings.ToJson());

        foreach (var (name, bytes) in evidence.Shim)
        {
            AddBytes(files, root, $"answers/{name}", name.Contains("prompt", StringComparison.OrdinalIgnoreCase) ? ArtifactClass.Prompt : ArtifactClass.Answer, bytes);
        }

        return files;
    }

    public static PendingArtifact RunRecord(GateCellWork work, ArtifactPath root, Evidence evidence, GateSettlement settlement)
    {
        var record = new JsonObject
        {
            ["runId"] = work.Run.Id.ToString("D"),
            ["cellId"] = work.Cell.Id.ToString("D"),
            ["attempt"] = work.Cell.Attempts,
            ["gate"] = work.Run.Gate.ToString(),
            ["task"] = work.Task.Id.Value,
            ["reviewer"] = work.Reviewer.Stamp,
            ["repeat"] = work.Cell.Repeat,
            ["pin"] = work.Cell.Pin.Describe,
            ["serverVersion"] = settlement.Notes.ServerVersion,
            ["outcome"] = settlement.Kind.ToString(),
            ["settingsHash"] = evidence.SettingsHash,
            ["tapMarkedAtClose"] = evidence.TapMarkedAtClose,
            ["broken"] = evidence.Run.Broken,
            ["resolveRefusals"] = new JsonArray([.. evidence.Run.Stages.Where(s => s.ResolveRefusal.Length > 0).Select(s => (JsonNode)$"{s.Name}: {s.ResolveRefusal}")]),
            ["settings"] = JsonNode.Parse(evidence.Settings.ToJson()),
            ["facts"] = settlement is GateSettlement.Completed completed ? JsonSerializer.SerializeToNode(completed.Facts) : null,
            ["failure"] = settlement is GateSettlement.Failed failed ? $"{failed.Cause.Kind}: {failed.Cause.Text}" : string.Empty,
        };

        return new PendingArtifact(ArtifactClass.RunRecord, GateArtifactPaths.Under(root, CellPaths.RunRecordFile), Encoding.UTF8.GetBytes(record.ToJsonString(Indented)));
    }

    private static string Requests(ProtocolRun run) =>
        new JsonArray([.. run.Stages.Select(s => (JsonNode)new JsonObject { ["stage"] = s.Name, ["tool"] = s.Tool, ["arguments"] = s.Arguments.DeepClone() })])
            .ToJsonString(Indented);

    private static void Add(List<PendingArtifact> files, ArtifactPath root, string name, ArtifactClass kind, string text)
    {
        if (text.Length > 0)
        {
            AddBytes(files, root, name, kind, Encoding.UTF8.GetBytes(text));
        }
    }

    private static void AddBytes(List<PendingArtifact> files, ArtifactPath root, string name, ArtifactClass kind, byte[] bytes)
    {
        if (root.Then(name) is Outcome<ArtifactPath>.Ok path)
        {
            files.Add(new PendingArtifact(kind, path.Value, bytes));
        }
    }

    /// <summary>A copy's file name, safe for an artefact path: the ordinal plus the original's name with every character
    /// outside the path alphabet replaced.</summary>
    public static string SafeName(int ordinal, string original) =>
        string.Create(CultureInfo.InvariantCulture, $"{ordinal:00}-{new string([.. original.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_')])}");
}

/// <summary>A path under an attempt's root built from a NAME the harness spells — a constant or a sanitised copy name —
/// so a refusal is a programming error, not an outcome.</summary>
public static class GateArtifactPaths
{
    public static ArtifactPath Under(ArtifactPath root, string name) =>
        root.Then(name).Match(p => p, reason => throw new InvalidOperationException($"the harness built an artefact name that does not parse — {reason}"));
}
