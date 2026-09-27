using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>Where each planted seed's evidence sat for the reviewer, read off the product's own turn-1 prompt.
/// Seed recall is reported apart from evidence availability: a seed the product never showed is not a seed
/// the reviewer missed.</summary>
public sealed class SeedEvidenceTests
{
    [Fact]
    public void A_seed_whose_planted_line_is_in_the_prompt_sat_in_the_pack()
    {
        var seed = Seed(file: "src/Orders/Total.cs", @new: "    return lines.Sum(l => l.Price);");

        SeedEvidence.Classify(seed, "diff --git a/src/Orders/Total.cs\n+    return lines.Sum(l => l.Price);\n")
            .Should().Be(EvidenceWhere.Pack);
    }

    [Fact]
    public void A_seed_that_removes_a_line_is_in_the_pack_when_its_file_is()
    {
        var removal = Seed(file: "src/Orders/Total.cs", @new: "");

        SeedEvidence.Classify(removal, "diff --git a/src/Orders/Total.cs\n-    Guard(lines);\n").Should().Be(EvidenceWhere.PackByFile);
        SeedEvidence.Classify(removal, "diff --git a/src/Other.cs\n").Should().Be(EvidenceWhere.OnRequest);
    }

    [Fact]
    public void A_seed_the_pack_does_not_carry_is_on_request()
    {
        SeedEvidence.Classify(Seed(file: "src/Orders/Total.cs", @new: "planted"), "diff --git a/src/Other.cs\n")
            .Should().Be(EvidenceWhere.OnRequest);
    }

    [Theory]
    [InlineData("config/credentials.json")]
    [InlineData("src/Secrets.cs")]
    [InlineData(".env.production")]
    [InlineData("auth/token-store.ts")]
    public void A_credential_shaped_file_is_withheld_whatever_the_prompt_says(string file)
    {
        SeedEvidence.Classify(Seed(file: file, @new: "planted"), "planted").Should().Be(EvidenceWhere.Withheld,
            "the product never serves a credential-shaped file, so the reviewer could not have seen it");
    }

    [Fact]
    public void Without_a_prompt_the_answer_is_unknown_not_on_request()
    {
        SeedEvidence.Classify(Seed(file: "src/A.cs", @new: "planted"), string.Empty).Should().Be(EvidenceWhere.Unknown);
    }

    [Fact]
    public void Only_the_first_sixty_characters_of_the_first_planted_line_are_looked_for()
    {
        var longLine = new string('x', 80);
        var seed = Seed(file: "src/A.cs", @new: longLine + "\nsecond line");

        SeedEvidence.Classify(seed, new string('x', 60)).Should().Be(EvidenceWhere.Pack, "the Python probe is the first line capped at 60");
    }

    private static SeedSpec Seed(string file, string @new) =>
        SeedSpec.Of("cs2-S1", file, "old", @new, "what", "trigger", "mechanism", "consequence", crossEpic: false).Ok();
}
