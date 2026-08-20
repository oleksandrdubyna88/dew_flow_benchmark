using Bench.Application;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>Reading an authored suite file. Every path here is a human hand-editing JSON, so every failure
/// is an ordinary answer the caller renders — never an exception that unwinds a run.</summary>
public sealed class SuiteJsonLoaderTests
{
    private static readonly CommitSha Commit = CommitSha.Parse(new string('a', 40)).Ok();

    [Fact]
    public void A_well_formed_file_loads_and_comes_out_frozen_at_version_one()
    {
        var suite = SuiteJsonLoader.Load("""
        {
          "id": "demo",
          "questions": [
            { "id": "q1", "prompt": "where is the total computed?",
              "expectations": [ { "kind": "Member", "file": "src/Orders.cs", "member": "OrderService.Total", "start": 10, "end": 24 } ] }
          ]
        }
        """, Commit).Ok();

        suite.IsFrozen.Should().BeTrue("what gets measured is the frozen version, never the file");
        suite.Version.Should().Be(1);
        suite.Stamp.Should().StartWith("demo@v1#");
    }

    [Fact]
    public void Every_anchor_is_stamped_with_the_commit_the_suite_was_authored_against()
    {
        var suite = SuiteJsonLoader.Load(
            """{"id":"demo","questions":[{"id":"q1","prompt":"p","expectations":[{"kind":"File","file":"src/A.cs"}]}]}""",
            Commit).Ok();

        suite.Questions.Single().Expectations.Single().Anchor.AuthoredAt.Should().Be(Commit);
    }

    [Fact]
    public void A_member_anchor_keeps_its_line_span_and_a_file_anchor_makes_no_line_claim()
    {
        var suite = SuiteJsonLoader.Load("""
        {"id":"d","questions":[{"id":"q1","prompt":"p","expectations":[
          {"kind":"Member","file":"src/A.cs","member":"A.Foo","start":10,"end":20},
          {"kind":"File","file":"src/B.cs"}]}]}
        """, Commit).Ok();

        var expectations = suite.Questions.Single().Expectations;
        expectations[0].Anchor.Lines.Should().Be(new LineSpan(10, 20));
        expectations[1].Anchor.IsWholeFile.Should().BeTrue();
    }

    [Fact]
    public void Windows_separators_in_an_authored_path_are_normalised()
    {
        var suite = SuiteJsonLoader.Load(
            """{"id":"d","questions":[{"id":"q1","prompt":"p","expectations":[{"kind":"File","file":"src\\Nested\\A.cs"}]}]}""",
            Commit).Ok();

        suite.Questions.Single().Expectations.Single().Anchor.FilePath
            .Should().Be("src/Nested/A.cs", "a suite authored on Windows is replayed on the CI runner");
    }

    [Fact]
    public void Malformed_json_is_an_answer_naming_what_is_wrong()
    {
        var refused = SuiteJsonLoader.Load("""{"id": "demo", "questions": [ """, Commit);

        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("not valid JSON");
    }

    [Fact]
    public void A_file_with_no_id_is_refused()
    {
        SuiteJsonLoader.Load("""{"questions":[{"id":"q1","prompt":"p","expectations":[]}]}""", Commit)
            .Reason().Should().Contain("no id");
    }

    [Fact]
    public void A_file_with_no_questions_is_refused_because_an_empty_version_is_not_worth_a_number()
    {
        SuiteJsonLoader.Load("""{"id":"demo","questions":[]}""", Commit)
            .Reason().Should().Contain("no questions");
    }

    [Fact]
    public void An_empty_document_is_refused_rather_than_read_as_an_empty_suite()
    {
        SuiteJsonLoader.Load("null", Commit).Reason().Should().Contain("empty");
    }

    [Fact]
    public void Comments_and_a_trailing_comma_are_tolerated_because_a_human_wrote_this_file()
    {
        var suite = SuiteJsonLoader.Load("""
        {
          // the first question
          "id": "demo",
          "questions": [
            { "id": "q1", "prompt": "p", "expectations": [ { "kind": "File", "file": "src/A.cs" } ] },
          ]
        }
        """, Commit);

        suite.Failed().Should().BeFalse();
    }

    /// <summary>
    /// <b>This test REVERSES a decision that was pinned here, and the reversal is the point.</b>
    ///
    /// <para>It used to read <c>An_unrecognised_expectation_kind_falls_back_to_File_rather_than_failing_the_batch</c>,
    /// and the fallback was deliberate: one bad entry should not cost a whole suite. That trade was
    /// defensible while the kinds were <c>File</c>, <c>Member</c>, <c>AnswerContains</c> and
    /// <c>AnswerExcludes</c> — four words that look nothing like each other, so a typo was unlikely and its
    /// cost was one loose expectation.</para>
    ///
    /// <para>Two things changed it. <c>ToolUsed</c> and <c>ToolNotUsed</c> differ from each other by three
    /// characters and from a misspelling by one, so the typo stopped being unlikely. And the cost was never
    /// really "one loose expectation": a <c>File</c> expectation against whatever path the entry carried —
    /// often none — is a retrieval anchor that can never be surfaced, so the question scores as a MISS
    /// forever and the author is told nothing. Every other unknown value in this system is refused by name;
    /// this was the exception, and it is not one any more.</para>
    /// </summary>
    [Fact]
    public void An_unrecognised_expectation_kind_REFUSES_the_suite_and_names_the_kinds_it_knows()
    {
        var refusal = SuiteJsonLoader.Load(
            """{"id":"d","questions":[{"id":"q1","prompt":"p","expectations":[{"kind":"Nonsense","file":"src/A.cs"}]}]}""",
            Commit);

        refusal.Failed().Should().BeTrue();
        refusal.Reason().Should().Contain("Nonsense")
            .And.Contain("q1", "a refusal that does not name the question leaves an author searching a file")
            .And.Contain("ToolUsed");
    }
}
