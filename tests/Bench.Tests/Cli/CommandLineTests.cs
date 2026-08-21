using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary>Flag values that do not read as what the verb needs are refused by NAME. The review found
/// the alternative: <c>--leg-wall-seconds 12O0</c> silently becoming the 600-second default — a
/// measurement running under a ceiling nobody chose, with nothing anywhere saying so.</summary>
public sealed class CommandLineTests
{
    [Fact]
    public void An_absent_numeric_flag_still_falls_back()
    {
        var command = CommandLine.Parse(["run"]);

        command.Int("leg-wall-seconds", 600).Should().Be(600);
        command.Double("min-spread", 0.25).Should().Be(0.25);
    }

    [Fact]
    public void A_present_but_unreadable_integer_is_refused_by_name_never_defaulted()
    {
        var command = CommandLine.Parse(["run", "--leg-wall-seconds", "12O0"]);

        var read = () => command.Int("leg-wall-seconds", 600);

        read.Should().Throw<CommandLineException>().WithMessage("*--leg-wall-seconds*12O0*");
    }

    [Fact]
    public void A_present_but_unreadable_double_is_refused_by_name_never_defaulted()
    {
        // The culture trap spelled out: "0,25" is a quarter on half the world's keyboards and fails
        // invariant parsing — the refusal must NAME it rather than quietly measure at the default.
        var command = CommandLine.Parse(["report", "--min-spread", "0,25"]);

        var read = () => command.Double("min-spread", 0.25);

        read.Should().Throw<CommandLineException>().WithMessage("*--min-spread*0,25*");
    }

    [Fact]
    public void The_refusal_surfaces_as_the_configuration_exit_code_not_a_crash()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = Program.Run(
            ["prune", "--db", "Host=nowhere;Database=x", "--hit-retention-days", "abc"],
            output, error, TestContext.Current.CancellationToken);

        code.Should().Be(ExitCodes.Configuration);
        error.ToString().Should().Contain("--hit-retention-days").And.Contain("abc");
    }
}
