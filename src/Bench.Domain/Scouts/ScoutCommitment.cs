using System.Buffers.Binary;
using System.Text;
using Bench.Domain.Gate;

namespace Bench.Domain.Scouts;

/// <summary>What a commitment is OF (plan §4 S4): a private task's statement, its whole ground truth, one file of it, or one claim a scout made.</summary>
public enum ScoutCommitmentField
{
    Statement,
    GroundTruth,
    GroundTruthFile,
    Claim,
}

/// <summary>The HMAC commitment a PRIVATE task's committed records carry instead of the plaintext (D10, §3.13) — encoding
/// <see cref="Version"/>:
/// <list type="bullet">
/// <item>HMAC-SHA256 under the campaign key (<see cref="FileHashKey"/>: 32 random bytes kept outside every checkout) over the fields
/// <c>version · campaign · label · field · content</c>, each LENGTH-PREFIXED (a 4-byte big-endian count of its UTF-8 bytes, then the bytes) —
/// so a separator inside a value cannot make two inputs read as one, and a label, a field word or a content that differs anywhere gives a
/// different MAC;</item>
/// <item>a statement is normalised first: Unicode NFC, CRLF → LF, trimmed — the same sentence typed on two machines commits equal;</item>
/// <item>the ground truth is its paths with <c>/</c>, ORDINAL-sorted and LF-joined, so the order they were listed in does not matter —
/// and each path has its own <see cref="ScoutCommitmentField.GroundTruthFile"/> commitment for the hand-check;</item>
/// <item>a claim commits its statement text, normalised as a task statement is.</item>
/// </list>
/// <b>No raw hash beside an HMAC:</b> a plain SHA-256 of a private statement or path is searchable by anyone holding the published digest and
/// a guess; keyed, the guess needs the key. A private claim's stored SHA-256 is therefore this <see cref="ScoutCommitmentField.Claim"/> MAC.</summary>
public static class ScoutCommitment
{
    public const string Version = "scout-commit-v1";

    public static string Word(ScoutCommitmentField field) => field switch
    {
        ScoutCommitmentField.Statement => "statement",
        ScoutCommitmentField.GroundTruth => "g",
        ScoutCommitmentField.GroundTruthFile => "g-file",
        _ => "claim",
    };

    /// <summary>The MAC of one framed field. <paramref name="content"/> is taken as given — normalise it first with
    /// <see cref="Statement"/> or <see cref="GroundTruth"/>.</summary>
    public static string Of(FileHashKey key, string campaign, string label, ScoutCommitmentField field, string content) =>
        key.Hash(Frame([Version, campaign, label, Word(field), content]));

    /// <summary>A statement as it is committed: NFC, LF, trimmed.</summary>
    public static string Statement(string statement) => statement.Replace("\r\n", "\n", StringComparison.Ordinal).Trim().Normalize(NormalizationForm.FormC);

    /// <summary>The ground truth as it is committed: <c>/</c>-separated, ordinal-sorted, LF-joined.</summary>
    public static string GroundTruth(IEnumerable<string> paths) => string.Join('\n', paths.Select(Path).Order(StringComparer.Ordinal));

    /// <summary>One path as it is committed: <c>/</c>-separated, trimmed.</summary>
    public static string Path(string path) => path.Trim().Replace('\\', '/');

    private static byte[] Frame(IReadOnlyList<string> fields)
    {
        var encoded = fields.Select(f => Encoding.UTF8.GetBytes(f)).ToList();
        var frame = new byte[encoded.Sum(e => e.Length + sizeof(uint))];
        var at = 0;

        foreach (var bytes in encoded)
        {
            BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(at, sizeof(uint)), (uint)bytes.Length);
            bytes.CopyTo(frame, at + sizeof(uint));
            at += sizeof(uint) + bytes.Length;
        }

        return frame;
    }
}

/// <summary>The campaign key and the campaign's seed string together — what every commitment of one campaign is made under. A class, not a
/// record: it holds the key, and a generated <c>ToString</c> or value equality has no business near one.</summary>
public sealed class ScoutCommitter
{
    private readonly FileHashKey _key;

    public ScoutCommitter(FileHashKey key, string campaign)
    {
        _key = key;
        Campaign = campaign.Trim();
    }

