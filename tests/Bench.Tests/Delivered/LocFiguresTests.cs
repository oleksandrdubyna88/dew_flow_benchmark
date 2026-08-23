using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>The cleaned-LOC family, end to end over real unified diffs.
/// <para>
/// What these pin is the property the whole port exists for: <b>volume must not buy size</b>. A diff of
/// blank lines and comments cleans to nothing, a wrapped statement costs what the one-liner costs, and the
/// three fates land in three different figures rather than one total that hides them.
/// </para></summary>
public sealed class LocFiguresTests
{
    [Fact]
    public void A_diff_of_COMMENTS_AND_BLANKS_cleans_to_nothing_while_the_raw_count_sees_it_all()
    {
        var figures = LocCalculator.Compute(Diff("src/Thing.cs", [
            "// a comment",
            "",
            "/// <summary>docs</summary>",
            "   ",
        ]));

        // The inflation case in miniature. Six raw lines of nothing, priced at zero — and the raw figure
        // still reports them, so the gap between the two is visible rather than silently absorbed.
        figures.Cleaned.Should().Be(0);
        figures.Diff.Should().Be(4);
        figures.FilesCounted.Should().Be(1);
    }

    [Fact]
    public void REMOVALS_count_toward_the_cleaned_figure()
    {
        var diff = string.Join('\n', [
            "diff --git a/src/Thing.cs b/src/Thing.cs",
            "--- a/src/Thing.cs",
            "+++ b/src/Thing.cs",
            "+var added = 1;",
            "-var removed = 2;",
        ]);

        // Measured on the source's corpus: deletions carry signal an additions-only figure misses, which is
        // why uncleaned churn outperformed the additions-only headline there.
        var figures = LocCalculator.Compute(diff);

        figures.Cleaned.Should().Be(2);
        figures.Added.Should().Be(1, "Added is the additions half, kept beside churn rather than instead of it");
    }

    [Fact]
    public void The_three_FATES_land_in_three_different_figures()
    {
        var figures = LocCalculator.Compute(string.Join('\n', [
            .. Lines("src/Thing.cs", ["var a = 1;"]),
            .. Lines("tests/ThingTests.cs", ["var b = 2;"]),
            .. Lines("node_modules/dep/index.js", ["var c = 3;"]),
        ]));

        // A test-only arm scores a real Excluded and a zero Cleaned. One total would report it as work.
        figures.Cleaned.Should().Be(1);
        figures.Excluded.Should().Be(1);
        figures.FilesCounted.Should().Be(1);
        figures.FilesEvidence.Should().Be(1);
        figures.FilesDropped.Should().Be(1);
    }

    [Fact]
    public void A_DROPPED_tree_is_absent_from_the_raw_figure_too()
    {
        var withVendor = LocCalculator.Compute(string.Join('\n', [
            .. Lines("src/Thing.cs", ["var a = 1;"]),
            .. Lines("vendor/huge/lib.php", [.. Enumerable.Repeat("$x = 1;", 500)]),
        ]));

        // "The diff as it arrived" is about what was PRICED, not about what git happened to carry: a
        // vendored tree left in the raw figure would dwarf every real number beside it.
        withVendor.Diff.Should().Be(1);
        withVendor.FilesDropped.Should().Be(1);
    }

    [Fact]
    public void WRAPPING_a_statement_does_not_change_what_it_costs()
    {
        var wrapped = LocCalculator.Compute(Diff("src/Thing.cs", [
            "var q = builder",
            "    .Where(x => x.Id > 0)",
            "    .ToList();",
        ]));

        var oneLine = LocCalculator.Compute(Diff("src/Thing.cs", [
            "var q = builder.Where(x => x.Id > 0).ToList();",
        ]));

        wrapped.Cleaned.Should().Be(oneLine.Cleaned);
        wrapped.Physical.Should().Be(3, "the unjoined figure is kept so the join's influence stays measurable");
        wrapped.Added.Should().Be(1);
    }

