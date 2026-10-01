using Bench.Application;
using Bench.Application.Probes;
using Bench.Domain.Probes;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Probes;

/// <summary>S2 acceptance 1 — the probes' launch surface, spelled per CLI by <see cref="CliArgv"/> over the widened
/// <see cref="AgentAskOptions"/> (D5). Asserting argv proves what is ASKED of the CLI, not that the CLI honours it; what this
/// layer guarantees is that an option a CLI has no flag for is refused by name, never dropped, and that the two launches the
/// §4 evidence rule compares — <c>read-denied</c> and <c>web-confined</c> — differ in the web switch alone.
/// <para>Flags measured on this machine 2026-10-01: codex-cli 0.156.1 (<c>--search</c> top-level; <c>exec</c> takes
/// <c>--add-dir</c>, <c>--json</c>, <c>-s read-only</c>, <c>-</c>), claude 2.1.258 (<c>-p</c>, <c>--output-format json</c>,
/// <c>--permission-mode plan</c>, <c>--disallowedTools</c>, <c>--add-dir</c>, <c>--strict-mcp-config</c>), agy 1.2.14
/// (<c>--print</c>, <c>--output-format stream-json</c>, <c>--mode plan</c>, <c>--add-dir</c>, <c>--model</c>; no deny-list flag).</para></summary>
public sealed class ProbeArgvTests
{
    private static readonly ProbeFixture Fixture = new(
        "/work/probes/run/cell/g1/a1", "/work/probes/run/cell/g1/a1/cwd", "/work/probes/run/cell/g1/a1/cwd/inside.txt",
        "/work/probes/run/cell/g1/a1/outside", "/work/probes/run/cell/g1/a1/outside/canary.txt", ProbeTokens.Of("IN-7f3a9c2e1b", "OUT-4d8e6f0a2c").Ok());

    private static readonly ProbeRuntime[] CliRuntimes = [ProbeRuntime.Claude, ProbeRuntime.Codex, ProbeRuntime.Antigravity];

