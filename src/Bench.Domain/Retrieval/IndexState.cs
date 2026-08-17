using Bench.Domain.Targets;
using Bench.Domain.Variants;

namespace Bench.Domain.Retrieval;

/// <summary>What a stored corpus actually IS, as the engine describes it.
/// <para>
/// This is the half of a variant that no request can select: a corpus is built by an indexing pass, and a
/// search reaches whichever collection the engine resolves. So the only way a run can be honest about it is
/// to READ what will answer and refuse a recipe that disagrees — which is one HTTP GET, before any cell
/// exists, and was missing for exactly one measurement: a variant declaring 512 embed tokens was recorded
/// against a 256-token index on 2026-08-17.
/// </para></summary>
/// <param name="OverlapTokens">Recorded, never compared — a variant has no field for it. Two corpora that
/// differ only in overlap are two corpora, and this benchmark cannot yet say so; storing the number is what
/// makes that statable later without re-running anything.</param>
public sealed record CorpusIdentity(
    string TextShape,
    int ChunkTokens,
    int OverlapTokens,
    string EmbedModel,
    string Tokenizer)
{
    /// <summary>Why this corpus is not the recipe's, or empty when it is.
    /// <para>
    /// Every difference is a refusal rather than a warning. A wrong chunk size or a wrong embedder does not
    /// degrade a measurement, it RELABELS it: the numbers are real and the row that names them describes a
    /// corpus that produced different ones.
    /// </para></summary>
    public string Refuse(CorpusSpec recipe)
    {
        if (!string.Equals(TextShape, recipe.TextShape, StringComparison.OrdinalIgnoreCase))
        {
            return $"the corpus that answers is shaped '{TextShape}' and the recipe names '{recipe.TextShape}'";
        }

        if (ChunkTokens != recipe.ChunkTokens)
        {
            return $"the corpus that answers was built at {ChunkTokens} embed tokens and the recipe names "
                + $"{recipe.ChunkTokens} — the numbers would be real and the row naming them would describe "
                + "a different corpus";
        }

        return SameModel(EmbedModel, recipe.EmbedModel)
            ? string.Empty
            : $"the corpus that answers was embedded by '{EmbedModel}' and the recipe names '{recipe.EmbedModel}'";
    }

    /// <summary>Whether two model names are the same model.
    /// <para>
    /// Compared after normalisation, not verbatim, because the two sides legitimately spell it differently:
    /// an operator types <c>bge-m3</c> into a catalog row and the engine reports
    /// <c>BAAI/bge-m3 (dense, FP32)</c>. Verbatim equality would refuse every correct recipe; CONTAINMENT
    /// would accept <c>bge-m3</c> against a <c>bge-m3-large</c> index, which is a false accept — the one
    /// direction that must not happen, since it is indistinguishable from a correct measurement afterwards.
    /// </para></summary>
    public static bool SameModel(string reported, string named) =>
        string.Equals(Normalise(reported), Normalise(named), StringComparison.OrdinalIgnoreCase);

    /// <summary>Vendor prefix and parenthetical detail dropped: <c>BAAI/bge-m3 (dense, FP32)</c> and
    /// <c>bge-m3</c> both reduce to <c>bge-m3</c>, while <c>bge-m3-large</c> stays itself.</summary>
    private static string Normalise(string model)
    {
        var withoutDetail = model.Split('(')[0].Trim();
        var segments = withoutDetail.Split('/');

        return segments[^1].Trim();
    }
}

/// <summary>Which tree an index was built from — three states, because two of them are not the same
/// disagreement.
/// <para>
/// <see cref="Unstamped"/> is the state of every index built before the engine began recording its commit
/// (<c>dew_flow_rag_qln@ee45e67</c>, 2026-08-17). It is not "a different commit": nothing is known, and a
/// strict equality check would have blocked every cell against every index in existence on the day the
/// stamp landed. So it is a distinct state, refused by default and passable with an explicit flag — the
/// same shape as <c>--no-checkout</c>, which allows an unverified commit and keeps saying so.
/// </para></summary>
public abstract record IndexCommit
{
    private IndexCommit() { }

    public sealed record Unstamped : IndexCommit
    {
        internal Unstamped() { }
    }

    public sealed record At : IndexCommit
    {
        internal At(CommitSha sha) => Sha = sha;

        public CommitSha Sha { get; }
    }

    public static IndexCommit None { get; } = new Unstamped();

