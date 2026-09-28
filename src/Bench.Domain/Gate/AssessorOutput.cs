using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Gate;

/// <summary>One finding's strict reading as the assessor wrote it, WITH its text — the cluster key and the note. This
/// record lives in the artefact store's verdict file; the database gets <see cref="ToVerdict"/>, which keeps the
/// cluster as a keyed hash and the note not at all (a note quotes code).</summary>
public sealed record AssessedRow(
    BlindedId Id,
    GateTaskId Task,
    StrictReading Reading,
    ValueLevel Value,
    SeverityFairness SeverityFair,
    Grounding Grounded,
    string Cluster,
    SeedHit SeedHit,
    string Note)
{
    /// <summary>The database's half. The cluster key is HMAC'd under the artefact root's key: a plain SHA-256 of a short
    /// <c>task:kebab-issue</c> key is confirmed by hashing guesses, which is the <see cref="GateFinding.FileHash"/> lesson.</summary>
    public Verdict.Strict ToVerdict(FileHashKey key) => new(Reading, Value, SeverityFair, Grounded, ClusterHash(key, Cluster), SeedHit);

    public static string ClusterHash(FileHashKey key, string cluster) => key.Hash("cluster:" + cluster.Trim());
}

/// <summary>What one assessor answer said about a batch: rows, or a failure by cause. A closed hierarchy, because the
/// two call for opposite actions — keep the rows and re-ask the missing, or retry the whole batch.</summary>
public abstract record BatchReading
{
    private BatchReading()
    {
    }

    /// <param name="Missing">Ids of the batch with no usable row — absent, or refused one by one (a seed of another task,
    /// another task's name): re-asked once, then left unassessed.</param>
    /// <param name="Refusals">Why each refused row was refused, naming the id and never its text.</param>
    public sealed record Answered(IReadOnlyList<AssessedRow> Rows, IReadOnlyList<BlindedId> Missing, IReadOnlyList<string> Refusals) : BatchReading;

    public sealed record Failed(AssessmentFailureCause Cause, string Reason) : BatchReading;
}

/// <summary>The assessor's answer read against the batch it was given — the other harness's <c>run_task</c> reading,
/// made strict where it was lenient: an answer that does not parse is <see cref="AssessmentFailureCause.Unparseable"/>;
/// a JSON document cut short is <see cref="AssessmentFailureCause.Truncated"/>; a row naming an id the batch did not
/// carry is <see cref="AssessmentFailureCause.UnknownIds"/>; nothing at all is <see cref="AssessmentFailureCause.NoAnswer"/>.
/// A row's <c>seed_hit</c> must be <c>none</c> or a seed of THE ROW'S OWN task — a seed of another task is a reading
/// of the wrong case, and it is refused rather than counted as a hit or quietly read as none.</summary>
public static class AssessorOutput
{
    public static readonly IReadOnlyList<string> Keys = ["id", "task", "verdict", "value", "severity_fair", "grounded", "cluster", "seed_hit", "note"];