    [Fact]
    public void Codex_puts_search_BEFORE_exec_and_the_prompt_on_stdin()
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliCodex, "gpt-6-astra", new AgentAskOptions { WebSearch = AgentWebSearch.On, JsonEvents = true, Sandbox = AgentSandbox.ReadOnly, McpServersOff = true }, ["coai"]).Ok();

        argv.Should().StartWith(["--search", "exec"], "`codex exec --search` exits 2 (measured 2026-10-01): the flag is top-level, before the subcommand");
        argv.Should().ContainInConsecutiveOrder("-s", "read-only");
        argv.Should().Contain("--json").And.Contain("--skip-git-repo-check", "the fixture directory is no git repository");
        argv.Should().ContainInConsecutiveOrder("-c", "mcp_servers.coai.enabled=false");
        argv.Should().ContainInConsecutiveOrder("-m", "gpt-6-astra");
        argv[^1].Should().Be("-", "the prompt arrives on stdin");
    }

    [Fact]
    public void Codex_web_off_is_its_default_and_a_grant_is_an_exec_flag()
    {
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", new AgentAskOptions { WebSearch = AgentWebSearch.Off, JsonEvents = true, AddDirectories = ["/x/outside"] }, []).Ok()
            .Should().Equal(["exec", "--skip-git-repo-check", "--color", "never", "--json", "--add-dir", "/x/outside", "-m", "m", "-"],
                "codex searches only with --search, so OFF adds nothing; --add-dir belongs to exec");
    }

    [Fact]
    public void Claude_web_on_leaves_WebSearch_and_WebFetch_out_of_the_deny_list_and_web_off_puts_them_in()
    {
        var on = CliArgv.For(ModelRuntimeKind.CliClaude, "claude-sonnet-4-5", new AgentAskOptions { WebSearch = AgentWebSearch.On, DisallowedTools = ["Edit", "Write"], JsonEvents = true, Sandbox = AgentSandbox.ReadOnly }, []).Ok();
        var off = CliArgv.For(ModelRuntimeKind.CliClaude, "claude-sonnet-4-5", new AgentAskOptions { WebSearch = AgentWebSearch.Off, DisallowedTools = ["Edit", "Write"], JsonEvents = true, AddDirectories = ["/x/outside"] }, []).Ok();

        on.Should().StartWith(["-p", "--model", "claude-sonnet-4-5", "--output-format", "json", "--permission-mode", "plan"]);
        on.Should().ContainInConsecutiveOrder("--disallowedTools", "Edit", "Write");
        on.Should().NotContain("WebSearch").And.NotContain("WebFetch", "web ON on claude is the two web tools NOT denied");
        off.Should().ContainInConsecutiveOrder("--disallowedTools", "Edit", "Write", "WebSearch", "WebFetch");
        off.Should().ContainInConsecutiveOrder("--add-dir", "/x/outside");
        CliArgv.For(ModelRuntimeKind.CliClaude, "m", new AgentAskOptions { WebSearch = AgentWebSearch.Off }, []).Ok()
            .Should().Equal(["-p", "--model", "m", "--disallowedTools", "WebSearch", "WebFetch"], "web OFF alone still needs the deny flag");
    }

    [Fact]
    public void Antigravity_prints_stream_json_in_plan_mode_with_a_grant()
    {
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "gemini-3.1-pro", new AgentAskOptions { JsonEvents = true, Sandbox = AgentSandbox.ReadOnly, AddDirectories = ["/x/outside"], WebSearch = AgentWebSearch.On }, []).Ok()
            .Should().Equal(["--print", "--model", "gemini-3.1-pro", "--output-format", "stream-json", "--mode", "plan", "--add-dir", "/x/outside"],
                "agy 1.2.14's measured flags; web ON is agy as it is");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "gemini-3.1-pro").Ok().Should().Equal(["--print", "--model", "gemini-3.1-pro"]);
    }

    [Fact]
    public void An_option_the_cli_has_no_flag_for_is_refused_by_name_never_dropped()
    {
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { DisallowedTools = ["Read"] }, []).Reason().Should().Contain("a tool deny-list", "agy has no deny-list flag");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { WebSearch = AgentWebSearch.Off }, []).Reason().Should().Contain("web search off", "nothing measured turns agy's web off");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { McpServersOff = true }, []).Reason().Should().Contain("MCP servers off");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { MaxTurns = 1 }, []).Reason().Should().Contain("a turn ceiling");
        CliArgv.For(ModelRuntimeKind.CliGemini, "m", new AgentAskOptions { AddDirectories = ["/x"] }, []).Reason().Should().Contain("a directory grant", "gemini's flag was never measured here");
        CliArgv.For(ModelRuntimeKind.CliGemini, "m", new AgentAskOptions { JsonEvents = true }, []).Reason().Should().Contain("JSON events");
        CliArgv.For(ModelRuntimeKind.CliGemini, "m", new AgentAskOptions { WebSearch = AgentWebSearch.On }, []).Reason().Should().Contain("web search on");
        CliArgv.For(ModelRuntimeKind.OpenAiEndpoint, "m", new AgentAskOptions { JsonEvents = true }, []).Reason().Should().Contain("not a CLI agent");
    }

    [Fact]
    public void Without_the_new_options_every_existing_launch_is_byte_for_byte_what_it_was()
    {
        AgentAskOptions.None.IsNone.Should().BeTrue();
        new AgentAskOptions { WebSearch = AgentWebSearch.On }.IsNone.Should().BeFalse();
        new AgentAskOptions { JsonEvents = true }.IsNone.Should().BeFalse();
        new AgentAskOptions { AddDirectories = ["/x"] }.IsNone.Should().BeFalse();
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", AgentAskOptions.None, ["coai"]).Ok().Should().Equal(["exec", "-m", "m", "-"]);
        CliArgv.For(ModelRuntimeKind.CliClaude, "m", new AgentAskOptions { DisallowedTools = ["Edit", "Write", "NotebookEdit"], Sandbox = AgentSandbox.ReadOnly, MaxTurns = 1, McpServersOff = true }, []).Ok()
            .Should().Equal(["-p", "--model", "m", "--permission-mode", "plan", "--disallowedTools", "Edit", "Write", "NotebookEdit", "--max-turns", "1", "--strict-mcp-config"],
                "the blinded assessor's launch is unchanged");
    }

    /// <summary>§4 and the evidence rule: the control's launch is the confined row's launch with web OFF — one denial list,
    /// asserted equal, so what <c>read-denied</c> shows (a denied read is visible in the transcript) is about the denial the row
    /// actually runs under.</summary>
    [Fact]
    public void The_read_denied_launch_is_the_web_confined_launch_with_web_off()
    {
        var control = ProbeLaunch.OptionsFor(ProbeKind.ReadDenied, ProbeRuntime.Claude, Fixture);
        var row = ProbeLaunch.OptionsFor(ProbeKind.WebConfined, ProbeRuntime.Claude, Fixture);

        control.WebSearch.Should().Be(AgentWebSearch.Off);
        row.WebSearch.Should().Be(AgentWebSearch.On);
        control.DisallowedTools.Should().Equal(row.DisallowedTools, "ONE denial list, shared");
        (control with { WebSearch = AgentWebSearch.On }).Should().Be(row, "nothing else differs");
        control.DisallowedTools.Should().Contain(["Read", "Glob", "Grep", "Bash", "Edit", "Write", "NotebookEdit", "Task", "Agent"], "the file tools, the write tools and the delegations — coai's consultant list plus the readers");
        control.DisallowedTools.Should().NotContain("WebSearch", "the web switch is the WebSearch option, so the two lists compare equal");
        ProbeLaunch.FileToolDenials.Should().Equal(control.DisallowedTools);
    }

    [Fact]
    public void The_read_probes_deny_only_what_coais_consultant_denies_and_turn_the_web_off_where_the_cli_can()
    {
        var claude = ProbeLaunch.OptionsFor(ProbeKind.ReadInside, ProbeRuntime.Claude, Fixture);

        claude.DisallowedTools.Should().Equal(["Edit", "Write", "NotebookEdit", "Bash", "Task", "Agent"], "coai's own consultant denial minus the two web tools, which the WebSearch switch spells");
        claude.WebSearch.Should().Be(AgentWebSearch.Off);
        claude.Sandbox.Should().Be(AgentSandbox.ReadOnly);
        claude.JsonEvents.Should().BeTrue();
        claude.McpServersOff.Should().BeTrue("an operator's MCP server must not be the thing that reads the file");
        claude.AddDirectories.Should().BeEmpty();
        ProbeLaunch.OptionsFor(ProbeKind.ReadOutsideGranted, ProbeRuntime.Claude, Fixture).AddDirectories.Should().Equal([Fixture.OutsideDirectory]);
        ProbeLaunch.OptionsFor(ProbeKind.ReadOutsideBare, ProbeRuntime.Codex, Fixture).AddDirectories.Should().BeEmpty();
        ProbeLaunch.OptionsFor(ProbeKind.ReadOutsideGranted, ProbeRuntime.Antigravity, Fixture).AddDirectories.Should().Equal([Fixture.OutsideDirectory], "agy takes --add-dir (measured)");
        ProbeLaunch.OptionsFor(ProbeKind.ReadInside, ProbeRuntime.Antigravity, Fixture).WebSearch.Should().Be(AgentWebSearch.Default, "agy has no web-off flag; a read probe asks no web question, so nothing is asked");
        ProbeLaunch.OptionsFor(ProbeKind.WebConfined, ProbeRuntime.Codex, Fixture).DisallowedTools.Should().BeEmpty("file tools are denied where the CLI has a flag for it — codex has none");
        ProbeLaunch.OptionsFor(ProbeKind.WebSearch, ProbeRuntime.Codex, Fixture).WebSearch.Should().Be(AgentWebSearch.On);
    }

    /// <summary>The coherence the plan asks for: the planner's applicability table and what <see cref="CliArgv"/> can honour
    /// agree — every pair the planner keeps is a launch the CLI spells, and the one it drops is one the CLI refuses.</summary>
    [Fact]
    public void Every_pair_the_planner_keeps_is_a_launch_CliArgv_spells_and_every_pair_it_drops_is_one_CliArgv_refuses()
    {
        foreach (var runtime in CliRuntimes)
        {
            var kind = ProbeLaunch.RuntimeKind(runtime).Ok();

            foreach (var probe in ProbeWord.All.Where(p => !ProbeTraits.IsApi(p)))
            {
                var argv = CliArgv.For(kind, "m", ProbeLaunch.OptionsFor(probe, runtime, Fixture), []);
                var dropped = ProbeApplicability.Reason(probe, runtime).Length > 0;

                argv.Failed().Should().Be(dropped, $"{ProbeWord.Of(probe)} × {runtime}: {(argv is Bench.Domain.Outcome<IReadOnlyList<string>>.Fail f ? f.Reason : "spelled")}");
            }
        }

        ProbeLaunch.RuntimeKind(ProbeRuntime.Api).Reason().Should().Contain("api", "the api subject launches no CLI");
    }

    [Fact]
    public void The_prompt_names_the_fixture_file_the_probe_is_about_and_asks_the_web_question_only_on_the_web_probes()
    {
        ProbeLaunch.Prompt(ProbeKind.ReadInside, Fixture).Should().Contain("inside.txt").And.NotContain(Fixture.CanaryFile).And.NotContain("@openai/codex");
        ProbeLaunch.Prompt(ProbeKind.ReadOutsideBare, Fixture).Should().Contain(Fixture.CanaryFile, "the absolute path, so the CLI has no excuse of not finding it");
        ProbeLaunch.Prompt(ProbeKind.ReadOutsideGranted, Fixture).Should().Contain(Fixture.CanaryFile);
        ProbeLaunch.Prompt(ProbeKind.ReadDenied, Fixture).Should().Contain(Fixture.CanaryFile).And.NotContain("@openai/codex");
        ProbeLaunch.Prompt(ProbeKind.WebSearch, Fixture).Should().Contain("@openai/codex").And.Contain("URL").And.NotContain("canary");
        ProbeLaunch.Prompt(ProbeKind.WebConfined, Fixture).Should().Contain("@openai/codex").And.Contain(Fixture.CanaryFile, "the confined row asks BOTH halves");
        ProbeLaunch.Prompt(ProbeKind.ReadInside, Fixture).Should().NotContain(Fixture.Tokens.Inside).And.NotContain(Fixture.Tokens.Outside, "a prompt that quoted the token would be its own canary");
    }
}