    public string Campaign { get; }

    public string Statement(string label, string statement) =>
        ScoutCommitment.Of(_key, Campaign, label, ScoutCommitmentField.Statement, ScoutCommitment.Statement(statement));

    public string GroundTruth(string label, IEnumerable<string> paths) =>
        ScoutCommitment.Of(_key, Campaign, label, ScoutCommitmentField.GroundTruth, ScoutCommitment.GroundTruth(paths));

    public string GroundTruthFile(string label, string path) =>
        ScoutCommitment.Of(_key, Campaign, label, ScoutCommitmentField.GroundTruthFile, ScoutCommitment.Path(path));

    public string Claim(string label, string statement) =>
        ScoutCommitment.Of(_key, Campaign, label, ScoutCommitmentField.Claim, ScoutCommitment.Statement(statement));

    /// <summary>A private task's two commitments.</summary>
    public ScoutTaskCommitments Task(ScoutTask task) => new(Statement(task.Id.Value, task.Statement), GroundTruth(task.Id.Value, task.GroundTruth));

    /// <summary>One commitment per file of G, in G's committed order.</summary>
    public IReadOnlyList<string> GroundTruthFiles(ScoutTask task) =>
        [.. task.GroundTruth.Select(ScoutCommitment.Path).Order(StringComparer.Ordinal).Select(p => GroundTruthFile(task.Id.Value, p))];

    /// <summary>Whether the commitments a public record carries are the ones this key and campaign derive from the private detail — the check
    /// <c>run</c> makes before a private task is measured; a mismatch is refused by LABEL, naming nothing the task holds.</summary>
    public Outcome<ScoutTask> Verified(ScoutTask task)
    {
        var derived = Task(task);

        return task.Commitments == derived
            ? Outcome<ScoutTask>.Success(task)
            : Outcome<ScoutTask>.Failure(
                $"task {task.Id} (private): its public commitments are not the ones this campaign key derives from the private detail under campaign '{Campaign}' "
                + "— another key, another campaign, or a statement or ground truth edited since the tasks were frozen; nothing is measured");
    }

    public override string ToString() => $"ScoutCommitter(campaign {Campaign}, key redacted)";
}

/// <summary>What a stored claim's "statement SHA-256" is per task (plan §4 S4, "no raw hash beside an HMAC"): a PUBLIC task's claim keeps its
/// plain SHA-256 — a public repository's text is searchable anyway — and a PRIVATE task's claim stores its <see cref="ScoutCommitmentField.Claim"/>
/// MAC instead. Built once per verb from the run's tasks; a run holding a private task and no committer is refused by the factory, so the
/// digest never has to decide what to do without a key.</summary>
public sealed class ScoutClaimDigest
{
    private readonly IReadOnlySet<string> _private;
    private readonly ScoutCommitter? _committer;

    private ScoutClaimDigest(IReadOnlySet<string> privateTasks, ScoutCommitter? committer)
    {
        _private = privateTasks;
        _committer = committer;
    }

    /// <summary>A digest for a run with no private task.</summary>
    public static ScoutClaimDigest PublicOnly { get; } = new(new HashSet<string>(StringComparer.Ordinal), null);

    public IReadOnlySet<string> PrivateTasks => _private;

    public static Outcome<ScoutClaimDigest> Of(IReadOnlyList<ScoutTask> tasks, ScoutCommitter? committer)
    {
        var privateTasks = tasks.Where(t => t.IsPrivate).Select(t => t.Id.Value).ToHashSet(StringComparer.Ordinal);

        return privateTasks.Count > 0 && committer is null
            ? Outcome<ScoutClaimDigest>.Failure($"the run holds {privateTasks.Count} private task(s) ({string.Join(", ", privateTasks.Order(StringComparer.Ordinal))}) and a private claim is stored as its HMAC — the campaign key is needed (--campaign-key or BENCH_SCOUT_CAMPAIGN_KEY)")
            : Outcome<ScoutClaimDigest>.Success(new ScoutClaimDigest(privateTasks, committer));
    }

    public string Of(string task, string statement) =>
        _private.Contains(task) && _committer is { } committer ? committer.Claim(task, statement) : StableHash.Of(statement);
}
