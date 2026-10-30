using ModelArms.Core.Experiment;

namespace ModelArms.Core.Launch;

/// <summary>The REAL paths of the executables a verb launches (plan D17 c, d): each is what <see cref="ExecutableResolution"/> answered for the command's first word, carried from the verb that resolved and printed it into
/// every launch — never a second look-up and never a bare name. <see cref="Node"/> is <c>lowPriority[0]</c>'s; an empty text is "not needed by this verb".</summary>
public sealed record ToolPaths(string Git, string Dotnet, string Node, string Build, string Suite, string Extension, string Npm)
{
    /// <summary>The paths of <paramref name="target"/>'s commands, each asked of <paramref name="realPathOf"/> (the verb's own resolutions: the name as configured → its real path, or the empty text when it did not resolve).
    /// <c>npm</c> is asked for only when the target has a <c>node_modules</c> folder to restore.</summary>
    public static ToolPaths Of(TargetFacts target, Func<string, string> realPathOf) =>
        new(
            realPathOf("git"),
            realPathOf("dotnet"),
            realPathOf(First(target.LowPriority)),
            realPathOf(First(target.Build)),
            realPathOf(First(target.Suite.Command)),
            realPathOf(First(target.Extension.Command)),
            target.NodeModules.Count > 0 ? realPathOf("npm") : string.Empty);

    /// <summary>The names of the paths that are empty although the target needs them: what a verb refuses before it launches anything.</summary>
    public IReadOnlyList<string> Missing(TargetFacts target) =>
        [.. new[] { ("git", Git), ("dotnet", Dotnet), ("target.lowPriority[0]", Node), ("target.build[0]", Build), ("target.suite.command[0]", Suite), ("target.extension.command[0]", Extension) }
            .Concat(target.NodeModules.Count > 0 ? [("npm", Npm)] : [])
            .Where(tool => tool.Item2.Length == 0)
            .Select(tool => tool.Item1)];

    private static string First(IReadOnlyList<string> command) => command.Count > 0 ? command[0] : string.Empty;
}

/// <summary>One launch of one of the target's tools, fully decided: what it is called in the logs, the real executable, its argument list (one element each — no shell string), the folder it starts in and the wall after which its
/// whole tree is killed.</summary>
public sealed record ToolCommand(string Label, string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory, TimeSpan Wall);

/// <summary>The walls of the target's tools (plan E3 §8 item 10): constants, not configuration — a new member of <c>experiment.json</c> would be a change to the schema nobody needs yet. A tool killed at its wall has no exit code, never a zero.</summary>
public static class TargetWalls
{
    public static TimeSpan Clone { get; } = TimeSpan.FromMinutes(30);

    public static TimeSpan Npm { get; } = TimeSpan.FromMinutes(30);

    public static TimeSpan Build { get; } = TimeSpan.FromMinutes(60);

    public static TimeSpan Suite { get; } = TimeSpan.FromMinutes(180);

    public static TimeSpan Format { get; } = TimeSpan.FromMinutes(30);

    public static TimeSpan Extension { get; } = TimeSpan.FromMinutes(60);

    public static TimeSpan Git { get; } = TimeSpan.FromSeconds(60);
}

/// <summary>The argument lists of the target's tools (C14; <c>prep-base.sh:12-17</c>, <c>objective-checks.sh:11-24</c>). A <i>lowered</i> command runs through the target's own wrapper — <c>lowPriority</c> is <c>[executable, script, …]</c> — as
/// <c>[the wrapper's words, the REAL path of the command's executable, the command's arguments]</c>: the wrapper is handed the resolved executable and not its name (E3 §8 item 11), so the path that was printed is the path launched,
/// and the real <c>node</c> runs the target's script because an arm can edit the script and not the interpreter. The wrapper's script, when relative, is made absolute against the clone as the shell did.</summary>
public static class TargetArgv
{
    private const string ResultsDirectory = "--results-directory";
    private const string CtrfFilename = "--report-xunit-ctrf-filename";

    public static ToolCommand Build(TargetFacts target, ToolPaths tools, string clone) =>
        Lowered("build", target, tools, target.Build, tools.Build, clone, clone, [], TargetWalls.Build);

