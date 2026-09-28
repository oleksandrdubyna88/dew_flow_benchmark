using Bench.Application;
using Bench.Application.Gate;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using Bench.Infrastructure.Process;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>S4.3 — the blinded assessor's launch, spelled per CLI by <see cref="CliArgv.For(ModelRuntimeKind, string, AgentAskOptions, IReadOnlyList{string})"/>.
/// Asserting argv proves what is ASKED of the CLI, not that the CLI honours it (the testing rule on delegated options);
/// the refusals are what this layer can guarantee: a guarantee the CLI has no flag for is refused, never dropped.</summary>
public sealed class AssessorArgvTests
{
    private const string Work = "/assess/batches/cs2-0000000a-a1";

    [Fact]
    public void The_codex_assessor_runs_read_only_with_the_output_schema_enforced_and_every_mcp_server_off()
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliCodex, "gpt-6-astra", FindingAssessor.OptionsFor(ModelRuntimeKind.CliCodex, Work), ["coai", "creds"]).Ok();

        argv.Should().ContainInConsecutiveOrder("-s", "read-only");
        argv.Should().ContainInConsecutiveOrder("--output-schema", Path.Combine(Work, FindingAssessor.SchemaFile));
        argv.Should().ContainInConsecutiveOrder("-o", Path.Combine(Work, FindingAssessor.AnswerFile));
        argv.Should().ContainInConsecutiveOrder("-c", "mcp_servers.coai.enabled=false", "-c", "mcp_servers.creds.enabled=false");
        argv.Should().ContainInConsecutiveOrder("-m", "gpt-6-astra");
        argv[0].Should().Be("exec");
        argv[^1].Should().Be("-", "the prompt arrives on stdin");
    }

    [Fact]
    public void The_claude_assessor_runs_in_plan_mode_with_the_write_tools_denied_one_turn_and_no_mcp_server()
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliClaude, "claude-opus-5", FindingAssessor.OptionsFor(ModelRuntimeKind.CliClaude, Work), []).Ok();

        argv.Should().ContainInConsecutiveOrder("--disallowedTools", "Edit", "Write", "NotebookEdit", "--max-turns", "1");
        argv.Should().ContainInConsecutiveOrder("--permission-mode", "plan");
        argv.Should().Contain("--strict-mcp-config");
        argv.Should().StartWith(["-p", "--model", "claude-opus-5"]);
    }

    [Fact]
    public void Without_options_every_existing_launch_is_byte_for_byte_what_it_was()
    {
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", AgentAskOptions.None, ["coai"]).Ok().Should().Equal(["exec", "-m", "m", "-"]);
        CliArgv.For(ModelRuntimeKind.CliClaude, "m", AgentAskOptions.None, []).Ok().Should().Equal(["-p", "--model", "m"]);
    }

    [Fact]
    public void A_guarantee_the_cli_has_no_flag_for_is_refused_by_name_never_dropped()
    {
        CliArgv.For(ModelRuntimeKind.CliClaude, "m", new AgentAskOptions { OutputSchemaFile = "s.json" }, []).Reason().Should().Contain("an output schema");
        CliArgv.For(ModelRuntimeKind.CliCodex, "m", new AgentAskOptions { MaxTurns = 1 }, []).Reason().Should().Contain("a turn ceiling");
        CliArgv.For(ModelRuntimeKind.CliGemini, "m", new AgentAskOptions { Sandbox = AgentSandbox.ReadOnly }, []).Reason().Should().Contain("a read-only sandbox");
    }

    [Fact]
    public void The_codex_config_names_its_mcp_servers_by_table()
    {
        const string toml = "model = \"gpt\"\n[mcp_servers.coai]\ncommand = \"coai-mcp\"\n[mcp_servers.creds]\n[mcp_servers.coai.env]\nX = \"1\"\n";

        CodexMcpServers.Parse(toml).Should().Equal(["coai", "creds"]);
        CodexMcpServers.Read(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.toml")).Ok().Should().BeEmpty();
    }

    [Fact]
    public void A_launch_that_writes_its_answer_to_a_file_is_answered_by_the_file_and_an_empty_file_is_a_refusal()
    {
        var ask = new AgentAsk(ModelRuntimeKind.CliCodex, "codex", "p", ".", TimeSpan.FromMinutes(1), "m")
        {
            Options = new AgentAskOptions { LastMessageFile = "/x/answer.json" },
        };
        var clean = new ProcessAttempt.Completed(new ProcessResult(0, "progress", "progress on stdout"));

        CliAgentRuntime.Read(clean, ask, TimeSpan.FromSeconds(3), """{"rows":[]}""").Ok().Text.Should().Be("""{"rows":[]}""");
        CliAgentRuntime.Read(clean, ask, TimeSpan.FromSeconds(3), string.Empty).Reason().Should().Contain("wrote no final message to answer.json");
    }
}
