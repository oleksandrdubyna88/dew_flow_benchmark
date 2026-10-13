using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Trace;

namespace Bench.Domain.Scouts;

/// <summary>One claim waiting for a verdict: which claim it is, the task whose tree it is checked in, and the claim as parsed.</summary>
public sealed record ScoutClaimToAssess(ScoutClaimKey Key, string Task, ScoutClaim Claim)
{
    public string StatementSha256 => StableHash.Of(Claim.Statement);
}

/// <summary>A claim as the assessor sees it: a FRESH blinded id and the claim — nothing about the arm, the model, the cell or the run.</summary>
public sealed record ScoutBlindClaim(BlindedId Id, ScoutClaimToAssess Claim)
{
    /// <summary>The one JSONL row the assessor reads — <c>id</c>, <c>path</c>, <c>line</c> (0 when none was cited), <c>claim</c>; no other field
    /// exists on it by construction.</summary>
    public string ToJson() => new JsonObject
    {
        ["id"] = Id.Value,
        ["path"] = Claim.Claim.Path.Replace('\\', '/'),
        ["line"] = Claim.Claim.Line.WasCaptured ? Claim.Claim.Line.Value : 0,
        ["claim"] = Claim.Claim.Statement,
    }.ToJsonString();
}

/// <summary>A verdict read back for one blinded claim.</summary>
public sealed record ScoutClaimAnswer(ScoutBlindClaim Claim, ScoutClaimVerdict Verdict);

/// <summary>The scouts' assessor, pure (§3.7): its OWN rubric and schema — the gate's assessor is shaped for finding batches, not one-line
/// claims — the claims a cell's stream carries, blinding with fresh shuffled ids (<see cref="BlindedId.Mint"/>, as the gate's
/// <see cref="BlindExport"/>), batching, and reading the answer. The launch is codex read-only with <c>FindingAssessor.OptionsFor</c>'s options,
/// working in the task's history-free tree (Application's <c>ScoutClaimAssessor</c>).</summary>
public static class ScoutAssessment
{
    public const string RubricVersion = "scout-assess-v1";

    /// <summary>Claims per launch — the gate's batch ceiling, so a batch's prompt stays far under any context the assessor has.</summary>
    public const int BatchSize = 24;

    /// <summary>The rubric of §3.7, verbatim in every prompt. Changing a word is a new <see cref="RubricVersion"/>; every verdict stores the
    /// rubric's SHA-256, so verdicts under two rubrics are never pooled unknowingly.</summary>
    public static string Rubric { get; } =
        $"""
        You check one-line factual claims that code-reading scouts made about the repository in your working directory. You may read any
        file in it; you may not change anything. Judge every INPUT ROW on its own, by opening the file it names.

        A claim is "wrong" when ANY of these holds:
        - the path does not exist in the repository;
        - the symbol, type, member or thing the claim names is not in the named file;
        - the code contradicts the stated behaviour;
        - the claim cites a line (line > 0) and what it describes is more than 10 lines away from that line.

        A claim is "right" when the file exists, what it names is there, the code supports the statement, and a cited line is within 10
        lines of what it describes. A claim that cites no line (line 0) is judged without the line test.

        A claim is "unresolved" ONLY when it is too vague to be checked against the code at all. Do not use it for a claim you could check.

        Answer with one JSON object, {"{"}"rows": [...]{"}"}, holding exactly one entry per input row: "id" copied exactly, "verdict" one of
        "wrong", "right", "unresolved", and a short "note" naming what you checked. ({RubricVersion})
        """;

    public static string RubricSha256 { get; } = StableHash.Of(Rubric);

