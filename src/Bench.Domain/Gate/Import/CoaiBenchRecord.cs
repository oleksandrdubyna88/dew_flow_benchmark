using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>A coai-bench case — a plan and the commit that implemented it — as the record names it (short shas).</summary>
public sealed record CoaiBenchCase(GateTaskId Task, string PlanFile, string Commit, string BaseRef);

/// <summary>What the coai-bench judge said about one finding: worth having (yes / no), or nothing yet. <c>unjudged</c>
/// is not a verdict and never becomes one.</summary>
public enum WorthWord
{
    Unjudged,
    Yes,
    No,
}

/// <summary>One STAGE of one coai-bench run — the unit an import makes a cell of: <c>plan-1</c> a plan cell, <c>code</c> a
/// code cell. <see cref="Key"/> is the record's own identity (<c>arm|case|repeat|startedUtc</c> plus the stage), so the
/// same record in a copied file is the same cell. <see cref="RunJson"/> is the stage WITHOUT the judge's fields — what a
/// re-import compares, so a judge pass that ran after the first import adds verdicts instead of reading as a changed run;
/// <see cref="SourceJson"/> is the stage whole, judge's reasons included, kept in the artefact root.</summary>
public sealed record CoaiBenchStage(
    string Key,
    GateKind Gate,
    string Arm,
    CoaiBenchCase Case,
    int Repeat,
    DateTimeOffset Started,
    GateRunFacts Facts,
    IReadOnlyList<ParsedFinding> Findings,
    IReadOnlyList<WorthWord> Worth,
    string JudgedBy,
    string SourceJson,
    string RunJson)
{
    /// <summary>The arm is a vendor SET and the record names no model, so the reviewer is named for the set and no catalog
    /// row is invented for it.</summary>
    public Outcome<GateReviewerId> Reviewer => GateReviewerId.Parse($"coai-bench-{ImportSlug.Of(Arm, 53)}");
}

public static class CoaiBenchRecords
{
    public const string Harness = "coai-bench";

    /// <summary>The judge name a record carried, or this when it named none (the early passes wrote no <c>judgedBy</c>).</summary>
    public const string UnrecordedJudge = "coai-bench-unrecorded";

    /// <summary>Every <c>plan-1</c> and <c>code</c> stage of a <c>runs.json</c> file (a list of <c>RunRecord</c>s). A stage
    /// of any other name is refused by name — a third stage nobody mapped would otherwise land in the wrong gate.</summary>
    public static Outcome<IReadOnlyList<CoaiBenchStage>> Parse(string json, PrivateNames privateNames)
    {
        try
        {
            return JsonNode.Parse(json) is JsonArray records
                ? Stages(records, privateNames)
                : Outcome<IReadOnlyList<CoaiBenchStage>>.Failure("the coai-bench file is not a JSON list of run records");
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<CoaiBenchStage>>.Failure($"the coai-bench file is not JSON — {ex.Message}");
        }
    }

    public static Outcome<GateKind> GateOf(string stage) => stage switch
    {
        "code" => Outcome<GateKind>.Success(GateKind.Code),
        _ when stage.StartsWith("plan-", StringComparison.Ordinal) => Outcome<GateKind>.Success(GateKind.Plan),
        _ => Outcome<GateKind>.Failure($"stage '{stage}' is neither a plan round (plan-N) nor the code stage"),
    };

    private static Outcome<IReadOnlyList<CoaiBenchStage>> Stages(JsonArray records, PrivateNames privateNames)
    {
        var stages = new List<CoaiBenchStage>();

        foreach (var (record, index) in records.Select((r, i) => (r as JsonObject ?? [], i)))
        {
            var read = Record(record, privateNames);
            if (read is Outcome<IReadOnlyList<CoaiBenchStage>>.Fail fail)
            {
                return Outcome<IReadOnlyList<CoaiBenchStage>>.Failure($"record {index + 1}: {fail.Reason}");
            }

            stages.AddRange(((Outcome<IReadOnlyList<CoaiBenchStage>>.Ok)read).Value);
        }

        return Outcome<IReadOnlyList<CoaiBenchStage>>.Success(stages);
    }

    private static Outcome<IReadOnlyList<CoaiBenchStage>> Record(JsonObject record, PrivateNames privateNames)
    {
        var c = record["case"] as JsonObject ?? [];
        var task = GateTaskId.Parse(JsonRead.Text(c, "name"));
        var arm = JsonRead.Text(record, "arm");

        if ((task, arm.Length) is not (Outcome<GateTaskId>.Ok t, > 0))
        {
            return Outcome<IReadOnlyList<CoaiBenchStage>>.Failure(task is Outcome<GateTaskId>.Fail f ? $"case: {f.Reason}" : "names no arm");
        }

        var @case = new CoaiBenchCase(t.Value, JsonRead.Text(c, "planFile"), JsonRead.Text(c, "commit"), JsonRead.Text(c, "baseRef"));
        var repeat = RepeatOf(record);
        var key = $"{arm}|{t.Value}|{repeat.ToString(CultureInfo.InvariantCulture)}|{JsonRead.Text(record, "startedUtc")}";
        var stages = new List<CoaiBenchStage>();

        foreach (var stage in JsonRead.List(record, "stages").Select(s => s as JsonObject ?? []))
        {
            var gate = GateOf(JsonRead.Text(stage, "stage"));
            if (gate is Outcome<GateKind>.Fail fail)
            {
                return Outcome<IReadOnlyList<CoaiBenchStage>>.Failure(fail.Reason);
            }

            stages.Add(Stage($"{key}|{JsonRead.Text(stage, "stage")}", ((Outcome<GateKind>.Ok)gate).Value, arm, @case, repeat, JsonRead.Utc(record, "startedUtc"), stage, JsonRead.Text(record, "judgedBy"), privateNames));
        }

        return Outcome<IReadOnlyList<CoaiBenchStage>>.Success(stages);
    }

