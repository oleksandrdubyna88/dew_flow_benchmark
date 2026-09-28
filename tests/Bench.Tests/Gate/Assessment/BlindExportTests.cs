using System.Reflection;
using Bench.Domain.Gate;
using FluentAssertions;
using Xunit;
using static Bench.Tests.Gate.Assessment.AssessmentFixtures;

namespace Bench.Tests.Gate.Assessment;

/// <summary>S4.2 — the blinded export. The assessor must never learn which model, run or reviewer produced a finding:
/// the row it reads has no field for any of them BY CONSTRUCTION, and a serialised row carries none of them as text; a
/// blinded id is never one the key already holds, and exporting the same findings twice adds nothing.</summary>
public sealed class BlindExportTests
{
    private static readonly Guid Campaign = Guid.Parse("0199aaaa-0000-7000-8000-000000000001");
    private static readonly Guid Cell = Guid.Parse("0199bbbb-0000-7000-8000-000000000002");

    [Fact]
    public void The_row_an_assessor_reads_has_no_field_that_could_name_a_model_a_run_or_a_reviewer()
    {
        var names = typeof(AssessmentRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();

        names.Should().NotContain(n => n.Contains("Model", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Run", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Cell", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Campaign", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Reviewer", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Ordinal", StringComparison.OrdinalIgnoreCase));
        names.Should().Contain(["Id", "Task", "Title", "Why", "Fix", "RepoPath", "Head", "SeedSpec"], "the walk must see the row's real fields, or it proves nothing");
    }

    [Fact]
    public void An_exported_row_contains_no_model_no_run_id_and_no_reviewer_as_text()
    {
        var finding = new FindingToAssess(Campaign, Cell, 0, Cs2, Grok, FindingJson(0));
        var entry = BlindExport.Plan([finding], [], new Random(7)).Single();
        var json = AssessmentRow.Of(entry.Id, entry.Task, finding.FindingJson, Evidence).ToJson();

        json.Should().NotContain(Campaign.ToString()).And.NotContain(Cell.ToString()).And.NotContain(Grok.Value)
            .And.NotContain("grok").And.NotContain("\"ordinal\"");
        json.Should().Contain(entry.Id.Value).And.Contain("\"repo_path\"").And.Contain("\"seed_spec\"").And.Contain("finding 0");
    }

    [Fact]
    public void A_blinded_id_is_never_one_the_key_already_holds_even_when_the_random_source_repeats_itself()
    {
        var first = BlindExport.Plan([Finding(0)], [], new Random(1)).Single();

        // The same seed replays the same draw — the second export must step past the id it would have repeated.
        var second = BlindExport.Plan([Finding(1)], [first], new Random(1)).Single();

        second.Id.Should().NotBe(first.Id);
        second.Id.Value.Should().MatchRegex("^[0-9a-f]{8}$");
    }

    [Fact]
    public void A_second_export_of_the_same_findings_adds_nothing()
    {
        var findings = Enumerable.Range(0, 5).Select(Finding).ToList();
        var key = BlindExport.Plan(findings, [], new Random(3));

        BlindExport.Plan(findings, key, new Random(4)).Should().BeEmpty();
        key.Should().HaveCount(5);
        key.Select(k => k.Id.Value).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_tasks_new_entries_are_shuffled_so_the_keys_order_says_nothing_about_the_run_order()
    {
        var findings = Enumerable.Range(0, 24).Select(Finding).ToList();

        BlindExport.Plan(findings, [], new Random(11)).Select(k => k.Ordinal).Should().NotEqual(Enumerable.Range(0, 24));
    }

    private static FindingToAssess Finding(int ordinal) => new(Campaign, Cell, ordinal, Cs2, Grok, FindingJson(ordinal));
}
