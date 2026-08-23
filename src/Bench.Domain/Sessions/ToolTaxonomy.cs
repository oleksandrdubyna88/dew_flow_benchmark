namespace Bench.Domain.Sessions;

/// <summary>Which tools read and which tools write, by NAME — the first half of the phase classifier.
/// <para>
/// <b>The names here are the real ones.</b> The draft specification this system was built from assumed
/// <c>read_file</c> / <c>edit_file</c> / <c>execute_command</c>; Claude Code's tools are called
/// <c>Read</c>, <c>Edit</c> and <c>Bash</c>, and a classifier keyed on the invented names would have
/// labelled every call <see cref="ToolClass.Unknown"/> while reporting confident phase totals over
/// nothing. Verified against the published tool reference, 2026-08-23.
/// </para>
/// <para>
/// <b>Per-runtime tables are DATA.</b> Adding Codex or Gemini must not touch the classifier — it builds a
/// taxonomy value and hands it in. <see cref="ClaudeCode"/> is one such value, not a special case in the
/// code.
/// </para></summary>
/// <param name="Read">Tools that observe and change nothing.</param>
/// <param name="Write">Tools that change the tree.</param>
/// <param name="Shell">Tools whose class depends on the COMMAND, not the name — settled by
/// <see cref="CommandClassifier"/> and, where the check ran, by the working tree itself.</param>
/// <param name="Neutral">Tools that belong to whichever phase surrounds them.</param>
public sealed record ToolTaxonomy(
    IReadOnlySet<string> Read,
    IReadOnlySet<string> Write,
    IReadOnlySet<string> Shell,
    IReadOnlySet<string> Neutral)
{
    /// <summary>How an MCP tool arrives in a hook payload: <c>mcp__&lt;server&gt;__&lt;tool&gt;</c>. The
    /// server segment is stripped before matching, so one taxonomy entry covers a tool however it is
    /// mounted — the same tool served by two servers is one tool.</summary>
    public const string McpPrefix = "mcp__";

    /// <summary>Claude Code's own surface, plus this family's MCP tools.
    /// <para>
    /// The MCP half follows the prefix convention the tool server already publishes — <c>rag_</c>,
    /// <c>graf_</c> and <c>rt_</c> read, and <c>rt_apply_edit_plan</c> is the single write verb. That
    /// convention is load-bearing rather than cosmetic, and this is the second place it pays for itself.
    /// </para></summary>
    public static ToolTaxonomy ClaudeCode { get; } = new(
        Read: Set(
            "Read", "Grep", "Glob", "WebFetch", "WebSearch", "NotebookRead", "LSP",
            "TaskGet", "TaskList", "ListAgents", "CronList"),
        Write: Set(
            "Edit", "Write", "NotebookEdit", "Artifact",
            "TaskCreate", "TaskUpdate", "CronCreate", "CronDelete",
            // The one MCP write verb in this family. Named rather than matched by prefix, because being
            // the only one is the property the tool server's design guarantees.
            "rt_apply_edit_plan"),
        Shell: Set("Bash", "PowerShell"),
        Neutral: Set(
            "TodoWrite", "AskUserQuestion", "Skill", "Agent", "Task", "Workflow", "Monitor",
            "SendMessage", "PushNotification", "ExitPlanMode", "EnterPlanMode", "ReportFindings",
            "ScheduleWakeup", "ToolSearch", "EnterWorktree", "ExitWorktree", "TaskOutput", "TaskStop"));

    /// <summary>The table for one runtime.
    /// <para>
    /// One entry today, and a lookup the day a second runtime lands — which is the whole reason the
    /// taxonomy is a VALUE rather than a static class. An unrecognised runtime gets this table rather than
    /// an empty one, deliberately: its tools then land in <see cref="ToolClass.Unknown"/> and are counted
    /// as the instrument's own error bar, where an empty taxonomy would classify everything as unknown and
    /// look identical to a runtime nobody had wired up.
    /// </para></summary>
    public static ToolTaxonomy For(string runtime) => ClaudeCode;

    /// <summary>Prefixes of this family's read-only MCP tools. A prefix rather than a list because the
    /// catalog grows without this file: a new <c>rag_</c> tool is a read tool by construction, and a
    /// taxonomy that had to be edited for each one would be a taxonomy that is wrong between releases.</summary>
    private static readonly string[] ReadPrefixes = ["rag_", "graf_", "rt_"];

    /// <summary>The class of one call, before the working tree gets a say.
    /// <para>
    /// <paramref name="command"/> is the shell command for <see cref="Shell"/> tools and is ignored for
    /// every other kind. An unknown name answers <see cref="ToolClass.Unknown"/> and is never guessed into
    /// <see cref="ToolClass.Neutral"/>: an unclassified tool is a hole in this table, and a report that
    /// renders it as harmless is a report that hides the hole.
    /// </para></summary>
    public ToolClass Classify(string toolName, string command)
    {
        var name = Strip(toolName);

        if (Shell.Contains(name))
        {
            return CommandClassifier.Classify(command);
        }

        return Match(name);
    }

    private ToolClass Match(string name)
    {
        if (Write.Contains(name))
        {
            return ToolClass.Write;
        }

        if (Read.Contains(name) || ReadPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
        {
            return ToolClass.Read;
        }

        return Neutral.Contains(name) ? ToolClass.Neutral : ToolClass.Unknown;
    }

    /// <summary>The tool's own name, with any MCP server namespace removed. A name with the prefix and no
    /// second separator is left alone rather than mangled — a shape we have not seen is not a shape to
    /// guess at.</summary>
    public static string Strip(string toolName)
    {
        if (!toolName.StartsWith(McpPrefix, StringComparison.Ordinal))
        {
            return toolName;
        }

        var separator = toolName.IndexOf("__", McpPrefix.Length, StringComparison.Ordinal);

        return separator < 0 ? toolName : toolName[(separator + 2)..];
    }

    /// <summary>Whether this tool SEARCHES — asks a question of the corpus rather than reading a named
    /// thing.
    /// <para>
    /// The distinction the variant-chain detector needs and nothing else does. Three reads of three files
    /// in one folder share most of their target's words and are perfectly ordinary; three searches that
    /// share most of their words are one question asked three ways, which is the pattern being hunted.
    /// Without this predicate the detector cannot tell those apart and fires on the ordinary one.
    /// </para>
    /// <para>Matched by word as well as by name, so a search tool this table has never seen — any
    /// repository's own <c>*_search_*</c> — is still recognised as one.</para></summary>
    public bool IsSearch(string toolName)
    {
        var name = Strip(toolName);

        return SearchNames.Contains(name)
            || SearchWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly IReadOnlySet<string> SearchNames = Set("Grep", "Glob", "WebSearch");

    private static readonly string[] SearchWords = ["search", "grep", "glob", "find"];

    private static IReadOnlySet<string> Set(params string[] names) =>
        new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
}
