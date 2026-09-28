using System.Globalization;
using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Application.Gate;

/// <summary>Whose verdicts a hand-check reads: a set of campaigns, one assessor, one rubric.</summary>
public sealed record HandCheckScope(IReadOnlyList<Guid> Campaigns, GateReviewerId Assessor, Rubric Rubric, RubricCatalog Catalog);

/// <summary>The hand-check — measurement rule 2 made into two verbs. <see cref="SampleAsync"/> draws verdicts at random from
/// the scope and writes, into the artefact root, a file a person reads against the code: each row the finding's text,
/// the verdict, the seed hit and the assessor's note, with an empty <c>agree</c> to fill in. <see cref="RecordAsync"/>
/// reads the answered file back and records the counts — but only after checking that every answered row was DRAWN,
/// still shows the verdict as STORED, is answered once, and that at least <see cref="HandCheck.MinVerdicts"/> are. The
/// database keeps the counts and the file's hash; the file stays where it is.</summary>
public sealed class GateHandChecks(
    IGateStore store, IGateArtifactStore artifacts, IGateVerdictStore verdicts, IGateAssessmentFiles files, TimeProvider clock, Random random)
{
    public const string Kind = "bench-gate-hand-check";

    public async Task<Outcome<string>> SampleAsync(HandCheckScope scope, int count, CancellationToken cancellationToken)
    {
        var joined = await JoinedAsync(scope, cancellationToken);
        if (joined is Outcome<IReadOnlyList<Drawable>>.Fail fail)
        {
            return Outcome<string>.Failure(fail.Reason);
        }

        var candidates = ((Outcome<IReadOnlyList<Drawable>>.Ok)joined).Value.ToArray();
        if (candidates.Length < HandCheck.MinVerdicts || count < HandCheck.MinVerdicts)
        {
            return Outcome<string>.Failure(
                $"{candidates.Length} verdict(s) of assessor '{scope.Assessor}' under {scope.Rubric.Stamp} in this scope, and {count} asked — a hand-check reads at least {HandCheck.MinVerdicts}");
        }

        // The campaigns this check can speak for are the ones that HAD verdicts to draw — never every campaign named: one
        // assessed after the draw has had none of its verdicts read by anybody.
        var covered = candidates.Select(d => d.Entry.CampaignId).Distinct().Order().ToList();
        random.Shuffle(candidates);
        var drawn = candidates.Take(count).Select(d => (Drawable: d, Row: Row(d))).ToList();
        var sampleId = $"{clock.GetUtcNow():yyyyMMddHHmmss}-{scope.Assessor}-{random.NextInt64(0, 1L << 32):x8}";

        return Outcome<string>.Success(await files.WriteHandCheckSampleAsync(sampleId, SampleText(sampleId, scope, covered, drawn), DrawnJson(drawn), cancellationToken));
    }

    public async Task<Outcome<HandCheck>> RecordAsync(HandCheckScope scope, string samplePath, CancellationToken cancellationToken)
    {
        var read = await files.ReadHandCheckSampleAsync(samplePath, cancellationToken);
        if (read is not Outcome<(string Sample, string Drawn, string Sha256)>.Ok { Value: var sample })
        {
            return Outcome<HandCheck>.Failure(((Outcome<(string, string, string)>.Fail)read).Reason);
        }

        var (covered, header) = Header(sample.Sample, scope);
        if (header.Length > 0)
        {
            return Outcome<HandCheck>.Failure(header);
        }

        var joined = await JoinedAsync(scope with { Campaigns = covered }, cancellationToken);
        if (joined is Outcome<IReadOnlyList<Drawable>>.Fail fail)
        {
            return Outcome<HandCheck>.Failure(fail.Reason);
        }

        var stored = ((Outcome<IReadOnlyList<Drawable>>.Ok)joined).Value.ToDictionary(d => (d.Entry.Id.Value, d.Verdict.BatchId));
        var truths = Drawn(sample.Drawn).Where(d => stored.ContainsKey((d.Id, d.Batch))).Select(d => Truth(stored[(d.Id, d.Batch)], d.Evidence)).ToList();

        return await HandCheckAnswers.Verify(Answers(sample.Sample), truths).Match(
            async counts => await verdicts.RecordHandCheckAsync(
                HandCheck.Of(covered, scope.Rubric, scope.Assessor, counts.Read, counts.Agreed, sample.Sha256, clock.GetUtcNow())
                    .Match(c => c, reason => throw new InvalidOperationException(reason)),
                cancellationToken),
            reason => Task.FromResult(Outcome<HandCheck>.Failure(reason)));
    }

    /// <summary>A stored verdict with everything a person needs to check it: its key entry, its log line (the note, the
    /// cluster), the finding's text.</summary>
    private sealed record Drawable(BlindKeyEntry Entry, GateVerdict Verdict, VerdictLine Line, string FindingJson);

    private async Task<Outcome<IReadOnlyList<Drawable>>> JoinedAsync(HandCheckScope scope, CancellationToken cancellationToken)
    {
        var key = await files.ReadKeyAsync(cancellationToken);
        var texts = await GateFindingTexts.ReadAsync(store, artifacts, scope.Campaigns, cancellationToken);

        if ((key, texts) is not (Outcome<IReadOnlyList<BlindKeyEntry>>.Ok { Value: var entries }, Outcome<IReadOnlyList<FindingToAssess>>.Ok { Value: var findings }))
        {
            return Outcome<IReadOnlyList<Drawable>>.Failure(key is Outcome<IReadOnlyList<BlindKeyEntry>>.Fail k ? k.Reason : ((Outcome<IReadOnlyList<FindingToAssess>>.Fail)texts).Reason);
        }

        var byFinding = entries.ToDictionary(e => (e.RunId, e.Ordinal));
        var textOf = findings.ToDictionary(f => f.Key, f => f.FindingJson);
        var lines = (await files.ReadVerdictLinesAsync(cancellationToken)).GroupBy(l => (l.Id, l.BatchId)).ToDictionary(g => g.Key, g => g.Last());
        var stored = await verdicts.VerdictsAsync([.. findings.Select(f => f.RunId).Distinct()], scope.Catalog, cancellationToken);

        return Outcome<IReadOnlyList<Drawable>>.Success(
            [.. stored
                .Where(v => v.Assessor == scope.Assessor && v.Rubric == scope.Rubric && v.Reading.CountsInRates)
                .Where(v => byFinding.ContainsKey((v.RunId, v.FindingOrdinal)) && textOf.ContainsKey((v.RunId, v.FindingOrdinal)))
                .Select(v => (Verdict: v, Entry: byFinding[(v.RunId, v.FindingOrdinal)]))
                .Where(p => lines.ContainsKey((p.Entry.Id.Value, p.Verdict.BatchId)))
                .Select(p => new Drawable(p.Entry, p.Verdict, lines[(p.Entry.Id.Value, p.Verdict.BatchId)], textOf[(p.Verdict.RunId, p.Verdict.FindingOrdinal)]))]);
    }

    /// <summary>A drawn verdict as it is STORED now, with the evidence hash the draw took of its row.</summary>
    private static HandCheckTruth Truth(Drawable d, string evidence) => new(d.Entry.Id, d.Entry.CampaignId, d.Verdict.BatchId, ReadingWord(d.Verdict.Reading), evidence);

    /// <summary>The hash of everything a row SHOWS — all of it but the person's own answer and comment. Re-serialised, so an
    /// editor that re-indents the line changes nothing; any changed value does.</summary>
    private static string Evidence(JsonObject row)
    {
        var shown = (JsonObject)row.DeepClone();
        shown.Remove("agree");
        shown.Remove("comment");
        return StableHash.Of(shown.ToJsonString());
    }

    private static string ReadingWord(Verdict verdict) => verdict is Verdict.Strict s ? s.Reading.ToString().ToLowerInvariant() : verdict.GetType().Name;

    private static string SampleText(string sampleId, HandCheckScope scope, IReadOnlyList<Guid> covered, IReadOnlyList<(Drawable Drawable, JsonObject Row)> drawn)
    {
        var header = new JsonObject
        {
            ["kind"] = Kind,
            ["sample"] = sampleId,
            ["rubric"] = scope.Rubric.Hash,
            ["rubricId"] = scope.Rubric.Id.Value,
            ["assessor"] = scope.Assessor.Value,
            ["campaigns"] = new JsonArray([.. covered.Select(c => (JsonNode)c.ToString("D"))]),
            ["instructions"] = "Read each finding against the code at its head. Set \"agree\" to true when the verdict, the seed hit and the value are right, "
                               + "false when any is wrong, and say why in \"comment\". Leave the other fields as they are.",
        };

        return string.Join('\n', [header.ToJsonString(), .. drawn.Select(d => d.Row.ToJsonString())]) + "\n";
    }

    private static JsonObject Row(Drawable d)
    {
        var finding = Json(d.FindingJson) as JsonObject ?? [];

        return new JsonObject
        {
            ["id"] = d.Entry.Id.Value,
            ["batch"] = d.Verdict.BatchId,
            ["task"] = d.Entry.Task.Value,
            ["verdict"] = ReadingWord(d.Verdict.Reading),
            ["value"] = d.Line.Value,
            ["severity_fair"] = d.Line.SeverityFair,
            ["grounded"] = d.Line.Grounded,
            ["seed_hit"] = d.Line.SeedHit,
            ["cluster"] = d.Line.Cluster,
            ["note"] = d.Line.Note,
            ["finding"] = finding.DeepClone(),
            ["agree"] = null,
            ["comment"] = string.Empty,
        };
    }

    private static string DrawnJson(IReadOnlyList<(Drawable Drawable, JsonObject Row)> drawn) =>
        new JsonArray([.. drawn.Select(d => (JsonNode)new JsonObject
        {
            ["id"] = d.Drawable.Entry.Id.Value,
            ["batch"] = d.Drawable.Verdict.BatchId,
            ["evidence"] = Evidence(d.Row),
        })]).ToJsonString();

    private static IReadOnlyList<(string Id, string Batch, string Evidence)> Drawn(string json) =>
        Json(json) is JsonArray rows
            ? [.. rows.OfType<JsonObject>().Select(r => (Str(r, "id"), Str(r, "batch"), Str(r, "evidence")))]
            : [];

    private static IReadOnlyList<HandCheckAnswer> Answers(string sample) =>
        [.. sample.Split('\n').Skip(1).Where(l => l.Trim().Length > 0).Select(Answer).SelectMany(a => a)];

    private static IReadOnlyList<HandCheckAnswer> Answer(string line)
    {
        if (Json(line) is not JsonObject o || BlindedId.Parse(Str(o, "id")) is not Outcome<BlindedId>.Ok { Value: var id })
        {
            return [];
        }

        var agree = o["agree"] is JsonValue v && v.TryGetValue<bool>(out var flag) ? (true, flag) : (false, false);

        return [new HandCheckAnswer(id, Str(o, "batch"), Str(o, "verdict"), Evidence(o), agree.Item1, agree.Item2)];
    }

    /// <summary>The campaigns the sample covers (those that had verdicts to draw), or a refusal: not this benchmark's file,
    /// another rubric or assessor, or campaigns outside the ones named.</summary>
    private static (IReadOnlyList<Guid> Covered, string Refusal) Header(string sample, HandCheckScope scope)
    {
        var header = Json(sample.Split('\n')[0]) as JsonObject ?? [];
        var campaigns = (header["campaigns"] as JsonArray ?? [])
            .Select(c => c is JsonValue v && v.TryGetValue<string>(out var s) && Guid.TryParse(s, CultureInfo.InvariantCulture, out var g) ? g : Guid.Empty)
            .ToList();

        var refusal = (Str(header, "kind") == Kind, Str(header, "rubric") == scope.Rubric.Hash, Str(header, "assessor") == scope.Assessor.Value,
                campaigns.Count > 0 && campaigns.All(scope.Campaigns.Contains)) switch
        {
            (false, _, _, _) => "the file is not a hand-check sample this benchmark wrote",
            (_, false, _, _) => $"the sample was drawn under another rubric than {scope.Rubric.Stamp}",
            (_, _, false, _) => $"the sample was drawn from another assessor's verdicts than '{scope.Assessor}'",
            (_, _, _, false) => "the sample covers campaigns outside the ones named — record it with the same --run/--scope it was sampled with",
            _ => string.Empty,
        };

        return (campaigns, refusal);
    }

    private static JsonNode? Json(string line)
    {
        try
        {
            return JsonNode.Parse(line);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Str(JsonObject o, string name) => o[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
}
