namespace Bench.Domain.Sessions;

/// <summary>What a SHELL command does — the half of the taxonomy a tool name cannot answer.
/// <para>
/// <c>Bash</c> covers <c>git status</c> and <c>rm -rf</c> equally, so a classifier keyed on tool names
/// alone produces a phase split that is confident and meaningless. Two instruments settle it, and both
/// are kept: this allowlist, and the working tree itself
/// (<see cref="MutationEvidence"/>) where the check ran.
/// </para>
/// <para>
/// <b>An unrecognised command is a WRITE.</b> Not because it probably is, but because the two errors are
/// not symmetrical: a write mistaken for a read silently inflates the research total and hides an edit
/// from every loop detector, while a read mistaken for a write costs one over-counted execution call that
/// the tree check then contradicts in the open. The allowlist grows from measurement — the sessions report
/// names every command that landed here as a write and changed nothing, which is exactly the candidate
/// list — and never from a guess about what looks harmless.
/// </para></summary>
public static class CommandClassifier
{
    /// <summary>Commands that observe and change nothing. Matched on the command's leading words, so
    /// <c>git status --porcelain</c> matches <c>git status</c> and <c>git push</c> matches nothing here.</summary>
    private static readonly string[] ReadOnly =
    [
        "git status", "git log", "git diff", "git show", "git branch", "git rev-parse", "git ls-files",
        "git blame", "git describe", "git remote -v", "git config --get", "git shortlog", "git cat-file",
        "ls", "dir", "pwd", "cd", "cat", "type", "head", "tail", "wc", "stat", "tree", "du", "df",
        "grep", "rg", "find", "findstr", "select-string", "get-content", "get-childitem",
        "echo", "which", "where", "whoami", "date", "hostname", "printenv", "env",
        "dotnet --info", "dotnet --version", "dotnet --list-sdks", "dotnet --list-runtimes",
        "node --version", "npm --version", "python --version", "cargo --version", "rustc --version",
        "docker ps", "docker images", "kubectl get", "gh pr view", "gh issue view", "gh repo view",
    ];

    /// <summary>Commands that build or test. Their own class rather than shell commands like any other:
    /// the compile-failure count this system reports has nowhere else to come from, and a build folded in
    /// among the edits makes the verification loop invisible.</summary>
    private static readonly string[] Verifying =
    [
        "dotnet build", "dotnet test", "dotnet run", "dotnet format", "dotnet publish", "msbuild",
        "cargo build", "cargo test", "cargo check", "cargo clippy",
        "npm test", "npm run build", "npm run test", "npm run lint", "yarn test", "yarn build",
        "pnpm test", "pnpm build", "pytest", "python -m pytest", "tox",
        "go build", "go test", "go vet", "make", "cmake --build", "ctest",
        "gradle build", "mvn test", "mvn package", "swift build", "swift test",
    ];

    /// <summary>Output shapes that mean a build or a test run FAILED.
    /// <para>
    /// Read only from calls that were already classified as verification, so an ordinary file containing
    /// the word "FAILED" cannot manufacture a compile failure out of a <c>cat</c>.
    /// </para></summary>
    private static readonly string[] FailureShapes =
    [
        "error CS", "error MSB", "Build FAILED", "error TS", "error[E", "error:",
        "Test Run Failed", "Tests failed", "FAILED", "AssertionError",
        "Traceback (most recent call last)", "SyntaxError", "cannot find symbol",
    ];

    /// <summary>Where one command line stops being one command. Split rather than matched whole, because
    /// <c>cd src &amp;&amp; dotnet build</c> is a build however it starts, and matching the leading words
    /// alone would file it under <c>cd</c>.</summary>
    private static readonly string[] Separators = ["&&", "||", ";", "|", "\n"];

    /// <summary>The strongest class any segment of the line carries. Write beats verify beats read, so a
    /// line that edits a file and then builds is an edit — the phase a call belongs to is decided by the
    /// most consequential thing it did, never by the last.</summary>
    public static ToolClass Classify(string command)
    {
        var segments = Segments(command);

        if (segments.Count == 0)
        {
            return ToolClass.Unknown;
        }

        if (segments.Any(s => !IsReadOnly(s) && !IsVerifying(s)))
        {
            return ToolClass.Write;
        }

        return segments.Any(IsVerifying) ? ToolClass.Verify : ToolClass.Read;
    }

    /// <summary>Whether a verification call's output says it failed.
    /// <para>
    /// Gated on the class rather than applied to every response: the shapes below are common English, and
    /// a grep whose hits contain "error:" is not a compile failure. Not captured means not counted — an
    /// absent response is never read as a pass.
    /// </para></summary>
    public static bool Failed(ToolClass toolClass, bool responseCaptured, string response) =>
        toolClass == ToolClass.Verify
        && responseCaptured
        && FailureShapes.Any(shape => response.Contains(shape, StringComparison.Ordinal));

    private static List<string> Segments(string command) =>
        [.. command
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalise)
            .Where(s => s.Length > 0)];

    /// <summary>One segment, reduced to the form the tables are written in: lower case, single spaces, and
    /// without a leading environment assignment or path prefix.</summary>
    private static string Normalise(string segment) =>
        string.Join(' ', segment.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private static bool IsReadOnly(string segment) => StartsWithAny(segment, ReadOnly);

    private static bool IsVerifying(string segment) => StartsWithAny(segment, Verifying);

    /// <summary>Matched on a word boundary, so <c>catalog-build.sh</c> is not <c>cat</c> and
    /// <c>lsof</c> is not <c>ls</c>. A prefix match without this is how an allowlist quietly admits
    /// everything that happens to share three letters with something safe.</summary>
    private static bool StartsWithAny(string segment, string[] table) =>
        table.Any(entry =>
            segment.StartsWith(entry, StringComparison.Ordinal)
            && (segment.Length == entry.Length || segment[entry.Length] is ' ' or '\t'));
}
