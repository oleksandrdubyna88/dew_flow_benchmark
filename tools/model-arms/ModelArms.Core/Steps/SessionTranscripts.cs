namespace ModelArms.Core.Steps;

/// <summary>Where claude keeps a session's transcript — the file whose existence decides between resuming a session and starting a new one (plan D7). Pure string work; the adapter probes the disk.</summary>
public static class SessionTranscripts
{
    /// <summary>The folder claude keeps its state in: <c>CLAUDE_CONFIG_DIR</c> when it is non-empty (claude's own relocation of <c>~/.claude</c>, which the child receives untouched — D19), else
    /// <c>&lt;HOME&gt;/.claude</c>, else <c>""</c> when neither is there to name. That <c>CLAUDE_CONFIG_DIR</c> relocates the projects folder is read, not measured.</summary>
    public static string ClaudeHome(IReadOnlyDictionary<string, string> environment) =>
        Value(environment, "CLAUDE_CONFIG_DIR") is { Length: > 0 } relocated ? relocated
        : Value(environment, "HOME") is { Length: > 0 } home ? Path.Combine(home, ".claude")
        : string.Empty;

    /// <summary><c>&lt;claudeHome&gt;/projects/&lt;slug&gt;/&lt;session:D&gt;.jsonl</c>, the slug being the working directory with every character outside <c>[A-Za-z0-9]</c> replaced by <c>-</c> (<c>run-arm.sh</c> of the
    /// part-6 experiment: <c>sed 's|[^A-Za-z0-9]|-|g'</c>). Measured on this host: <c>/home/jinx/git/dew_flow_conventions</c> is <c>-home-jinx-git-dew-flow-conventions</c>, and the recorded transcript folder of the C1 arm's
    /// clone is <c>-home-jinx-git-coai-exp2-r-&lt;token&gt;-arm</c>. A character outside the basic plane is two UTF-16 units and so two dashes, as claude's own JavaScript replacement would write it — not measured.</summary>
    public static string PathOf(string claudeHome, string workingDirectory, Guid session) =>
        Path.Combine(claudeHome, "projects", Slug(workingDirectory), session.ToString("D") + ".jsonl");

    private static string Slug(string workingDirectory) => new([.. workingDirectory.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);

    private static string Value(IReadOnlyDictionary<string, string> environment, string name) => environment.TryGetValue(name, out var value) ? value : string.Empty;
}
