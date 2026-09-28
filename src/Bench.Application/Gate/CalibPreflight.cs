using System.Text;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>One calibration record ready to import: the line, the reviewer definition it was measured with, and its
/// reply as the one parser reads it (no reply file — a run that died before answering — is an empty answer).</summary>
public sealed record CalibCell(CalibRecord Record, CalibModel Model, ReviewerDefinition Definition, ParsedReply Reply, ProductPin Pin);

/// <summary>Everything the calibration's files say, read and checked BEFORE the first byte is written.</summary>
public sealed record CalibSource(IReadOnlyList<CalibCell> Cells, IReadOnlyList<CalibKeyEntry> Key, IReadOnlyList<CalibVerdictLine> Verdicts)
{
    public bool HasVerdicts => Verdicts.Count > 0 || Key.Count > 0;
}

/// <summary>The pre-flight (plan round, finding 0): every line of <c>runs.jsonl</c>, every run's <c>reply.json</c>, the key
/// and every verdict line are read and checked against the suite and the models file, and the first thing that does not
/// read refuses the WHOLE import — naming the file and the line — so a truncated file can never import half a campaign.</summary>
public static class CalibPreflight
{
    public const string RunsFile = "runs.jsonl";
    public const string RunsFolder = "runs";
    public const string AssessFile = "assess.jsonl";
    public const string KeyFile = "assess-key/key.json";

    public static async Task<Outcome<CalibSource>> ReadAsync(
        IImportSource source, GateSuite suite, IReadOnlyDictionary<string, CalibModel> models, PrivateNames privateNames, CancellationToken cancellationToken)
    {
        if (!source.Exists(RunsFile))
        {
            return Outcome<CalibSource>.Failure($"{source.Label} has no {RunsFile} — not a calibration workspace");
        }

        var runs = await source.ReadBytesAsync(RunsFile, cancellationToken);
        if (runs is Outcome<byte[]>.Fail unread)
        {
            return Outcome<CalibSource>.Failure(unread.Reason);
        }

        var records = Lines(((Outcome<byte[]>.Ok)runs).Value).Select((line, i) => CalibRecords.Parse(line, i + 1, privateNames)).ToList();
        var bad = records.OfType<Outcome<CalibRecord>.Fail>().FirstOrDefault();
        if (bad is not null)
        {
            return Outcome<CalibSource>.Failure(bad.Reason);
        }

        var latest = CalibRecords.Latest(records.OfType<Outcome<CalibRecord>.Ok>().Select(r => r.Value));
        var cells = new List<CalibCell>();
        foreach (var record in latest)
        {
            var cell = await CellAsync(source, suite, models, record, cancellationToken);
            if (cell is Outcome<CalibCell>.Fail fail)
            {
                return Outcome<CalibSource>.Failure(fail.Reason);
            }

            cells.Add(((Outcome<CalibCell>.Ok)cell).Value);
        }

        return OnePerKey(cells) is { Length: > 0 } twice
            ? Outcome<CalibSource>.Failure(twice)
            : await AssessmentAsync(source, suite, cells, cancellationToken);
    }

    private static string OnePerKey(IReadOnlyList<CalibCell> cells) =>
        cells.GroupBy(c => (c.Record.Phase, c.Record.Task.Value, c.Definition.Hash, c.Record.Model, c.Record.Repeat, c.Record.Attempt))
            .Where(g => g.Count() > 1)
            .Select(g => $"records {string.Join(", ", g.Select(c => c.Record.Id))} are one cell attempt (phase, task, reviewer, repeat, attempt) — refused rather than one hiding the other")
            .FirstOrDefault(string.Empty);