    /// <summary>The suite with exactly the shell's two options appended: where its results go and the CTRF's file name.</summary>
    public static ToolCommand Suite(TargetFacts target, ToolPaths tools, string clone, string resultsDirectory) =>
        Lowered("suite", target, tools, target.Suite.Command, tools.Suite, clone, clone, [ResultsDirectory, resultsDirectory, CtrfFilename, target.Suite.CtrfFile], TargetWalls.Suite);

    public static ToolCommand Extension(TargetFacts target, ToolPaths tools, string clone) =>
        Lowered("extension", target, tools, target.Extension.Command, tools.Extension, clone, Path.Combine(clone, target.Extension.Directory), [], TargetWalls.Extension);

    /// <summary>One command per formatter verb, each over every changed file (each file its own argument), from the clone and NOT lowered (<c>objective-checks.sh:21-22</c>); none when nothing changed. A file name that starts with a dash is
    /// handed as <c>./name</c>, so no file an arm named can be read by the formatter as an option.</summary>
    public static IReadOnlyList<ToolCommand> Format(TargetFacts target, ToolPaths tools, string clone, IReadOnlyList<string> changedCs) =>
        changedCs.Count == 0
            ? []
            : [.. target.Format.Verbs.Select(verb => new ToolCommand(
                $"format {verb}",
                tools.Dotnet,
                ["format", target.Format.Solution, verb, "--verify-no-changes", "--no-restore", "--include", .. changedCs.Select(AsPath)],
                clone,
                TargetWalls.Format))];

    /// <summary>The <c>node_modules</c> folders under <c>rulesSubmodule</c>, restored BEFORE the build with <c>--ignore-scripts</c> (<c>prep-base.sh:12</c>): the submodule's own scripts are not run.</summary>
    public static IReadOnlyList<ToolCommand> RulesNpmCi(TargetFacts target, ToolPaths tools, string clone) => NpmCi(target, tools, clone, underRules: true);

    /// <summary>Every other <c>node_modules</c> folder, restored AFTER the build with its scripts (<c>prep-base.sh:15</c>).</summary>
    public static IReadOnlyList<ToolCommand> OtherNpmCi(TargetFacts target, ToolPaths tools, string clone) => NpmCi(target, tools, clone, underRules: false);

    private static ToolCommand Lowered(
        string label, TargetFacts target, ToolPaths tools, IReadOnlyList<string> command, string executable, string clone, string workingDirectory, IReadOnlyList<string> extra, TimeSpan wall) =>
        new(label, tools.Node, [.. Wrapper(target.LowPriority, clone), executable, .. command.Skip(1), .. extra], workingDirectory, wall);

    /// <summary><c>lowPriority[1..]</c>, its first word (the script) made absolute against the clone when it is relative.</summary>
    private static IEnumerable<string> Wrapper(IReadOnlyList<string> lowPriority, string clone) =>
        lowPriority.Skip(1).Select((word, index) => index == 0 && !Path.IsPathRooted(word) ? Path.Combine(clone, word) : word);

    private static IReadOnlyList<ToolCommand> NpmCi(TargetFacts target, ToolPaths tools, string clone, bool underRules) =>
        [.. target.NodeModules.Where(entry => IsUnder(entry, target.RulesSubmodule) == underRules).Select(entry => new ToolCommand(
            $"npm ci {Folder(entry)}".TrimEnd(),
            tools.Npm,
            ["ci", "--no-audit", "--no-fund", .. underRules ? new[] { "--ignore-scripts" } : []],
            Path.Combine(clone, Folder(entry)),
            TargetWalls.Npm))];

    /// <summary>Whether <paramref name="entry"/> is the rules submodule's folder or inside it, compared as forward-slash text.</summary>
    private static bool IsUnder(string entry, string rules)
    {
        var folder = Normal(entry);
        var root = Normal(rules);
        return folder == root || folder.StartsWith(root + "/", StringComparison.Ordinal);
    }

    /// <summary>The folder an entry lives in — <c>src_vs_code/node_modules</c> restores in <c>src_vs_code</c>; a bare <c>node_modules</c> in the clone itself.</summary>
    private static string Folder(string entry) => Normal(entry).LastIndexOf('/') is var cut and >= 0 ? Normal(entry)[..cut] : string.Empty;

    private static string Normal(string path) => path.Replace('\\', '/').Trim('/');

    private static string AsPath(string file) => file.StartsWith('-') ? "./" + file : file;
}
