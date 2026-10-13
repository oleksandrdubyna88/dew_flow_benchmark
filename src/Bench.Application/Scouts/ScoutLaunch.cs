namespace Bench.Application.Scouts;

/// <summary>The three files the confined launch names (S5): the empty MCP config (<c>--mcp-config</c>), the PINNED plugin directory
/// (<c>--plugin-dir</c> — the one version folder of <c>feature-dev</c> claude loads for the user, resolved from the installed plugin cache
/// the pin reader hashes) and the pinned settings file (<c>--settings</c> — the operator's <c>effortLevel</c> and <c>modelSettings</c>, copied
/// once and hashed as the settings pin).</summary>
public sealed record ScoutLaunchFiles(string EmptyMcpConfigFile, string PluginDirectory, string PinnedSettingsFile);

/// <summary>The one launch of a scout cell (plan §3.4, widened by E2 S5), spelled once as <see cref="AgentAskOptions"/> so the runner, the
/// alias check, the access check and the argv tests read one definition:
/// <c>-p --model opus --output-format stream-json --verbose --tools Task Read Glob Grep --strict-mcp-config --mcp-config &lt;empty&gt;
/// --forward-subagent-text --max-budget-usd &lt;cap&gt; --no-session-persistence --setting-sources "" --settings &lt;pinned&gt;
/// --plugin-dir &lt;pinned feature-dev&gt;</c>, the prompt on stdin, the working directory the task's history-free tree.
/// <list type="bullet">
/// <item><b>The allow-list is the confinement</b> (§3.2, measured on claude 2.1.289): the main session is offered exactly the Agent tool
/// (<c>Task</c>) and the three readers; nothing is in a deny list beside it. <b>Never <c>--restricted</c></b> — it ignores the user
/// settings the plugin agent comes from, and <c>feature-dev:code-explorer</c> is then "not found".</item>
/// <item><b>MCP is off</b>: <c>--strict-mcp-config</c> with an EMPTY <c>--mcp-config</c>, the launch the confinement probe ran.</item>
/// <item><b>The children are visible</b>: <c>--forward-subagent-text</c> prints every child event under its Agent call's
/// <c>parent_tool_use_id</c>, which is what <c>ClaudeSubagents</c> groups by.</item>
/// <item><b>The cap is the arm's</b> and never a default — the ledger reserves it plus 20 % before the launch (§3.10).</item>
/// <item><b>No settings file is loaded</b> (S5, measured 2026-10-13 on claude 2.1.289 — <c>tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-confine-sources-*.ndjson</c>):
/// <c>--setting-sources ""</c> drops the operator's user settings, so the directory they GRANT (<c>permissions.additionalDirectories</c>)
/// is denied to a scout like any other outside path, and the login stays. What the user settings also carried comes back pinned:
/// the plugin through <c>--plugin-dir</c>, the effort through <c>--settings</c>. User-level hooks — a future sub-agent model guard
/// included — are not loaded under empty sources, so no guard switch exists in this launch.</item>
/// </list></summary>
public static class ScoutLaunch
{
    /// <summary>The main session is always the Opus caller; only the scouts' model varies by arm.</summary>
    public const string MainModel = "opus";

    /// <summary>What the empty MCP config file holds.</summary>
    public const string EmptyMcpConfig = "{\"mcpServers\":{}}\n";

    /// <summary>The wall of a CHECK launch — <c>alias-check</c>, <c>access-check</c>: one Agent call that lists or reads a handful of files,
    /// capped at $1. Ten minutes is an order of magnitude above the live runs (20–60 s) and still ends a CLI that hung; a measuring cell's
    /// wall is the schedule's, not this one.</summary>
    public static TimeSpan CheckWall { get; } = TimeSpan.FromMinutes(10);

    /// <summary>The main session's tools: the Agent tool (spelled <c>Task</c> in <c>--tools</c>) and the three readers.</summary>
    public static IReadOnlyList<string> AllowedTools { get; } = ["Task", "Read", "Glob", "Grep"];

    public static AgentAskOptions Options(decimal capUsd, ScoutLaunchFiles files) => new()
    {
        JsonEvents = true,
        AllowedTools = AgentToolAllowlist.Only(AllowedTools),
        McpServersOff = true,
        McpConfigFile = files.EmptyMcpConfigFile,
        ForwardSubagentText = true,
        MaxBudgetUsd = capUsd,
        NoSessionPersistence = true,
        SettingSources = AgentSettingSources.None,
        SettingsFile = files.PinnedSettingsFile,
        PluginDirectories = [files.PluginDirectory],
    };
}
