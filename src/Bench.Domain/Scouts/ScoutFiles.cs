using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>The scouts' operator files, read and written in ONE place, each a refusal by name when it is malformed:
/// <list type="bullet">
/// <item><b>candidates</b> (<c>bench scouts tasks --candidates</c>) — <c>{ "candidates": [ { "id", "repository", "parent", "fix", "kind",
/// "statement", "planFile", "private", "language", "mergeStyle", "number", "baseRefOid", "baseRefSource" } ] }</c>; a PRIVATE candidate's id is
/// its label (<c>"label"</c> may stand in for <c>"id"</c>) and its refusals name nothing it holds; a <c>pr-squash</c> or <c>pr-rebase</c>
/// candidate names its PR <c>number</c>, a <c>pr-rebase</c> one its <c>baseRefOid</c> with its source, a <c>local-squash</c> one no number (S7);</item>
/// <item><b>tasks</b> (<c>tasks.json</c>, what <c>tasks</c> writes and <c>run</c> reads; <c>pilot-tasks.json</c> for the pilot) — the frozen tasks
/// through the one projection (<see cref="ScoutPublication.TaskJson"/>), each with its ROLE (main, reserve, pilot): a public task whole, with
/// its added identifiers so the leak check runs again on the way in; a private task as label, language, sizes and commitments — the file is
/// committed to <c>research/</c>; it carries the campaign string, which IS the draw's seed (S7);</item>
/// <item><b>private tasks</b> (<c>tasks.private.json</c>, refused inside git) — the private tasks' detail, what <c>run</c> re-derives the
/// commitments from (<see cref="Resolve"/>);</item>
/// <item><b>prices</b> (<c>--prices</c>) — <c>{ "source": "https://…", "readOn": "yyyy-MM-dd", "models": [ { "modelId", "input", "output",
/// "cacheRead", "cacheWrite5m", "cacheWrite1h" } ] }</c>, USD per million tokens.</item>
/// </list></summary>
public static class ScoutFiles
{
    public const string PrivateTasksFlag = "--private-tasks-file";

    public static string KindWord(ScoutQuestionKind kind) => kind switch
    {
        ScoutQuestionKind.WhereDecided => "where-decided",
        ScoutQuestionKind.HowEndToEnd => "how-end-to-end",
        _ => "what-must-change",
    };

    public static Outcome<ScoutQuestionKind> Kind(string word) =>
        Enum.GetValues<ScoutQuestionKind>().Where(k => KindWord(k) == word.Trim()).Select(Outcome<ScoutQuestionKind>.Success).DefaultIfEmpty(
            Outcome<ScoutQuestionKind>.Failure($"'{word}' is not a question kind — where-decided, how-end-to-end or what-must-change")).First();

    public static Outcome<IReadOnlyList<ScoutCandidate>> Candidates(string json) =>
        Array(json, "candidates").Match(items => Collect(items.Select(Candidate)), Outcome<IReadOnlyList<ScoutCandidate>>.Failure);

