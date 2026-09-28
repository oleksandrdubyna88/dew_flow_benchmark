using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bench.Domain.Targets;

namespace Bench.Domain.Gate;

/// <summary>The id a finding is shown to an assessor under — eight lower-case hex characters, minted fresh, carrying
/// nothing: not the model, not the run, not the reviewer, not the finding's position. The other harness's
/// <c>secrets.token_hex(4)</c>.</summary>
public sealed partial record BlindedId
{
    private BlindedId(string value) => Value = value;

    public string Value { get; }

    [GeneratedRegex("^[0-9a-f]{8}$")]
    private static partial Regex Shape { get; }

    public static Outcome<BlindedId> Parse(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        return Shape.IsMatch(trimmed)
            ? Outcome<BlindedId>.Success(new BlindedId(trimmed))
            : Outcome<BlindedId>.Failure($"'{trimmed}' is not a blinded id — eight lower-case hex characters");
    }

    /// <summary>A fresh id no entry of <paramref name="taken"/> holds. Drawn until fresh: a collision is rare at eight hex
    /// characters and a reuse would hand one finding's verdict to another.</summary>
    public static BlindedId Mint(ISet<string> taken, Random random)
    {
        while (true)
        {
            var value = random.NextInt64(0, 1L << 32).ToString("x8", CultureInfo.InvariantCulture);

            if (taken.Add(value))
            {
                return new BlindedId(value);
            }
        }
    }

    public override string ToString() => Value;
}

/// <summary>One line of the blinding KEY — which finding a blinded id stands for. The key lives ONLY in the artefact
/// root: the database holds verdicts already joined to (cell, ordinal), and the assessor never sees any of this.</summary>
/// <param name="RunId">The cell whose settled attempt produced the finding — the session a verdict joins on.</param>
public sealed record BlindKeyEntry(BlindedId Id, Guid CampaignId, Guid RunId, int Ordinal, GateTaskId Task, GateReviewerId Reviewer);

/// <summary>A finding the export may blind: where it came from and its JSON as the product wrote it (from the cell's
/// <c>findings.jsonl</c> in the artefact store — never from the database, which holds no text).</summary>
public sealed record FindingToAssess(Guid CampaignId, Guid RunId, int Ordinal, GateTaskId Task, GateReviewerId Reviewer, string FindingJson)
{
    public (Guid RunId, int Ordinal) Key => (RunId, Ordinal);
}

/// <summary>What the assessor is told about a TASK: where the read-only checkout at the variant head is, the base and the
/// head, and where the task's seed list is. None of it names a model, a run or a reviewer.</summary>
public sealed record TaskEvidence(string RepoPath, CommitSha Base, CommitSha Head, string SeedSpecPath);

/// <summary>One row the assessor reads — and the ONLY fields it can carry. There is no model, run, cell, campaign or
/// reviewer field on this type BY CONSTRUCTION; a reflection test holds it to that. The field names are the other
/// harness's, so the strict rubric's text (which names <c>repo_path</c>, <c>head</c>, <c>seed_spec</c>) is sent
/// verbatim.</summary>
public sealed record AssessmentRow(
    BlindedId Id,
    GateTaskId Task,
    string Severity,
    string Category,
    string File,
    int Line,
    string Title,
    string Why,
    string Fix,
    string RepoPath,
    CommitSha Base,
    CommitSha Head,
    string SeedSpec)
{
    /// <summary>A finding's JSON, as the product wrote it, read into a row; any field it lacks is empty.</summary>
    public static AssessmentRow Of(BlindedId id, GateTaskId task, string findingJson, TaskEvidence evidence)
    {
        var f = Parse(findingJson);

        return new AssessmentRow(
            id, task, Text(f, "severity"), Text(f, "category"), Text(f, "file"),
            f["line"] is JsonValue l && l.TryGetValue<int>(out var line) && line > 0 ? line : 0,
            Text(f, "title"), Text(f, "why"), Text(f, "fix"),
            evidence.RepoPath, evidence.Base, evidence.Head, evidence.SeedSpecPath);
    }

    /// <summary>One JSONL line, keys in the other harness's order and spelling.</summary>
    public string ToJson() => new JsonObject
    {
        ["id"] = Id.Value,
        ["task"] = Task.Value,
        ["variant"] = "seeded",
        ["severity"] = Severity,
        ["category"] = Category,
        ["file"] = File,
        ["line"] = Line,
        ["title"] = Title,
        ["why"] = Why,
        ["fix"] = Fix,
        ["repo_path"] = RepoPath.Replace('\\', '/'),
        ["base"] = Base.Value,
        ["head"] = Head.Value,
        ["seed_spec"] = SeedSpec.Replace('\\', '/'),
    }.ToJsonString();

    private static JsonObject Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static string Text(JsonObject o, string name) =>
        o[name] is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToJsonString()) : string.Empty;
}

/// <summary>The blinded export — the other harness's <c>export</c>: every finding not yet in the key gets a FRESH id,
/// never one the key already holds, and each task's new entries are shuffled so the key's order says nothing about
/// which run a finding came from. A finding already in the key is skipped, so a second export of the same findings
/// adds nothing.</summary>
public static class BlindExport
{
    public static IReadOnlyList<BlindKeyEntry> Plan(IReadOnlyList<FindingToAssess> findings, IReadOnlyList<BlindKeyEntry> key, Random random)
    {
        var exported = key.Select(k => (k.RunId, k.Ordinal)).ToHashSet();
        var taken = key.Select(k => k.Id.Value).ToHashSet(StringComparer.Ordinal);

        var fresh = findings
            .Where(f => exported.Add(f.Key))
            .Select(f => new BlindKeyEntry(BlindedId.Mint(taken, random), f.CampaignId, f.RunId, f.Ordinal, f.Task, f.Reviewer))
            .GroupBy(e => e.Task.Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => Shuffled([.. g], random));

        return [.. fresh];
    }

    private static IReadOnlyList<BlindKeyEntry> Shuffled(List<BlindKeyEntry> entries, Random random)
    {
        var copy = entries.ToArray();
        random.Shuffle(copy);
        return copy;
    }
}