    /// <summary><c>repeat</c> is a number in the record, but some files spelled it as a string.</summary>
    private static int RepeatOf(JsonObject record) =>
        JsonRead.Long(record["repeat"]) is { } n ? (int)n
        : int.TryParse(JsonRead.Text(record, "repeat"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 1;

    private static CoaiBenchStage Stage(
        string key, GateKind gate, string arm, CoaiBenchCase @case, int repeat, DateTimeOffset started, JsonObject stage, string judgedBy, PrivateNames privateNames)
    {
        var findings = JsonRead.List(stage, "findings").Select(f => f as JsonObject ?? []).ToList();
        var reply = GateReplyParser.Parse(new JsonObject
        {
            ["verdict"] = JsonRead.Text(stage, "verdict"),
            ["findings"] = new JsonArray([.. findings.Select(f => (JsonNode)Stripped(f))]),
        }.ToJsonString());

        var run = (JsonObject)stage.DeepClone();
        run["findings"] = new JsonArray([.. findings.Select(f => (JsonNode)Stripped(f))]);

        return new CoaiBenchStage(
            key, gate, arm, @case, repeat, started,
            CoaiBenchFacts.Of(stage, reply, privateNames),
            reply.Findings,
            [.. findings.Select(f => Worth(JsonRead.Text(f, "useful")))],
            judgedBy,
            stage.ToJsonString(),
            run.ToJsonString());
    }

    /// <summary>The finding as the REVIEWER wrote it — the judge's two fields removed, so the finding's text hash is of the
    /// finding, not of somebody's opinion of it.</summary>
    private static JsonObject Stripped(JsonObject finding)
    {
        var copy = (JsonObject)finding.DeepClone();
        copy.Remove("useful");
        copy.Remove("verdict");
        return copy;
    }

    private static WorthWord Worth(string word) => word.Trim().ToLowerInvariant() switch
    {
        "yes" => WorthWord.Yes,
        "no" => WorthWord.No,
        _ => WorthWord.Unjudged,
    };
}

/// <summary>A coai-bench stage's facts. That harness recorded the stage's seconds, verdict, error, tokens in/out (from the
/// reply's cost block) and cost — and NO ledger: turns, HTTP calls, served/refused, cached and reasoning tokens are <i>not
/// captured</i> (<see cref="GateRunFacts.TurnFactsCaptured"/> false), never zeros. Valid is the gate's one rule — a
/// verdict of proceed or revise — with "a ledger turn" replaced by "no error", the only evidence the record has that the
/// reviewer answered; it is the rule a native plan or code cell is judged by too, so a <c>good_enough</c> round is not
/// valid here either.</summary>
public static class CoaiBenchFacts
{
    private const string NoLedger = "coai-bench recorded no ledger";

    public static GateRunFacts Of(JsonObject stage, ParsedReply reply, PrivateNames privateNames)
    {
        var error = JsonRead.Text(stage, "error");
        var valid = error.Length == 0 && GateVerdictWords.IsPassing(reply.Verdict);
        var seconds = JsonRead.Double(stage["seconds"]);

        return new GateRunFacts(
            valid,
            reply.Verdict,
            ReplyParsed: error.Length == 0,
            error.Length == 0 ? CapturedCount.Number(reply.Findings.Count) : CapturedCount.Unavailable("the stage errored"),
            Turns: 0,
            HttpCalls: 0,
            [],
            [],
            Tokens(stage, "tokensIn"),
            Tokens(stage, "tokensOut"),
            CapturedCount.Unavailable(NoLedger),
            CapturedCount.Unavailable(NoLedger),
            seconds,
            seconds,
            [],
            [],
            Cost(stage),
            Served: 0,
            Refused: 0,
            valid ? FailureCause.None : Failure(error, reply.Verdict, privateNames))
        {
            TurnFactsCaptured = false,
        };
    }

    private static CapturedUsd Cost(JsonObject stage) =>
        stage["costUsd"] is JsonValue v && v.TryGetValue<double>(out var usd)
            ? CapturedUsd.Amount((decimal)usd)
            : CapturedUsd.Unavailable("coai-bench recorded no cost");

    /// <summary>A token count of zero is what the product's cost block says when a CLI reported nothing — not a
    /// measurement of zero tokens.</summary>
    private static CapturedCount Tokens(JsonObject stage, string name) =>
        JsonRead.Long(stage[name]) is { } n and > 0 ? CapturedCount.Number(n) : CapturedCount.Unavailable("coai-bench recorded no token count");

    private static FailureCause Failure(string error, GateVerdictWord verdict, PrivateNames privateNames) =>
        error.Length > 0
            ? new FailureCause(FailureKind.Unexplained, FailureRedaction.Redact($"the coai-bench stage errored: {error}", privateNames))
            : new FailureCause(FailureKind.VerdictNotPassing, $"verdict {verdict} — the gate's valid rule takes proceed or revise");
}
