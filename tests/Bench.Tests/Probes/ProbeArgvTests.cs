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
/// §4 evidence rule compares — <c>read-denied</c> and <c>web-confined</c> — differ in the web switch alone, in every mode.
/// <para>Flags measured on this machine 2026-10-01: codex-cli 0.156.1 (<c>--search</c> top-level; <c>exec</c> takes
/// <c>--add-dir</c>, <c>--json</c>, <c>-s read-only</c>, <c>-</c>), claude 2.1.258 (<c>-p</c>, <c>--output-format stream-json --verbose</c>,
/// <c>--permission-mode plan</c>, <c>--disallowedTools</c>, <c>--tools</c> — an allow-list, <c>""</c> offers nothing —, <c>--restricted</c>,
/// <c>--add-dir</c>, <c>--strict-mcp-config</c>), agy 1.2.14 (<c>--print=</c> EMPTY with the prompt as an NDJSON message on stdin,
/// <c>--input-format stream-json</c>, <c>--output-format stream-json</c>, <c>--mode plan</c>, <c>--add-dir</c>, <c>--model</c>; no deny-list flag).</para></summary>
public sealed class ProbeArgvTests
{
    private static readonly ProbeFixture Fixture = new(
        "/work/probes/run/cell/g1/a1", "/work/probes/run/cell/g1/a1/cwd", "/work/probes/run/cell/g1/a1/cwd/inside.txt",
        "/work/probes/run/cell/g1/a1/outside", "/work/probes/run/cell/g1/a1/outside/canary.txt", ProbeTokens.Of("IN-7f3a9c2e1b", "OUT-4d8e6f0a2c").Ok());

    private static readonly ProbeRuntime[] CliRuntimes = [ProbeRuntime.Claude, ProbeRuntime.Codex, ProbeRuntime.Antigravity];

    private static readonly ProbeConfinement[] ClaudeModes = [ProbeConfinement.Denylist, ProbeConfinement.Allowlist, ProbeConfinement.Restricted];

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

    /// <summary>S2b, finding 2: the transcript is the STREAM — the <c>json</c> envelope never says which tool ran.</summary>
    [Fact]
    public void Claude_web_on_leaves_WebSearch_and_WebFetch_out_of_the_deny_list_and_web_off_puts_them_in()
    {
        var on = CliArgv.For(ModelRuntimeKind.CliClaude, "claude-sonnet-4-5", new AgentAskOptions { WebSearch = AgentWebSearch.On, DisallowedTools = ["Edit", "Write"], JsonEvents = true, Sandbox = AgentSandbox.ReadOnly }, []).Ok();
        var off = CliArgv.For(ModelRuntimeKind.CliClaude, "claude-sonnet-4-5", new AgentAskOptions { WebSearch = AgentWebSearch.Off, DisallowedTools = ["Edit", "Write"], JsonEvents = true, AddDirectories = ["/x/outside"] }, []).Ok();

        on.Should().StartWith(["-p", "--model", "claude-sonnet-4-5", "--output-format", "stream-json", "--verbose", "--permission-mode", "plan"]);
        on.Should().ContainInConsecutiveOrder("--disallowedTools", "Edit", "Write");
        on.Should().NotContain("WebSearch").And.NotContain("WebFetch", "web ON on claude is the two web tools NOT denied");
        off.Should().ContainInConsecutiveOrder("--disallowedTools", "Edit", "Write", "WebSearch", "WebFetch");
        off.Should().ContainInConsecutiveOrder("--add-dir", "/x/outside");
        CliArgv.For(ModelRuntimeKind.CliClaude, "m", new AgentAskOptions { WebSearch = AgentWebSearch.Off }, []).Ok()
            .Should().Equal(["-p", "--model", "m", "--disallowedTools", "WebSearch", "WebFetch"], "web OFF alone still needs the deny flag");
    }

