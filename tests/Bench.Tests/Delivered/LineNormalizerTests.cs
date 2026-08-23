using Bench.Delivered;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Delivered;

/// <summary>A size that does not depend on the author's line-break taste.
/// <para>
/// The C# adaptations are what these mostly pin. The source drops any line starting <c>#</c> — right for
/// PHP, and in C# that is the preprocessor: dropping <c>#if</c> does not remove a comment, it MERGES two
/// mutually exclusive branches into a statement that never existed. The string masker is the other half:
/// C# verbatim and raw literals break the source's escape model, and a mask left open swallows every
/// <c>//</c> after it.
/// </para></summary>
public sealed class LineNormalizerTests
{
    private const string CSharp = "src/Thing.cs";

    private const string Php = "src/Thing.php";

    [Fact]
    public void A_PREPROCESSOR_CONDITIONAL_survives_because_it_decides_what_compiles()
    {
        string[] lines = ["#if DEBUG", "var x = 1;", "#else", "var x = 2;", "#endif"];

        var normalized = LineNormalizer.Normalize(lines, CSharp);

        // Dropping these merges two branches into one nonsense statement, and the joiner then folds the
        // wreckage into a logical line. The source's rule would have dropped all three.
        normalized.Should().Contain("#if DEBUG").And.Contain("#else").And.Contain("#endif");
    }

    [Theory]
    [InlineData("#region Plumbing")]
    [InlineData("#endregion")]
    [InlineData("#pragma warning disable CA1822")]
    [InlineData("#nullable enable")]
    public void A_NON_LOGIC_DIRECTIVE_still_drops(string line)
    {
        // Presentation and compiler settings. None of them changes which statements compile, which is
        // exactly what separates them from #if.
        LineNormalizer.IsDroppable(line, LanguageProfile.CSharp).Should().BeTrue();
    }

    [Fact]
    public void A_HASH_COMMENT_still_drops_where_hash_IS_a_comment()
    {
        // The inherited behaviour, kept intact so the source's fixtures still measure identically.
        LineNormalizer.IsDroppable("# a php comment", LanguageProfile.Curly).Should().BeTrue();
        LineNormalizer.IsDroppable("#[Route('/x')]", LanguageProfile.Curly).Should().BeFalse();
    }

    [Theory]
    [InlineData("// a line comment")]
    [InlineData("/* a block */")]
    [InlineData("/// <summary>docs</summary>")]
    [InlineData("* a docblock continuation")]
    public void C_SHARP_comments_all_drop_including_the_doc_ones(string line) =>
        LineNormalizer.IsDroppable(line, LanguageProfile.CSharp).Should().BeTrue();

    [Fact]
    public void A_C_SHARP_ATTRIBUTE_is_a_statement_and_was_never_a_comment()
    {
        // Why ConfiguringAnnotations is EMPTY for C#: there is nothing to rescue from the comment rule,
        // because attributes are real [ … ] lines that the rule never touched.
        LineNormalizer.IsDroppable("[Fact]", LanguageProfile.CSharp).Should().BeFalse();
        LanguageProfile.CSharp.ConfiguringAnnotations.Should().BeEmpty();
    }

    [Fact]
    public void A_URL_inside_a_string_is_not_read_as_a_comment()
    {
        var kept = LineNormalizer.StripInlineComment(
            """var url = "https://example.invalid/x"; // the real comment""", LanguageProfile.CSharp);

        kept.Should().Contain("https://example.invalid/x").And.NotContain("the real comment");
    }