    private static Outcome<ScoutCandidate> Candidate(JsonNode? node)
    {
        var label = Text(node, "id").Length > 0 ? Text(node, "id") : Text(node, "label");
        var id = ScoutTaskId.Parse(label);
        var privacy = Flag(node, "private") ? ScoutTaskPrivacy.Private : ScoutTaskPrivacy.Public;

        return (id, CommitSha.Parse(Text(node, "parent")), CommitSha.Parse(Text(node, "fix")), Kind(Text(node, "kind"))) switch
        {
            (Outcome<ScoutTaskId>.Fail f, _, _, _) => Outcome<ScoutCandidate>.Failure(privacy == ScoutTaskPrivacy.Private ? $"candidate '{label}' {ScoutPublication.PrivateMark}: its id is not a label" : f.Reason),
            (_, Outcome<CommitSha>.Fail f, _, _) => Outcome<ScoutCandidate>.Failure(Malformed(label, privacy, "parent", f.Reason)),
            (_, _, Outcome<CommitSha>.Fail f, _) => Outcome<ScoutCandidate>.Failure(Malformed(label, privacy, "fix", f.Reason)),
            (_, _, _, Outcome<ScoutQuestionKind>.Fail f) => Outcome<ScoutCandidate>.Failure($"candidate {label}: {f.Reason}"),
            (Outcome<ScoutTaskId>.Ok i, Outcome<CommitSha>.Ok p, Outcome<CommitSha>.Ok s, Outcome<ScoutQuestionKind>.Ok k) => Classified(
                new ScoutCandidate(i.Value, Text(node, "repository"), p.Value, s.Value, k.Value, Text(node, "statement"), Text(node, "planFile"))
                { Privacy = privacy, Language = Text(node, "language") }).Match(c => Labelled(c, node).Match(l => Landed(l, node), Outcome<ScoutCandidate>.Failure), Outcome<ScoutCandidate>.Failure),
            _ => Outcome<ScoutCandidate>.Failure("unreachable"),
        };
    }

    /// <summary>A malformed sha of a public candidate is named with its value; of a private candidate, by field only.</summary>
    private static string Malformed(string label, ScoutTaskPrivacy privacy, string field, string reason) =>
        privacy == ScoutTaskPrivacy.Private ? $"candidate {label} {ScoutPublication.PrivateMark}: its {field} is not a commit sha" : $"candidate {label}: {field} — {reason}";

    private static Outcome<ScoutCandidate> Classified(ScoutCandidate candidate) =>
        ScoutTaskPrivacies.Refusal(candidate.Privacy, candidate.Id.Value, candidate.Language) is { Length: > 0 } refusal
            ? Outcome<ScoutCandidate>.Failure($"candidate {refusal["task ".Length..]}")
            : Outcome<ScoutCandidate>.Success(candidate);

    /// <summary>S7: <c>"label"</c> may stand in for a private candidate's <c>"id"</c> (S4: the id IS the label); both present and different is a
    /// contradiction the file must resolve.</summary>
    private static Outcome<ScoutCandidate> Labelled(ScoutCandidate candidate, JsonNode? node) =>
        Text(node, "label") is { Length: > 0 } label && label != candidate.Id.Value
            ? Outcome<ScoutCandidate>.Failure($"candidate {candidate.Id}: its \"label\" '{label}' is not its id — a candidate's id IS its label")
            : Outcome<ScoutCandidate>.Success(candidate);

    /// <summary>S7: how the fix landed, and what that shape requires — a PR number for a PR, none for a local commit, a base ref for a
    /// rebase-merged PR. A private candidate's malformed base ref is named by field only.</summary>
    private static Outcome<ScoutCandidate> Landed(ScoutCandidate candidate, JsonNode? node)
    {
        var style = ScoutMergeStyles.Parse(Text(node, "mergeStyle"));
        var baseRef = BaseRef(candidate, node);

        return (style, baseRef) switch
        {
            (Outcome<ScoutMergeStyle>.Fail f, _) => Outcome<ScoutCandidate>.Failure($"candidate {candidate.Id}: \"mergeStyle\" — {f.Reason}"),
            (_, Outcome<ScoutBaseRef>.Fail f) => Outcome<ScoutCandidate>.Failure(f.Reason),
            (Outcome<ScoutMergeStyle>.Ok s, Outcome<ScoutBaseRef>.Ok b) => Keyed(candidate with { MergeStyle = s.Value, Number = Int(node, "number"), BaseRef = b.Value }),
            _ => Outcome<ScoutCandidate>.Failure("unreachable"),
        };
    }

