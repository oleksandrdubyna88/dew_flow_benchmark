using System.Text.Json.Nodes;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Probes;
using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The assessor's pure half (§3.7): every parseable claim of a stream keyed by (cell, scout ordinal, place), blinded with FRESH ids into
/// rows that carry nothing but the id, the path, the line and the claim; its own rubric and schema; and an answer read strictly — an id it
/// does not know, names twice or answers with another word is left unassessed rather than guessed.</summary>
public sealed class ScoutAssessmentTests
{
    private static ScoutClaimToAssess Claim(int index, string path = "src/Alpha.cs", string statement = "defines the type") =>
        new(new ScoutClaimKey(Guid.Parse("01900000-0000-7000-8000-000000000001"), 1, index), "t01", new ScoutClaim(path, CapturedCount.Number(index), statement));

    [Fact]
    public void Every_parseable_claim_of_a_cells_stream_is_keyed_by_its_scout_and_its_place()
    {
        var task = ScoutStoreFixtures.FrozenTask("t01");
        var arm = ScoutArms.Of(ScoutArmId.B1);
        var cell = Guid.CreateVersion7();

        var claims = ScoutAssessment.ClaimsOf(cell, task, arm, ClaudeSubagents.Read(ScoutCellStreams.For(task, arm)));

        // The synthetic stream: two scouts, each "- <G[i]>:1 — defines the type" and "- README.md — describes the project".
        claims.Select(c => (c.Key.Ordinal, c.Key.Index, c.Claim.Path, c.Claim.Statement)).Should().Equal(
            (1, 1, "src/Store.cs", "defines the type"), (1, 2, "README.md", "describes the project"),
            (2, 1, "src/Sweep.cs", "defines the type"), (2, 2, "README.md", "describes the project"));
        claims.Should().OnlyContain(c => c.Key.CellId == cell && c.Task == "t01");
    }

    [Fact]
    public void Blinding_mints_fresh_distinct_ids_shuffles_and_leaves_the_row_nothing_but_id_path_line_and_claim()
    {
        var claims = Enumerable.Range(1, 30).Select(i => Claim(i)).ToList();

        var blind = ScoutAssessment.Blind(claims, new Random(5));

        blind.Select(b => b.Id.Value).Should().OnlyHaveUniqueItems().And.OnlyContain(id => BlindedId.Parse(id) is Outcome<BlindedId>.Ok);
        blind.Select(b => b.Claim.Key.Index).Should().NotEqual(Enumerable.Range(1, 30), "the order must not say where a claim came from");
        var row = JsonNode.Parse(blind[0].ToJson())!.AsObject();
        row.Select(p => p.Key).Should().Equal("id", "path", "line", "claim");
        blind[0].ToJson().Should().NotContainAny(["t01", "01900000", "opus", "sonnet"]);
        ScoutAssessment.Batches(blind).Select(b => b.Count).Should().Equal(24, 6);
    }

    [Fact]
    public void The_prompt_is_the_rubric_then_the_rows_and_the_rubric_carries_the_four_wrong_rules()
    {
        var blind = ScoutAssessment.Blind([Claim(1)], new Random(1));

        var prompt = ScoutAssessment.Prompt(blind);

        prompt.Should().StartWith(ScoutAssessment.Rubric.TrimEnd()).And.Contain("INPUT ROWS (1 claims)").And.EndWith(blind[0].ToJson());
        ScoutAssessment.Rubric.Should().Contain("does not exist").And.Contain("not in the named file").And.Contain("contradicts").And.Contain("more than 10 lines");
        ScoutAssessment.RubricSha256.Should().Be(StableHash.Of(ScoutAssessment.Rubric));
        JsonNode.Parse(ScoutAssessment.Schema)!["properties"]!["rows"]!["items"]!["properties"]!["verdict"]!["enum"]!.AsArray()
            .Select(v => v!.GetValue<string>()).Should().Equal("wrong", "right", "unresolved");
    }

    [Fact]
    public void An_answer_is_read_strictly_and_what_it_does_not_settle_stays_unassessed()
    {
        var blind = ScoutAssessment.Blind([Claim(1), Claim(2), Claim(3), Claim(4)], new Random(2));
        var (a, b, c, d) = (blind[0].Id.Value, blind[1].Id.Value, blind[2].Id.Value, blind[3].Id.Value);
        var answer = $$"""
            Here is my answer:
            {"rows":[{"id":"{{a}}","verdict":"wrong","note":"x"},{"id":"{{b}}","verdict":"maybe","note":"x"},
                     {"id":"{{c}}","verdict":"right","note":"x"},{"id":"{{c}}","verdict":"wrong","note":"x"},{"id":"ffffffff","verdict":"right","note":"x"}]}
            """;

        var read = ScoutAssessment.Read(answer, blind).Ok();

        read.Should().ContainSingle().Which.Should().Match<ScoutClaimAnswer>(r => r.Claim.Id.Value == a && r.Verdict == ScoutClaimVerdict.Wrong,
            "b answered with another word, c twice, d not at all, and ffffffff is not in the batch");
        ScoutAssessment.Read("I could not decide.", blind).Failed().Should().BeTrue();
        ScoutAssessment.Read("{\"verdicts\":[]}", blind).Reason().Should().Contain("\"rows\"");
        _ = d;
    }

    [Fact]
    public void What_the_store_keeps_is_the_statements_hash_and_a_repository_relative_path_never_a_machine_path()
    {
        var blind = ScoutAssessment.Blind([Claim(1, path: "C:/work/tree/src/Alpha.cs"), Claim(2, path: "D:/elsewhere/x.cs")], new Random(3));

        var stored = blind.Select(b => ScoutAssessment.Stored(new ScoutClaimAnswer(b, ScoutClaimVerdict.Right), Guid.Empty, "C:/work/tree", "gpt-test", ScoutStoreFixtures.Noon, ScoutClaimDigest.PublicOnly))
            .OrderBy(s => s.Key.Index).ToList();

        stored.Select(s => s.Path).Should().Equal("src/Alpha.cs", string.Empty);
        stored[0].StatementSha256.Should().Be(StableHash.Of("defines the type"));
        stored[0].Should().Match<ScoutAssessedClaim>(s => s.AssessorModel == "gpt-test" && s.RubricSha256 == ScoutAssessment.RubricSha256 && s.Line.Value == 1);
    }

    [Fact]
    public void A_teams_tally_counts_wrong_over_decided_and_leaves_unresolved_out_of_the_denominator()
    {
        ScoutClaimVerdicts.Tally([ScoutClaimVerdict.Wrong, ScoutClaimVerdict.Right, ScoutClaimVerdict.Unresolved, ScoutClaimVerdict.Unresolved])
            .Should().Be(new ClaimTally(1, 2));
        ScoutClaimVerdicts.Parse("WRONG ").Ok().Should().Be(ScoutClaimVerdict.Wrong);
        ScoutClaimVerdicts.Parse("maybe").Reason().Should().Contain("wrong, right, unresolved");
    }
}
