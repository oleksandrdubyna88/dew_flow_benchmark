using Bench.Domain.Scouts;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>§3.1's selection, pure: the cut-offs are evidence (a missing, unpublished or ambiguous id STOPS it), only a P after the LATER
/// cut-off is a candidate, a pilot task never is, G is filtered and sized, the draw is seeded per repository, and an added identifier is one
/// that occurs nowhere at P.</summary>
public sealed class ScoutSelectionTests
{
    private const string Opus = "claude-opus-5-5";
    private const string Sonnet = "claude-sonnet-5-5";

    private static string Cutoffs(string opusCutoff = "2026-03-31", string sonnetCutoff = "2026-01-31", string extra = "") => $$"""
        { "models": [
          { "modelId": "{{Opus}}", "cutoff": "{{opusCutoff}}", "source": "https://example.invalid/models", "readOn": "2026-10-12" },
          { "modelId": "{{Sonnet}}", "cutoff": "{{sonnetCutoff}}", "source": "https://example.invalid/models", "readOn": "2026-10-12" }{{extra}}
        ] }
        """;

    [Fact]
    public void The_later_of_the_two_recorded_cut_offs_is_the_bar()
    {
        ScoutCutoffs.Parse(Cutoffs()).Ok().LaterOf(Opus, Sonnet).Ok().Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void An_id_with_no_recorded_cut_off_stops_the_selection_naming_it()
    {
        ScoutCutoffs.Parse(Cutoffs()).Ok().LaterOf("claude-opus-6-0", Sonnet).Reason().Should().Contain("claude-opus-6-0").And.Contain("stops");
    }

    [Fact]
    public void An_unpublished_or_ambiguous_cut_off_stops_the_selection_rather_than_being_guessed()
    {
        var unpublished = ScoutCutoffs.Parse(Cutoffs(sonnetCutoff: "")).Ok();
        var twice = ScoutCutoffs.Parse(Cutoffs(extra: $$""", { "modelId": "{{Opus}}", "cutoff": "2026-05-01", "source": "https://x.invalid", "readOn": "2026-10-12" }""")).Ok();

        unpublished.LaterOf(Opus, Sonnet).Reason().Should().Contain("no published cut-off").And.Contain(Sonnet);
        twice.LaterOf(Opus, Sonnet).Reason().Should().Contain("listed 2 times").And.Contain("ambiguous");
    }

    [Theory]
    [InlineData("""{ "models": [ { "modelId": "m", "cutoff": "2026-03-31", "source": "http://insecure", "readOn": "2026-10-12" } ] }""", "https")]
    [InlineData("""{ "models": [ { "modelId": "m", "cutoff": "March 2026", "source": "https://x", "readOn": "2026-10-12" } ] }""", "not a date")]
    [InlineData("""{ "models": [ { "modelId": "m", "cutoff": "2026-03-31", "source": "https://x" } ] }""", "readOn")]
    [InlineData("""{ "nothing": [] }""", "models")]
    public void A_cut_offs_file_missing_its_evidence_is_refused_by_name(string json, string named)
    {
        ScoutCutoffs.Parse(json).Reason().Should().Contain(named);
    }

    [Fact]
    public void A_candidate_is_admitted_only_after_the_later_cut_off_and_a_pilot_task_never()
    {
        var later = new DateOnly(2026, 3, 31);
        IReadOnlyList<ScoutCandidateFacts> candidates =
        [
            Facts("after", new DateTimeOffset(2026, 4, 1, 0, 0, 1, TimeSpan.Zero)),
            Facts("on-the-day", new DateTimeOffset(2026, 3, 31, 23, 59, 0, TimeSpan.Zero)),
            Facts("before", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero)),
            Facts("pilot-one", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
        ];

        var selection = ScoutCandidates.Admit(candidates, later, new HashSet<string>(["pilot-one"], StringComparer.Ordinal));

        selection.Admitted.Select(c => c.Candidate.Id.Value).Should().Equal("after");
        selection.Excluded.Should().Contain(e => e.Id.Value == "pilot-one" && e.Reason.Contains("pilot"));
        selection.Excluded.Should().Contain(e => e.Id.Value == "on-the-day" && e.Reason.Contains("not after the later cut-off 2026-03-31"));
        selection.Excluded.Should().Contain(e => e.Id.Value == "before");
    }

    [Fact]
    public void G_out_of_range_or_docs_only_is_excluded_with_its_reason()
    {
        var later = new DateOnly(2026, 1, 1);
        var at = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var selection = ScoutCandidates.Admit(
            [Facts("two", at, ["a.cs", "b.cs"]), Facts("docs", at, ["a.md", "b.md", "c.md"]), Facts("many", at, [.. Enumerable.Range(1, 16).Select(i => $"f{i}.cs")])],
            later, new HashSet<string>(StringComparer.Ordinal));

        selection.Admitted.Should().BeEmpty();
        selection.Excluded.Select(e => e.Reason).Should().Contain(r => r.Contains("2 file(s)")).And.Contain(r => r.Contains("docs-only")).And.Contain(r => r.Contains("16 file(s)"));
    }

    [Fact]
    public void The_draw_is_seeded_per_repository_and_reproducible()
    {
        var at = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        IReadOnlyList<ScoutCandidateFacts> admitted = [.. Enumerable.Range(1, 6).Select(i => Facts($"c{i}", at, repository: i % 2 == 0 ? "org/even" : "org/odd"))];

        var first = ScoutCandidates.Draw(admitted, 2, seed: 7);
        var again = ScoutCandidates.Draw(admitted, 2, seed: 7);
        var other = ScoutCandidates.Draw(admitted, 2, seed: 8);

        first.Should().HaveCount(4).And.Equal(again);
        first.GroupBy(c => c.Candidate.Repository).Should().OnlyContain(g => g.Count() == 2);
        other.Select(c => c.Candidate.Id.Value).Should().NotEqual(first.Select(c => c.Candidate.Id.Value), "another seed draws another set (for this fixture)");
    }

    [Fact]
    public void G_keeps_the_files_that_existed_at_P_and_drops_lockfiles_generated_files_changelogs_manifests_and_the_plan()
    {
        var atParent = new HashSet<string>(["src/A.cs", "src/B.cs", "package-lock.json", "CHANGELOG.md", "src/X.Designer.cs", "version.json", "todo/PLAN_x.md", "docs/Guide.md"], StringComparer.Ordinal);

        ScoutGroundTruth.Of(["src/A.cs", "src/B.cs", "src/New.cs", "package-lock.json", "CHANGELOG.md", "src/X.Designer.cs", "version.json", "todo/PLAN_x.md", "docs/Guide.md"], atParent, "todo/PLAN_x.md")
            .Should().Equal("docs/Guide.md", "src/A.cs", "src/B.cs");
    }

    [Fact]
    public void An_added_identifier_is_one_that_occurs_nowhere_in_the_tree_at_P()
    {
        var atParent = new HashSet<string>(ScoutGroundTruth.Identifiers("public sealed class ScoutStore { void Settle() {} } the and for"), StringComparer.Ordinal);

        ScoutGroundTruth.AddedIdentifiers(["    public void Recover(ScoutStore store) => Settle();", "// the ledger"], atParent)
            .Should().Equal("Recover", "ledger", "store");
    }

    [Fact]
    public void A_drawn_statement_naming_an_added_identifier_is_refused_when_frozen()
    {
        var frozen = ScoutTask.Frozen(
            ScoutTaskId.Parse("t1").Ok(), "org/repo", CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(),
            ScoutQuestionKind.WhereDecided, "Where does Recover decide the charge?", ["src/A.cs", "src/B.cs", "src/C.cs"], ["Recover"], [], new string('c', 64));

        frozen.Reason().Should().Contain("AddedIdentifier 'Recover'");
    }

    private static ScoutCandidateFacts Facts(string id, DateTimeOffset parentDate, IReadOnlyList<string>? groundTruth = null, string repository = "org/repo") =>
        new(new ScoutCandidate(ScoutTaskId.Parse(id).Ok(), repository, CommitSha.Parse(new string('a', 40)).Ok(), CommitSha.Parse(new string('b', 40)).Ok(), ScoutQuestionKind.HowEndToEnd, "How does it work?", string.Empty),
            parentDate, groundTruth ?? ["src/A.cs", "src/B.cs", "src/C.cs"]);
}
