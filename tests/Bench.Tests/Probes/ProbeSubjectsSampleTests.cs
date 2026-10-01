using Bench.Domain.Probes;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>The checked-in subjects file the plan's run uses (<c>samples/question-consultant-probe-subjects.json</c>) parses under the
/// D4 rules and plans the matrix §4 describes — the pairs the planner drops are the ones the write-up lists as not measured.</summary>
public sealed class ProbeSubjectsSampleTests
{
    [Fact]
    public void The_plans_subjects_file_parses_and_plans_every_probe_it_can_with_the_dropped_pairs_named()
    {
        var subjects = ProbeSubjectsFile.Read(File.ReadAllText(Path.Combine(TestRepository.Root(), "samples", "question-consultant-probe-subjects.json"))).Ok();

        subjects.Select(s => (s.Id.Value, s.Runtime, s.ModelId, s.ExecutableRef)).Should().Equal(
        [
            ("claude-sonnet", ProbeRuntime.Claude, "sonnet", "BENCH_CLAUDE"),
            ("codex-astra", ProbeRuntime.Codex, "gpt-6-astra", "BENCH_CODEX"),
            ("codex-terra", ProbeRuntime.Codex, "gpt-5.6-terra", "BENCH_CODEX"),
            ("agy-gemini", ProbeRuntime.Antigravity, "gemini-3.1-pro-high", "BENCH_AGY"),
            ("grok-api", ProbeRuntime.Api, "grok-4.7", "BENCH_GATE_COAI_EXE"),
        ]);
        subjects[^1].Should().Match<ProbeSubject>(s => s.Vendor == "grok" && s.Endpoint == "https://api.x.ai/v1" && s.Dialect == "xai");

        var plan = ProbeMatrix.Plan(ProbeWord.All, subjects, repeats: 3).Ok();

        plan.Cells.Should().HaveCount(3 * ((4 * 6) - 1 + 1), "four CLIs × six CLI probes, less read-denied on agy, plus api-reachable on grok — three repeats");
        plan.Dropped.Select(d => $"{ProbeWord.Of(d.Probe)} × {d.Subject}").Should().Contain(["read-denied × agy-gemini", "api-reachable × claude-sonnet", "read-inside × grok-api"]);
    }
}
