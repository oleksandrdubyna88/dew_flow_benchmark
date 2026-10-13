using Bench.Domain.Scouts;
using Bench.Domain.Trace;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>The output contract's parsers (§3.3) and the claim arithmetic of §3.6 — "claims, exactly": every line is counted, an
/// unparseable one is never dropped silently, a scout without a CLAIMS block is SILENT, and rates are pooled.</summary>
public sealed class ScoutOutputTests
{
    private const string Report = """
        I looked at the probes context first.

        ## KEY FILES
        - `src/Bench.Domain/Probes/ClaudeStream.cs`
        - ./src/Bench.Domain/Probes/ProbeJson.cs:12
        * tests\Bench.Tests\Probes\ProbeVerdictsTests.cs
        the stream reader, mostly

        **CLAIMS:**
        1. src/Bench.Domain/Probes/ClaudeStream.cs:40 — reads the result envelope last
        2. src/Bench.Domain/Probes/ProbeJson.cs — tolerates duplicate JSON keys
        3. reads every line of stdout
        4. src/Bench.Domain/Probes/ProbeJson.cs:12 —
        5. `src/Bench.Domain/Probes/ProbeTools.cs:0` — classifies tools
        """;

    [Fact]
    public void Key_files_are_read_as_written_with_bullets_and_backticks_stripped_and_a_sentence_counted_unparseable()
    {
        var keyFiles = ScoutOutput.KeyFiles(Report);

        keyFiles.BlockFound.Should().BeTrue();
        keyFiles.Paths.Should().Equal(
            "src/Bench.Domain/Probes/ClaudeStream.cs",
            "./src/Bench.Domain/Probes/ProbeJson.cs:12",
            @"tests\Bench.Tests\Probes\ProbeVerdictsTests.cs");
        (keyFiles.Unparseable, keyFiles.Extra).Should().Be((1, 0));
    }

    [Fact]
    public void A_well_formed_claim_parses_and_a_line_without_a_path_or_without_a_statement_is_counted_not_assessed()
    {
        var claims = ScoutOutput.Claims(Report);

        claims.Silent.Should().BeFalse();
        claims.Claims.Should().Equal(
            new ScoutClaim("src/Bench.Domain/Probes/ClaudeStream.cs", CapturedCount.Number(40), "reads the result envelope last"),
            new ScoutClaim("src/Bench.Domain/Probes/ProbeJson.cs", CapturedCount.Unavailable("the claim cites no line"), "tolerates duplicate JSON keys"));
        claims.Unparseable.Should().Be(3, "no path; a path:line with no statement; a line 0 that is no line");
        claims.Extra.Should().Be(0);
    }

    [Fact]
    public void A_scout_with_no_claims_block_is_silent_and_contributes_no_claim()
    {
        var claims = ScoutOutput.Claims("KEY FILES\n- src/a.cs\n\nThat is all I found.");

        (claims.Silent, claims.Claims.Count, claims.Unparseable).Should().Be((true, 0, 0));
        ScoutOutput.KeyFiles("no blocks at all").Should().BeEquivalentTo(new ScoutKeyFiles(false, [], 0, 0));
    }

    [Fact]
    public void Lines_beyond_the_tenth_are_counted_as_extra_and_never_assessed()
    {
        var twelve = "CLAIMS\n" + string.Join('\n', Enumerable.Range(1, 12).Select(i => $"- src/f{i}.cs:{i} — claim {i}"));

        var claims = ScoutOutput.Claims(twelve);

        claims.Claims.Should().HaveCount(10);
        claims.Claims[^1].Path.Should().Be("src/f10.cs");
        (claims.Unparseable, claims.Extra).Should().Be((0, 2));
        ScoutOutput.KeyFiles("KEY FILES\n" + string.Join('\n', Enumerable.Range(1, 11).Select(i => $"src/f{i}.cs"))).Extra.Should().Be(1);
    }

    [Fact]
    public void A_line_number_a_model_made_up_is_clamped_by_refusal()
    {
        ScoutOutput.Claims("CLAIMS\n- src/a.cs:99999999999 — overflows an int").Unparseable.Should().Be(1);
        ScoutOutput.Claims("CLAIMS\n- src/a.cs:1000001 — past the cap").Unparseable.Should().Be(1);
        ScoutOutput.Claims("CLAIMS\n- src/a.cs:12-20 — a range cites its first line").Claims.Single().Line.Should().Be(CapturedCount.Number(12));
    }

    [Fact]
    public void The_separator_may_be_an_em_dash_an_en_dash_or_a_spaced_hyphen()
    {
        ScoutOutput.Claims("CLAIMS\n- a/b.cs — one\n- a/c.cs – two\n- a/d.cs - three\n- a/e.cs-four").Claims.Select(c => c.Statement)
            .Should().Equal("one", "two", "three");
    }

    /// <summary>Pooling, against a hand-computed table: three scouts with 1 of 4, 0 of 6 and 2 of 2 wrong pool to 3 of 12 = 2.5 per 10 —
    /// the mean of the per-scout rates (2.5, 0, 10) would say 4.17, and that is exactly the error pooling exists to avoid.</summary>
    [Fact]
    public void A_teams_rate_is_pooled_over_its_claims_not_averaged_over_its_scouts()
    {
        ClaimTally[] team = [new(1, 4), new(0, 6), new(2, 2)];

        ScoutClaimRates.Pooled(team).Should().Be(new ClaimRate(true, 2.5m));
        team.Average(t => (decimal)t.Wrong / t.Assessed * 10m).Should().BeApproximately(4.1667m, 0.0001m, "the per-scout mean the rule refuses");
    }

    [Fact]
    public void A_tasks_rate_is_pooled_over_its_runs_so_a_run_with_one_claim_weighs_less_than_a_run_with_ten()
    {
        ScoutClaimRates.Pooled([new ClaimTally(1, 1), new ClaimTally(0, 10)]).PerTen.Should().BeApproximately(0.909m, 0.001m, "1 of 11, not the mean of 10 and 0");
    }

    [Fact]
    public void A_team_with_no_assessed_claim_has_no_rate_and_never_a_zero()
    {
        ScoutClaimRates.Pooled([new ClaimTally(0, 0), new ClaimTally(0, 0)]).Should().Be(ClaimRate.NotMeasured);
        ScoutClaimRates.Pooled([]).Measured.Should().BeFalse();
    }
}
