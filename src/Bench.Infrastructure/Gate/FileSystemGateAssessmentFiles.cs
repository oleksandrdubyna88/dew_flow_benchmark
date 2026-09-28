using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Infrastructure.Gate;

/// <summary>The assessment's private files under <c>&lt;artefact-root&gt;/assess/</c>:
/// <code>
/// assess/
///   key.json                  the blinding key (blinded id → campaign, cell, ordinal, task, reviewer), replaced atomically
///   key.lock                  held EXCLUSIVELY while the key is read, extended and replaced
///   locks/&lt;assessor&gt;.lock      held for the length of one assessor's pass
///   seeds/&lt;task&gt;.json          the task's seeds, as the assessor's seed_spec names them
///   batches/&lt;batch&gt;/          prompt.txt, verdict-schema.json, answer.json — one folder per batch attempt, never reused
///   verdicts/&lt;assessor&gt;.jsonl  the verdict log WITH its text (cluster keys, notes) — one writer per file
///   hand-check/&lt;sample&gt;.jsonl  a hand-check sample, and &lt;sample&gt;.drawn.json, what was drawn
/// </code>
/// The root is the artefact root <see cref="FileSystemGateArtifactStore"/> opened (refused inside any git checkout), so
/// none of this is ever one <c>git add</c> from a public repository. Every name inside it is built from ids — a task id,
/// an assessor id, a batch id — never from a model, a run or a reviewer.</summary>
public sealed class FileSystemGateAssessmentFiles(string artifactRoot) : IGateAssessmentFiles
{
    public const string Folder = "assess";
    private static readonly TimeSpan KeyLockWait = TimeSpan.FromSeconds(60);

    private string Root => Path.Combine(artifactRoot, Folder);

    private string KeyFile => Path.Combine(Root, "key.json");

    public Task<Outcome<IReadOnlyList<BlindKeyEntry>>> ReadKeyAsync(CancellationToken cancellationToken) => Task.FromResult(ReadKey());

    public async Task<Outcome<IReadOnlyList<BlindKeyEntry>>> ExtendKeyAsync(
        Func<IReadOnlyList<BlindKeyEntry>, IReadOnlyList<BlindKeyEntry>> fresh, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Root);
        await using var hold = await KeyLockAsync(cancellationToken);

        if (ReadKey() is not Outcome<IReadOnlyList<BlindKeyEntry>>.Ok { Value: var held })
        {
            return ReadKey();
        }

        var added = fresh(held);
        if (added.Count == 0)
        {
            return Outcome<IReadOnlyList<BlindKeyEntry>>.Success(held);
        }

