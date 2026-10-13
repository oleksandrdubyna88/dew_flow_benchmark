using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>A task's statement is frozen before any run and refused when it names the answer (§3.1's automatic leak check).</summary>
public sealed class ScoutTaskTests
{
    private static readonly string[] Truth = ["src/Bench.Application/Probes/ProbeCampaign.cs", "src/Bench.Domain/Probes/ProbeRun.cs", "tests/Bench.Tests/io.cs"];

    [Fact]
    public void A_statement_carrying_a_ground_truth_stem_is_refused_naming_the_stem()
    {
        LeakCheck.Find("Why does the probecampaign stop when a quota runs out?", Truth, []).Should().Equal(new LeakFinding(LeakKind.Stem, "ProbeCampaign"));
    }

    [Fact]
    public void A_statement_carrying_a_ground_truth_basename_is_refused_as_the_basename_and_its_stem()
    {
        LeakCheck.Find("What does ProbeRun.cs freeze on the run?", Truth, []).Select(f => f.Kind).Should().Equal(LeakKind.Basename, LeakKind.Stem);
    }

    [Fact]
    public void An_identifier_the_diff_added_is_refused_as_a_whole_word_only()
    {
        LeakCheck.Find("Where is the UnmeasuredAttempts count forgiven?", Truth, ["UnmeasuredAttempts"]).Should().Equal(new LeakFinding(LeakKind.AddedIdentifier, "UnmeasuredAttempts"));
        LeakCheck.Find("How are runs resumed after a crash?", ["src/Run.cs"], ["Run"]).Should().BeEmpty("'Run' as a word is not in 'runs'");
    }

    [Fact]
    public void A_clean_statement_passes_and_a_token_shorter_than_three_characters_is_not_checked()
    {
        LeakCheck.Find("How does a quota stop reach the operator?", Truth, ["Go"]).Should().BeEmpty("'io' and 'Go' are too short to refuse a sentence by");
    }

    [Fact]
    public void Freezing_runs_the_leak_check_and_refuses_a_leaking_statement()
    {
        Freeze("Where does ProbeCampaign decide to bench a subject?").Reason().Should().Contain("leaks the answer").And.Contain("Stem 'ProbeCampaign'");
        Freeze("Where is the decision to stop a subject after an account runs out made?").Ok().GroundTruth.Should().Equal(Truth);
    }

    [Fact]
    public void A_task_is_refused_for_a_bad_shape_naming_what_is_wrong()
    {
        Freeze("One. Two. Three. Four.").Reason().Should().Contain("4 sentences");
        Freeze("A fine question?", truth: []).Reason().Should().Contain("ground truth is empty");
        Freeze("A fine question?", truth: ["../outside.cs"]).Reason().Should().Contain("climbs out");
        // D10 (2026-10-12) widened what a repository may be: a url is read as it is (a private repository's local clone), so the refusal is
        // now for a value that is neither a slug nor a url.
        Freeze("A fine question?", repository: "org repo").Reason().Should().Contain("'org repo' is not a repository").And.Contain(ScoutRepository.Forms);
        Freeze("A fine question?", repository: "https://github.com/org/repo").Ok().Repository.Should().Be("https://github.com/org/repo");
        Freeze("A fine question?", treeHash: "abc").Reason().Should().Contain("not a tree hash");
        ScoutTaskId.Parse("Bad Id").Reason().Should().Contain("not a task id");
    }

    private static Bench.Domain.Outcome<ScoutTask> Freeze(string statement, string[]? truth = null, string repository = "example-org/example-repo", string? treeHash = null) =>
        ScoutTask.Frozen(
            ScoutTaskId.Parse("t01").Ok(), repository, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, statement, truth ?? Truth, [], [], treeHash ?? new string('c', 64));
}