    /// <summary>The output schema codex enforces on its final message.</summary>
    public static string Schema { get; } = new JsonObject
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("rows"),
        ["properties"] = new JsonObject
        {
            ["rows"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("id", "verdict", "note"),
                    ["properties"] = new JsonObject
                    {
                        ["id"] = new JsonObject { ["type"] = "string" },
                        ["verdict"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. ScoutClaimVerdicts.Words.Select(w => (JsonNode)w)]) },
                        ["note"] = new JsonObject { ["type"] = "string" },
                    },
                },
            },
        },
    }.ToJsonString();

    /// <summary>Every parseable claim of one cell's stream, keyed by (cell, scout ordinal, place among its claims) — the scouts matched to
    /// their ordinals exactly as at settlement (<see cref="ScoutCellReading.Reports"/>).</summary>
    public static IReadOnlyList<ScoutClaimToAssess> ClaimsOf(Guid cellId, ScoutTask task, ScoutArm arm, SubagentTranscript transcript) =>
        [.. ScoutCellReading.Reports(transcript, ScoutPrompts.ForCell(task, arm).Sha256s)
            .SelectMany(report => ScoutOutput.Claims(report.Text).Claims
                .Select((claim, i) => new ScoutClaimToAssess(new ScoutClaimKey(cellId, report.Ordinal, i + 1), task.Id.Value, claim)))];

    /// <summary>Fresh blinded ids, the claims SHUFFLED so the order says nothing about which cell or arm a claim came from.</summary>
    public static IReadOnlyList<ScoutBlindClaim> Blind(IReadOnlyList<ScoutClaimToAssess> claims, Random random)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var blinded = claims.Select(c => new ScoutBlindClaim(BlindedId.Mint(taken, random), c)).ToArray();
        random.Shuffle(blinded);
        return blinded;
    }

    public static IReadOnlyList<IReadOnlyList<ScoutBlindClaim>> Batches(IReadOnlyList<ScoutBlindClaim> claims) =>
        [.. claims.Chunk(BatchSize).Select(chunk => (IReadOnlyList<ScoutBlindClaim>)chunk)];

    public static string Prompt(IReadOnlyList<ScoutBlindClaim> batch) =>
        new StringBuilder(Rubric.TrimEnd())
            .Append(CultureInfo.InvariantCulture, $"\n\nINPUT ROWS ({batch.Count} claims)\n")
            .Append(string.Join('\n', batch.Select(c => c.ToJson())))
            .ToString();

    /// <summary>The verdicts an answer gives, for the ids of THIS batch that it names exactly once with a legal verdict. An id it does not
    /// name, names twice, or answers with another word is left unassessed — the next <c>assess</c> asks again; nothing is guessed.</summary>
    public static Outcome<IReadOnlyList<ScoutClaimAnswer>> Read(string answer, IReadOnlyList<ScoutBlindClaim> batch)
    {
        var rows = Rows(answer);

        if (rows is Outcome<JsonArray>.Fail fail)
        {
            return Outcome<IReadOnlyList<ScoutClaimAnswer>>.Failure(fail.Reason);
        }

        var said = ((Outcome<JsonArray>.Ok)rows).Value.OfType<JsonObject>()
            .Select(r => (Id: Text(r, "id"), Verdict: ScoutClaimVerdicts.Parse(Text(r, "verdict"))))
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .Where(g => g.Count() == 1 && g.First().Verdict is Outcome<ScoutClaimVerdict>.Ok)
            .ToDictionary(g => g.Key, g => ((Outcome<ScoutClaimVerdict>.Ok)g.First().Verdict).Value, StringComparer.Ordinal);

        return Outcome<IReadOnlyList<ScoutClaimAnswer>>.Success(
            [.. batch.Where(c => said.ContainsKey(c.Id.Value)).Select(c => new ScoutClaimAnswer(c, said[c.Id.Value]))]);
    }

    /// <summary>What the store keeps of one answered claim: never the statement, only its hash; the path only when it is repository-relative
    /// once the tree's own root is taken off it (a claim naming a machine path keeps an empty path).</summary>
    public static ScoutAssessedClaim Stored(ScoutClaimAnswer answer, Guid runId, string treeRoot, string assessorModel, DateTimeOffset now)
    {
        var path = RecallMatcher.Normalise(answer.Claim.Claim.Claim.Path, treeRoot);

        return new ScoutAssessedClaim(
            answer.Claim.Claim.Key, runId, RepositoryRelative.Refusal(path).Length == 0 ? path : string.Empty, answer.Claim.Claim.Claim.Line,
            answer.Claim.Claim.StatementSha256, answer.Verdict, assessorModel, RubricSha256, now);
    }

    private static Outcome<JsonArray> Rows(string answer)
    {
        var text = answer.Trim();
        var (start, end) = (text.IndexOf('{'), text.LastIndexOf('}'));

        if (start < 0 || end <= start)
        {
            return Outcome<JsonArray>.Failure("the assessor's answer holds no JSON object");
        }

        try
        {
            return JsonNode.Parse(text[start..(end + 1)]) is JsonObject { } o && o["rows"] is JsonArray rows
                ? Outcome<JsonArray>.Success(rows)
                : Outcome<JsonArray>.Failure("the assessor's answer has no \"rows\" array");
        }
        catch (JsonException ex)
        {
            return Outcome<JsonArray>.Failure($"the assessor's answer is not JSON — {ex.Message}");
        }
    }

    private static string Text(JsonObject o, string name) =>
        o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    /// <summary>The cited line of a stored claim, as a captured count.</summary>
    public static CapturedCount Line(bool captured, long value) => captured ? CapturedCount.Number(value) : CapturedCount.Unavailable("the claim cites no line");
}