    private static Outcome<ScoutBaseRef> BaseRef(ScoutCandidate candidate, JsonNode? node) =>
        Text(node, "baseRefOid") switch
        {
            { Length: 0 } => Outcome<ScoutBaseRef>.Success(ScoutBaseRef.None),
            var oid => CommitSha.Parse(oid).Match(
                sha => Outcome<ScoutBaseRef>.Success(new ScoutBaseRef(sha.Value, Text(node, "baseRefSource"))),
                reason => Outcome<ScoutBaseRef>.Failure(Malformed(candidate.Id.Value, candidate.Privacy, "baseRefOid", reason))),
        };

    private static Outcome<ScoutCandidate> Keyed(ScoutCandidate c) =>
        (ScoutMergeStyles.IsPullRequest(c.MergeStyle) && !c.IsPullRequest, c.MergeStyle == ScoutMergeStyle.LocalSquash && c.IsPullRequest, c.MergeStyle == ScoutMergeStyle.PrRebase && !c.BaseRef.IsRecorded) switch
        {
            (true, _, _) => Outcome<ScoutCandidate>.Failure($"candidate {c.Id}: a {ScoutMergeStyles.Word(c.MergeStyle)} candidate is a GitHub PR and names its \"number\" — its key is repo#number"),
            (_, true, _) => Outcome<ScoutCandidate>.Failure($"candidate {c.Id}: a local-squash candidate is a local commit and has no PR \"number\" — its key is repo@fixsha"),
            (_, _, true) => Outcome<ScoutCandidate>.Failure($"candidate {c.Id}: a pr-rebase candidate records \"baseRefOid\" (the PR's base, which P must be) and \"baseRefSource\""),
            _ => Outcome<ScoutCandidate>.Success(c),
        };

    /// <summary>One frozen task and the identifiers its diff added — what <c>tasks.json</c> carries per public task; a private task's go to
    /// the private file. <paramref name="GroundTruthFileCommitments"/> are a private task's per-file commitments, in G's committed order;
    /// <see cref="Role"/> is what the draw made it (S7).</summary>
    public sealed record FrozenTask(ScoutTask Task, IReadOnlyList<string> AddedIdentifiers)
    {
        public IReadOnlyList<string> GroundTruthFileCommitments { get; init; } = [];

        public ScoutTaskRole Role { get; init; } = ScoutTaskRole.Main;
    }

    /// <summary>The tasks of a <c>tasks.json</c> that holds PUBLIC tasks only — the ones that RUN: a reserve stays in the file for the operator and
    /// is never planned (S7). A private entry refuses the read naming its label and the flag that brings the private detail — <see cref="Resolve"/>
    /// is the door for a file holding one.</summary>
    public static Outcome<IReadOnlyList<ScoutTask>> Tasks(string json) =>
        Entries(json).Match(entries => entries.Any(e => e.IsPrivate)
            ? Outcome<IReadOnlyList<ScoutTask>>.Failure(
                $"task {entries.First(e => e.IsPrivate).Id} is private — pass {PrivateTasksFlag} <tasks.private.json> (outside git) and the campaign key; its public record holds commitments, not the task")
            : Collect(entries.Where(e => e.Runs).Select(e => e.Task)), Outcome<IReadOnlyList<ScoutTask>>.Failure);

    /// <summary>The campaign's seed string a <c>tasks.json</c> was written under; empty when it names none.</summary>
    public static string Campaign(string json) => Text(Root(json), "campaign");

    /// <summary>One entry of <c>tasks.json</c>: a public task frozen again, or a private task's label with its commitments; and its role.</summary>
    public sealed record PublicEntry(string Id, bool IsPrivate, Outcome<ScoutTask> Task, string Language, ScoutTaskCommitments Commitments, string TreeHash)
    {
        public ScoutTaskRole Role { get; init; } = ScoutTaskRole.Main;

        /// <summary>Main and pilot tasks run; a reserve waits.</summary>
        public bool Runs => Role != ScoutTaskRole.Reserve;
    }

    public static Outcome<IReadOnlyList<PublicEntry>> Entries(string json) =>
        Array(json, "tasks").Match(items => Collect(items.Select(Entry)), Outcome<IReadOnlyList<PublicEntry>>.Failure);

