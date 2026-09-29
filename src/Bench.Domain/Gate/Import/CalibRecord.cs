using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain.Trace;

namespace Bench.Domain.Gate;

/// <summary>The transport preset one calibration line recorded — what the other harness's <c>child_env</c> turned into
/// the product's knobs. <c>capMin</c> is absent on the earliest phase-1 lines; <c>child_env</c> sent 20 for it then
/// (<c>preset.get("capMin", 20)</c>), so 20 is what was SENT, not a guess.</summary>
public sealed record CalibPreset(string Dialect, string Effort, int MaxTokens, int TimeoutMinutes, int FollowUps, int CapMinutes)
{
    public const int HarnessDefaultCapMinutes = 20;

    /// <summary>The other harness never wrote a thinking field (<c>child_env</c>), so its runs ran at the vendor's DEFAULT —
    /// which is what the row now says (D2 of the fidelity plan; rows imported before say OFF, and are kept as they are).</summary>
    public Outcome<ReviewerTransport> Transport =>
        ReviewerTransport.Parse(Dialect, Effort, MaxTokens, TimeoutMinutes, FollowUps, CapMinutes, ThinkingSetting.VendorDefault);
}

/// <summary>One line of the calibration harness's <c>runs.jsonl</c>, read into the gate's shapes. The mapping decisions
/// all live here, in one place, as pure functions over the line: which cell it is, which attempt, which pin, and its
/// facts field for field (<see cref="CalibFacts"/>).</summary>
/// <param name="Id">The harness's own run id — <c>p2-grok-4.7-js3-r1</c>, <c>…-a2</c> for a second attempt. The import's
/// source key: it carries the attempt, so a new attempt is a new id and a MUTATION is the same id with other bytes.</param>
/// <param name="Repeat">The repeat for phase 2; for phase 1 the ITERATION — each iteration was its own setting, and the
/// other harness counted each as its own cell.</param>
/// <param name="SourceJson">The line as read, compacted — what <c>import-source.json</c> commits and a re-import compares.</param>
public sealed record CalibRecord(
    string Id,
    int Phase,
    string Model,
    GateTaskId Task,
    int Repeat,
    int Attempt,
    CalibPreset Preset,
    string ProductSha,
    CapturedCount DirtyFiles,
    DateTimeOffset Started,
    GateRunFacts Facts,
    string SourceJson)
{
    /// <summary>The population the other harness compared within — the imported pin's version text.</summary>
    public string Population => $"{CalibRecords.Harness} phase {Phase.ToString(CultureInfo.InvariantCulture)}";

    public Outcome<ProductPin> Pin => ProductPin.Imported(ProductSha, DirtyFiles, Population);
}

public static class CalibRecords
{
    public const string Harness = "calib-py";

    /// <summary>One line, or a refusal naming the line number and the field — a line that does not read is a refusal of
    /// the whole import, never a skipped record (a skipped line would be a run missing from every figure, silently).</summary>
    public static Outcome<CalibRecord> Parse(string line, int lineNumber, PrivateNames privateNames)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject o
                ? Read(o, lineNumber, privateNames)
                : Outcome<CalibRecord>.Failure($"runs.jsonl line {lineNumber} is not a JSON object");
        }
        catch (JsonException)
        {
            return Outcome<CalibRecord>.Failure($"runs.jsonl line {lineNumber} is not JSON — a truncated line is refused, never skipped");
        }
    }

    /// <summary>The other harness's <c>latest_by_id</c>: a re-run of one id (<c>--force</c>) supersedes the earlier line,
    /// in file order.</summary>
    public static IReadOnlyList<CalibRecord> Latest(IEnumerable<CalibRecord> records)
    {
        var latest = new Dictionary<string, CalibRecord>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var record in records)
        {
            if (latest.TryAdd(record.Id, record))
            {
                order.Add(record.Id);
                continue;
            }

            latest[record.Id] = record;
        }

        return [.. order.Select(id => latest[id])];
    }

    private static Outcome<CalibRecord> Read(JsonObject o, int lineNumber, PrivateNames privateNames)
    {
        var id = JsonRead.Text(o, "id");
        var phase = JsonRead.Int(o, "phase");
        var task = GateTaskId.Parse(JsonRead.Text(o, "task"));
        var refusal = (id.Length, phase, task, JsonRead.Text(o, "model").Length) switch
        {
            (0, _, _, _) => "has no id",
            (_, not (1 or 2), _, _) => $"has phase {phase} — the calibration has phases 1 and 2",
            (_, _, Outcome<GateTaskId>.Fail f, _) => $"task: {f.Reason}",
            (_, _, _, 0) => "names no model",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<CalibRecord>.Failure($"runs.jsonl line {lineNumber} ({(id.Length > 0 ? id : "no id")}) {refusal}")
            : Outcome<CalibRecord>.Success(Record(o, id, phase, ((Outcome<GateTaskId>.Ok)task).Value, privateNames));
    }

    private static CalibRecord Record(JsonObject o, string id, int phase, GateTaskId task, PrivateNames privateNames) =>
        new(
            id,
            phase,
            JsonRead.Text(o, "model"),
            task,
            phase == 1 ? JsonRead.Int(o, "iteration", 1) : JsonRead.Int(o, "rep", 1),
            JsonRead.Int(o, "attempt", 1),
            Preset(o["transport"] as JsonObject ?? []),
            JsonRead.Text(o, "product_sha"),
            JsonRead.Count(o, "product_dirty_files", "the other harness recorded no dirty count"),
            JsonRead.Utc(o, "started"),
            CalibFacts.Of(o, privateNames),
            o.ToJsonString());

    private static CalibPreset Preset(JsonObject t) =>
        new(
            JsonRead.Text(t, "dialect"),
            JsonRead.Text(t, "effort"),
            JsonRead.Int(t, "maxTokens"),
            JsonRead.Int(t, "timeoutMin"),
            JsonRead.Int(t, "followUps"),
            JsonRead.Int(t, "capMin", CalibPreset.HarnessDefaultCapMinutes));
}

/// <summary>Small readers over a <see cref="JsonObject"/> that answer "absent" as a value, never a throw: every mapping
/// decision about an absent field is then the caller's, and it is written down there.</summary>
internal static class JsonRead
{
    public static string Text(JsonObject o, string name) =>
        o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    public static int Int(JsonObject o, string name, int absent = 0) => Long(o[name]) is { } n ? (int)n : absent;

    public static bool Flag(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public static bool IsNull(JsonObject o, string name) => o[name] is null;

    public static long? Long(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<double>(out var d) && double.IsFinite(d) ? (long)Math.Round(d) : null;

    public static double Double(JsonNode? node) => node is JsonValue v && v.TryGetValue<double>(out var d) ? d : 0;

    public static CapturedCount Count(JsonObject o, string name, string absentReason) =>
        Long(o[name]) is { } n ? CapturedCount.Number(n) : CapturedCount.Unavailable(absentReason);

    public static IReadOnlyList<JsonNode?> List(JsonObject o, string name) => o[name] is JsonArray a ? [.. a] : [];

    /// <summary>A UTC timestamp, or the epoch when the field is absent or does not parse — a sort key, never a claim.</summary>
    public static DateTimeOffset Utc(JsonObject o, string name) =>
        DateTimeOffset.TryParse(Text(o, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc)
            ? utc
            : DateTimeOffset.UnixEpoch;
}
