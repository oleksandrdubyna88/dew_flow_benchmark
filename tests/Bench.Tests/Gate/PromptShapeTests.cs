using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate;

/// <summary>The turn-1 prompt's SHAPE (E7, S7.2a): the prompt with the two things the product writes per run — its session
/// id on every section fence and its data directory on the one gate-history line — learned and replaced, and NOTHING
/// else touched. The vectors are synthetic, shaped like the measured prompts (`p2-grok-4.7-cs2-r1`/`-r2`: 8 fences, one
/// history line, 18 differing lines between them), with no private content.</summary>
public sealed class PromptShapeTests
{
    [Fact]
    public void Two_runs_differing_only_in_their_session_and_data_directory_have_one_shape()
    {
        var first = PromptShape.Of(Prompt("fb0f5dd6", @"C:\runs\p2-grok-4.7-cs2-r1\data"));
        var second = PromptShape.Of(Prompt("0eaabe87", @"C:\runs\p2-grok-4.7-cs2-r2\data"));

        first.Sha256.Should().Be(second.Sha256, "the session id and the data directory are per run by construction");
        PromptShape.Compare(first, second).Should().BeOfType<PromptComparison.Same>();
        first.Sha256.Should().Be(Hash(Prompt("<session>", "<data-dir>").Replace("<data-dir>/coai.db", @"<data-dir>\coai.db", StringComparison.Ordinal)),
            "only the id on the fences and the directory on the history line are replaced — the separator stays");
    }

    [Fact]
    public void An_id_in_parentheses_inside_the_tasks_own_text_is_left_alone_and_reported()
    {
        var first = PromptShape.Of(Prompt("fb0f5dd6", "/d", body: "var marker = Find(deadbeef) ---"));
        var second = PromptShape.Of(Prompt("0eaabe87", "/d", body: "var marker = Find(cafebabe) ---"));

        var compared = PromptShape.Compare(first, second);

        compared.Should().BeOfType<PromptComparison.Differs>("only whole fence lines carry the session id");
        ((PromptComparison.Differs)compared).Lines.Should().Equal(BodyLine);
    }

    [Fact]
    public void A_fence_carrying_a_second_id_makes_the_shape_ambiguous_and_names_its_line()
    {
        var mixed = Prompt("fb0f5dd6", "/d").Replace("--- epics — claims (fb0f5dd6) ---", "--- epics — claims (12345678) ---", StringComparison.Ordinal);

        var shape = PromptShape.Of(mixed);

        shape.AmbiguousLines.Should().Equal(5);
        PromptShape.Compare(shape, PromptShape.Of(Prompt("0eaabe87", "/e"))).Should().BeOfType<PromptComparison.Ambiguous>()
            .Which.Lines.Should().Equal(5);
    }

    [Fact]
    public void A_second_gate_history_line_is_ambiguous_rather_than_normalised()
    {
        var twice = Prompt("fb0f5dd6", "/d", body: "Gate history unavailable: there is no rounds database at /x/coai.db.");

        PromptShape.Of(twice).AmbiguousLines.Should().Equal([BodyLine, HistoryLine], "neither can be told apart as THE product's line");
    }

    [Fact]
    public void A_quoted_copy_of_the_history_sentence_is_the_tasks_text_and_stays()
    {
        var first = PromptShape.Of(Prompt("fb0f5dd6", "/d", body: "    \"Gate history unavailable: there is no rounds database at /a/coai.db.\""));
        var second = PromptShape.Of(Prompt("0eaabe87", "/e", body: "    \"Gate history unavailable: there is no rounds database at /b/coai.db.\""));

        PromptShape.Compare(first, second).Should().BeOfType<PromptComparison.Differs>().Which.Lines.Should().Equal(BodyLine);
    }