    /// <summary>S2b, finding 1: the allow-list and restricted mode as claude 2.1.258 takes them — <c>--tools</c> names the ONLY tools
    /// offered (an empty value offers nothing; <c>default</c> does not compose with names), <c>--restricted</c> stands alone; under an
    /// allow-list web OFF is the web tools' ABSENCE, never a deny entry beside it.</summary>
    [Fact]
    public void Claude_spells_an_allow_list_with_tools_and_restricted_mode_with_restricted()
    {
        var readers = CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", new AgentAskOptions { AllowedTools = AgentToolAllowlist.Only(["Read", "Glob", "Grep"]), WebSearch = AgentWebSearch.Off, JsonEvents = true, Sandbox = AgentSandbox.ReadOnly, McpServersOff = true }, []).Ok();
        var nothing = CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", new AgentAskOptions { AllowedTools = AgentToolAllowlist.Only([]), WebSearch = AgentWebSearch.Off }, []).Ok();
        var restricted = CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", new AgentAskOptions { Restricted = true, AllowedTools = AgentToolAllowlist.Only(["Read", "Glob", "Grep", "WebSearch", "WebFetch"]), WebSearch = AgentWebSearch.On, Sandbox = AgentSandbox.ReadOnly }, []).Ok();

        readers.Should().Equal(["-p", "--model", "sonnet", "--output-format", "stream-json", "--verbose", "--permission-mode", "plan", "--tools", "Read", "Glob", "Grep", "--strict-mcp-config"],
            "web OFF under an allow-list is the two web tools not being in it — no --disallowedTools");
        nothing.Should().Equal(["-p", "--model", "sonnet", "--tools", ""], "`--tools \"\"` offers nothing at all (init.tools = [] on 2.1.258)");
        restricted.Should().Equal(["-p", "--model", "sonnet", "--permission-mode", "plan", "--restricted", "--tools", "Read", "Glob", "Grep", "WebSearch", "WebFetch"]);
        CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", new AgentAskOptions { Restricted = true, WebSearch = AgentWebSearch.Off }, []).Ok()
            .Should().Equal(["-p", "--model", "sonnet", "--restricted", "--disallowedTools", "WebSearch", "WebFetch"], "without an allow-list, web OFF is still a deny entry");
    }