    public static IndexCommit Of(CommitSha sha) => new At(sha);

    /// <summary>Reads a stamp the engine reported. An unparseable one is <see cref="Unstamped"/> rather than
    /// an exception: a value this side cannot read is a thing it does not know, which is the state that
    /// already exists.</summary>
    public static IndexCommit Read(string? sha) =>
        CommitSha.Parse(sha).Match(Of, _ => None);

    public string Describe => this is At at ? at.Sha.Value[..12] : "unstamped";
}

/// <summary>The engine's answer to "what is in the index for this project, branch and corpus".</summary>
/// <param name="WorkingTreeDirty">Whether the pass that wrote this collection scanned a tree with
/// uncommitted changes. A dirty tree makes the stamp a claim about a commit the index does not actually
/// contain, so it is carried and refused like a mismatch.</param>
public sealed record IndexState(
    string Collection,
    bool Exists,
    long Points,
    CorpusIdentity Corpus,
    string Fingerprint,
    IndexCommit Commit,
    bool WorkingTreeDirty,
    bool PassSucceeded)
{
    public string Describe =>
        $"{Collection} · {Points} point(s) · {Corpus.TextShape}/{Corpus.ChunkTokens}/{Corpus.EmbedModel} · commit {Commit.Describe}";
}

/// <summary>A corpus this run may measure, and what is still unproven about it.</summary>
/// <param name="Warning">Empty when everything checked out. Non-empty when the operator allowed something
/// that could not be verified — and then it is printed, because an unverified measurement that reads as a
/// verified one is the whole failure this file exists to prevent.</param>
public sealed record IndexApproval(string Describe, string Warning);

/// <summary>Deciding whether an index may serve a recipe at a pinned commit.
/// <para>
/// Pure, and separate from the adapter that fetched the state, because every rule here is a judgement about
/// two values rather than a fact about HTTP — and because these are the judgements that decide whether a
/// published number means what its row says.
/// </para></summary>
public static class IndexReadiness
{
    public static Outcome<IndexApproval> Of(
        IndexState state, CorpusSpec recipe, CommitSha target, bool allowUnstamped)
    {
        var refusal = Refuse(state, recipe, target, allowUnstamped);

        return refusal.Length > 0
            ? Outcome<IndexApproval>.Failure(refusal)
            : Outcome<IndexApproval>.Success(new IndexApproval(state.Describe, Warning(state, allowUnstamped)));
    }

    private static string Refuse(IndexState state, CorpusSpec recipe, CommitSha target, bool allowUnstamped) =>
        (state.Exists && state.Points > 0, state.PassSucceeded, state.WorkingTreeDirty) switch
        {
            (false, _, _) =>
                $"collection '{state.Collection}' holds nothing — this corpus was never indexed, and a search "
                + "against it would return zero hits, which is indistinguishable from a hard question",
            (_, false, _) =>
                $"the newest pass that wrote '{state.Collection}' did not succeed, so what is in there is a "
                + "partial index nobody can describe",
            (_, _, true) =>
                $"the pass that built '{state.Collection}' scanned a tree with uncommitted changes, so its "
                + "commit stamp names a tree the index does not contain",
            _ => Corpus(state, recipe) is { Length: > 0 } wrong ? wrong : Commit(state, target, allowUnstamped),
        };

    private static string Corpus(IndexState state, CorpusSpec recipe) => state.Corpus.Refuse(recipe);

    private static string Commit(IndexState state, CommitSha target, bool allowUnstamped) =>
        state.Commit switch
        {
            IndexCommit.At at when at.Sha == target => string.Empty,
            IndexCommit.At at =>
                $"the index was built from {at.Sha.Value[..12]} and this run measures {target.Value[..12]} — "
                + "an anchor is true at exactly one tree, so recall against another one measures nothing",
            _ when allowUnstamped => string.Empty,
            _ =>
                $"'{state.Collection}' carries no commit stamp, so nothing can say which tree it describes. "
                + "Pass --allow-unstamped-index to measure it anyway, and the run will say so",
        };

    private static string Warning(IndexState state, bool allowUnstamped) =>
        allowUnstamped && state.Commit is IndexCommit.Unstamped
            ? $"'{state.Collection}' carries no commit stamp — the corpus matches the recipe, but which tree it "
              + "describes is UNVERIFIED, and every anchor is true at exactly one tree"
            : string.Empty;
}
