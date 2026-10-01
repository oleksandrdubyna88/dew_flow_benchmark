using Bench.Domain.Probes;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>The probe run's order plan: repeats OUTERMOST, subjects rotated on the global slot counter (the gate matrix's
/// shape over probe × subject), and every pair a runtime cannot honour dropped and NAMED — the write-up's "not measured"
/// list is read off the plan, never guessed.</summary>
public sealed class ProbeMatrixTests
{
    private static readonly IReadOnlyList<ProbeKind> CliProbes =
        [ProbeKind.ReadInside, ProbeKind.ReadOutsideBare, ProbeKind.ReadOutsideGranted, ProbeKind.WebSearch, ProbeKind.ReadDenied, ProbeKind.WebConfined];

    [Fact]
    public void The_matrix_is_one_cell_per_applicable_pair_and_repeat_with_repeats_outermost()
    {
        var plan = ProbeMatrix.Plan(ProbeWord.All, Subjects(("claude-sonnet", "claude"), ("codex-astra", "codex"), ("agy-gemini", "antigravity"), ("grok-api", "api")), repeats: 3).Ok();

        // claude 6 + codex 6 + agy 5 (read-denied needs web OFF, which antigravity has no flag for) + api 1 = 18 cells a repeat.
        plan.Cells.Should().HaveCount(18 * 3);
        plan.Cells.Should().Contain(c => c.Probe == ProbeKind.ReadOutsideGranted && c.Subject.Value == "agy-gemini",
            "agy 1.2.14 takes --add-dir (measured 2026-10-01) — the grant pair is measured, not dropped");
        plan.Cells.Select(c => c.Repeat).Distinct().Should().BeEquivalentTo([1, 2, 3], "repeats are numbered from one");

        var slots = plan.Cells.GroupBy(c => c.Slot).OrderBy(g => g.Key).Select(g => g.First()).ToList();
        slots.Select(s => s.Repeat).Should().BeInAscendingOrder("repeats are OUTERMOST — every probe once, then every probe again");

        foreach (var lineage in plan.Cells.GroupBy(c => (c.Probe, c.Subject.Value)))
        {
            lineage.OrderBy(c => c.Slot).Select(c => c.Repeat).Should().Equal([1, 2, 3],
                $"{lineage.Key}: the repeats of one pair arrive in order, hours apart, so a stopped run resumes where it was");
        }
    }

    [Fact]
    public void Inapplicable_pairs_are_dropped_and_named()
    {
        var plan = ProbeMatrix.Plan(ProbeWord.All, Subjects(("claude-sonnet", "claude"), ("agy-gemini", "antigravity"), ("grok-api", "api")), repeats: 1).Ok();

        plan.Dropped.Should().Contain(d => d.Probe == ProbeKind.ReadInside && d.Subject.Value == "grok-api" && d.Reason.Contains("api", StringComparison.Ordinal),
            "an api subject runs no CLI probe");
        plan.Dropped.Should().Contain(d => d.Probe == ProbeKind.ApiReachable && d.Subject.Value == "claude-sonnet",
            "api-reachable runs through the product, not a CLI");
        plan.Dropped.Should().Contain(d => d.Probe == ProbeKind.ReadDenied && d.Subject.Value == "agy-gemini" && d.Reason.Contains("web", StringComparison.Ordinal),
            "read-denied is web OFF with the file tools denied, and antigravity has a flag for neither — the pair is dropped by name, not measured as a refusal");
        plan.Dropped.Should().NotContain(d => d.Probe == ProbeKind.ReadOutsideGranted && d.Subject.Value == "agy-gemini",
            "every CLI here takes a directory grant (claude, codex and agy all have --add-dir)");
        plan.Dropped.Should().HaveCount(6 + 2 + 1, "api × six CLI probes, two CLIs × api-reachable, antigravity × read-denied");
        plan.Cells.Should().NotContain(c => plan.Dropped.Any(d => d.Probe == c.Probe && d.Subject == c.Subject), "a dropped pair is planned nowhere");
        plan.Cells.Should().ContainSingle(c => c.Probe == ProbeKind.ApiReachable).Which.Subject.Value.Should().Be("grok-api");
    }

    [Fact]
    public void First_position_is_balanced_across_the_whole_matrix_at_an_odd_repeat_count()
    {
        var plan = ProbeMatrix.Plan(CliProbes, Subjects(("claude-sonnet", "claude"), ("codex-astra", "codex")), repeats: 3).Ok();

        var firsts = plan.Cells.Where(c => c.Position == 0).GroupBy(c => c.Subject.Value).ToDictionary(g => g.Key, g => g.Count());

        firsts.Should().HaveCount(2);
        (firsts.Values.Max() - firsts.Values.Min()).Should().BeLessThanOrEqualTo(1, "SlotRotation, the same function the other matrices use — no subject always goes first");
    }

    [Fact]
    public void Positions_within_one_slot_are_dense_and_start_at_zero()
    {
        var plan = ProbeMatrix.Plan([ProbeKind.ReadInside], Subjects(("claude-sonnet", "claude"), ("codex-astra", "codex"), ("agy-gemini", "antigravity")), repeats: 1).Ok();

        plan.Cells.Select(c => c.Position).Should().BeEquivalentTo([0, 1, 2]);
        plan.Cells.Select(c => c.Slot).Distinct().Should().Equal([0]);
    }

    [Fact]
    public void A_slot_nobody_can_run_is_not_planned_and_a_plan_with_nothing_left_is_refused_naming_the_drops()
    {
        var mixed = ProbeMatrix.Plan([ProbeKind.ApiReachable, ProbeKind.ReadInside], Subjects(("claude-sonnet", "claude")), repeats: 2).Ok();
        mixed.Cells.Select(c => c.Slot).Distinct().Should().Equal([0, 1], "the api-reachable slot has no subject and takes no slot number");

        ProbeMatrix.Plan([ProbeKind.ApiReachable], Subjects(("claude-sonnet", "claude")), repeats: 1).Reason()
            .Should().Contain("every pair was dropped").And.Contain("api-reachable × claude-sonnet");
    }

    [Fact]
    public void An_empty_axis_or_a_duplicate_is_refused_by_name()
    {
        ProbeMatrix.Plan([], Subjects(("claude-sonnet", "claude")), repeats: 1).Reason().Should().Contain("at least one probe");
        ProbeMatrix.Plan(CliProbes, [], repeats: 1).Reason().Should().Contain("at least one subject");
        ProbeMatrix.Plan(CliProbes, Subjects(("claude-sonnet", "claude")), repeats: 0).Reason().Should().Contain("repeats must be at least 1");
        ProbeMatrix.Plan([ProbeKind.ReadInside, ProbeKind.ReadInside], Subjects(("claude-sonnet", "claude")), repeats: 1).Reason().Should().Contain("'read-inside' is listed twice");
        ProbeMatrix.Plan(CliProbes, Subjects(("claude-sonnet", "claude"), ("claude-sonnet", "codex")), repeats: 1).Reason().Should().Contain("'claude-sonnet' is listed twice");
    }

    internal static IReadOnlyList<ProbeSubject> Subjects(params (string Id, string Runtime)[] subjects) =>
        [.. subjects.Select(s => s.Runtime == "api"
            ? ProbeSubject.Parse(s.Id, s.Runtime, "model-x", "BENCH_X", "vendor-x", "https://api.vendor.example.com/v1", "openai").Ok()
            : ProbeSubject.Parse(s.Id, s.Runtime, "model-x", "BENCH_X").Ok())];
}
