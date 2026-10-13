namespace Bench.Application.Scouts;

/// <summary>The one launch of a scout cell (plan §3.4), spelled once as <see cref="AgentAskOptions"/> so the runner, the alias check and
/// the argv tests read one definition:
/// <c>-p --model opus --output-format stream-json --verbose --tools Task Read Glob Grep --strict-mcp-config --mcp-config &lt;empty&gt;
/// --forward-subagent-text --max-budget-usd &lt;cap&gt; --no-session-persistence</c>, the prompt on stdin, the working directory the task's
/// history-free tree.
/// <list type="bullet">
/// <item><b>The allow-list is the confinement</b> (§3.2, measured on claude 2.1.289): the main session is offered exactly the Agent tool
/// (<c>Task</c>) and the three readers; nothing is in a deny list beside it. <b>Never <c>--restricted</c></b> — it ignores the user
/// settings the plugin agent comes from, and <c>feature-dev:code-explorer</c> is then "not found".</item>
/// <item><b>MCP is off</b>: <c>--strict-mcp-config</c> with an EMPTY <c>--mcp-config</c>, the launch the confinement probe ran.</item>
/// <item><b>The children are visible</b>: <c>--forward-subagent-text</c> prints every child event under its Agent call's
/// <c>parent_tool_use_id</c>, which is what <c>ClaudeSubagents</c> groups by.</item>
/// <item><b>The cap is the arm's</b> and never a default — the ledger reserves it plus 20 % before the launch (§3.10).</item>
/// </list></summary>
public static class ScoutLaunch
{
    /// <summary>The main session is always the Opus caller; only the scouts' model varies by arm.</summary>
    public const string MainModel = "opus";

    /// <summary>What the empty MCP config file holds.</summary>
    public const string EmptyMcpConfig = "{\"mcpServers\":{}}\n";

    /// <summary>The main session's tools: the Agent tool (spelled <c>Task</c> in <c>--tools</c>) and the three readers.</summary>
    public static IReadOnlyList<string> AllowedTools { get; } = ["Task", "Read", "Glob", "Grep"];

    public static AgentAskOptions Options(decimal capUsd, string emptyMcpConfigFile) => new()
    {
        JsonEvents = true,
        AllowedTools = AgentToolAllowlist.Only(AllowedTools),
        McpServersOff = true,
        McpConfigFile = emptyMcpConfigFile,
        ForwardSubagentText = true,
        MaxBudgetUsd = capUsd,
        NoSessionPersistence = true,
    };
}
