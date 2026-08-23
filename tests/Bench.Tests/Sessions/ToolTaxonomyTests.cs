using Bench.Domain.Sessions;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Sessions;

/// <summary>The taxonomy, and the defect it exists because of.
/// <para>
/// The specification this system was built from named the tools <c>read_file</c>, <c>edit_file</c> and
/// <c>execute_command</c>. Claude Code's tools are called <c>Read</c>, <c>Edit</c> and <c>Bash</c>. A
/// classifier keyed on the invented names would have labelled every call <see cref="ToolClass.Unknown"/>
/// while reporting confident phase totals over nothing — so the refuted names are asserted here BY NAME,
/// the way this repository pins every defect it has already paid for.
/// </para></summary>
public sealed class ToolTaxonomyTests
{
    private static readonly ToolTaxonomy Taxonomy = ToolTaxonomy.ClaudeCode;

    [Theory]
    [InlineData("Read")]
    [InlineData("Grep")]
    [InlineData("Glob")]
    [InlineData("WebFetch")]
    public void The_real_read_tools_are_reads(string tool) =>
        Taxonomy.Classify(tool, string.Empty).Should().Be(ToolClass.Read);

    [Theory]
    [InlineData("Edit")]
    [InlineData("Write")]
    [InlineData("NotebookEdit")]
    public void The_real_write_tools_are_writes(string tool) =>
        Taxonomy.Classify(tool, string.Empty).Should().Be(ToolClass.Write);

    [Theory]
    [InlineData("read_file")]
    [InlineData("edit_file")]
    [InlineData("execute_command")]
    public void The_names_the_draft_invented_classify_as_unknown_rather_than_as_anything(string invented) =>
        Taxonomy.Classify(invented, string.Empty).Should().Be(ToolClass.Unknown,
            "an unclassified tool must be visible as a hole in this table, never rendered as harmless");

    [Fact]
    public void An_mcp_tool_is_matched_by_its_own_name_whatever_server_mounts_it()
    {
        Taxonomy.Classify("mcp__dewflow__rag_search_project_context", string.Empty)
            .Should().Be(ToolClass.Read);

        Taxonomy.Classify("mcp__anything__rt_apply_edit_plan", string.Empty)
            .Should().Be(ToolClass.Write,
                "the one write verb in this family, named rather than matched by prefix");
    }

    [Fact]
    public void A_shell_call_is_classified_by_its_command_and_not_by_its_name()
    {
        Taxonomy.Classify("Bash", "git status --porcelain").Should().Be(ToolClass.Read);
        Taxonomy.Classify("Bash", "dotnet build src/x.slnx").Should().Be(ToolClass.Verify);
        Taxonomy.Classify("Bash", "rm -rf build").Should().Be(ToolClass.Write);
    }

    [Fact]
    public void An_unrecognised_command_is_a_write_because_the_two_errors_are_not_symmetrical()
    {
        Taxonomy.Classify("Bash", "some-unknown-tool --go").Should().Be(ToolClass.Write,
            "a write mistaken for a read hides an edit from every detector; a read mistaken for a write "
            + "costs one over-counted call that the tree check then contradicts in the open");
    }

    [Fact]
    public void A_chained_command_takes_the_strongest_class_any_segment_carries()
    {
        CommandClassifier.Classify("cd src && dotnet build").Should().Be(ToolClass.Verify,
            "matching the leading words alone would file a build under 'cd'");

        CommandClassifier.Classify("git status && rm -rf out").Should().Be(ToolClass.Write);
    }

    [Fact]
    public void The_allowlist_matches_whole_words_and_never_a_bare_prefix()
    {
        CommandClassifier.Classify("lsof -i :5177").Should().Be(ToolClass.Write,
            "'lsof' is not 'ls' — a prefix match is how an allowlist quietly admits everything sharing "
            + "three letters with something safe");
    }

    [Fact]
    public void A_build_failure_is_read_only_from_a_verification_call()
    {
        CommandClassifier.Failed(ToolClass.Verify, true, "Foo.cs(3,5): error CS1002: ; expected")
            .Should().BeTrue();

        CommandClassifier.Failed(ToolClass.Read, true, "Foo.cs(3,5): error CS1002: ; expected")
            .Should().BeFalse("a grep whose hits contain 'error CS' is not a compile failure");
    }

    [Fact]
    public void An_uncaptured_response_is_never_read_as_a_passing_build() =>
        CommandClassifier.Failed(ToolClass.Verify, false, string.Empty).Should().BeFalse();

    [Fact]
    public void A_search_tool_is_recognised_even_when_this_table_has_never_seen_it()
    {
        Taxonomy.IsSearch("Grep").Should().BeTrue();
        Taxonomy.IsSearch("mcp__somebody__their_search_thing").Should().BeTrue();
        Taxonomy.IsSearch("Read").Should().BeFalse();
    }
}
