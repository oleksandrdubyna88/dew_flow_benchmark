using Bench.Application;
using Bench.Application.Scouts;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Scouts;

/// <summary>S2 — the two new launcher options and the scout cell's whole launch (plan §3.4), spelled by <see cref="CliArgv"/>; S5 — the three
/// options of the confined launch (<c>--setting-sources ""</c>, <c>--settings</c>, <c>--plugin-dir</c>). Asserting argv proves what is ASKED
/// of the CLI; what this layer guarantees is the exact launch the confinement probes measured, and that an option a CLI has no flag for is
/// refused BY NAME, never dropped.</summary>
public sealed class ScoutArgvTests
{
    private static readonly ScoutLaunchFiles Files = new("/a/empty-mcp.json", "/a/plugins/feature-dev/f359d43b4060", "/a/scouts/pinned-settings.json");

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

    /// <summary>S5: no settings file is <c>--setting-sources ""</c> — an EMPTY ARGUMENT the CLI reads as "no source", not an absent flag (which
    /// would load every source); a list is comma-joined; the settings file and each plugin directory follow.</summary>
    [Fact]
    public void Claude_spells_empty_setting_sources_as_an_empty_argument_then_the_settings_file_and_each_plugin_directory()
    {
        var none = CliArgv.For(ModelRuntimeKind.CliClaude, "opus", new AgentAskOptions { SettingSources = AgentSettingSources.None, SettingsFile = "/p/pinned.json", PluginDirectories = ["/p/one", "/p/two"] }, []).Ok();
        var some = CliArgv.For(ModelRuntimeKind.CliClaude, "opus", new AgentAskOptions { SettingSources = AgentSettingSources.Only(["project", "local"]) }, []).Ok();

        none.Should().Equal("-p", "--model", "opus", "--setting-sources", "", "--settings", "/p/pinned.json", "--plugin-dir", "/p/one", "--plugin-dir", "/p/two");
        some.Should().Equal("-p", "--model", "opus", "--setting-sources", "project,local");
        AgentSettingSources.None.Should().Be(AgentSettingSources.Only([]), "equal by content");
        AgentSettingSources.None.Should().NotBe(AgentSettingSources.NotAsked, "an empty list is a request, not an absence");
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
        CliArgv.For(runtime, "m", new AgentAskOptions { SettingSources = AgentSettingSources.None }, []).Reason().Should().Contain("setting sources");
        CliArgv.For(runtime, "m", new AgentAskOptions { SettingsFile = "/x/pinned.json" }, []).Reason().Should().Contain("a settings file");
        CliArgv.For(runtime, "m", new AgentAskOptions { PluginDirectories = ["/x/plugin"] }, []).Reason().Should().Contain("plugin directories");
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
        new AgentAskOptions { SettingSources = AgentSettingSources.None }.IsNone.Should().BeFalse("an empty source list is a request");
        new AgentAskOptions { SettingsFile = "x" }.IsNone.Should().BeFalse();
        new AgentAskOptions { PluginDirectories = ["x"] }.IsNone.Should().BeFalse();
    }

    /// <summary>§3.4 widened by S5: <c>-p --model opus --output-format stream-json --verbose --tools Task Read Glob Grep --strict-mcp-config
    /// --mcp-config &lt;empty&gt; --forward-subagent-text --max-budget-usd &lt;cap&gt; --no-session-persistence --setting-sources ""
    /// --settings &lt;pinned&gt; --plugin-dir &lt;pinned feature-dev&gt;</c> — never <c>--restricted</c>, which removes the plugin agent under
    /// test (the 2026-10-12 confinement probe); never a <c>--disallowedTools</c> beside the allow-list; never an absent <c>--setting-sources</c>,
    /// which would load the user settings and their grant back (the 2026-10-13 grant-drop probe).</summary>
    [Fact]
    public void The_scout_cell_launches_the_allow_list_with_MCP_off_the_stream_the_forwarded_children_the_cap_no_settings_source_and_the_pinned_files()
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliClaude, "opus", ScoutLaunch.Options(1.25m, Files), []).Ok();

        argv.Should().Equal(
            "-p", "--model", "opus", "--output-format", "stream-json", "--verbose", "--tools", "Task", "Read", "Glob", "Grep",
            "--strict-mcp-config", "--mcp-config", "/a/empty-mcp.json", "--forward-subagent-text", "--max-budget-usd", "1.25", "--no-session-persistence",
            "--setting-sources", "", "--settings", "/a/scouts/pinned-settings.json", "--plugin-dir", "/a/plugins/feature-dev/f359d43b4060");
        argv.Should().NotContain("--restricted").And.NotContain("--disallowedTools", "an allow-list is the confinement — nothing beside it");
        argv.Should().ContainInConsecutiveOrder("--setting-sources", "").And.NotContain("--add-dir", "nothing is granted beyond the tree");
        ScoutLaunch.MainModel.Should().Be("opus", "the main session is always the Opus caller; only the scouts' model varies");
    }
}
