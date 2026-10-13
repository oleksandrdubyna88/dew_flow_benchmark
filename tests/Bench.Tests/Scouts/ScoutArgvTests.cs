using Bench.Application;
using Bench.Application.Scouts;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>S2 — the two new launcher options and the scout cell's whole launch (plan §3.4), spelled by <see cref="CliArgv"/>. Asserting
/// argv proves what is ASKED of the CLI; what this layer guarantees is the exact launch the confinement probe measured, and that an
/// option a CLI has no flag for is refused BY NAME, never dropped.</summary>
public sealed class ScoutArgvTests
{
    [Fact]
    public void Claude_spells_forward_subagent_text_and_the_budget_cap_in_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

        try
        {
            var argv = CliArgv.For(ModelRuntimeKind.CliClaude, "opus", new AgentAskOptions { ForwardSubagentText = true, MaxBudgetUsd = 2.5m }, []).Ok();

            argv.Should().Equal(["-p", "--model", "opus", "--forward-subagent-text", "--max-budget-usd", "2.5"],
                "a cap typed on a comma-decimal machine must still reach the CLI as 2.5, never 2,5");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(ModelRuntimeKind.CliCodex)]
    [InlineData(ModelRuntimeKind.CliAntigravity)]
    [InlineData(ModelRuntimeKind.CliGemini)]
    public void Every_other_CLI_refuses_each_new_option_by_name(ModelRuntimeKind runtime)
    {
        CliArgv.For(runtime, "m", new AgentAskOptions { ForwardSubagentText = true }, []).Reason().Should().Contain("forwarded subagent text");
        CliArgv.For(runtime, "m", new AgentAskOptions { MaxBudgetUsd = 1m }, []).Reason().Should().Contain("a budget cap");
        CliArgv.For(runtime, "m", new AgentAskOptions { McpConfigFile = "/x/empty.json" }, []).Reason().Should().Contain("an MCP config file");
        CliArgv.For(runtime, "m", new AgentAskOptions { NoSessionPersistence = true }, []).Reason().Should().Contain("no session persistence");
    }

    [Fact]
    public void A_negative_budget_cap_is_refused_rather_than_launched_uncapped()
    {
        CliArgv.For(ModelRuntimeKind.CliClaude, "opus", new AgentAskOptions { MaxBudgetUsd = -1m }, []).Reason().Should().Contain("not a cap");
    }

    [Fact]
    public void No_new_option_changes_an_existing_callers_argv()
    {
        new AgentAskOptions().IsNone.Should().BeTrue();
        new AgentAskOptions { MaxBudgetUsd = 1m }.IsNone.Should().BeFalse();
        new AgentAskOptions { ForwardSubagentText = true }.IsNone.Should().BeFalse();
        new AgentAskOptions { McpConfigFile = "x" }.IsNone.Should().BeFalse();
        new AgentAskOptions { NoSessionPersistence = true }.IsNone.Should().BeFalse();
    }

    /// <summary>§3.4: <c>-p --model opus --tools Task Read Glob Grep --strict-mcp-config --mcp-config &lt;empty&gt; --output-format
    /// stream-json --verbose --forward-subagent-text --max-budget-usd &lt;cap&gt; --no-session-persistence</c> — never <c>--restricted</c>,
    /// which removes the plugin agent under test (the 2026-10-12 confinement probe).</summary>
    [Fact]
    public void The_scout_cell_launches_the_allow_list_with_MCP_off_the_stream_the_forwarded_children_and_the_cap()
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliClaude, "opus", ScoutLaunch.Options(1.25m, "/a/empty-mcp.json"), []).Ok();

        argv.Should().Equal(
            "-p", "--model", "opus", "--output-format", "stream-json", "--verbose", "--tools", "Task", "Read", "Glob", "Grep",
            "--strict-mcp-config", "--mcp-config", "/a/empty-mcp.json", "--forward-subagent-text", "--max-budget-usd", "1.25", "--no-session-persistence");
        argv.Should().NotContain("--restricted").And.NotContain("--disallowedTools", "an allow-list is the confinement — nothing beside it");
        ScoutLaunch.MainModel.Should().Be("opus", "the main session is always the Opus caller; only the scouts' model varies");
    }
}
