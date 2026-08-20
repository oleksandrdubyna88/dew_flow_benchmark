using Bench.Application;
using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>Reading a diagnosis out of an Investigate phase's answer
/// (todo/PLAN_investigate_vs_implement.md §3.2). Extraction, never repair — the authoring pass's own
/// rule, over the same <c>AgentJson</c> mechanics — and three states rather than two: parsed, absent,
/// malformed-with-the-error. A model that cannot follow the contract is a reportable fact, but a
/// different fact from a wrong diagnosis.</summary>
public sealed class DiagnosisJsonTests
{
    private const string Valid =
        """
        {
          "anchors": [{ "path": "src/Retry/Policy.cs", "member": "Policy.NextDelay", "lines": { "start": 120, "end": 141 } }],
          "mechanism": "the delay is recomputed from scratch on every attempt",
          "fixIntent": "carry the previous delay through the retry state"
        }
        """;

    [Fact]
    public void A_fenced_diagnosis_parses()
    {
        var reading = DiagnosisJson.Read($"```json\n{Valid}\n```");

        var parsed = reading.Should().BeOfType<DiagnosisReading.Parsed>().Subject;
        parsed.Diagnosis.Anchors.Should().ContainSingle(a =>
            a.Path == "src/Retry/Policy.cs" && a.Member == "Policy.NextDelay" && a.Lines.Start == 120);
        parsed.Diagnosis.Mechanism.Should().Contain("recomputed");
    }

    [Fact]
    public void Prose_before_the_object_is_kept_beside_it_not_lost()
    {
        var reading = DiagnosisJson.Read($"I looked at the retry loop first.\n\n{Valid}");

        var parsed = reading.Should().BeOfType<DiagnosisReading.Parsed>().Subject;
        parsed.Said.Should().Contain("retry loop");
    }

    [Fact]
    public void An_unrelated_braced_snippet_before_the_diagnosis_does_not_steal_the_read()
    {
        var reading = DiagnosisJson.Read(
            "The struct is initialised as `new Options { }` here.\n\n" + Valid);

        reading.Should().BeOfType<DiagnosisReading.Parsed>(
            "an empty object in prose is not recognisably a diagnosis, and the scan must keep looking");
    }

    [Fact]
    public void An_answer_with_no_object_at_all_reads_as_absent()
    {
        DiagnosisJson.Read("The bug is in the retry policy, around NextDelay.")
            .Should().BeOfType<DiagnosisReading.Absent>();
    }

    [Fact]
    public void A_diagnosis_without_a_mechanism_is_malformed_and_names_the_gap()
    {
        var reading = DiagnosisJson.Read("""{ "anchors": [], "mechanism": "" }""");

        reading.Should().BeOfType<DiagnosisReading.Malformed>()
            .Subject.ParseError.Should().Contain("mechanism");
    }

    [Fact]
    public void Unknown_extra_fields_are_tolerated_in_an_agent_answer()
    {
        var withExtras = Valid.TrimEnd().TrimEnd('}') + ", \"confidence\": \"high\" }";

        DiagnosisJson.Read(withExtras).Should().BeOfType<DiagnosisReading.Parsed>(
            "an agent's answer is a payload, not a catalog row — an extra field is noise, not a hazard");
    }

    [Fact]
    public void Anchors_may_omit_member_and_lines()
    {
        var reading = DiagnosisJson.Read(
            """{ "anchors": [{ "path": "src/F.cs" }], "mechanism": "the cause" }""");

        var parsed = reading.Should().BeOfType<DiagnosisReading.Parsed>().Subject;
        parsed.Diagnosis.Anchors.Single().Member.Should().BeEmpty();
        parsed.Diagnosis.Anchors.Single().Lines.IsWhole.Should().BeTrue();
    }

    [Fact]
    public void Broken_json_reads_as_malformed_with_the_parser_speaking()
    {
        var reading = DiagnosisJson.Read("""{ "anchors": [{ "path": "src/F.cs" ], "mechanism": "x" }""");

        reading.Should().BeOfType<DiagnosisReading.Malformed>()
            .Subject.ParseError.Should().NotBeEmpty();
    }
}