    [Fact]
    public void An_empty_diff_is_zero_rather_than_a_crash()
    {
        LocCalculator.Compute(string.Empty).Should().Be(LocFigures.None);
        LocCalculator.Compute([]).Should().Be(LocFigures.None);
    }

    [Fact]
    public void Several_diffs_fold_into_one_reading_because_a_task_may_span_repositories()
    {
        var figures = LocCalculator.Compute([
            Diff("a/src/Thing.cs", ["var a = 1;"]),
            Diff("b/src/Other.cs", ["var b = 2;"]),
        ]);

        figures.Cleaned.Should().Be(2);
        figures.FilesCounted.Should().Be(2);
    }

    [Fact]
    public void The_named_figures_are_readable_by_the_key_a_report_column_uses()
    {
        var figures = LocCalculator.Compute(Diff("src/Thing.cs", ["var a = 1;"]));

        figures.ValueOf(Figures.Cleaned).Should().Be(figures.Cleaned);
        figures.ValueOf(Figures.Diff).Should().Be(figures.Diff);
        figures.ValueOf(Figures.Excluded).Should().Be(figures.Excluded);
        figures.ValueOf("something nobody records").Should().Be(0);
    }

    [Fact]
    public void The_cleaner_NAMES_what_it_removed_rather_than_dropping_it_silently()
    {
        var cleaned = DiffCleaner.Clean(string.Join('\n', [
            .. Lines("src/Thing.cs", ["var a = 1;"]),
            .. Lines("node_modules/dep/index.js", ["var c = 3;"]),
        ]));

        cleaned.ExcludedPaths.Should().ContainSingle().Which.Should().Be("node_modules/dep/index.js");
        cleaned.Diff.Should().Contain("src/Thing.cs").And.NotContain("node_modules");
        cleaned.Truncated.Should().BeFalse();
    }

    [Fact]
    public void EVIDENCE_survives_the_cleaner_even_though_it_is_not_priced()
    {
        var cleaned = DiffCleaner.Clean(string.Join('\n', Lines("tests/ThingTests.cs", ["var b = 2;"])));

        // Excluded from SIZE, still shown: a point proven by a test has to remain findable in the prompt.
        cleaned.Diff.Should().Contain("tests/ThingTests.cs");
        cleaned.ExcludedPaths.Should().BeEmpty();
    }

    [Fact]
    public void A_diff_over_the_cap_is_cut_MID_FILE_and_says_so()
    {
        var huge = Diff("src/Thing.cs", [.. Enumerable.Repeat(new string('x', 1000), 400)]);

        var cleaned = DiffCleaner.Clean(huge);

        // Byte-identical to the reference on purpose: the marker lands inside the prompt, so keeping only
        // whole files would produce a different prompt hash than the run the calibration came from.
        cleaned.Truncated.Should().BeTrue();
        cleaned.Diff.Should().EndWith(DiffCleaner.TruncationMarker);
        cleaned.Diff.Length.Should().Be(DiffCleaner.MaxDiffChars + DiffCleaner.TruncationMarker.Length);
    }

    [Fact]
    public void The_changed_files_table_reports_raw_counts_per_file()
    {
        var files = DiffParser.ChangedFiles(string.Join('\n', [
            .. Lines("src/Thing.cs", ["var a = 1;"]),
            .. Lines("src/Other.cs", ["var b = 2;", "var c = 3;"]),
        ]));

        files.Select(f => f.Path).Should().BeEquivalentTo(["src/Thing.cs", "src/Other.cs"]);
        files.Single(f => f.Path == "src/Other.cs").Added.Should().Be(2);
    }

    // ---- scaffolding -------------------------------------------------------------------------------

    private static string Diff(string path, IReadOnlyList<string> added) => string.Join('\n', Lines(path, added));

    private static string[] Lines(string path, IReadOnlyList<string> added) =>
    [
        $"diff --git a/{path} b/{path}",
        $"--- a/{path}",
        $"+++ b/{path}",
        "@@ -0,0 +1,1 @@",
        .. added.Select(line => "+" + line),
    ];
}