    public static BatchReading Read(string text, IReadOnlyList<AssessmentRow> batch, IReadOnlyCollection<SeedId> seedsOfTask)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return new BatchReading.Failed(AssessmentFailureCause.NoAnswer, "the assessor answered nothing");
        }

        return Document(trimmed) switch
        {
            JsonObject { } root when root["rows"] is JsonArray rows => Rows(rows, batch, seedsOfTask),
            JsonNode => new BatchReading.Failed(AssessmentFailureCause.Unparseable, "the answer is JSON but not an object with a 'rows' array"),
            _ when IsPrefix(trimmed) => new BatchReading.Failed(AssessmentFailureCause.Truncated, $"the answer is a JSON document cut short after {trimmed.Length} characters"),
            _ => new BatchReading.Failed(AssessmentFailureCause.Unparseable, "the answer is not JSON"),
        };
    }

    private static BatchReading Rows(JsonArray rows, IReadOnlyList<AssessmentRow> batch, IReadOnlyCollection<SeedId> seedsOfTask)
    {
        var ids = batch.Select(r => r.Id.Value).ToHashSet(StringComparer.Ordinal);
        var parsed = rows.Select(Row).ToList();
        var broken = parsed.FirstOrDefault(p => p.Reason.Length > 0);
        var unknown = parsed.Where(p => p.Reason.Length == 0 && !ids.Contains(p.Id)).Select(p => p.Id).Distinct(StringComparer.Ordinal).ToList();
        var twice = parsed.GroupBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        return (broken, unknown.Count, twice) switch
        {
            ({ } b, _, _) => new BatchReading.Failed(AssessmentFailureCause.Unparseable, b.Reason),
            (_, > 0, _) => new BatchReading.Failed(AssessmentFailureCause.UnknownIds, $"the answer names {unknown.Count} id(s) the batch did not carry: {string.Join(", ", unknown)}"),
            (_, _, { } dup) => new BatchReading.Failed(AssessmentFailureCause.Unparseable, $"the answer names id {dup.Key} {dup.Count()} times — every id appears exactly once"),
            _ => Answered(parsed, batch, seedsOfTask),
        };
    }

    private static BatchReading.Answered Answered(IReadOnlyList<ParsedRow> parsed, IReadOnlyList<AssessmentRow> batch, IReadOnlyCollection<SeedId> seedsOfTask)
    {
        var seeds = seedsOfTask.Select(s => s.Value).ToHashSet(StringComparer.Ordinal);
        var byId = parsed.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var rows = new List<AssessedRow>();
        var missing = new List<BlindedId>();
        var refusals = new List<string>();

        foreach (var asked in batch)
        {
            var refusal = byId.TryGetValue(asked.Id.Value, out var row) ? Refusal(row, asked, seeds) : "no row";

            if (refusal.Length == 0)
            {
                rows.Add(row!.ToAssessed(asked));
                continue;
            }

            missing.Add(asked.Id);
            if (refusal != "no row")
            {
                refusals.Add($"{asked.Id}: {refusal}");
            }
        }

        return new BatchReading.Answered(rows, missing, refusals);
    }

    /// <summary>Why a well-formed row is still not a reading of the case it names — empty when it is.</summary>
    private static string Refusal(ParsedRow row, AssessmentRow asked, IReadOnlySet<string> seeds) =>
        (string.Equals(row.Task, asked.Task.Value, StringComparison.Ordinal), row.SeedWord) switch
        {
            (false, _) => $"the row names task '{row.Task}', and this finding is task {asked.Task}'s",
            (_, "none") => string.Empty,
            (_, var seed) when seeds.Contains(seed) => string.Empty,
            (_, var seed) => $"seed_hit '{seed}' is not a seed of task {asked.Task} — a seed of another task is a reading of the wrong case",
        };

    private static ParsedRow Row(JsonNode? node)
    {
        if (node is not JsonObject o)
        {
            return ParsedRow.Broken("a row is not a JSON object");
        }

        var missing = Keys.Where(k => o[k] is not JsonValue v || !v.TryGetValue<string>(out _)).ToList();
        if (missing.Count > 0)
        {
            return ParsedRow.Broken($"a row lacks the string key(s) {string.Join(", ", missing)} — the output schema requires all nine");
        }

        return ParsedRow.Of(o);
    }

    private static JsonNode? Document(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="text"/> is valid JSON as far as it goes and simply stops — a cut answer, which
    /// is a different failure from prose. A reader that is told more may follow reads to the end without an error.</summary>
    private static bool IsPrefix(string text)
    {
        if (text[0] is not ('{' or '['))
        {
            return false;
        }

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), isFinalBlock: false, state: default);
        try
        {
            while (reader.Read())
            {
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record ParsedRow(string Id, string Task, string SeedWord, string Reason, JsonObject Source)
    {
        public static ParsedRow Broken(string reason) => new(string.Empty, string.Empty, string.Empty, reason, []);

        public static ParsedRow Of(JsonObject o)
        {
            var words = (Word(o, "verdict"), Word(o, "value"), Word(o, "severity_fair"), Word(o, "grounded"));
            var bad = BadWord(words);

            var seed = Word(o, "seed_hit");

            return new ParsedRow(Word(o, "id"), Word(o, "task"), seed.Length == 0 || seed.Equals("none", StringComparison.OrdinalIgnoreCase) ? "none" : seed, bad, o);
        }

        public AssessedRow ToAssessed(AssessmentRow asked) => new(
            asked.Id,
            asked.Task,
            Enum.Parse<StrictReading>(Word(Source, "verdict"), ignoreCase: true),
            Enum.Parse<ValueLevel>(Word(Source, "value"), ignoreCase: true),
            Enum.Parse<SeverityFairness>(Word(Source, "severity_fair"), ignoreCase: true),
            Enum.Parse<Grounding>(Word(Source, "grounded"), ignoreCase: true),
            Word(Source, "cluster"),
            SeedWord == "none" ? new SeedHit.None() : SeedHit.Parse(SeedWord),
            Word(Source, "note"));

        private static string BadWord((string Verdict, string Value, string Fair, string Grounded) w) =>
            (Known<StrictReading>(w.Verdict), Known<ValueLevel>(w.Value), Known<SeverityFairness>(w.Fair), Known<Grounding>(w.Grounded)) switch
            {
                (false, _, _, _) => $"verdict '{w.Verdict}' is not supported, partial, refuted or unresolved",
                (_, false, _, _) => $"value '{w.Value}' is not high, medium, low or none",
                (_, _, false, _) => $"severity_fair '{w.Fair}' is not yes, overstated or understated",
                (_, _, _, false) => $"grounded '{w.Grounded}' is not yes, near or no",
                _ => string.Empty,
            };

        private static bool Known<T>(string word)
            where T : struct, Enum =>
            word.Length > 0 && word.All(char.IsLetter) && Enum.TryParse<T>(word, ignoreCase: true, out _);

        private static string Word(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : string.Empty;
    }
}