    private static Outcome<PublicEntry> Entry(JsonNode? node)
    {
        var id = Text(node, "id");
        var isPrivate = Text(node, "privacy") == ScoutTaskPrivacies.PrivateWord;
        var commitments = new ScoutTaskCommitments(Text(node, "statementCommitment"), Text(node, "groundTruthCommitment"));

        return ScoutTaskRoles.Parse(Text(node, "role")).Match(
            role => Outcome<PublicEntry>.Success(new PublicEntry(id, isPrivate, isPrivate ? Outcome<ScoutTask>.Failure($"task {id} is private") : TaskOf(node, ScoutTaskPrivacy.Public), Text(node, "language"), commitments, Text(node, "treeHash")) { Role = role }),
            reason => Outcome<PublicEntry>.Failure($"task {id}: {reason}"));
    }

    private static Outcome<ScoutTask> TaskOf(JsonNode? node, ScoutTaskPrivacy privacy)
    {
        var id = ScoutTaskId.Parse(Text(node, "id"));

        return (id, CommitSha.Parse(Text(node, "parent")), CommitSha.Parse(Text(node, "fix")), Kind(Text(node, "kind"))) switch
        {
            (Outcome<ScoutTaskId>.Ok i, Outcome<CommitSha>.Ok p, Outcome<CommitSha>.Ok s, Outcome<ScoutQuestionKind>.Ok k) => ScoutTask.Frozen(
                i.Value, Text(node, "repository"), p.Value, s.Value, k.Value, Text(node, "statement"),
                Texts(node, "groundTruth"), Texts(node, "addedIdentifiers"), Texts(node, "removals"), Text(node, "treeHash"), privacy, Text(node, "language")),
            _ => Outcome<ScoutTask>.Failure($"task '{Text(node, "id")}' has a malformed id, parent, fix or kind"),
        };
    }

    /// <summary>The tasks of a <c>tasks.json</c> together with the PRIVATE detail: every private entry must have its task in the private file,
    /// and the task's commitments re-derived under <paramref name="committer"/> must equal the public ones — a mismatch is refused by LABEL.
    /// A private task in the private file that the public file does not name is refused too: the public file is the pre-registration.</summary>
    public static Outcome<IReadOnlyList<ScoutTask>> Resolve(string publicJson, string privateJson, ScoutCommitter committer)
    {
        var entries = Entries(publicJson);
        var detail = PrivateTasks(privateJson);

        return (entries, detail) switch
        {
            (Outcome<IReadOnlyList<PublicEntry>>.Fail f, _) => Outcome<IReadOnlyList<ScoutTask>>.Failure(f.Reason),
            (_, Outcome<IReadOnlyList<FrozenTask>>.Fail f) => Outcome<IReadOnlyList<ScoutTask>>.Failure($"the private tasks file: {f.Reason}"),
            (Outcome<IReadOnlyList<PublicEntry>>.Ok e, Outcome<IReadOnlyList<FrozenTask>>.Ok d) => Resolved(e.Value, d.Value, committer),
            _ => Outcome<IReadOnlyList<ScoutTask>>.Failure("unreachable"),
        };
    }

    /// <summary>Every listed private entry is verified, reserves included (a reserve's commitment is part of the pre-registration); only the
    /// entries that run are answered.</summary>
    private static Outcome<IReadOnlyList<ScoutTask>> Resolved(IReadOnlyList<PublicEntry> entries, IReadOnlyList<FrozenTask> detail, ScoutCommitter committer)
    {
        var listed = entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var stray = detail.Select(d => d.Task.Id.Value).FirstOrDefault(id => !listed.Contains(id));

        return stray is null
            ? Collect(entries.Select(e => Paired(e, detail, committer))).Match(
                all => Outcome<IReadOnlyList<ScoutTask>>.Success([.. all.Where(t => t.Entry.Runs).Select(t => t.Task)]), Outcome<IReadOnlyList<ScoutTask>>.Failure)
            : Outcome<IReadOnlyList<ScoutTask>>.Failure($"the private tasks file names task {stray}, which tasks.json does not — the public file is the pre-registration");
    }

