using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Suites;

/// <summary>Checking a question's ground truth against the tree, mechanically.
/// <para>
/// Pure — the tree is a dictionary here — because the whole value of this check is that it needs no agent. It
/// exists because all three live reviewer notes on 2026-08-18 LED with exactly this check, which means three
/// agent launches per question were buying arithmetic.
/// </para></summary>
public sealed class AnchorCheckTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('c', 40)).Ok();

    private static readonly string[] File =
    [
        "namespace Rag.Domain;",           // 1
        "",                                // 2
        "public static class StoreNaming", // 3
        "{",                               // 4
        "    public static StoreKind KindOf(string image, string name) =>", // 5
        "        image.Length > 0 ? Image(image) : Named(name);",           // 6
        "}",                               // 7
    ];

    [Fact]
    public void A_member_inside_its_claimed_span_resolves()
    {
        var proof = AnchorCheck.Verify(Member("src/StoreNaming.cs", "StoreNaming.KindOf", 5, 6), Tree());

        proof.Resolved.Should().BeTrue();
        proof.Describe.Should().Contain("resolves");
    }

    [Fact]
    public void A_file_that_is_not_in_the_tree_is_named_as_missing()
    {
        var proof = AnchorCheck.Verify(Member("src/Gone.cs", "Gone.Method", 1, 5), Tree());

        proof.State.Should().Be(AnchorState.FileMissing);
        proof.Detail.Should().Contain("no such file");
    }

    [Fact]
    public void A_span_running_past_the_END_of_the_file_says_both_numbers()
    {
        var proof = AnchorCheck.Verify(Member("src/StoreNaming.cs", "StoreNaming.KindOf", 40, 60), Tree());

        // The commonest stale anchor: the file was cut down and the range was not.
        proof.State.Should().Be(AnchorState.SpanBeyondFile);
        proof.Detail.Should().Contain("40").And.Contain("60").And.Contain("7");
    }

    [Fact]
    public void A_member_whose_name_is_ELSEWHERE_in_the_file_says_where()
    {
        var proof = AnchorCheck.Verify(Member("src/StoreNaming.cs", "StoreNaming.KindOf", 1, 4), Tree());

        // "Wrong question" and "stale line range" are different problems with different fixes, and the line
        // numbers are the whole of what tells them apart.
        proof.State.Should().Be(AnchorState.MemberOutsideSpan);
        proof.Detail.Should().Contain("line(s) 5");
    }

    [Fact]
    public void A_member_that_is_nowhere_in_the_file_says_THAT()
    {
        AnchorCheck.Verify(Member("src/StoreNaming.cs", "StoreNaming.Vanished", 1, 4), Tree())
            .Detail.Should().Contain("nowhere in the file");
    }

    [Fact]
    public void A_whole_FILE_anchor_only_needs_the_file_to_exist()
    {
        AnchorCheck.Verify(SourceAnchor.File("src/StoreNaming.cs", Commit), Tree()).Resolved.Should().BeTrue();
    }

    [Fact]
    public void Answer_expectations_are_not_checked_against_the_tree()
    {
        var question = new Question(
            "q1",
            "what does it inspect first?",
            [
                Expectation.Member(Member("src/StoreNaming.cs", "StoreNaming.KindOf", 5, 6)),
                new Expectation(ExpectationKind.AnswerContains, SourceAnchor.File(string.Empty, Commit), "image", true),
            ],
            "the image");

        // There is nothing about a tree to check in "the answer contains 'image'", and treating its empty path
        // as a missing file would fail every well-formed question in the bank.
        AnchorCheck.Verify(question, Tree()).Should().ContainSingle().Which.Resolved.Should().BeTrue();
    }

    [Fact]
    public void A_path_that_escapes_the_ROOT_reads_as_nothing_to_find()
    {
        // Resolve-then-compare, because `a/../../b` and a symlink both spell fine. An anchor's path is written by
        // an agent, so it is data, and data that walks out of the tree must not be read.
        RootedPath.Under(Path.Combine(Path.GetTempPath(), "tree"), "../../windows/system32/config")
            .Reason().Should().Contain("outside the tree");
    }

    [Fact]
    public void A_path_INSIDE_the_root_resolves_to_a_full_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "tree");

        RootedPath.Under(root, "src/A.cs").Ok().Should().Be(Path.Combine(root, "src", "A.cs"));
    }

    private static SourceAnchor Member(string file, string member, int start, int end) =>
        SourceAnchor.Member(file, member, new LineSpan(start, end), Commit);

    private static Func<string, TreeFile> Tree() =>
        path => path == "src/StoreNaming.cs" ? TreeFile.Of(File) : TreeFile.Absent;
}
