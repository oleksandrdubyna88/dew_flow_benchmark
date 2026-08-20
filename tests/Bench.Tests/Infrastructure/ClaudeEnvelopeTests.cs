using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Infrastructure;

/// <summary>The CLI subject's envelope (todo/PLAN_investigate_vs_implement.md §3.6): cache tokens are
/// billed tokens and are SUMMED into the input, the CLI's own cost is authoritative when present, and
/// everything unreported is NOT CAPTURED — never zero, which is the "cost unknown" that looks free.</summary>
public sealed class ClaudeEnvelopeTests
{
    private const string Full =
        """
        {"type":"result","subtype":"success","is_error":false,"result":"the answer",
         "total_cost_usd":0.1234,
         "usage":{"input_tokens":100,"output_tokens":20,"cache_read_input_tokens":1000,"cache_creation_input_tokens":50}}
        """;

    [Fact]
    public void A_full_envelope_reads_whole_and_the_cache_is_billed_input()
    {
        var reading = ClaudeEnvelope.Read(Full).Ok();

        reading.Text.Should().Be("the answer");
        reading.TokensIn.Value.Should().Be(1150, "cache read and creation are billed tokens, and a total that omits them under-reports every leg");
        reading.TokensOut.Value.Should().Be(20);
        reading.CostCaptured.Should().BeTrue();
        reading.CostUsd.Should().Be(0.1234m);
    }

    [Fact]
    public void An_envelope_without_usage_reads_as_not_captured_never_as_zero()
    {
        var reading = ClaudeEnvelope.Read("""{"result":"ok","is_error":false,"total_cost_usd":0}""").Ok();

        reading.TokensIn.WasCaptured.Should().BeFalse();
        reading.CostCaptured.Should().BeFalse("a zero cost and an unreported one are different facts");
    }

    [Fact]
    public void An_error_envelope_is_a_refusal_carrying_the_CLIs_own_words()
    {
        ClaudeEnvelope.Read("""{"is_error":true,"result":"credit balance too low"}""")
            .Reason().Should().Contain("credit balance");
    }

    [Fact]
    public void A_banner_before_the_envelope_does_not_defeat_the_read()
    {
        ClaudeEnvelope.Read("Ignoring 5 permissions.allow entries.\n" + Full).Ok()
            .Text.Should().Be("the answer");
    }

    [Fact]
    public void Plain_text_is_a_refusal_naming_the_likely_cause()
    {
        ClaudeEnvelope.Read("Just an answer, no JSON anywhere.")
            .Reason().Should().Contain("--output-format json");
    }
}
