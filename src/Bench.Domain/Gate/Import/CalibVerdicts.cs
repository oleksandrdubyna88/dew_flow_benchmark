using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>One entry of the calibration's blinding key — a blinded id → the harness's run id and the finding's index in
/// that run's reply. The import maps the run id to its DERIVED cell id, so the key needs no database to be read.</summary>
public sealed record CalibKeyEntry(BlindedId Id, string Run, int Index, GateTaskId Task);

/// <summary>One line of the calibration's <c>assess.jsonl</c>: the reading WITH its text (the note, the cluster), the
/// assessor word the other harness wrote, and the batch it came from.</summary>
public sealed record CalibVerdictLine(AssessedRow Row, string Assessor, string Batch, DateTimeOffset Utc);

public static class CalibVerdicts
{
    public static Outcome<IReadOnlyList<CalibKeyEntry>> Key(string json)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject root
                ? Entries(root)
                : Outcome<IReadOnlyList<CalibKeyEntry>>.Failure("the calibration key is not a JSON object of blinded id → entry");
        }
        catch (JsonException ex)
        {
            return Outcome<IReadOnlyList<CalibKeyEntry>>.Failure($"the calibration key is not JSON — {ex.Message}");
        }
    }

    /// <summary>One verdict line against the suite (a <c>seed_hit</c> must be a seed of the row's own task — the E4 rule),
    /// or a refusal naming the line.</summary>
    public static Outcome<CalibVerdictLine> Line(string line, int lineNumber, GateSuite suite)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject o
                ? Read(o, lineNumber, suite)
                : Outcome<CalibVerdictLine>.Failure($"assess.jsonl line {lineNumber} is not a JSON object");
        }
        catch (JsonException)
        {
            return Outcome<CalibVerdictLine>.Failure($"assess.jsonl line {lineNumber} is not JSON — a truncated line is refused, never skipped");
        }
    }

    /// <summary>The other harness's <c>latest_by_id</c> over the verdict log: a later line of one id supersedes the earlier.</summary>
    public static IReadOnlyList<CalibVerdictLine> Latest(IEnumerable<CalibVerdictLine> lines) =>
        [.. lines.GroupBy(l => l.Row.Id.Value, StringComparer.Ordinal).Select(g => g.Last())];

    private static Outcome<IReadOnlyList<CalibKeyEntry>> Entries(JsonObject root)
    {
        var entries = new List<CalibKeyEntry>();

        foreach (var (id, node) in root)
        {
            var o = node as JsonObject ?? [];
            var parsed = (BlindedId.Parse(id), GateTaskId.Parse(JsonRead.Text(o, "task")), JsonRead.Text(o, "run"), JsonRead.Long(o["index"]));

            if (parsed is not (Outcome<BlindedId>.Ok b, Outcome<GateTaskId>.Ok t, { Length: > 0 } run, { } index and >= 0))
            {
                return Outcome<IReadOnlyList<CalibKeyEntry>>.Failure($"key entry '{id}' does not read as a blinded id with a task, a run and an index");
            }

            entries.Add(new CalibKeyEntry(b.Value, run, (int)index, t.Value));
        }

        return Outcome<IReadOnlyList<CalibKeyEntry>>.Success(entries);
    }

    private static Outcome<CalibVerdictLine> Read(JsonObject o, int lineNumber, GateSuite suite)
    {
        var id = BlindedId.Parse(JsonRead.Text(o, "id"));
        var task = suite.Tasks.FirstOrDefault(t => t.Id.Value == JsonRead.Text(o, "task"));
        var words = (Word<StrictReading>(o, "verdict"), Word<ValueLevel>(o, "value"), Word<SeverityFairness>(o, "severity_fair"), Word<Grounding>(o, "grounded"));
        var seed = SeedHit.Parse(JsonRead.Text(o, "seed_hit"));

        var refusal = (id, task, words, seed) switch
        {
            (Outcome<BlindedId>.Fail f, _, _, _) => f.Reason,
            (_, null, _, _) => $"task '{JsonRead.Text(o, "task")}' is not a task of suite {suite.Stamp}",
            (_, _, (null, _, _, _) or (_, null, _, _) or (_, _, null, _) or (_, _, _, null), _) => "carries a word outside the strict rubric",
            (_, _, _, SeedHit.Of hit) when !task.Seeds.Any(s => s.Id.Value == hit.Seed.Value) => $"names seed '{hit.Seed}', which is not a seed of task {task.Id}",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<CalibVerdictLine>.Failure($"assess.jsonl line {lineNumber}: {refusal}")
            : Outcome<CalibVerdictLine>.Success(new CalibVerdictLine(
                new AssessedRow(((Outcome<BlindedId>.Ok)id).Value, task!.Id, words.Item1!.Value, words.Item2!.Value, words.Item3!.Value, words.Item4!.Value,
                    JsonRead.Text(o, "cluster"), seed, JsonRead.Text(o, "note")),
                JsonRead.Text(o, "assessor"),
                JsonRead.Text(o, "batch"),
                DateTimeOffset.TryParse(JsonRead.Text(o, "utc"), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var utc) ? utc : DateTimeOffset.UnixEpoch));
    }

    private static T? Word<T>(JsonObject o, string name)
        where T : struct, Enum =>
        Enum.TryParse<T>(JsonRead.Text(o, name), ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : null;
}
