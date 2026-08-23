using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>What a changed file counts as.
/// <para>
/// Two of these are MEASUREMENT-PINNED rather than stylistic, and both are recorded in the source's own
/// comments: case-sensitive matching (a case-insensitive table once read a 9.42-hour ticket as 0 lines) and
/// first-match-wins order (so a lockfile under a vendored tree is never read as evidence). The rest pin the
/// C# band this port added, and that the additions changed nothing for the inherited rows.
/// </para></summary>
public sealed class PathCategoryTableTests
{
    [Theory]
    [InlineData("src/Rag.Api/SearchEndpoints.cs")]
    [InlineData("src/Bench.Domain/Runs/Matrix.cs")]
    [InlineData("app/Controller/OrderController.php")]
    public void Ordinary_source_is_COUNTED_because_the_default_is_to_price_a_file(string path)
    {
        // A tree this table has never seen is measured, never silently ignored. The opposite default would
        // let an unrecognised language score zero and look like a subject that wrote nothing.
        PathCategoryTable.Fate(path).Should().Be(PathFate.Counted);
        PathCategoryTable.Categorize(path).Should().Be("logic");
    }

    [Fact]
    public void Matching_is_CASE_SENSITIVE_and_that_cost_a_ticket_its_whole_size()
    {
        // The pinned one. Hand-written data-fix commands lived under Command/Migration/IssueNNNN/ while
        // schema migrations lived in doctrine_migrations/; a case-insensitive table read the first as the
        // second and priced a 9.42-hour ticket at 0 lines.
        PathCategoryTable.Fate("src/Command/Migration/Issue4711/FixOrders.php").Should().Be(PathFate.Counted);
        PathCategoryTable.Fate("src/doctrine_migrations/Version20240101.php").Should().Be(PathFate.Evidence);
    }

    [Fact]
    public void EF_migrations_are_evidence_WITHOUT_making_the_table_case_insensitive()
    {
        // The C# trap that mirrors the pinned one: EF's folder is `Migrations/`, capitalised, and the
        // inherited row spells it lowercase. A separate row is the fix; relaxing the case rule is not.
        PathCategoryTable.Fate("src/Bench.Infrastructure/Persistence/Migrations/20260817_Init.cs")
            .Should().Be(PathFate.Evidence);
        PathCategoryTable.Categorize("src/Bench.Infrastructure/Persistence/Migrations/20260817_Init.cs")
            .Should().Be("migration-ef");
    }

    [Fact]
    public void ORDER_decides_when_two_rows_could_claim_one_path()
    {
        // A lockfile inside a vendored tree matches both rows. First match wins, the no-value trees come
        // first, and so it drops rather than being read as anything.
        PathCategoryTable.Fate("vendor/acme/lib/composer.lock").Should().Be(PathFate.Dropped);
        PathCategoryTable.Categorize("vendor/acme/lib/composer.lock").Should().Be("vendored");
    }

    [Theory]
    [InlineData("src/Bench.Domain/obj/Debug/net10.0/Bench.Domain.AssemblyInfo.cs")]
    [InlineData("tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.dll")]
    public void Build_output_is_DROPPED_rather_than_priced(string path)
    {
        // The C# tree a stray `git add -A` sweeps in by the thousand. Counted, it would dwarf every real
        // number in the diff; as evidence it would claim a generated assembly proves something.
        PathCategoryTable.Fate(path).Should().Be(PathFate.Dropped);
    }

    [Theory]
    [InlineData("src/Web/Resources.Designer.cs")]
    [InlineData("src/Web/Views_Home.g.cs")]
    [InlineData("src/Web/Model.generated.cs")]
    public void Generated_CSharp_is_EVIDENCE_not_size(string path)
    {
        // Tooling wrote it, so it prices nothing — but it stays visible, because a point anchored in
        // generated code must remain findable.
        PathCategoryTable.Fate(path).Should().Be(PathFate.Evidence);
    }

    [Theory]
    [InlineData("src/Bench.Delivered/Bench.Delivered.csproj")]
    [InlineData("Directory.Build.props")]
    [InlineData("dew_flow_benchmark.slnx")]
    public void Build_SHAPE_is_proof_rather_than_priced_logic(string path)
    {
        // A csproj change is real evidence that a project moved. Pricing it as implementation would pay for
        // a one-line package bump like a feature.
        PathCategoryTable.Fate(path).Should().Be(PathFate.Evidence);
    }

    [Theory]
    [InlineData("tests/Unit/OrderTest.php", PathFate.Evidence)]
    [InlineData("features/behat/checkout.feature", PathFate.Evidence)]
    [InlineData("node_modules/left-pad/index.js", PathFate.Dropped)]
    [InlineData("src/Api/Generated/PetApiClient.php", PathFate.Evidence)]
    [InlineData("README.md", PathFate.Evidence)]
    [InlineData(".claude/rules/common/testing.md", PathFate.Dropped)]
    public void The_C_SHARP_additions_changed_NOTHING_for_the_inherited_rows(string path, PathFate fate)
    {
        // Additions only, no re-fates. If a new row had shadowed an inherited one, the port's parity with
        // the measured baseline would be gone and nothing else would say so.
        PathCategoryTable.Fate(path).Should().Be(fate);
    }

    [Fact]
    public void Every_inherited_row_is_MARKED_as_inherited()
    {
        // The same discipline the weighting protocol uses: a rule accepted on another stack's evidence is
        // distinguishable from one this project measured, so a later recalibration knows what to re-check.
        PathCategoryTable.Seed.Where(r => r.Inherited).Should().HaveCountGreaterThan(5);
        PathCategoryTable.Seed.Where(r => !r.Inherited).Select(r => r.Name)
            .Should().BeEquivalentTo(["build-output", "migration-ef", "generated-csharp", "build-shape"]);
    }

    [Fact]
    public void A_SINGLE_star_does_not_cross_a_directory_boundary_and_a_double_one_does()
    {
        // The dialect is public contract because callers write their own exclusion globs. Someone guessing
        // that `*` spans directories writes an exclusion that silently matches nothing — and an exclusion
        // that matches nothing looks exactly like one that had nothing to exclude.
        PathCategoryTable.MatchesGlob("**/*.cs", "src/deep/Thing.cs").Should().BeTrue();
        PathCategoryTable.MatchesGlob("*.cs", "src/Thing.cs").Should().BeFalse();
        PathCategoryTable.MatchesGlob("**/*.cs", "Thing.cs").Should().BeTrue("**/ matches no leading directories too");
    }

    [Fact]
    public void An_ALTERNATION_matches_either_branch_and_a_QUESTION_MARK_one_character()
    {
        PathCategoryTable.MatchesGlob("a/{b,c}/d", "a/c/d").Should().BeTrue();
        PathCategoryTable.MatchesGlob("a/{b,c}/d", "a/e/d").Should().BeFalse();
        PathCategoryTable.MatchesGlob("v?/x", "v1/x").Should().BeTrue();
        PathCategoryTable.MatchesGlob("v?/x", "v11/x").Should().BeFalse();
    }

    [Fact]
    public void An_extra_exclusion_glob_reaches_the_cleaner_in_the_same_dialect()
    {
        var diff = string.Join('\n', [
            "diff --git a/src/Keep.cs b/src/Keep.cs", "+var a = 1;",
            "diff --git a/src/spikes/Throwaway.cs b/src/spikes/Throwaway.cs", "+var b = 2;",
        ]);

        var cleaned = DiffCleaner.Clean(diff, ["**/spikes/**"]);

        cleaned.ExcludedPaths.Should().ContainSingle().Which.Should().Be("src/spikes/Throwaway.cs");
    }
}
