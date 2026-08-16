using Bench.Domain;
using Bench.Domain.Registry;
using Bench.Domain.Runs;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Registry;

/// <summary>The registry's publication rule: <b>references, never values</b>.
/// <para>
/// This database is meant to be published unedited. A guarantee scoped to result rows while the registry
/// sits in the same schema is not a guarantee — it is a redaction pass nobody has scheduled. So the type
/// refuses the two shapes people actually paste, and says why rather than "invalid".
/// </para></summary>
public sealed class ModelConfigTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("https://api.example.invalid/v1")]
    [InlineData("/home/jinx/.local/bin/claude")]
    [InlineData("C:\\Users\\strug\\AppData\\claude.exe")]
    [InlineData("\\\\wsl.localhost\\Ubuntu\\home\\jinx\\bin\\codex")]
    public void A_value_pasted_where_a_reference_belongs_is_refused_and_told_why(string value)
    {
        var refused = Config(baseUrlRef: value);

        refused.Failed().Should().BeTrue();
        refused.Reason().Should().Contain("VALUE").And.Contain("environment variable")
            .And.Contain("published unedited", "the refusal has to teach the rule, or the next person pastes it again");
    }

    [Theory]
    [InlineData("sk-proj-abc123def456")]
    [InlineData("my key")]
    [InlineData("BENCH/QWEN/URL")]
    public void A_secret_shaped_or_malformed_reference_is_refused_as_a_reference(string value)
    {
        Config(apiKeyRef: value).Failed().Should().BeTrue(
            "an api key pasted into a reference field would be stored verbatim and published");
    }

    [Theory]
    [InlineData("BENCH_QWEN_URL")]
    [InlineData("Agent:Models:Qwen:Url")]
    [InlineData("_private.ref")]
    public void An_environment_variable_name_or_a_configuration_path_is_a_reference(string value)
    {
        Config(baseUrlRef: value).Ok().BaseUrlRef.Should().Be(value);
    }

    [Fact]
    public void Sampling_and_prices_stay_VALUES_because_they_are_neither_secret_nor_machine_specific()
    {
        var config = ModelConfig.Parse(
            "qwen3-coder:latest", "BENCH_QWEN_URL", string.Empty, string.Empty,
            new Sampling(0.0, 7), inputPerMTok: 1.25m, outputPerMTok: 5m).Ok();

        // A run must be able to say what sampling it ASKED for, and what its tokens cost. Neither is a
        // property of this machine or this account, so neither leaves.
        config.Sampling.Seed.Should().Be(7);
        config.InputCostPerMTok.Should().Be(1.25m);
        config.References.Should().Equal("BENCH_QWEN_URL");
    }

    [Fact]
    public void A_model_id_is_data_but_a_url_wearing_its_place_is_not()
    {
        Config(modelId: "qwen3-coder:latest").Ok().ModelId.Should().Be(
            "qwen3-coder:latest", "an id is the same on every machine, and a result that could not name it would be unreadable");

        Config(modelId: "http://127.0.0.1:11434/v1").Failed().Should().BeTrue();
    }

    [Fact]
    public void An_unset_model_id_is_a_refusal_never_a_default()
    {
        Config(modelId: "").Reason().Should().Contain("never a fallback to a default");
    }

    [Fact]
    public void A_registry_row_with_no_references_at_all_is_legal_and_says_so()
    {
        // A bridge-local model has no endpoint to name. "No references" is a state, not an omission.
        var config = Config(baseUrlRef: string.Empty).Ok();

        config.References.Should().BeEmpty();
        config.Describe.Should().Contain("no references");
    }

    private static Outcome<ModelConfig> Config(
        string modelId = "qwen3-coder:latest",
        string baseUrlRef = "BENCH_QWEN_URL",
        string apiKeyRef = "",
        string executableRef = "") =>
        ModelConfig.Parse(modelId, baseUrlRef, apiKeyRef, executableRef, Sampling.Deterministic(1));
}
