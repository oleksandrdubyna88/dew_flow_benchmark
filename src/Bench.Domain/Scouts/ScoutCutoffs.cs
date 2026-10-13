using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bench.Domain.Scouts;

/// <summary>One resolved model id's published training-data cut-off (§3.1): the date, where the vendor published it, and the date it
/// was read. Evidence, not memory.</summary>
public sealed record ModelCutoff(string ModelId, DateOnly Cutoff, string SourceUrl, DateOnly ReadOn);

/// <summary><c>cutoffs.json</c> (§3.1, §3.12) — committed BEFORE the candidate list, and the only door to it:
/// <code>{ "models": [ { "modelId": "claude-opus-5-5", "cutoff": "2026-03-31", "source": "https://…", "readOn": "2026-10-12" } ] }</code>
/// A cut-off that is unpublished (empty or absent) or ambiguous for that exact id — the id missing, or listed twice — STOPS the
/// selection: it is never guessed, because a model that saw the code in training gains recall that says nothing about scouting.</summary>
public sealed record ScoutCutoffs(IReadOnlyList<ModelCutoff> Models, IReadOnlyList<string> Unpublished)
{
    public static Outcome<ScoutCutoffs> Parse(string json)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return Outcome<ScoutCutoffs>.Failure($"the cut-offs file is not JSON — {ex.Message}");
        }

        return root?["models"] is JsonArray models
            ? Entries(models)
            : Outcome<ScoutCutoffs>.Failure("the cut-offs file has no \"models\" array");
    }

    /// <summary>The LATER of the two resolved ids' cut-offs — what a candidate's P must lie after. Refused, naming the id, when either id
    /// has no published cut-off, is listed twice, or is missing.</summary>
    public Outcome<DateOnly> LaterOf(string opusId, string sonnetId) =>
        Of(opusId).Match(
            opus => Of(sonnetId).Match(
                sonnet => Outcome<DateOnly>.Success(opus.Cutoff > sonnet.Cutoff ? opus.Cutoff : sonnet.Cutoff),
                Outcome<DateOnly>.Failure),
            Outcome<DateOnly>.Failure);

    public Outcome<ModelCutoff> Of(string modelId)
    {
        var found = Models.Where(m => string.Equals(m.ModelId, modelId, StringComparison.Ordinal)).ToList();

        return (Unpublished.Contains(modelId, StringComparer.Ordinal), found.Count) switch
        {
            (true, _) => Outcome<ModelCutoff>.Failure($"'{modelId}' has no published cut-off in the cut-offs file — the selection stops; a cut-off is never guessed"),
            (_, 0) => Outcome<ModelCutoff>.Failure($"'{modelId}' is not in the cut-offs file — record its published cut-off (date, source url, date read) first; the selection stops"),
            (_, 1) => Outcome<ModelCutoff>.Success(found[0]),
            _ => Outcome<ModelCutoff>.Failure($"'{modelId}' is listed {found.Count} times in the cut-offs file — an ambiguous cut-off stops the selection"),
        };
    }

    private static Outcome<ScoutCutoffs> Entries(JsonArray models)
    {
        var read = models.Select(Entry).ToList();

        return read.OfType<Outcome<CutoffEntry>.Fail>().FirstOrDefault() is { } bad
            ? Outcome<ScoutCutoffs>.Failure(bad.Reason)
            : Outcome<ScoutCutoffs>.Success(Split([.. read.OfType<Outcome<CutoffEntry>.Ok>().Select(o => o.Value)]));
    }

    private static ScoutCutoffs Split(IReadOnlyList<CutoffEntry> entries) =>
        new([.. entries.OfType<CutoffEntry.Published>().Select(e => e.Cutoff)], [.. entries.OfType<CutoffEntry.NotPublished>().Select(e => e.ModelId)]);

    /// <summary>One entry: a published cut-off, or the id of one that is NOT published (an empty or absent <c>cutoff</c>) — kept, so a
    /// lookup of that id says "unpublished" rather than "missing".</summary>
    private static Outcome<CutoffEntry> Entry(JsonNode? node)
    {
        var id = Text(node, "modelId");
        var cutoff = Text(node, "cutoff");

        return (id.Length > 0, cutoff.Length > 0) switch
        {
            (false, _) => Outcome<CutoffEntry>.Failure("a cut-offs entry has no \"modelId\" — the exact resolved id it is for"),
            (_, false) => Outcome<CutoffEntry>.Success(new CutoffEntry.NotPublished(id)),
            _ => Published(id, cutoff, Text(node, "source"), Text(node, "readOn")),
        };
    }

    private static Outcome<CutoffEntry> Published(string id, string cutoff, string source, string readOn) =>
        (Date(cutoff), source.StartsWith("https://", StringComparison.Ordinal), Date(readOn)) switch
        {
            (Outcome<DateOnly>.Fail, _, _) => Outcome<CutoffEntry>.Failure($"'{id}': cutoff '{cutoff}' is not a date (yyyy-MM-dd)"),
            (_, false, _) => Outcome<CutoffEntry>.Failure($"'{id}': a published cut-off names where it was published — an https \"source\""),
            (_, _, Outcome<DateOnly>.Fail) => Outcome<CutoffEntry>.Failure($"'{id}': \"readOn\" '{readOn}' is not a date (yyyy-MM-dd) — the date the source was read is part of the evidence"),
            (Outcome<DateOnly>.Ok c, _, Outcome<DateOnly>.Ok r) => Outcome<CutoffEntry>.Success(new CutoffEntry.Published(new ModelCutoff(id, c.Value, source, r.Value))),
            _ => Outcome<CutoffEntry>.Failure("unreachable"),
        };

    private static Outcome<DateOnly> Date(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Outcome<DateOnly>.Success(date)
            : Outcome<DateOnly>.Failure(text);

    private abstract record CutoffEntry
    {
        private CutoffEntry()
        {
        }

        public sealed record Published(ModelCutoff Cutoff) : CutoffEntry;

        public sealed record NotPublished(string ModelId) : CutoffEntry;
    }

    private static string Text(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;
}
