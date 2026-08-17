using Bench.Domain.Retrieval;
using Bench.Domain.Targets;
using Bench.Domain.Variants;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Retrieval;

/// <summary>Whether an index may serve a recipe at a pinned commit.
/// <para>
/// <b>Every rule here was paid for.</b> On 2026-08-17 a run recorded a variant declaring 512 embed tokens
/// against a 256-token index: every number in it real, and the row naming them describing a corpus that
/// would have produced different ones. The corpus half of a variant is the half no request can select — a
/// search reaches whichever collection the engine resolves — so the only honest move is to READ what will
/// answer and refuse a disagreement, which is what this class decides.
/// </para></summary>
public sealed class IndexReadinessTests
{
    private static readonly CommitSha Target = CommitSha.Parse(new string('a', 40)).Ok();
    private static readonly CommitSha Other = CommitSha.Parse(new string('b', 40)).Ok();

    [Fact]
    public void A_corpus_built_at_another_chunk_size_is_refused_naming_both()
    {
        var refused = IndexReadiness.Of(State(chunkTokens: 256), Recipe(chunkTokens: 512), Target, allowUnstamped: false);

        // The defect this whole file exists for, and the message has to carry both numbers: an operator
        // reading "the corpora differ" cannot tell which side to change.
        refused.Reason().Should().Contain("256").And.Contain("512");
        refused.Reason().Should().Contain("describe a different corpus");
    }

    [Fact]
    public void A_model_name_the_engine_spells_differently_is_the_same_model()
    {
        var approved = IndexReadiness.Of(
            State(embedModel: "BAAI/bge-m3 (dense, FP32)", commit: IndexCommit.Of(Target)),
            Recipe(embedModel: "bge-m3"),
            Target,
            allowUnstamped: false);

        // An operator types `bge-m3` into a catalog row and the engine reports its own full identity. Verbatim
        // equality here would refuse every correct recipe in the catalog.
        //
        // Asserted on the Ok case rather than through `Failed()` with a because-argument: `Reason()` throws on
        // a success and the argument is evaluated eagerly, so the message would crash the passing test.
        approved.Ok().Describe.Should().Contain("bge-m3");
    }

    [Fact]
    public void A_LARGER_model_of_the_same_family_is_NOT_the_same_model()
    {
        var refused = IndexReadiness.Of(
            State(embedModel: "BAAI/bge-m3-large", commit: IndexCommit.Of(Target)),
            Recipe(embedModel: "bge-m3"),
            Target,
            allowUnstamped: false);

        // The direction that must never be lenient. A containment rule would accept this — and a false ACCEPT
        // is indistinguishable from a correct measurement once the run is over, while a false refusal is a
        // sentence an operator reads and fixes.
        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("bge-m3-large").And.Contain("bge-m3");
    }

    [Fact]
    public void A_differently_shaped_corpus_is_refused()
    {
        IndexReadiness.Of(State(textShape: "SourceOnly"), Recipe(textShape: "GraphHeader"), Target, allowUnstamped: false)
            .Reason().Should().Contain("SourceOnly").And.Contain("GraphHeader");
    }

    [Fact]
    public void An_empty_collection_is_refused_because_zero_hits_reads_as_a_hard_question()
    {
        IndexReadiness.Of(State(exists: false, points: 0), Recipe(), Target, allowUnstamped: false)
            .Reason().Should().Contain("never indexed");
    }

    [Fact]
    public void A_collection_whose_newest_pass_did_not_succeed_is_refused()
    {
        IndexReadiness.Of(State(passSucceeded: false), Recipe(), Target, allowUnstamped: false)
            .Reason().Should().Contain("did not succeed");
    }

    [Fact]
    public void An_index_built_from_a_dirty_tree_is_refused_however_well_its_stamp_reads()
    {
        // The stamp then names a commit the index does not actually contain, which is worse than no stamp
        // because it reads as evidence.
        IndexReadiness.Of(State(commit: IndexCommit.Of(Target), dirty: true), Recipe(), Target, allowUnstamped: false)
            .Reason().Should().Contain("uncommitted changes");
    }

    [Fact]
    public void An_index_from_another_commit_is_refused_because_an_anchor_is_true_at_one_tree()
    {
        var refused = IndexReadiness.Of(State(commit: IndexCommit.Of(Other)), Recipe(), Target, allowUnstamped: false);

        refused.Reason().Should().Contain(Other.Value[..12]).And.Contain(Target.Value[..12]);
        refused.Reason().Should().Contain("exactly one tree");
    }

    [Fact]
    public void The_matching_commit_is_approved_with_nothing_left_unverified()
    {
        var approved = IndexReadiness.Of(State(commit: IndexCommit.Of(Target)), Recipe(), Target, allowUnstamped: false).Ok();

        approved.Warning.Should().BeEmpty();
        approved.Describe.Should().Contain(Target.Value[..12]).And.Contain("point(s)");
    }

    [Fact]
    public void An_UNSTAMPED_index_is_refused_by_default_and_says_which_flag_allows_it()
    {
        var refused = IndexReadiness.Of(State(), Recipe(), Target, allowUnstamped: false);

        // Not "a different commit" — nothing is known. Every index built before the engine began stamping is
        // in this state, so a strict equality check would have blocked every cell against every index in
        // existence on the day the stamp landed.
        refused.Reason().Should().Contain("no commit stamp").And.Contain("--allow-unstamped-index");
    }

    [Fact]
    public void An_UNSTAMPED_index_may_be_measured_deliberately_and_the_run_keeps_saying_so()
    {
        var approved = IndexReadiness.Of(State(), Recipe(), Target, allowUnstamped: true).Ok();

        // The same shape as --no-checkout: the escape hatch exists, and it keeps its warning, because an
        // unverified measurement that reads as a verified one is the whole failure being prevented.
        approved.Warning.Should().Contain("UNVERIFIED").And.Contain("exactly one tree");
    }

    [Fact]
    public void The_corpus_is_checked_BEFORE_the_commit()
    {
        // Both wrong: the corpus is the disagreement an operator can act on without re-indexing, and naming
        // the commit first would send them to fix the wrong thing.
        IndexReadiness.Of(State(chunkTokens: 256, commit: IndexCommit.Of(Other)), Recipe(chunkTokens: 512), Target, false)
            .Reason().Should().Contain("embed tokens").And.NotContain("exactly one tree");
    }

    private static IndexState State(
        bool exists = true,
        long points = 76_137,
        string textShape = "GraphHeader",
        int chunkTokens = 512,
        string embedModel = "bge-m3",
        IndexCommit? commit = null,
        bool dirty = false,
        bool passSucceeded = true) =>
        new(
            "code_project_shape_fingerprint",
            exists,
            points,
            new CorpusIdentity(textShape, chunkTokens, OverlapTokens: 64, embedModel, Tokenizer: "bge"),
            "FINGERPRINT",
            commit ?? IndexCommit.None,
            dirty,
            passSucceeded);

    private static CorpusSpec Recipe(
        string textShape = "GraphHeader", int chunkTokens = 512, string embedModel = "bge-m3") =>
        CorpusSpec.Parse(textShape, chunkTokens, embedModel).Ok();
}
