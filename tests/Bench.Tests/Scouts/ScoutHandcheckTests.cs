using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.7's hand-check draw and file: the matcher's reading of each (output, G file) pair, a draw fixed by its seed, and a Markdown file
/// in which every item carries exactly one agree / disagree pair for the reader to tick — and never names the arm that produced it.</summary>
public sealed class ScoutHandcheckTests
{
    private static readonly string[] G = ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"];

    private static readonly string[] Tree = ["README.md", "src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs", "src/Other.cs"];

    [Fact]
    public void Each_file_of_G_becomes_one_pair_read_by_the_matcher_over_the_tree()
    {
        var cell = Guid.CreateVersion7();

        var pairs = ScoutHandcheck.PairsOf(cell, "t01", G, ["`Alpha.cs:12`", "README.md", "C:/work/tree/src/Gamma.cs"], Tree, "C:/work/tree");

        pairs.Select(p => (p.GroundTruthFile, p.Found)).Should().Equal(("src/Alpha.cs", true), ("src/Beta.cs", false), ("src/Gamma.cs", true));
        pairs.Should().OnlyContain(p => p.KeyFilesAsWritten.Count == 3, "the reader judges the output AS WRITTEN, not the matcher's normalisation of it");
    }

    [Fact]
    public void The_draw_is_fixed_by_its_seed_and_a_pool_smaller_than_N_gives_what_it_has()
    {
        var pairs = Pool(40);
        var claims = Claims(30);

        var first = ScoutHandcheck.Draw(pairs, claims, 5, seed: 11);
        var again = ScoutHandcheck.Draw([.. pairs.Reverse()], [.. claims.Reverse()], 5, seed: 11);
        var other = ScoutHandcheck.Draw(pairs, claims, 5, seed: 12);

        again.Found.Should().Equal(first.Found, "the same seed draws the same items whatever order the pool comes in");
        again.Claims.Should().Equal(first.Claims);
        other.Found.Select(p => p.DrawKey).Should().NotEqual(first.Found.Select(p => p.DrawKey));
        (first.Found.Count, first.NotFound.Count, first.Claims.Count).Should().Be((5, 5, 5));
        first.Found.Should().OnlyContain(p => p.Found);
        first.NotFound.Should().OnlyContain(p => !p.Found);
        ScoutHandcheck.Draw(Pool(2), [], 20, seed: 1).Found.Should().HaveCount(2, "two cells, one found pair each — a draw of 20 gives the two");
    }

    [Fact]
    public void The_file_gives_every_item_one_agree_and_one_disagree_box_and_a_note_line_and_names_no_arm()
    {
        var draw = ScoutHandcheck.Draw(Pool(6), Claims(2), 2, seed: 3);

        var markdown = ScoutHandcheck.Markdown(Guid.Parse("01900000-0000-7000-8000-000000000009"), draw);
        var items = draw.Found.Count + draw.NotFound.Count + draw.Claims.Count;

        Count(markdown, "\n- [ ] agree\n").Should().Be(items);
        Count(markdown, "\n- [ ] disagree\n").Should().Be(items);
        Count(markdown, "\n- note: \n").Should().Be(items);
        markdown.Should().Contain("### F01").And.Contain("### N01").And.Contain("### C01").And.Contain("--seed 3").And.Contain("19 of 20").And.Contain("18 of 20");
        markdown.Should().Contain("> `src/Alpha.cs:4` — defines the type").And.Contain("assessor: **wrong**");
        markdown.Should().NotContainAny(["A1", "B1", "opus", "sonnet"]);
        ScoutHandcheck.Markdown(Guid.Parse("01900000-0000-7000-8000-000000000009"), draw).Should().Be(markdown, "the file is a pure function of the draw");
    }

    private static int Count(string text, string part) => (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;

    private static IReadOnlyList<HandcheckPair> Pool(int cells) =>
        [.. Enumerable.Range(0, cells).SelectMany(i => ScoutHandcheck.PairsOf(new Guid(i, 0, 0, new byte[8]), $"t{i % 3:00}", G, [G[i % 3]], Tree, string.Empty))];

    private static IReadOnlyList<HandcheckClaim> Claims(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new HandcheckClaim(
            new ScoutClaimKey(new Guid(i, 0, 0, new byte[8]), 1, 1), "t01", new ScoutClaim("src/Alpha.cs", CapturedCount.Number(4), "defines the type"),
            i % 2 == 0 ? ScoutClaimVerdict.Wrong : ScoutClaimVerdict.Right))];
}