    /// <summary>S2b, finding 3: every live agy cell exited 2 — <c>Error: --print took "--model" as its prompt</c>. The launch is coai's
    /// (<c>AntigravityConsultant.cs</c>): the EMPTY <c>--print=</c>, the stream both ways, the prompt as one NDJSON user message on stdin.</summary>
    [Fact]
    public void Antigravity_is_launched_with_an_empty_print_the_stream_both_ways_and_the_prompt_as_an_ndjson_message()
    {
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "gemini-3.1-pro", new AgentAskOptions { JsonEvents = true, Sandbox = AgentSandbox.ReadOnly, AddDirectories = ["/x/outside"], WebSearch = AgentWebSearch.On }, []).Ok()
            .Should().Equal(["--print=", "--input-format", "stream-json", "--output-format", "stream-json", "--mode", "plan", "--model", "gemini-3.1-pro", "--add-dir", "/x/outside"],
                "agy 1.2.14 as coai launches it; web ON is agy as it is");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "gemini-3.1-pro").Ok()
            .Should().Equal(["--print=", "--input-format", "stream-json", "--output-format", "stream-json", "--model", "gemini-3.1-pro"], "the stream is the only launch that takes a prompt on stdin");
        AntigravityStdin.UserMessage("Read the file at C:\\x\\outside\\canary.txt and say \"hi\".")
            .Should().Be("{\"event\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"Read the file at C:\\\\x\\\\outside\\\\canary.txt and say \\u0022hi\\u0022.\"}}\n",
                "serialised, never interpolated — a prompt carries backslashes and quotes");
    }

    [Fact]
    public void An_option_the_cli_has_no_flag_for_is_refused_by_name_never_dropped()
    {
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { DisallowedTools = ["Read"] }, []).Reason().Should().Contain("a tool deny-list", "agy has no deny-list flag");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { WebSearch = AgentWebSearch.Off }, []).Reason().Should().Contain("web search off", "nothing measured turns agy's web off");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { McpServersOff = true }, []).Reason().Should().Contain("MCP servers off");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { MaxTurns = 1 }, []).Reason().Should().Contain("a turn ceiling");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { AllowedTools = AgentToolAllowlist.Only(["view_file"]) }, []).Reason().Should().Contain("a tool allow-list");
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", new AgentAskOptions { AllowedTools = AgentToolAllowlist.Only([]) }, []).Reason().Should().Contain("a tool allow-list");
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", new AgentAskOptions { Restricted = true }, []).Reason().Should().Contain("restricted mode");
        CliArgv.For(ModelRuntimeKind.CliAntigravity, "m", new AgentAskOptions { Restricted = true }, []).Reason().Should().Contain("restricted mode");
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
        new AgentAskOptions { AllowedTools = AgentToolAllowlist.Only([]) }.IsNone.Should().BeFalse("an empty allow-list is a real request: offer nothing");
        new AgentAskOptions { Restricted = true }.IsNone.Should().BeFalse();
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", AgentAskOptions.None, ["coai"]).Ok().Should().Equal(["exec", "-m", "m", "-"]);
        CliArgv.For(ModelRuntimeKind.CliClaude, "m", new AgentAskOptions { DisallowedTools = ["Edit", "Write", "NotebookEdit"], Sandbox = AgentSandbox.ReadOnly, MaxTurns = 1, McpServersOff = true }, []).Ok()
            .Should().Equal(["-p", "--model", "m", "--permission-mode", "plan", "--disallowedTools", "Edit", "Write", "NotebookEdit", "--max-turns", "1", "--strict-mcp-config"],
                "the blinded assessor's launch is unchanged");
    }

    /// <summary>§4 and the evidence rule, in EVERY mode: the control's launch is the confined row's launch with web OFF — one tool list,
    /// the two web tools out of it — so what <c>read-denied</c> shows (a denied read is visible in the transcript) is about the denial
    /// the row actually runs under.</summary>
    [Theory]
    [InlineData(ProbeConfinement.Denylist)]
    [InlineData(ProbeConfinement.Allowlist)]
    [InlineData(ProbeConfinement.Restricted)]
    public void The_read_denied_launch_is_the_web_confined_launch_with_web_off_in_every_mode(ProbeConfinement mode)
    {
        var control = ProbeLaunch.OptionsFor(ProbeKind.ReadDenied, ProbeRuntime.Claude, mode, Fixture);
        var row = ProbeLaunch.OptionsFor(ProbeKind.WebConfined, ProbeRuntime.Claude, mode, Fixture);

        control.WebSearch.Should().Be(AgentWebSearch.Off);
        row.WebSearch.Should().Be(AgentWebSearch.On);
        control.DisallowedTools.Should().Equal(row.DisallowedTools, "ONE denial list, shared");
        control.AllowedTools.Names.Should().Equal(row.AllowedTools.Names.Except(ProbeLaunch.ClaudeWebTools), "ONE allow-list, the web tools out of it");
        (control with { WebSearch = AgentWebSearch.On, AllowedTools = row.AllowedTools }).Should().Be(row, "nothing else differs");
    }

    /// <summary>Mode by mode, what claude is asked (S2b, finding 1). Denylist is coai's own lists; allowlist names what the probe needs and
    /// NOTHING for the control; restricted keeps the readers in every launch — the flag's promise to confine them is what is measured.</summary>
    [Fact]
    public void Each_claude_confinement_mode_spells_its_own_tool_list_per_probe()
    {
        var denylist = ProbeLaunch.OptionsFor(ProbeKind.ReadDenied, ProbeRuntime.Claude, ProbeConfinement.Denylist, Fixture);
        denylist.DisallowedTools.Should().Contain(["Read", "Glob", "Grep", "Bash", "Edit", "Write", "NotebookEdit", "Task", "Agent"], "coai's reviewer list (ClaudeRuntime.cs) — the readers, the shell, the write tools, the delegations");
        denylist.DisallowedTools.Should().NotContain("WebSearch", "the web switch is the WebSearch option, so the two lists compare equal");
        denylist.AllowedTools.Should().Be(AgentToolAllowlist.NotAsked);
        denylist.Restricted.Should().BeFalse();
        ProbeLaunch.FileToolDenials.Should().Equal(denylist.DisallowedTools);
        ProbeLaunch.OptionsFor(ProbeKind.ReadInside, ProbeRuntime.Claude, ProbeConfinement.Denylist, Fixture).DisallowedTools
            .Should().Equal(["Edit", "Write", "NotebookEdit", "Bash", "Task", "Agent"], "coai's consultant denial (ClaudeConsultant.cs) minus the two web tools, which the WebSearch switch spells");

        Allowed(ProbeKind.ReadInside, ProbeConfinement.Allowlist).Should().Equal(["Read", "Glob", "Grep"]);
        Allowed(ProbeKind.ReadOutsideGranted, ProbeConfinement.Allowlist).Should().Equal(["Read", "Glob", "Grep"]);
        Allowed(ProbeKind.ReadDenied, ProbeConfinement.Allowlist).Should().BeEmpty("the control offers NOTHING — `--tools \"\"`");
        Allowed(ProbeKind.WebSearch, ProbeConfinement.Allowlist).Should().Equal(["Read", "Glob", "Grep", "WebSearch", "WebFetch"], "web-search denies no file tool in any mode — the deny list leaves Read to it too");
        Allowed(ProbeKind.WebConfined, ProbeConfinement.Allowlist).Should().Equal(["WebSearch", "WebFetch"], "a denial is an absence");
        ProbeLaunch.OptionsFor(ProbeKind.WebConfined, ProbeRuntime.Claude, ProbeConfinement.Allowlist, Fixture).DisallowedTools.Should().BeEmpty();

        Allowed(ProbeKind.ReadInside, ProbeConfinement.Restricted).Should().Equal(["Read", "Glob", "Grep"]);
        Allowed(ProbeKind.ReadDenied, ProbeConfinement.Restricted).Should().Equal(["Read", "Glob", "Grep"], "the readers stay: --restricted confines them to the working directories, and that is the promise measured");
        Allowed(ProbeKind.WebConfined, ProbeConfinement.Restricted).Should().Equal(["Read", "Glob", "Grep", "WebSearch", "WebFetch"]);
        ProbeLaunch.OptionsFor(ProbeKind.WebConfined, ProbeRuntime.Claude, ProbeConfinement.Restricted, Fixture).Restricted.Should().BeTrue();
    }

    [Fact]
    public void The_read_probes_turn_the_web_off_where_the_cli_can_and_ask_nothing_of_agy()
    {
        var claude = ProbeLaunch.OptionsFor(ProbeKind.ReadInside, ProbeRuntime.Claude, ProbeConfinement.Denylist, Fixture);

        claude.WebSearch.Should().Be(AgentWebSearch.Off);
        claude.Sandbox.Should().Be(AgentSandbox.ReadOnly);
        claude.JsonEvents.Should().BeTrue();
        claude.McpServersOff.Should().BeTrue("an operator's MCP server must not be the thing that reads the file");
        claude.AddDirectories.Should().BeEmpty();
        ProbeLaunch.OptionsFor(ProbeKind.ReadOutsideGranted, ProbeRuntime.Claude, ProbeConfinement.Allowlist, Fixture).AddDirectories.Should().Equal([Fixture.OutsideDirectory]);
        ProbeLaunch.OptionsFor(ProbeKind.ReadOutsideBare, ProbeRuntime.Codex, ProbeConfinement.Default, Fixture).AddDirectories.Should().BeEmpty();
        ProbeLaunch.OptionsFor(ProbeKind.ReadOutsideGranted, ProbeRuntime.Antigravity, ProbeConfinement.Default, Fixture).AddDirectories.Should().Equal([Fixture.OutsideDirectory], "agy takes --add-dir (measured)");
        ProbeLaunch.OptionsFor(ProbeKind.ReadInside, ProbeRuntime.Antigravity, ProbeConfinement.Default, Fixture).WebSearch.Should().Be(AgentWebSearch.Default, "agy has no web-off flag; a read probe asks no web question, so nothing is asked");
        ProbeLaunch.OptionsFor(ProbeKind.WebConfined, ProbeRuntime.Codex, ProbeConfinement.Default, Fixture).Should().Match<AgentAskOptions>(o => o.DisallowedTools.Count == 0 && !o.AllowedTools.Asked && !o.Restricted,
            "file tools are denied where the CLI has a flag for it — codex has none");
        ProbeLaunch.OptionsFor(ProbeKind.WebSearch, ProbeRuntime.Codex, ProbeConfinement.Default, Fixture).WebSearch.Should().Be(AgentWebSearch.On);
    }

    /// <summary>The coherence the plan asks for: the planner's applicability table and what <see cref="CliArgv"/> can honour
    /// agree — every pair the planner keeps is a launch the CLI spells, in every mode, and the one it drops is one the CLI refuses.</summary>
    [Fact]
    public void Every_pair_the_planner_keeps_is_a_launch_CliArgv_spells_and_every_pair_it_drops_is_one_CliArgv_refuses()
    {
        foreach (var runtime in CliRuntimes)
        {
            var kind = ProbeLaunch.RuntimeKind(runtime).Ok();

            foreach (var mode in runtime == ProbeRuntime.Claude ? ClaudeModes : [ProbeConfinement.Default])
            {
                foreach (var probe in ProbeWord.All.Where(p => !ProbeTraits.IsApi(p)))
                {
                    var argv = CliArgv.For(kind, "m", ProbeLaunch.OptionsFor(probe, runtime, mode, Fixture), []);
                    var dropped = ProbeApplicability.Reason(probe, runtime).Length > 0;

                    argv.Failed().Should().Be(dropped, $"{ProbeWord.Of(probe)} × {runtime} ({mode}): {(argv is Bench.Domain.Outcome<IReadOnlyList<string>>.Fail f ? f.Reason : "spelled")}");
                }
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

    private static IReadOnlyList<string> Allowed(ProbeKind probe, ProbeConfinement mode) => ProbeLaunch.OptionsFor(probe, ProbeRuntime.Claude, mode, Fixture).AllowedTools.Names;
}
