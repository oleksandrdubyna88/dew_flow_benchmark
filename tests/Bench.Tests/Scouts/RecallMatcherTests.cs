using Bench.Domain.Scouts;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The recall matcher (§3.6, hand-checked in §3.7): the spellings a scout writes a path in, resolved against the files that
/// exist in the tree at P. A unique basename names its file; an ambiguous one names nothing.</summary>
public sealed class RecallMatcherTests
{
    private static readonly string[] TreeFiles =
    [
        "src/Bench.Domain/Probes/ClaudeStream.cs",
        "src/Bench.Domain/Probes/ProbeJson.cs",
        "src/Bench.Application/Program.cs",
        "hosts/Cli/Program.cs",
        "README.md",
    ];

    private static readonly string[] GroundTruth = ["src/Bench.Domain/Probes/ClaudeStream.cs", "src/Bench.Application/Program.cs", "README.md"];

    private static RecallResult Match(params string[] keyFiles) => RecallMatcher.Match(keyFiles, GroundTruth, TreeFiles, "C:/work/tree");

    [Theory]
    [InlineData("src/Bench.Domain/Probes/ClaudeStream.cs")]
    [InlineData(@"src\Bench.Domain\Probes\ClaudeStream.cs")]
    [InlineData("./src/Bench.Domain/Probes/ClaudeStream.cs")]
    [InlineData("src/Bench.Domain/Probes/ClaudeStream.cs:42")]
    [InlineData("src/Bench.Domain/Probes/ClaudeStream.cs:42-60")]
    [InlineData("src/Bench.Domain/Probes/ClaudeStream.cs#L42")]
    [InlineData("`src/Bench.Domain/Probes/ClaudeStream.cs`")]
    [InlineData(@"C:\work\tree\src\Bench.Domain\Probes\ClaudeStream.cs")]
    [InlineData("SRC/bench.domain/probes/claudestream.cs")]
    [InlineData("ClaudeStream.cs")]
    [InlineData("Probes/ClaudeStream.cs")]
    public void Every_spelling_of_one_file_finds_that_file(string entry)
    {
        Match(entry).Found.Should().Equal("src/Bench.Domain/Probes/ClaudeStream.cs");
    }

    /// <summary>The break-it target: <c>Program.cs</c> exists twice in the tree, so naming it says nothing about which one — even though
    /// one of the two is in G. A matcher that accepts any basename turns this red.</summary>
    [Fact]
    public void An_ambiguous_basename_matches_nothing_even_when_one_of_its_files_is_in_the_ground_truth()
    {
        Match("Program.cs").Found.Should().BeEmpty();
        Match("Bench.Application/Program.cs").Found.Should().Equal(["src/Bench.Application/Program.cs"], "a longer suffix that is unique does name it");
    }

    [Fact]
    public void A_unique_basename_matches_its_file()
    {
        Match("README.md", "ProbeJson.cs").Found.Should().Equal("README.md");
    }

    [Fact]
    public void Recall_is_the_share_of_the_ground_truth_the_team_named()
    {
        var result = Match("ClaudeStream.cs", "README.md", "src/Bench.Domain/Probes/ProbeJson.cs", "does/not/exist.cs");

        (result.Found.Count, result.Total).Should().Be((2, 3));
        result.Recall.Should().BeApproximately(0.6667m, 0.0001m);
    }

    [Fact]
    public void Without_the_trees_file_list_only_an_exact_path_can_match_because_uniqueness_cannot_be_proven()
    {
        var noTree = RecallMatcher.Match(["ClaudeStream.cs", "./README.md"], GroundTruth, [], string.Empty);

        noTree.Found.Should().Equal("README.md");
    }

    [Fact]
    public void A_suffix_must_end_on_a_segment_boundary()
    {
        Match("eam.cs").Found.Should().BeEmpty("'eam.cs' is the tail of ClaudeStream.cs's name, not a file");
    }
}