    private static Outcome<(PublicEntry Entry, ScoutTask Task)> Paired(PublicEntry entry, IReadOnlyList<FrozenTask> detail, ScoutCommitter committer) =>
        (entry.IsPrivate ? Private(entry, detail, committer) : entry.Task).Match(
            task => Outcome<(PublicEntry, ScoutTask)>.Success((entry, task)), Outcome<(PublicEntry, ScoutTask)>.Failure);

    private static Outcome<ScoutTask> Private(PublicEntry entry, IReadOnlyList<FrozenTask> detail, ScoutCommitter committer) =>
        detail.FirstOrDefault(d => d.Task.Id.Value == entry.Id) is { } found
            ? committer.Verified(found.Task with { Commitments = entry.Commitments })
            : Outcome<ScoutTask>.Failure($"task {entry.Id} {ScoutPublication.PrivateMark} has no entry in the private tasks file — tasks.json holds its commitments only");

    /// <summary>The private file's tasks: each frozen again (its shape, its leak check), with its added identifiers.</summary>
    public static Outcome<IReadOnlyList<FrozenTask>> PrivateTasks(string json) =>
        Array(json, "tasks").Match(
            items => Collect(items.Select(node => TaskOf(node, ScoutTaskPrivacy.Private).Match(
                task => Outcome<FrozenTask>.Success(new FrozenTask(task, Texts(node, "addedIdentifiers"))),
                Outcome<FrozenTask>.Failure))),
            Outcome<IReadOnlyList<FrozenTask>>.Failure);

    /// <summary><c>tasks.json</c> (or <c>pilot-tasks.json</c>) — every task through the projection, under the campaign string that seeded the draw.</summary>
    public static string WriteTasks(IReadOnlyList<FrozenTask> tasks, string campaign, string cutoffsNote) =>
        new JsonObject
        {
            ["campaign"] = campaign,
            ["cutoffs"] = cutoffsNote,
            ["tasks"] = new JsonArray([.. tasks.Select(t => (JsonNode)ScoutPublication.TaskJson(t.Task, t.AddedIdentifiers, t.GroundTruthFileCommitments, t.Role))]),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

    /// <summary><c>tasks.private.json</c> — the PRIVATE tasks whole, for a file that never enters git.</summary>
    public static string WritePrivateTasks(IReadOnlyList<FrozenTask> tasks) =>
        new JsonObject
        {
            ["warning"] = "PRIVATE — never commit this file; tasks.json carries these tasks' commitments only",
            ["tasks"] = new JsonArray([.. tasks.Where(t => t.Task.IsPrivate).Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.Task.Id.Value,
                ["role"] = ScoutTaskRoles.Word(t.Role),
                ["privacy"] = ScoutTaskPrivacies.Word(t.Task.Privacy),
                ["language"] = t.Task.Language,
                ["repository"] = t.Task.Repository,
                ["parent"] = t.Task.Parent.Value,
                ["fix"] = t.Task.Fix.Value,
                ["kind"] = KindWord(t.Task.Kind),
                ["statement"] = t.Task.Statement,
                ["groundTruth"] = Array(t.Task.GroundTruth),
                ["addedIdentifiers"] = Array(t.AddedIdentifiers),
                ["removals"] = Array(t.Task.Removals),
                ["treeHash"] = t.Task.TreeHash,
            })]),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

