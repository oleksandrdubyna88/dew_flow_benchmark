using Bench.Domain.Authoring;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Authoring;

/// <summary>Deriving diagnosis ground truth from a reference fix's unified diff
/// (todo/PLAN_investigate_vs_implement.md §3.3, step 3's pure half).
/// <para>
/// The spans are OLD-side coordinates on purpose: the solver investigates the tree at
/// <c>baseCommit</c>, so a diagnosis names lines in THAT tree, and the fix diff's a-side is exactly
/// that tree's numbering. Test files are carried separately — the fix's test changes are its
/// verification, not the place the defect is caused, and they are the hidden-test candidates.
/// </para></summary>
public sealed class FixDiffTests
{
    private static readonly CommitSha Base = CommitSha.Parse(new string('b', 40)).Ok();

    private const string OneHunk =
        """
        diff --git a/src/Retry/Policy.cs b/src/Retry/Policy.cs
        --- a/src/Retry/Policy.cs
        +++ b/src/Retry/Policy.cs
        @@ -118,7 +118,8 @@ public sealed class Policy
             public TimeSpan NextDelay()
             {
        -        var delay = Base * Attempt;
        +        var delay = Carry(Base, Attempt);
        +        _carried = delay;
                 return Jitter(delay);
             }
        """;

    [Fact]
    public void A_modified_line_becomes_an_old_side_anchor_in_its_file()
    {
        var anchors = FixDiff.Parse(OneHunk).Ok().CausalAnchors(Base);

        var anchor = anchors.Should().ContainSingle().Subject;
        anchor.FilePath.Should().Be("src/Retry/Policy.cs");
        anchor.Lines.IsWhole.Should().BeFalse();
        anchor.Lines.Start.Should().Be(120, "the changed line is old line 120, and old-side numbering is the tree the solver read");
        anchor.Lines.End.Should().BeGreaterThanOrEqualTo(120);
    }

    [Fact]
    public void An_insertion_only_hunk_still_anchors_to_where_it_landed()
    {
        const string insertion =
            """
            diff --git a/src/F.cs b/src/F.cs
            --- a/src/F.cs
            +++ b/src/F.cs
            @@ -10,2 +10,4 @@
                 first();
            +    inserted();
            +    also();
                 second();
            """;

        var anchors = FixDiff.Parse(insertion).Ok().CausalAnchors(Base);

        var anchor = anchors.Should().ContainSingle().Subject;
        anchor.Lines.IsWhole.Should().BeFalse();
        anchor.Lines.Start.Should().BeInRange(10, 11, "an insertion between old lines 10 and 11 is found by pointing at either neighbour");
    }

    [Fact]
    public void Two_far_apart_hunks_become_two_spans_not_one_stretched_claim()
    {
        const string twoHunks =
            """
            diff --git a/src/F.cs b/src/F.cs
            --- a/src/F.cs
            +++ b/src/F.cs
            @@ -10,3 +10,3 @@
                 a();
            -    b();
            +    b2();
                 c();
            @@ -200,3 +200,3 @@
                 x();
            -    y();
            +    y2();
                 z();
            """;

        var anchors = FixDiff.Parse(twoHunks).Ok().CausalAnchors(Base);

        anchors.Should().HaveCount(2, "one span from line 11 to line 201 would match a diagnosis pointing anywhere between them");
        anchors[0].Lines.Start.Should().Be(11);
        anchors[1].Lines.Start.Should().Be(201);
    }

    [Fact]
    public void A_file_the_fix_CREATED_is_no_causal_anchor_at_all()
    {
        const string newFile =
            """
            diff --git a/src/New.cs b/src/New.cs
            --- /dev/null
            +++ b/src/New.cs
            @@ -0,0 +1,2 @@
            +public sealed class New
            +{ }
            """;

        FixDiff.Parse(newFile).Ok().CausalAnchors(Base).Should().BeEmpty(
            "a diagnosis cannot point at a file that does not exist in the tree it investigated");
    }

    [Fact]
    public void A_file_the_fix_DELETED_is_a_whole_file_anchor()
    {
        const string deleted =
            """
            diff --git a/src/Old.cs b/src/Old.cs
            --- a/src/Old.cs
            +++ /dev/null
            @@ -1,2 +0,0 @@
            -public sealed class Old
            -{ }
            """;

        var anchor = FixDiff.Parse(deleted).Ok().CausalAnchors(Base).Should().ContainSingle().Subject;

        anchor.FilePath.Should().Be("src/Old.cs");
        anchor.Lines.IsWhole.Should().BeTrue();
    }

    [Fact]
    public void A_renamed_file_anchors_under_its_OLD_path()
    {
        const string renamed =
            """
            diff --git a/src/Before.cs b/src/After.cs
            --- a/src/Before.cs
            +++ b/src/After.cs
            @@ -5,3 +5,3 @@
                 a();
            -    b();
            +    b2();
                 c();
            """;

        FixDiff.Parse(renamed).Ok().CausalAnchors(Base).Single().FilePath.Should().Be(
            "src/Before.cs", "the solver reads the base tree, where only the old name exists");
    }

    [Fact]
    public void The_fixes_own_test_changes_are_hidden_test_candidates_not_causes()
    {
        const string withTests =
            """
            diff --git a/src/Retry/Policy.cs b/src/Retry/Policy.cs
            --- a/src/Retry/Policy.cs
            +++ b/src/Retry/Policy.cs
            @@ -10,3 +10,3 @@
                 a();
            -    b();
            +    b2();
                 c();
            diff --git a/tests/Retry/PolicyTests.cs b/tests/Retry/PolicyTests.cs
            --- a/tests/Retry/PolicyTests.cs
            +++ b/tests/Retry/PolicyTests.cs
            @@ -1,2 +1,6 @@
                 existing();
            +    theNewRegressionTest();
                 more();
            """;

        var diff = FixDiff.Parse(withTests).Ok();

        diff.CausalAnchors(Base).Should().ContainSingle(a => a.FilePath == "src/Retry/Policy.cs");
        diff.TestFiles.Should().ContainSingle().Which.Should().Be("tests/Retry/PolicyTests.cs");
    }

    [Fact]
    public void An_empty_or_diffless_text_is_refused_by_name()
    {
        FixDiff.Parse("   ").Reason().Should().Contain("diff");
        FixDiff.Parse("commit message text, no hunks anywhere").Reason().Should().Contain("diff");
    }

    [Fact]
    public void Windows_style_paths_in_a_diff_normalise_like_every_other_anchor()
    {
        const string backslashes =
            """
            diff --git a/src\Sub\F.cs b/src\Sub\F.cs
            --- a/src\Sub\F.cs
            +++ b/src\Sub\F.cs
            @@ -3,3 +3,3 @@
                 a();
            -    b();
            +    b2();
                 c();
            """;

        FixDiff.Parse(backslashes).Ok().CausalAnchors(Base).Single().FilePath.Should().Be("src/Sub/F.cs");
    }
}
