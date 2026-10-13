using System.Globalization;
using System.Text;

namespace Bench.Domain.Scouts;

/// <summary>A cell's prompts: the DRIVER the main session reads on stdin, and the scouts' focus prompts it must pass verbatim — one per
/// Agent call, in focus order — with their SHA-256s, which the read-back compares the calls with (§3.5).</summary>
public sealed record ScoutCellPrompts(string Driver, IReadOnlyList<string> Scouts)
{
    public IReadOnlyList<string> Sha256s => [.. Scouts.Select(StableHash.Of)];

    public string DriverSha256 => StableHash.Of(Driver);
}

/// <summary>The frozen prompts of §3.3/§3.4, composed — a pure function of the task's statement, the focus area and the arm, so the
/// same cell always sends the same bytes and its hash can be checked before and after (E2 S5 commits them to <c>prompts.json</c>).
/// <list type="bullet">
/// <item><b>A focus prompt</b> per scout: the frozen statement, the scout's focus area (F1..F5 — distinct, never copies), and the output
/// contract — <c>KEY FILES</c> (at most 10 repository-relative paths) then <c>CLAIMS</c> (at most 10 lines,
/// <c>path[:line] — one factual statement about that file</c>), identical across arms.</item>
/// <item><b>The driver</b>: launch exactly these N Agent calls in ONE message — the arm's agent type, the arm's model ALIAS, each prompt
/// between its markers verbatim — read nothing yourself, then reply DONE.</item>
/// </list>
/// <see cref="Version"/> names this wording; changing a word is a new version, and a run is never resumed across one.</summary>
public static class ScoutPrompts
{
    public const string Version = "scout-prompts-v1";

    public static ScoutCellPrompts ForCell(ScoutTask task, ScoutArm arm)
    {
        IReadOnlyList<string> scouts = [.. arm.Focuses.Select(focus => Focus(task.Statement, focus))];

        return new ScoutCellPrompts(Driver(arm, scouts), scouts);
    }

    public static string Focus(string statement, ScoutFocus focus) =>
        "You are scouting a code repository — your working directory is its root. Investigate this question:\n\n"
        + statement.Trim() + "\n\n"
        + $"Your focus area: {Area(focus)}. Stay inside your focus area; other scouts cover the rest.\n"
        + "Read only inside your working directory. Do not edit anything.\n\n"
        + "End your answer with exactly two blocks, in this order:\n\n"
        + ScoutOutput.KeyFilesHeader + "\n"
        + $"- at most {ScoutOutput.MaxLines} repository-relative paths of the files that matter most, one per line\n\n"
        + ScoutOutput.ClaimsHeader + "\n"
        + $"- at most {ScoutOutput.MaxLines} lines, each `path[:line] — one factual statement about that file` (what it defines, calls, decides or contains)";

    public static string Driver(ScoutArm arm, IReadOnlyList<string> prompts)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Launch exactly {prompts.Count} Agent call(s) in ONE message, all at once, then reply DONE and nothing else.\n")
            .Append(CultureInfo.InvariantCulture, $"Every call uses subagent_type \"{arm.AgentType}\" and model \"{arm.ModelAlias}\".\n")
            .Append("Each call's prompt is the text between its BEGIN and END markers, copied VERBATIM — not the marker lines themselves.\n")
            .Append("Do not read, search or open anything yourself, and call no other tool.\n");

        foreach (var (prompt, i) in prompts.Select((p, i) => (p, i + 1)))
        {
            text.Append(CultureInfo.InvariantCulture, $"\n=== AGENT {i} PROMPT BEGIN ===\n").Append(prompt)
                .Append(CultureInfo.InvariantCulture, $"\n=== AGENT {i} PROMPT END ===\n");
        }

        return text.ToString();
    }

    public static string Area(ScoutFocus focus) => focus switch
    {
        ScoutFocus.SimilarFeatures => "F1 — similar features already in the repository, and how they are built",
        ScoutFocus.Architecture => "F2 — the architecture and the abstractions involved",
        ScoutFocus.CurrentImplementation => "F3 — the current implementation of the behaviour in question",
        ScoutFocus.TestsAndFixtures => "F4 — the tests and fixtures that cover it",
        _ => "F5 — wiring, configuration and entry points",
    };
}
