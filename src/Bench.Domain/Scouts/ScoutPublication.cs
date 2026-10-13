using System.Text.Json.Nodes;
using Bench.Domain.Targets;

namespace Bench.Domain.Scouts;

/// <summary>THE public projection of a task (plan §3.13, E2 S4) — the one door every public writer goes through: <c>tasks.json</c>, the report
/// (its JSON and its text), <c>cells.jsonl</c>, the RESULTS skeleton, the hand-check Markdown, the public export's rows, a console line, a log
/// line, a refusal. A PUBLIC task passes through unchanged. A PRIVATE task is reduced to what D10 lets a public repository hold: its label (its
/// id), its language, sizes, metrics, reason codes and its HMAC commitments — never its url, P, S, statement, ground truth, removals, KEY FILES
/// or a claim's path. Nothing here reads a deny-list: the deny-list scan is the second line (<see cref="ScoutScrubber"/>), this is the first.
/// <para>
/// Each decision is a function of the task's privacy and nothing else, so a writer added later has one question to ask — "what does the
/// projection say this field is?" — and no list of its own to keep.
/// </para></summary>
public static class ScoutPublication
{
    public const string PrivateMark = "(private)";

    /// <summary>What a public writer calls the task's repository: the slug or url of a public task, the LABEL of a private one.</summary>
    public static string Repository(ScoutTask task) => task.IsPrivate ? task.Id.Value : task.Repository;

    /// <summary>What a public writer calls a CANDIDATE's repository (S7, the selection's console lines): its short name for a public candidate,
    /// the candidate's label with the mark for a private one — a private clone's folder name is the repository's name.</summary>
    public static string Repository(ScoutCandidate candidate) =>
        candidate.IsPrivate ? $"{candidate.Label} {PrivateMark}" : ScoutRepository.ShortName(candidate.Repository);

    public static string Statement(ScoutTask task) => task.IsPrivate ? string.Empty : task.Statement;

    public static IReadOnlyList<string> GroundTruth(ScoutTask task) => task.IsPrivate ? [] : task.GroundTruth;

    public static IReadOnlyList<string> Removals(ScoutTask task) => task.IsPrivate ? [] : task.Removals;

    /// <summary>A commit of the task's repository, as a public writer may print it: the sha of a public task's, nothing of a private one's
    /// ("no raw hash beside an HMAC" — a commit sha is a digest of the private history).</summary>
    public static string Commit(ScoutTaskPrivacy privacy, CommitSha sha) => privacy == ScoutTaskPrivacy.Private ? string.Empty : sha.Value;

    /// <summary>A commit as a console or log line prints it — its short form, or the private mark.</summary>
    public static string CommitShort(ScoutTaskPrivacy privacy, CommitSha sha) => privacy == ScoutTaskPrivacy.Private ? PrivateMark : sha.Short;

    /// <summary>Paths of the task's repository a public writer may carry: a public task's as they are, a private task's none at all
    /// (a scout's KEY FILES, the files removed, the identifiers added).</summary>
    public static IReadOnlyList<string> Paths(ScoutTaskPrivacy privacy, IReadOnlyList<string> paths) => privacy == ScoutTaskPrivacy.Private ? [] : paths;

    /// <summary>One path of the task's repository — a claim's — or empty for a private task.</summary>
    public static string Path(ScoutTaskPrivacy privacy, string path) => privacy == ScoutTaskPrivacy.Private ? string.Empty : path;

    /// <summary>How a console line, a report line or a table names the task's repository and language: <c>owner/name</c>, or
    /// <c>private · PHP</c>.</summary>
    public static string Describe(ScoutTask task) =>
        task.IsPrivate ? $"{ScoutTaskPrivacies.PrivateWord} · {task.Language}" : task.Language.Length > 0 ? $"{task.Repository} · {task.Language}" : task.Repository;

    /// <summary>The "frozen" console line of <c>bench scouts tasks</c>: what a public task is, by repository and P; what a private task is, by
    /// label and language only.</summary>
    public static string FrozenLine(ScoutTask task, int groundTruth, string treeHash, IReadOnlyList<string> removed) =>
        task.IsPrivate
            ? $"frozen         {task.Id} — {ScoutTaskPrivacies.PrivateWord} · {task.Language}, |G| = {groundTruth}, tree {treeHash[..12]}{(removed.Count > 0 ? $", removed {removed.Count} file(s)" : string.Empty)}"
            : $"frozen         {task.Id} — {task.Repository} at {task.Parent.Short}, |G| = {groundTruth}, tree {treeHash[..12]}{(removed.Count > 0 ? $", removed {string.Join(", ", removed)}" : string.Empty)}";

    /// <summary>A refusal or a failure sentence about the task, as a public writer may print it. A public task's passes as written. A PRIVATE
    /// task's is reduced to the label and <paramref name="summary"/> — a short phrase the harness itself wrote, never git's or the filesystem's
    /// words, which quote paths and urls — and says where the detail went (<see cref="WithheldFile"/>, a file under the trees root, which is
    /// never public).</summary>
    public static string Refusal(ScoutTaskPrivacy privacy, string label, string summary, string detail) =>
        privacy == ScoutTaskPrivacy.Private
            ? $"task {label} {PrivateMark}: {summary} — the detail is withheld from every public writer; it is in {WithheldFile(label)} under the trees root"
            : detail;

    /// <summary>Where the withheld detail of a private task's refusal is written, relative to the trees root (<c>.scout-trees/</c> is the tree
    /// builder's own folder there, and the trees root is git-ignored or outside every checkout by rule).</summary>
    public static string WithheldFile(string label) => $".scout-trees/{label}.withheld.txt";

    /// <summary>The task as <c>tasks.json</c> carries it (§3.12): a public task whole, with the identifiers its diff added so the leak check runs
    /// again on the way in; a private task as its label, privacy, language, kind, |G|, tree hash, and its commitments — the two of the task and
    /// one per file of G, the latter from <paramref name="groundTruthFiles"/> in G's committed order. The <paramref name="role"/> (S7: main,
    /// reserve, pilot) is a closed word and public either way.</summary>
    public static JsonObject TaskJson(ScoutTask task, IReadOnlyList<string> addedIdentifiers, IReadOnlyList<string> groundTruthFiles, ScoutTaskRole role = ScoutTaskRole.Main)
    {
        var node = new JsonObject
        {
            ["id"] = task.Id.Value,
            ["role"] = ScoutTaskRoles.Word(role),
            ["privacy"] = ScoutTaskPrivacies.Word(task.Privacy),
            ["language"] = task.Language,
            ["kind"] = ScoutFiles.KindWord(task.Kind),
            ["groundTruthCount"] = task.GroundTruth.Count,
            ["treeHash"] = task.TreeHash,
        };

        if (task.IsPrivate)
        {
            node["statementCommitment"] = task.Commitments.Statement;
            node["groundTruthCommitment"] = task.Commitments.GroundTruth;
            node["groundTruthFileCommitments"] = Array(groundTruthFiles);
            return node;
        }

        node["repository"] = task.Repository;
        node["parent"] = task.Parent.Value;
        node["fix"] = task.Fix.Value;
        node["statement"] = task.Statement;
        node["groundTruth"] = Array(task.GroundTruth);
        node["addedIdentifiers"] = Array(addedIdentifiers);
        node["removals"] = Array(task.Removals);
        return node;
    }

    private static JsonArray Array(IReadOnlyList<string> values) => new([.. values.Select(v => (JsonNode)JsonValue.Create(v))]);
}
