using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>The verdicts and hand-checks in the database — ids, enum names, hashes and counts; never a note, never a
/// cluster's text, never a blinded id (the key that maps one to a finding is the artefact root's).</summary>
public interface IGateVerdictStore
{
    /// <summary>A batch's verdicts in ONE transaction. Refused whole when any names a finding that is not stored (a
    /// (cell, ordinal) no settled attempt produced). A verdict already stored — the same cell, ordinal, rubric hash,
    /// assessor and batch — is not stored twice: a replay after a crash changes nothing. Returns how many were new.</summary>
    Task<Outcome<int>> RecordAsync(IReadOnlyList<GateVerdict> verdicts, CancellationToken cancellationToken);

    /// <summary>Every verdict on findings of <paramref name="runIds"/> (cell ids), read back under the catalog — a row
    /// under a rubric hash the catalog does not hold is skipped, never guessed at.</summary>
    Task<IReadOnlyList<GateVerdict>> VerdictsAsync(IReadOnlyCollection<Guid> runIds, RubricCatalog catalog, CancellationToken cancellationToken);

    Task<Outcome<HandCheck>> RecordHandCheckAsync(HandCheck check, CancellationToken cancellationToken);

    Task<IReadOnlyList<HandCheck>> HandChecksAsync(RubricCatalog catalog, CancellationToken cancellationToken);
}

/// <summary>The assessment's private files, under <c>&lt;artefact-root&gt;/assess/</c>: the blinding key, each task's seed
/// list for the assessor, one folder per batch (prompt, schema, answer), the verdict log WITH its text (notes, cluster
/// keys), and the hand-check samples. Outside git and outside the database, like every other artefact.</summary>
public interface IGateAssessmentFiles
{
    /// <summary>The whole key, or a refusal when the file exists and does not read as one — a key that half-read would
    /// mint ids it already holds.</summary>
    Task<Outcome<IReadOnlyList<BlindKeyEntry>>> ReadKeyAsync(CancellationToken cancellationToken);

    /// <summary>Extends the key under an EXCLUSIVE lock — read, <paramref name="fresh"/> computes the new entries from what
    /// is held, the whole key replaced atomically (staged, flushed, renamed) — and returns the key as it now stands. Two
    /// passes (the two assessors run side by side) never lose each other's entries, and a crash leaves the old key or the
    /// new one, never a torn line.</summary>
    Task<Outcome<IReadOnlyList<BlindKeyEntry>>> ExtendKeyAsync(
        Func<IReadOnlyList<BlindKeyEntry>, IReadOnlyList<BlindKeyEntry>> fresh, CancellationToken cancellationToken);

    /// <summary>The one pass of THIS assessor, held for its length — a second pass of the same assessor is refused rather
    /// than asking every finding twice and interleaving two writers in one log. Other assessors are not blocked.</summary>
    Task<Outcome<IAsyncDisposable>> LockAssessorAsync(GateReviewerId assessor, CancellationToken cancellationToken);

    /// <summary>Writes the task's seed list (the file the assessor's <c>seed_spec</c> names) and returns its absolute path.</summary>
    Task<string> WriteSeedSpecAsync(GateTask task, CancellationToken cancellationToken);

    /// <summary>A FRESH folder for one batch attempt, named for the batch id and nothing else; its absolute path.</summary>
    Task<string> BeginBatchAsync(string batchId, CancellationToken cancellationToken);

    Task WriteBatchFileAsync(string batchDirectory, string name, string text, CancellationToken cancellationToken);

    /// <summary>Copies a batch folder's files into the artefact root (<c>assess/batches/&lt;batch&gt;/</c>) — the prompt as sent
    /// is the evidence behind the prompt hash on every verdict it produced.</summary>
    Task ArchiveBatchAsync(string batchDirectory, string batchId, CancellationToken cancellationToken);