    /// <summary><c>schedule.json</c> (S6) — <c>{ "campaign", "fromPilotRun", "budgets": [ { "arm", "capUsd", "wallSeconds" } ] }</c>.</summary>
    public static string WriteSchedule(ScoutScheduleFile schedule) =>
        new JsonObject
        {
            ["campaign"] = schedule.Campaign,
            ["fromPilotRun"] = schedule.FromRun,
            ["budgets"] = new JsonArray([.. schedule.Budgets.Select(b => (JsonNode)new JsonObject
            {
                ["arm"] = ScoutArms.Word(b.Arm),
                ["capUsd"] = b.CapUsd,
                ["wallSeconds"] = b.WallSeconds,
            })]),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

    public static Outcome<ScoutScheduleFile> Schedule(string json) =>
        Array(json, "budgets").Match(
            items => Collect(items.Select(Budget)).Match(
                budgets => Outcome<ScoutScheduleFile>.Success(new ScoutScheduleFile(Text(Root(json), "campaign"), Text(Root(json), "fromPilotRun"), budgets)),
                Outcome<ScoutScheduleFile>.Failure),
            Outcome<ScoutScheduleFile>.Failure);

    private static Outcome<ScoutArmBudget> Budget(JsonNode? node) =>
        ScoutArms.Parse(Text(node, "arm")).Match(
            arm => ScoutArmBudget.Of(arm.Id, Number(node, "capUsd"), Int(node, "wallSeconds")),
            reason => Outcome<ScoutArmBudget>.Failure($"schedule.json: {reason}"));

    public static Outcome<PriceTable> Prices(string json)
    {
        var models = Array(json, "models");
        var root = Root(json);
        var readOn = DateOnly.TryParseExact(Text(root, "readOn"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);

        return (models, readOn) switch
        {
            (Outcome<IReadOnlyList<JsonNode?>>.Fail f, _) => Outcome<PriceTable>.Failure(f.Reason),
            (_, false) => Outcome<PriceTable>.Failure("the price table has no \"readOn\" date (yyyy-MM-dd) — the date it was read is part of every cost"),
            (Outcome<IReadOnlyList<JsonNode?>>.Ok ok, _) => Collect(ok.Value.Select(Price)).Match(prices => PriceTable.Of(Text(root, "source"), date, prices), Outcome<PriceTable>.Failure),
            _ => Outcome<PriceTable>.Failure("unreachable"),
        };
    }

    private static Outcome<ModelPrice> Price(JsonNode? node) =>
        ModelPrice.Of(Text(node, "modelId"), Number(node, "input"), Number(node, "output"), Number(node, "cacheRead"), Number(node, "cacheWrite5m"), Number(node, "cacheWrite1h"));

    private static Outcome<IReadOnlyList<T>> Collect<T>(IEnumerable<Outcome<T>> items)
    {
        var all = items.ToList();

        return all.OfType<Outcome<T>.Fail>().FirstOrDefault() is { } fail
            ? Outcome<IReadOnlyList<T>>.Failure(fail.Reason)
            : Outcome<IReadOnlyList<T>>.Success([.. all.OfType<Outcome<T>.Ok>().Select(o => o.Value)]);
    }

    private static JsonNode? Root(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Outcome<IReadOnlyList<JsonNode?>> Array(string json, string name) =>
        Root(json)?[name] is JsonArray items
            ? Outcome<IReadOnlyList<JsonNode?>>.Success([.. items])
            : Outcome<IReadOnlyList<JsonNode?>>.Failure($"the file is not JSON with a \"{name}\" array");

    private static JsonArray Array(IReadOnlyList<string> values) => new([.. values.Select(v => (JsonNode)JsonValue.Create(v))]);

    private static string Text(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty;

    private static bool Flag(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static int Int(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : 0;

    private static IReadOnlyList<string> Texts(JsonNode? node, string name) =>
        node?[name] is JsonArray items ? [.. items.Select(i => i is JsonValue v && v.TryGetValue<string>(out var t) ? t.Trim() : string.Empty).Where(t => t.Length > 0)] : [];

    private static decimal Number(JsonNode? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : -1m;
}
