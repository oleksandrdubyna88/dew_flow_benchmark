using Bench.Cli;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>D20: the verb and exactly its positional words are taken by a per-verb table; the rest reaches the shared flag parser unchanged. A
/// refusal names the verb's shape.
/// <para>
/// What this does NOT prove: the flags' meaning (each verb reads its own), or that every verb is dispatched (<c>ModelArmsProgramTests</c>).
/// </para></summary>
public sealed class ModelArmsCommandLineTests
{
    [Fact]
    public void The_verb_and_its_positionals_are_taken_first_and_the_rest_reaches_CommandLine_Parse_unchanged()
    {
        var line = ModelArmsCommandLine.Split(["run-all", "C1", "CR1", "--experiment", "/r", "--seconds", "5"]).Ok();

        line.Verb.Should().Be("run-all");
        line.Positionals.Should().Equal("C1", "CR1");
        line.Rest.Should().Equal("run-all", "--experiment", "/r", "--seconds", "5");

        var flags = CommandLine.Parse(line.Rest);
        flags.Verb.Should().Be("run-all");
        flags.Value("experiment").Should().Be("/r");
        flags.Int("seconds", 0).Should().Be(5);
        flags.Operand(0).Should().BeEmpty("the positionals were taken first; the shared parser sees none");
    }

    public static TheoryData<string[], string> WrongCounts() => new()
    {
        { ["run-arm"], "model-arms run-arm <arm>" },
        { ["run-arm", "C1", "C2"], "model-arms run-arm <arm>" },
        { ["validate", "x"], "model-arms validate" },
        { ["init"], "model-arms init <root>" },
    };

    [Theory]
    [MemberData(nameof(WrongCounts))]
    public void Too_few_or_too_many_positionals_are_refused_naming_the_verbs_shape(string[] args, string usage)
    {
        var refused = ModelArmsCommandLine.Split(args);

        refused.Failed().Should().BeTrue($"'{string.Join(' ', args)}' has the wrong number of positional words");
        refused.Reason().Should().Contain(usage).And.Contain($"{args[0]} takes");
    }

    [Fact]
    public void An_unknown_positional_is_refused()
    {
        ModelArmsCommandLine.Split(["gate-lock", "frob"]).Reason().Should().Contain("'frob' is not a gate-lock action").And.Contain("status or hold");

        var stray = ModelArmsCommandLine.Split(["run-arm", "C1", "--experiment", "/r", "C2"]);

        stray.Reason().Should().Contain("'C2' is not a flag or a value").And.Contain("run-arm");
    }

    [Fact]
    public void Run_all_takes_one_or_more_arms_and_gate_lock_takes_status_or_hold()
    {
        ModelArmsCommandLine.Split(["run-all", "C1"]).Ok().Positionals.Should().Equal("C1");
        ModelArmsCommandLine.Split(["run-all", "C1", "CR1", "SRS1"]).Ok().Positionals.Should().Equal("C1", "CR1", "SRS1");
        ModelArmsCommandLine.Split(["gate-lock", "hold", "--seconds", "3"]).Ok().Rest.Should().Equal("gate-lock", "--seconds", "3");
        ModelArmsCommandLine.Split(["gate-lock", "status"]).Ok().Positionals.Should().Equal("status");
        ModelArmsCommandLine.Split(["run-all"]).Failed().Should().BeTrue("run-all needs at least one arm");
    }

    [Fact]
    public void No_arguments_is_the_help_and_an_unknown_first_word_names_the_verbs()
    {
        var help = ModelArmsCommandLine.Split([]).Ok();

        help.Verb.Should().Be("help");
        help.Rest.Should().Equal("help");

        var refused = ModelArmsCommandLine.Split(["frob"]).Reason();

        refused.Should().StartWith("'frob' is not a verb");
        foreach (var verb in ModelArmsCommandLine.Verbs)
        {
            refused.Should().Contain(verb, "the refusal names every legal verb");
        }
    }

    [Fact]
    public void A_flag_takes_one_bare_value_and_a_second_bare_word_is_refused_naming_it()
    {
        ModelArmsCommandLine.Split(["status", "--parse-only", "--experiment", "/r"]).Ok().Rest.Should().Equal("status", "--parse-only", "--experiment", "/r");

        ModelArmsCommandLine.Split(["status", "--experiment", "/r", "extra"]).Reason().Should().Contain("'extra' is not a flag or a value");
    }
}
