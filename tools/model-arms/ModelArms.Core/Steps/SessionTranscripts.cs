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

    /// <summary><c>&lt;claudeHome&gt;/projects/&lt;slug&gt;/&lt;session:D&gt;.jsonl</c>, or <c>""</c> when <paramref name="claudeHome"/> is empty — with neither <c>HOME</c> nor <c>CLAUDE_CONFIG_DIR</c> there is nothing to name, and a RELATIVE path
    /// would probe the process's own folder. The adapter reads <c>""</c> as "no transcript" (→ a fresh start, the safe side); claude itself may fall back to the passwd home (assumed, not measured).
    /// <para>
    /// The slug is the working directory with every UTF-16 unit outside <c>[A-Za-z0-9]</c> replaced by <c>-</c> (the part-6 shell: <c>sed 's|[^A-Za-z0-9]|-|g'</c>); measured on this host,
    /// <c>/home/jinx/git/dew_flow_conventions</c> is <c>-home-jinx-git-dew-flow-conventions</c>. A slug longer than <see cref="SlugLimit"/> is cut to its first 200 and <c>-</c> plus the base-36 text of
    /// <c>abs(hash)</c> is appended, the hash being the 31-multiplier hash of the ORIGINAL path over its UTF-16 units — the rule READ from the claude 2.1.310 binary (its function <c>FH</c>), not measured with a real session.
    /// A character outside the basic plane is two units, so two dashes and two steps of the hash, as claude's JavaScript has it.
    /// </para></summary>
    public static string PathOf(string claudeHome, string workingDirectory, Guid session) =>
        claudeHome.Length == 0 ? string.Empty : Path.Combine(claudeHome, "projects", Slug(workingDirectory), session.ToString("D") + ".jsonl");

    /// <summary>The longest slug claude keeps whole.</summary>
    public const int SlugLimit = 200;

    private static string Slug(string workingDirectory)
    {
        var slug = new string([.. workingDirectory.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);
        return slug.Length <= SlugLimit ? slug : $"{slug[..SlugLimit]}-{Base36(Math.Abs((long)Hash(workingDirectory)))}";
    }

    /// <summary><c>h = (h &lt;&lt; 5) - h + charCode | 0</c> for every UTF-16 unit, wrapping at 32 bits as JavaScript's <c>|0</c> does.</summary>
    private static int Hash(string text) => text.Aggregate(0, (hash, c) => unchecked((hash << 5) - hash + c));

    private static string Base36(long value)
    {
        const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var text = new Stack<char>();
        do
        {
            text.Push(Digits[(int)(value % 36)]);
            value /= 36;
        }
        while (value > 0);

        return new string([.. text]);
    }

    private static string Value(IReadOnlyDictionary<string, string> environment, string name) => environment.TryGetValue(name, out var value) ? value : string.Empty;
}
