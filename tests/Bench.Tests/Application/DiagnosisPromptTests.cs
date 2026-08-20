using Bench.Application;
using Bench.Domain.Retrieval;
using Bench.Domain.Suites;
using Bench.Domain.Targets;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Application;

/// <summary>The Investigate phase's prompt: the ordinary leg prompt with the diagnosis contract
/// appended — and the baseline halves untouched, because an arm that quietly reshaped the reading
/// prompt would no longer be comparable against it.</summary>
public sealed class DiagnosisPromptTests
{
    private static readonly Question Ask = new(
        "fix-q", "Retries stop honouring the cap. Investigate.", [], string.Empty);

    [Fact]
    public void The_contract_rides_after_the_ordinary_prompt()
    {
        var prompt = DiagnosisPrompt.Assemble(Ask, RetrievedContext.NotPerformed, RagPromptLimits.Default);

        prompt.Should().StartWith(Ask.Prompt, "the statement is what the solver investigates");
        prompt.Should().Contain("```json").And.Contain("\"mechanism\"").And.Contain("\"anchors\"");
        prompt.Should().Contain("CAUSED", "the symptom-trap's rule is stated out loud, never only graded");
    }

    [Fact]
    public void The_contract_asks_for_what_the_reader_reads()
    {
        // The contract and DiagnosisJson are two halves of one shape; the cheap guard is that the
        // contract's own example parses through the real reader.
        var example = DiagnosisPrompt.Contract[DiagnosisPrompt.Contract.IndexOf("```json", StringComparison.Ordinal)..];

        DiagnosisJson.Read(example).Should().BeOfType<Bench.Domain.Runs.DiagnosisReading.Parsed>(
            "a contract whose own example the reader refuses would teach every subject a broken shape");
    }
}