    [Fact]
    public void Line_endings_are_not_folded_because_the_reviewer_reads_the_bytes()
    {
        var lf = PromptShape.Of(Prompt("fb0f5dd6", "/d"));
        var crlf = PromptShape.Of(Prompt("0eaabe87", "/d").Replace("\n", "\r\n", StringComparison.Ordinal));

        var compared = PromptShape.Compare(lf, crlf);

        compared.Should().BeOfType<PromptComparison.Differs>("a clone checked out with other line endings is another input");
        crlf.AmbiguousLines.Should().BeEmpty("a CRLF fence is still a fence — its id is learned and the \\r is kept");
        crlf.Sha256.Should().Be(Hash(Prompt("<session>", "<data-dir>").Replace("\n", "\r\n", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_inserted_line_reports_where_it_began_and_counts_every_shifted_position()
    {
        var first = PromptShape.Of(Prompt("fb0f5dd6", "/d"));
        var inserted = PromptShape.Of(Prompt("0eaabe87", "/d", body: "unchanged body line\nan extra line"));

        var compared = (PromptComparison.Differs)PromptShape.Compare(first, inserted);

        compared.Lines[0].Should().Be(BodyLine + 1, "the first differing position is where the insertion began");
        compared.Count.Should().Be(PromptLineCount - BodyLine + 1, "every later line shifted, and the longer side has one more");
    }

    [Fact]
    public void At_most_twenty_positions_are_listed_and_the_count_says_how_many_there_were()
    {
        var first = PromptShape.Of(string.Join('\n', Enumerable.Range(0, 30).Select(i => $"line {i}")));
        var second = PromptShape.Of(string.Join('\n', Enumerable.Range(0, 30).Select(i => $"LINE {i}")));

        var compared = (PromptComparison.Differs)PromptShape.Compare(first, second);

        compared.Count.Should().Be(30);
        compared.Lines.Should().Equal(Enumerable.Range(1, PromptComparison.ListedLines));
    }

    [Fact]
    public void The_shape_hash_is_the_sha256_of_the_normalised_text()
    {
        var shape = PromptShape.Of("--- the plan (fb0f5dd6) ---");

        shape.Sha256.Should().Be(Hash("--- the plan (<session>) ---"));
    }

    [Fact]
    public void A_windows_and_a_unix_data_directory_are_two_inputs_because_the_separator_is_not_part_of_the_directory()
    {
        var windows = PromptShape.Of(Prompt("fb0f5dd6", @"C:\runs\a\data"));
        var unix = PromptShape.Of(Prompt("0eaabe87", "/home/runs/b/data"));

        PromptShape.Compare(windows, unix).Should().BeOfType<PromptComparison.Differs>().Which.Lines.Should().Equal(HistoryLine);
    }

    [Fact]
    public void A_shape_turned_into_a_string_never_carries_the_prompt()
    {
        var shape = PromptShape.Of(Prompt("fb0f5dd6", "/d", body: "private source of a customer repository"));

        shape.ToString().Should().NotContain("private source").And.Contain(shape.Sha256);
    }

    [Fact]
    public void Two_session_ids_carried_equally_often_name_every_fence_rather_than_pick_one()
    {
        var split = string.Join('\n', "--- a (11111111) ---", "body", "--- b (22222222) ---");

        PromptShape.Of(split).AmbiguousLines.Should().Equal(1, 3);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    private const int BodyLine = 3;

    private const int HistoryLine = 9;

    private static int PromptLineCount => Prompt("fb0f5dd6", "/d").Split('\n').Length;

    /// <summary>A prompt shaped like the product's: fences carrying the session id, one body line (line 3), the gate
    /// history naming the data directory.</summary>
    private static string Prompt(string session, string dataDir, string body = "unchanged body line") =>
        string.Join('\n',
            "You review a feature.",
            $"--- the plan — the scope ({session}) ---",
            body,
            $"--- end of the plan — the scope ({session}) ---",
            $"--- epics — claims ({session}) ---",
            "- one epic",
            $"--- end of epics — claims ({session}) ---",
            $"--- gate history — evidence, not proof ({session}) ---",
            $"Gate history unavailable: there is no rounds database at {dataDir}{(dataDir.Contains('\\') ? '\\' : '/')}coai.db.",
            $"--- end of gate history — evidence, not proof ({session}) ---",
            "Return ONLY a JSON object.");
}