    /// <summary>Appends a batch's lines to the ASSESSOR's verdict log (one file per assessor, one writer per file under
    /// <see cref="LockAssessorAsync"/>) and flushes them to disk before returning.</summary>
    Task AppendVerdictLinesAsync(GateReviewerId assessor, IReadOnlyList<VerdictLine> lines, CancellationToken cancellationToken);

    /// <summary>Every assessor's log lines; a torn last line of a killed append is skipped.</summary>
    Task<IReadOnlyList<VerdictLine>> ReadVerdictLinesAsync(CancellationToken cancellationToken);

    /// <summary>Writes a hand-check sample and the manifest of what was drawn; returns the sample's absolute path.</summary>
    Task<string> WriteHandCheckSampleAsync(string sampleId, string sampleText, string drawnJson, CancellationToken cancellationToken);

    /// <summary>A sample and its drawn manifest, read back from inside <c>assess/hand-check/</c> — a path elsewhere is refused.</summary>
    Task<Outcome<(string Sample, string Drawn, string Sha256)>> ReadHandCheckSampleAsync(string samplePath, CancellationToken cancellationToken);
}

/// <summary>One line of the verdict log — the full reading WITH its text, or an assessment failure — as the artefact
/// root keeps it. The database's half is the <see cref="GateVerdict"/>; the two join on (id, batch). A line whose batch
/// never reached the database (a crash between the two writes) is an orphan: it is never read as a verdict.</summary>
public sealed record VerdictLine(
    string Id,
    string Task,
    string Assessor,
    string BatchId,
    string RubricHash,
    string Reading,
    string Value,
    string SeverityFair,
    string Grounded,
    string Cluster,
    string SeedHit,
    string Note,
    string Failure,
    DateTimeOffset Utc)
{
    public static VerdictLine Of(AssessedRow row, GateReviewerId assessor, string batchId, Rubric rubric, DateTimeOffset utc) =>
        new(row.Id.Value, row.Task.Value, assessor.Value, batchId, rubric.Hash,
            Word(row.Reading), Word(row.Value), Word(row.SeverityFair), Word(row.Grounded),
            row.Cluster, row.SeedHit is SeedHit.Of hit ? hit.Seed.Value : "none", row.Note, string.Empty, utc);

    public static VerdictLine FailureOf(BlindedId id, GateTaskId task, AssessmentFailureCause cause, GateReviewerId assessor, string batchId, Rubric rubric, DateTimeOffset utc) =>
        new(id.Value, task.Value, assessor.Value, batchId, rubric.Hash, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, cause.ToString(), utc);

    public string ToJson() => new JsonObject
    {
        ["id"] = Id,
        ["task"] = Task,
        ["assessor"] = Assessor,
        ["batch"] = BatchId,
        ["rubric"] = RubricHash,
        ["verdict"] = Reading,
        ["value"] = Value,
        ["severity_fair"] = SeverityFair,
        ["grounded"] = Grounded,
        ["cluster"] = Cluster,
        ["seed_hit"] = SeedHit,
        ["note"] = Note,
        ["failure"] = Failure,
        ["utc"] = Utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
    }.ToJsonString();

    /// <summary>A line read back, or none when it does not parse — a torn LAST line of an append killed mid-write.</summary>
    public static IReadOnlyList<VerdictLine> Parse(string line)
    {
        try
        {
            return JsonNode.Parse(line) is JsonObject o
                ? [new VerdictLine(S(o, "id"), S(o, "task"), S(o, "assessor"), S(o, "batch"), S(o, "rubric"), S(o, "verdict"), S(o, "value"),
                    S(o, "severity_fair"), S(o, "grounded"), S(o, "cluster"), S(o, "seed_hit"), S(o, "note"), S(o, "failure"),
                    DateTimeOffset.TryParse(S(o, "utc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var utc) ? utc : DateTimeOffset.UnixEpoch)]
                : [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static string S(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;

    private static string Word<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();
}