    [Fact]
    public void A_VERBATIM_string_ending_in_a_backslash_does_not_swallow_the_rest_of_the_line()
    {
        // The C# masker's reason for existing. Under the source's escape model the closing quote of
        // @"C:\path\" reads as escaped, the mask stays open to end of line, and the trailing comment — plus
        // every // after it — becomes invisible. The symptom is a line that keeps its comment.
        var kept = LineNormalizer.StripInlineComment(
            """var p = @"C:\path\"; // drop me""", LanguageProfile.CSharp);

        kept.Should().NotContain("drop me");
        kept.Should().Contain(@"C:\path\");
    }

    [Fact]
    public void A_RAW_string_may_contain_quotes_and_still_close()
    {
        var kept = LineNormalizer.StripInlineComment(
            """"var j = """{"a":"b"}"""; // drop me"""", LanguageProfile.CSharp);

        kept.Should().NotContain("drop me");
    }

    [Fact]
    public void A_WRAPPED_CALL_costs_the_same_as_the_one_liner_it_could_have_been()
    {
        string[] wrapped = ["var q = builder", ".Where(x => x.Id > 0)", ".OrderBy(x => x.Name)", ".ToList();"];
        string[] oneLine = ["var q = builder.Where(x => x.Id > 0).OrderBy(x => x.Name).ToList();"];

        // The whole point of the family: the metric must not measure the author's line-break taste. The
        // leading-dot chain head the source built for PHP -> chains covers C# LINQ unchanged.
        LineNormalizer.NormalizeAndJoin(wrapped, CSharp).Should().HaveCount(1);
        LineNormalizer.NormalizeAndJoin(wrapped, CSharp).Sum(LineNormalizer.WrappedLineCount)
            .Should().Be(LineNormalizer.NormalizeAndJoin(oneLine, CSharp).Sum(LineNormalizer.WrappedLineCount));
    }

    [Fact]
    public void A_STATEMENT_that_ENDS_does_not_attach_to_the_next_one()
    {
        string[] lines = ["var a = 1;", "var b = 2;"];

        LineNormalizer.NormalizeAndJoin(lines, CSharp).Should().HaveCount(2);
    }

    [Fact]
    public void A_long_logical_line_is_WRAPPED_so_length_still_costs()
    {
        // Joining must not make a 300-character statement free. It counts as the physical lines it would
        // have taken at the inherited 100-character width.
        LineNormalizer.WrappedLineCount(new string('x', 250)).Should().Be(3);
        LineNormalizer.WrappedLineCount(string.Empty).Should().Be(0);
    }

    [Fact]
    public void A_STYLESHEET_rule_head_opens_a_rule_rather_than_continuing_one()
    {
        string[] lines = ["a { color: red }", ".btn { color: blue }"];

        // Inherited behaviour: in CSS a leading `.` opens a selector. Reading it as a fluent-chain head
        // would glue every rule in the file into one logical line.
        LineNormalizer.NormalizeAndJoin(lines, "site.css").Should().HaveCount(2);
    }

    [Fact]
    public void A_PHP_docblock_annotation_that_CONFIGURES_still_survives()
    {
        // The inherited distinction, and the reason the C# profile's empty list is a decision rather than
        // an omission: on that stack a mapping annotation IS code, written inside a comment.
        LineNormalizer.IsDroppable(" * @ORM\\Column(type=\"string\")", LanguageProfile.Curly).Should().BeFalse();
        LineNormalizer.IsDroppable(" * @param string $name", LanguageProfile.Curly).Should().BeTrue();
        LineNormalizer.IsDroppable(" * @ORM\\Column(type=\"string\")", LanguageProfile.CSharp).Should().BeTrue();
    }

    [Fact]
    public void A_hash_in_the_MIDDLE_of_a_C_SHARP_line_does_not_truncate_it()
    {
        // The source cuts at the first `#` outside a string. In C# that is not a comment opener at all, so
        // cutting there would silently drop the tail of a real statement.
        LineNormalizer.StripInlineComment("""var s = Colour("#fff") + 1;""", LanguageProfile.CSharp)
            .Should().Be("""var s = Colour("#fff") + 1;""");

        LineNormalizer.StripInlineComment("$s = 'x'; # gone", LanguageProfile.Curly).Should().Be("$s = 'x'; ");
    }

    [Fact]
    public void Whitespace_is_collapsed_so_indentation_costs_nothing() =>
        LineNormalizer.CollapseWhitespace("    var    x =  1;   ").Should().Be("var x = 1;");

    [Fact]
    public void A_PHP_file_still_normalizes_the_way_the_source_measured_it()
    {
        string[] lines = ["<?php", "# comment", "$a = 1;", "", "// another", "$b = 2;"];

        // Parity: the inherited profile must keep producing the inherited answer, or the port's evidence
        // is about a normalizer nobody measured.
        LineNormalizer.Normalize(lines, Php).Should().BeEquivalentTo(["<?php", "$a = 1;", "$b = 2;"]);
    }
}
