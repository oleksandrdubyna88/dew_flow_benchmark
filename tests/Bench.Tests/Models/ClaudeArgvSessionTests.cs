using Bench.Application;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Models;

/// <summary>Model arms E1.S5 — the launcher's four claude options (a session id, a session to resume, a permission mode, a permission
/// allow-list), spelled by <see cref="CliArgv"/>. Asserting argv proves what is ASKED of the CLI, not that the CLI honours it (the testing rule
/// on delegated options); what this layer guarantees is the spelling, and that an option a CLI has no flag for is refused by name, never
/// dropped.</summary>
public sealed class ClaudeArgvSessionTests
{
    private const string IdText = "0f3c9a1e-5b7d-4c2a-9e8f-1234567890ab";

    private static readonly Guid Id = Guid.Parse(IdText);

    private static IReadOnlyList<string> Claude(AgentAskOptions options) =>
        CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", options, []).Ok();

    private static string Refusal(ModelRuntimeKind runtime, AgentAskOptions options)
    {
        var refused = CliArgv.For(runtime, "m", options, []);
        refused.Failed().Should().BeTrue($"{runtime} must refuse, never drop, {options}");

        return refused.Reason();
    }

    [Fact]
    public void A_fresh_attempt_passes_session_id_and_a_resume_passes_resume()
    {
        Claude(new AgentAskOptions { SessionId = Id }).Should().Equal("-p", "--model", "sonnet", "--session-id", IdText);
        Claude(new AgentAskOptions { ResumeSessionId = Id }).Should().Equal("-p", "--model", "sonnet", "--resume", IdText);
        Claude(new AgentAskOptions { SessionId = Id, JsonEvents = true })
            .Should().Equal("-p", "--model", "sonnet", "--session-id", IdText, "--output-format", "stream-json", "--verbose");
    }

    [Fact]
    public void Session_id_and_resume_together_are_refused()
    {
        var other = Guid.NewGuid();

        var reason = Refusal(ModelRuntimeKind.CliClaude, new AgentAskOptions { SessionId = Id, ResumeSessionId = other });

        reason.Should().Contain("starts a session or resumes one, never both");
        reason.Should().Contain(IdText).And.Contain(other.ToString("D"));
    }

    [Theory]
    [InlineData(AgentPermissionMode.AcceptEdits, "acceptEdits")]
    [InlineData(AgentPermissionMode.Default, "default")]
    [InlineData(AgentPermissionMode.Plan, "plan")]
    public void Permission_mode_accept_edits_default_and_plan_are_spelled(AgentPermissionMode mode, string word)
    {
        Claude(new AgentAskOptions { PermissionMode = mode }).Should().Equal("-p", "--model", "sonnet", "--permission-mode", word);
        Claude(new AgentAskOptions { PermissionMode = mode, JsonEvents = true })
            .Should().Equal("-p", "--model", "sonnet", "--output-format", "stream-json", "--verbose", "--permission-mode", word);
    }

    [Fact]
    public void Permission_mode_beside_the_read_only_sandbox_is_refused()
    {
        Refusal(ModelRuntimeKind.CliClaude, new AgentAskOptions { Sandbox = AgentSandbox.ReadOnly, PermissionMode = AgentPermissionMode.AcceptEdits })
            .Should().Contain("read-only sandbox already spells --permission-mode plan");
        Refusal(ModelRuntimeKind.CliClaude, new AgentAskOptions { Sandbox = AgentSandbox.ReadOnly, PermissionMode = AgentPermissionMode.Plan })
            .Should().Contain("read-only sandbox already spells --permission-mode plan");

        Claude(new AgentAskOptions { Sandbox = AgentSandbox.ReadOnly })
            .Should().Equal("-p", "--model", "sonnet", "--permission-mode", "plan");
    }

    [Fact]
    public void Permitted_tool_patterns_are_allowedTools_and_the_builtin_list_stays_tools()
    {
        var both = Claude(new AgentAskOptions
        {
            AllowedTools = AgentToolAllowlist.Only(["Read", "Edit"]),
            PermittedTools = ["Bash(git *)", "Read", "mcp__coai__open"],
        });

        both.Should().Equal("-p", "--model", "sonnet", "--tools", "Read", "Edit", "--allowedTools", "Bash(git *)", "Read", "mcp__coai__open");

        var alone = Claude(new AgentAskOptions { PermittedTools = ["Bash(git *)"] });

        alone.Should().Equal("-p", "--model", "sonnet", "--allowedTools", "Bash(git *)");
        alone.Should().NotContain("--tools");
    }

    [Theory]
    [InlineData(ModelRuntimeKind.CliCodex)]
    [InlineData(ModelRuntimeKind.CliGemini)]
    [InlineData(ModelRuntimeKind.CliAntigravity)]
    public void Codex_gemini_and_antigravity_refuse_the_four_options_by_name(ModelRuntimeKind runtime)
    {
        Refusal(runtime, new AgentAskOptions { SessionId = Id }).Should().Contain("a session id").And.Contain("has no flag for");
        Refusal(runtime, new AgentAskOptions { ResumeSessionId = Id }).Should().Contain("a session to resume").And.Contain("has no flag for");
        Refusal(runtime, new AgentAskOptions { PermissionMode = AgentPermissionMode.Default }).Should().Contain("a permission mode").And.Contain("has no flag for");
        Refusal(runtime, new AgentAskOptions { PermittedTools = ["Read"] }).Should().Contain("a permission allow-list").And.Contain("has no flag for");
    }

    [Fact]
    public void IsNone_is_false_for_each_new_option()
    {
        new AgentAskOptions { SessionId = Id }.IsNone.Should().BeFalse("a session id is a request");
        new AgentAskOptions { ResumeSessionId = Id }.IsNone.Should().BeFalse("a session to resume is a request");
        new AgentAskOptions { PermissionMode = AgentPermissionMode.Default }.IsNone.Should().BeFalse("a permission mode is a request");
        new AgentAskOptions { PermittedTools = ["Read"] }.IsNone.Should().BeFalse("a permission allow-list is a request");

        new AgentAskOptions().IsNone.Should().BeTrue("every existing caller's argv stays Plain");
        AgentAskOptions.None.IsNone.Should().BeTrue();
    }
}
