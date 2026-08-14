using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Suites;

/// <summary>Carrying a suite to a newer commit. Re-running the same questions against new code is the
/// whole point of a benchmark; doing it silently is the failure. An expectation is a claim about a
/// tree, and a tree that moved underneath it turns a scored miss into a fact about the suite rather
/// than about the engine.</summary>
public sealed class RetargetTests
{
    private static readonly CommitSha Old = CommitSha.Parse(new string('1', 40)).Ok();
    private static readonly CommitSha New = CommitSha.Parse(new string('2', 40)).Ok();

    [Fact]
    public void An_anchor_whose_file_is_gone_vanishes_and_is_not_repaired()
    {
        var anchor = SourceAnchor.Member("src/A.cs", "A.Foo", new LineSpan(10, 20), Old);

        var verdict = Retarget.Verdict(anchor, AnchorProbe.Missing);
        var (result, needsReview) = Retarget.Apply(anchor, verdict, New);

        verdict.Should().BeOfType<RetargetVerdict.Vanished>();
        needsReview.Should().BeTrue();
        result.AuthoredAt.Should().Be(Old, "a vanished anchor is reported, never quietly re-stamped as valid at the new commit");
    }

    [Fact]
    public void A_member_that_moved_is_rewritten_and_flagged_for_review()
    {
        var anchor = SourceAnchor.Member("src/A.cs", "A.Foo", new LineSpan(10, 20), Old);

        var verdict = Retarget.Verdict(anchor, AnchorProbe.FoundAt(31));
        var (result, needsReview) = Retarget.Apply(anchor, verdict, New);

        verdict.Should().BeOfType<RetargetVerdict.Moved>()
            .Which.Should().Be(new RetargetVerdict.Moved(10, 31));
        needsReview.Should().BeTrue("a moved anchor is also how a wrong one starts scoring");
        result.Lines.Should().Be(new LineSpan(31, 41), "the span keeps its length");
        result.AuthoredAt.Should().Be(New);
    }

    [Fact]
    public void A_member_that_did_not_move_is_carried_forward_without_review()
    {
        var anchor = SourceAnchor.Member("src/A.cs", "A.Foo", new LineSpan(10, 20), Old);

        var verdict = Retarget.Verdict(anchor, AnchorProbe.FoundAt(10));
        var (result, needsReview) = Retarget.Apply(anchor, verdict, New);

        verdict.Should().BeOfType<RetargetVerdict.Unchanged>();
        needsReview.Should().BeFalse();
        result.AuthoredAt.Should().Be(New);
    }

    [Fact]
    public void A_member_that_no_longer_exists_in_a_file_that_does_still_vanishes()
    {
        var anchor = SourceAnchor.Member("src/A.cs", "A.Foo", new LineSpan(10, 20), Old);

        Retarget.Verdict(anchor, AnchorProbe.FileOnly)
            .Should().BeOfType<RetargetVerdict.Vanished>()
            .Which.Reason.Should().Contain("A.Foo");
    }

    [Fact]
    public void A_whole_file_anchor_makes_no_line_claim_and_survives_a_reshuffle()
    {
        var anchor = SourceAnchor.File("src/A.cs", Old);

        Retarget.Verdict(anchor, AnchorProbe.FoundAt(999)).Should().BeOfType<RetargetVerdict.Unchanged>();
    }

    [Fact]
    public void Anchor_paths_are_normalised_so_a_suite_authored_on_windows_replays_on_linux()
    {
        SourceAnchor.File(@"src\Nested\A.cs", Old).FilePath.Should().Be("src/Nested/A.cs");
    }
}