    private static async Task<Outcome<CalibCell>> CellAsync(
        IImportSource source, GateSuite suite, IReadOnlyDictionary<string, CalibModel> models, CalibRecord record, CancellationToken cancellationToken)
    {
        var task = suite.Task(record.Task, GateKind.Feature);
        var definition = models.TryGetValue(record.Model, out var row)
            ? CalibReviewers.Definition(row, record.Preset).Match(d => Outcome<(CalibModel, ReviewerDefinition)>.Success((row, d)), Outcome<(CalibModel, ReviewerDefinition)>.Failure)
            : Outcome<(CalibModel, ReviewerDefinition)>.Failure($"model '{record.Model}' is not in the calibration models file");
        var reply = await ReplyAsync(source, record, cancellationToken);

        return (task, definition, reply, record.Pin) switch
        {
            (Outcome<GateTask>.Fail f, _, _, _) => Outcome<CalibCell>.Failure($"record {record.Id}: {f.Reason}"),
            (_, Outcome<(CalibModel, ReviewerDefinition)>.Fail f, _, _) => Outcome<CalibCell>.Failure($"record {record.Id}: {f.Reason}"),
            (_, _, Outcome<ParsedReply>.Fail f, _) => Outcome<CalibCell>.Failure($"record {record.Id}: {f.Reason}"),
            (_, _, _, Outcome<ProductPin>.Fail f) => Outcome<CalibCell>.Failure($"record {record.Id}: its product pin — {f.Reason}"),
            (_, Outcome<(CalibModel, ReviewerDefinition)>.Ok d, Outcome<ParsedReply>.Ok r, Outcome<ProductPin>.Ok p) =>
                Outcome<CalibCell>.Success(new CalibCell(record, d.Value.Item1, d.Value.Item2, r.Value, p.Value)),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>The reply the record's findings are read from. Its findings must be as many as the line recorded — a reply
    /// that disagrees with its own run record is refused, because the verdicts index into one of the two.</summary>
    private static async Task<Outcome<ParsedReply>> ReplyAsync(IImportSource source, CalibRecord record, CancellationToken cancellationToken)
    {
        var path = $"{RunsFolder}/{record.Id}/reply.json";
        var bytes = source.Exists(path) ? await source.ReadBytesAsync(path, cancellationToken) : Outcome<byte[]>.Success([]);
        if (bytes is Outcome<byte[]>.Fail unread)
        {
            return Outcome<ParsedReply>.Failure(unread.Reason);
        }

        var reply = GateReplyParser.Parse(Encoding.UTF8.GetString(((Outcome<byte[]>.Ok)bytes).Value));
        var recorded = record.Facts.Findings.WasCaptured ? (int)record.Facts.Findings.Value : 0;

        return reply.Findings.Count == recorded
            ? Outcome<ParsedReply>.Success(reply)
            : Outcome<ParsedReply>.Failure($"its reply carries {reply.Findings.Count} finding(s) and its run record says {recorded}");
    }

    private static async Task<Outcome<CalibSource>> AssessmentAsync(IImportSource source, GateSuite suite, IReadOnlyList<CalibCell> cells, CancellationToken cancellationToken)
    {
        var (hasKey, hasLog) = (source.Exists(KeyFile), source.Exists(AssessFile));
        if (!hasKey && !hasLog)
        {
            return Outcome<CalibSource>.Success(new CalibSource(cells, [], []));
        }

        var key = hasKey
            ? (await source.ReadBytesAsync(KeyFile, cancellationToken)).Match(b => CalibVerdicts.Key(Encoding.UTF8.GetString(b)), Outcome<IReadOnlyList<CalibKeyEntry>>.Failure)
            : Outcome<IReadOnlyList<CalibKeyEntry>>.Failure($"{AssessFile} is there and {KeyFile} is not — a verdict cannot be joined to its finding without the key");
        var log = hasLog ? await source.ReadBytesAsync(AssessFile, cancellationToken) : Outcome<byte[]>.Success([]);
        if (log is Outcome<byte[]>.Fail unread)
        {
            return Outcome<CalibSource>.Failure(unread.Reason);
        }

        var lines = Lines(((Outcome<byte[]>.Ok)log).Value).Select((l, i) => CalibVerdicts.Line(l, i + 1, suite)).ToList();

        return (key, lines.OfType<Outcome<CalibVerdictLine>.Fail>().FirstOrDefault()) switch
        {
            (Outcome<IReadOnlyList<CalibKeyEntry>>.Fail f, _) => Outcome<CalibSource>.Failure(f.Reason),
            (_, { } bad) => Outcome<CalibSource>.Failure(bad.Reason),
            (Outcome<IReadOnlyList<CalibKeyEntry>>.Ok k, _) => Joined(cells, k.Value, CalibVerdicts.Latest(lines.OfType<Outcome<CalibVerdictLine>.Ok>().Select(o => o.Value))),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    /// <summary>Every key entry names a record this import carries and a finding that record has; every verdict names an
    /// id the key holds, of the task the key says.</summary>
    private static Outcome<CalibSource> Joined(IReadOnlyList<CalibCell> cells, IReadOnlyList<CalibKeyEntry> key, IReadOnlyList<CalibVerdictLine> verdicts)
    {
        var byId = cells.ToDictionary(c => c.Record.Id, StringComparer.Ordinal);
        var entries = key.ToDictionary(k => k.Id.Value, StringComparer.Ordinal);
        var strayKey = key.FirstOrDefault(k => !byId.TryGetValue(k.Run, out var c) || k.Index >= c.Reply.Findings.Count || k.Task.Value != c.Record.Task.Value);
        var strayVerdict = verdicts.FirstOrDefault(v => !entries.TryGetValue(v.Row.Id.Value, out var k) || k.Task.Value != v.Row.Task.Value);

        return (strayKey, strayVerdict) switch
        {
            ({ } k, _) => Outcome<CalibSource>.Failure($"key entry {k.Id} names finding {k.Index} of record {k.Run}, which this workspace does not have"),
            (_, { } v) => Outcome<CalibSource>.Failure($"verdict {v.Row.Id} names an id the key does not hold for task {v.Row.Task}"),
            _ => Outcome<CalibSource>.Success(new CalibSource(cells, key, verdicts)),
        };
    }

    private static IReadOnlyList<string> Lines(byte[] bytes) =>
        [.. Encoding.UTF8.GetString(bytes).Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0)];
}