        IReadOnlyList<BlindKeyEntry> key = [.. held, .. added];
        await ReplaceAsync(KeyFile, KeyJson(key), cancellationToken);
        return Outcome<IReadOnlyList<BlindKeyEntry>>.Success(key);
    }

    public Task<Outcome<IAsyncDisposable>> LockAssessorAsync(GateReviewerId assessor, CancellationToken cancellationToken)
    {
        var file = Path.Combine(Root, "locks", $"{assessor.Value}.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        try
        {
            return Task.FromResult(Outcome<IAsyncDisposable>.Success(new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)));
        }
        catch (IOException)
        {
            return Task.FromResult(Outcome<IAsyncDisposable>.Failure(
                $"another assessment pass of assessor '{assessor}' is running against this artefact root — one pass per assessor at a time"));
        }
    }

    public async Task<string> WriteSeedSpecAsync(GateTask task, CancellationToken cancellationToken)
    {
        var file = Path.Combine(Root, "seeds", $"{task.Id.Value}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var seeds = new JsonArray([.. task.Seeds.Select(s => (JsonNode)new JsonObject
        {
            ["id"] = s.Id.Value, ["file"] = s.File, ["old"] = s.Old, ["new"] = s.New, ["what"] = s.What,
            ["trigger"] = s.Trigger, ["mechanism"] = s.Mechanism, ["consequence"] = s.Consequence, ["crossEpic"] = s.CrossEpic,
        })]);

        await ReplaceAsync(file, seeds.ToJsonString(), cancellationToken);
        return file;
    }

    public Task<string> BeginBatchAsync(string batchId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Root, "batches", Segment(batchId));

        if (Directory.Exists(directory))
        {
            throw new InvalidOperationException($"batch folder {batchId} already exists — a batch attempt is never reused");
        }

        Directory.CreateDirectory(directory);
        return Task.FromResult(directory);
    }

    public Task WriteBatchFileAsync(string batchDirectory, string name, string text, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(Path.Combine(batchDirectory, Segment(name)), text, new UTF8Encoding(false), cancellationToken);

    public async Task AppendVerdictLinesAsync(GateReviewerId assessor, IReadOnlyList<VerdictLine> lines, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var file = Path.Combine(Root, "verdicts", $"{assessor.Value}.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        // One writer per file (the assessor's pass lock), so an append from this process is the file's only append.
        await using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(string.Concat(lines.Select(l => l.ToJson() + "\n"))), cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    public async Task<IReadOnlyList<VerdictLine>> ReadVerdictLinesAsync(CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Root, "verdicts");
        var lines = new List<VerdictLine>();

        foreach (var file in Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, "*.jsonl").Order(StringComparer.Ordinal)] : Array.Empty<string>())
        {
            foreach (var line in (await File.ReadAllTextAsync(file, cancellationToken)).Split('\n').Where(l => l.Trim().Length > 0))
            {
                lines.AddRange(VerdictLine.Parse(line));
            }
        }

        return lines;
    }

    public async Task<string> WriteHandCheckSampleAsync(string sampleId, string sampleText, string drawnJson, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Root, "hand-check");
        Directory.CreateDirectory(folder);

        var sample = Path.Combine(folder, $"{Segment(sampleId)}.jsonl");
        await ReplaceAsync(Path.Combine(folder, $"{Segment(sampleId)}.drawn.json"), drawnJson, cancellationToken);
        await ReplaceAsync(sample, sampleText, cancellationToken);
        return sample;
    }

    public async Task<Outcome<(string Sample, string Drawn, string Sha256)>> ReadHandCheckSampleAsync(string samplePath, CancellationToken cancellationToken)
    {
        var folder = Path.GetFullPath(Path.Combine(Root, "hand-check")) + Path.DirectorySeparatorChar;
        var sample = Path.GetFullPath(samplePath);
        var drawn = sample.EndsWith(".jsonl", StringComparison.Ordinal) ? sample[..^".jsonl".Length] + ".drawn.json" : string.Empty;

        var refusal = (sample.StartsWith(folder, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), File.Exists(sample), drawn.Length > 0 && File.Exists(drawn)) switch
        {
            (false, _, _) => "a hand-check sample is read from the artefact root's assess/hand-check folder, where it was written — not from anywhere else",
            (_, false, _) => $"the sample {Path.GetFileName(sample)} is not there",
            (_, _, false) => $"the sample {Path.GetFileName(sample)} has no drawn manifest beside it — it was not drawn by this benchmark",
            _ => string.Empty,
        };

        if (refusal.Length > 0)
        {
            return Outcome<(string, string, string)>.Failure(refusal);
        }

        var bytes = await File.ReadAllBytesAsync(sample, cancellationToken);

        return Outcome<(string, string, string)>.Success(
            (Encoding.UTF8.GetString(bytes), await File.ReadAllTextAsync(drawn, cancellationToken), Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }

    private Outcome<IReadOnlyList<BlindKeyEntry>> ReadKey()
    {
        if (!File.Exists(KeyFile))
        {
            return Outcome<IReadOnlyList<BlindKeyEntry>>.Success([]);
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(KeyFile)) is JsonArray rows
                ? Entries(rows)
                : Outcome<IReadOnlyList<BlindKeyEntry>>.Failure("the blinding key is not a JSON array — refused rather than re-minted");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Outcome<IReadOnlyList<BlindKeyEntry>>.Failure($"the blinding key does not parse ({ex.Message.Split('\n')[0]}) — refused rather than re-minted");
        }
    }

    private static Outcome<IReadOnlyList<BlindKeyEntry>> Entries(JsonArray rows)
    {
        var entries = new List<BlindKeyEntry>();

        foreach (var row in rows.OfType<JsonObject>())
        {
            var entry = Entry(row);

            if (entry.Count == 0)
            {
                return Outcome<IReadOnlyList<BlindKeyEntry>>.Failure($"a blinding-key entry does not read back ('{Str(row, "id")}') — refused rather than re-minted");
            }

            entries.AddRange(entry);
        }

        return Outcome<IReadOnlyList<BlindKeyEntry>>.Success(entries);
    }

    private static IReadOnlyList<BlindKeyEntry> Entry(JsonObject row)
    {
        var ordinal = row["ordinal"] is JsonValue o && o.TryGetValue<int>(out var n) ? n : -1;

        return (BlindedId.Parse(Str(row, "id")), Guid.TryParse(Str(row, "campaign"), out var campaign), Guid.TryParse(Str(row, "run"), out var run),
                GateTaskId.Parse(Str(row, "task")), GateReviewerId.Parse(Str(row, "reviewer")), ordinal) switch
        {
            (Outcome<BlindedId>.Ok id, true, true, Outcome<GateTaskId>.Ok task, Outcome<GateReviewerId>.Ok reviewer, >= 0) =>
                [new BlindKeyEntry(id.Value, campaign, run, ordinal, task.Value, reviewer.Value)],
            _ => [],
        };
    }

    private static string KeyJson(IReadOnlyList<BlindKeyEntry> key) =>
        new JsonArray([.. key.Select(k => (JsonNode)new JsonObject
        {
            ["id"] = k.Id.Value,
            ["campaign"] = k.CampaignId.ToString("D"),
            ["run"] = k.RunId.ToString("D"),
            ["ordinal"] = k.Ordinal,
            ["task"] = k.Task.Value,
            ["reviewer"] = k.Reviewer.Value,
        })]).ToJsonString();

    /// <summary>Staged, flushed to disk, renamed over the target: a crash leaves the old file or the new one.</summary>
    private static async Task ReplaceAsync(string file, string text, CancellationToken cancellationToken)
    {
        var staging = $"{file}.staging-{Guid.NewGuid():N}";

        await using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
            stream.Flush(flushToDisk: true);
        }

        File.Move(staging, file, overwrite: true);
    }

    /// <summary>The key's exclusive lock: an open with no sharing, retried until the other holder is done or the wait
    /// runs out. The operating system releases it when a holder dies, so a crash never leaves the key locked.</summary>
    private async Task<FileStream> KeyLockAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + KeyLockWait;

        while (true)
        {
            try
            {
                return new FileStream(Path.Combine(Root, "key.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }
    }

    /// <summary>A file-name segment built from an id: anything outside <c>[A-Za-z0-9._-]</c> is replaced, so no id can
    /// name a path outside its folder.</summary>
    private static string Segment(string name)
    {
        var safe = new string([.. name.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_')]);
        return safe.Trim('.').Length == 0 ? "_" : safe;
    }

    private static string Str(JsonObject o, string name) =>
        o[name] is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.ToString()) : string.Empty;
}
