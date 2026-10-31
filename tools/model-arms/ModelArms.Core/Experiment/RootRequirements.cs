using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>One file or folder of the experiment root a verb needs, and the verb that writes it. An empty path is the root folder itself.
/// <paramref name="MustExist"/> is false for the two things a verb creates: the root (<c>init</c>) and the baseline (<c>prep-base</c>),
/// which are refused when they are already there.</summary>
public sealed record RootRequirement(string RelativePath, bool MustExist, string Producer);

/// <summary>D18's table: which files of the root each verb needs, in the order they are checked, so a half-made root is refused naming the
/// first file its stage lacks and the verb that writes it. A pure table and a pure check over a predicate: the console asks the file system.
/// <para>
/// The names come from <see cref="RootPaths"/>, the one spelling of the root's layout, read once against a stand-in root; the two files
/// the operator names (<c>scenarios</c>, <c>baseline</c>) arrive as parameters.
/// </para></summary>
public static class RootRequirements
{
    private const string Init = "init";
    private const string PrepBase = "prep-base";
    private const string Make = "make";

    private static readonly RootPaths Layout = new(Path.GetFullPath(Path.DirectorySeparatorChar.ToString()));

    /// <summary>What every verb that reads an experiment reads: the root and its <c>experiment.json</c>.</summary>
    public static IReadOnlyList<RootRequirement> ToRead { get; } =
    [
        new(string.Empty, true, Init),
        new(Name(Layout.ExperimentFile), true, Init),
    ];

    /// <summary>The requirements of <paramref name="verb"/>. <paramref name="arms"/> are the arms a run verb was given: each must have been made.
    /// An unknown verb needs nothing: the command line refuses it before this is asked.</summary>
    public static IReadOnlyList<RootRequirement> For(string verb, IReadOnlyList<string> arms, string scenariosFile, string baselineFile) =>
        verb switch
        {
            Init => [new(string.Empty, false, Init)],
            "validate" => Inputs(scenariosFile),
            PrepBase => [.. Inputs(scenariosFile), new(baselineFile, false, PrepBase)],
            Make or "chain" => [.. Prepared(scenariosFile, baselineFile), new(Name(Layout.Base), true, PrepBase)],
            "run-arm" or "run-all" or "checks" => [.. Prepared(scenariosFile, baselineFile), .. Made(arms)],
            "status" or "lock" or "unlock" or "gate-lock" => ToRead,
            _ => [],
        };

    /// <summary>The first requirement not met, as a sentence naming the path and what to do; the empty string when every one is met.
    /// <paramref name="exists"/> answers for a full path (a file or a folder).</summary>
    public static string FirstUnmet(IReadOnlyList<RootRequirement> requirements, string root, Func<string, bool> exists)
    {
        var paths = new RootPaths(root);
        return requirements.Select(requirement => Unmet(requirement, root, paths, exists)).FirstOrDefault(sentence => sentence.Length > 0) ?? string.Empty;
    }

    private static IReadOnlyList<RootRequirement> Inputs(string scenariosFile) =>
        [.. ToRead, new(scenariosFile, true, Init)];

    private static IReadOnlyList<RootRequirement> Prepared(string scenariosFile, string baselineFile) =>
        [.. Inputs(scenariosFile), new(baselineFile, true, PrepBase)];

    private static IEnumerable<RootRequirement> Made(IReadOnlyList<string> arms) =>
        [new(Name(Layout.ArmMapFile), true, Make), .. arms.Select(arm => new RootRequirement(Name(Layout.Made(arm)), true, Make))];

    private static string Name(string full) => Path.GetRelativePath(Layout.Root, full).Replace('\\', '/');

    private static string Unmet(RootRequirement requirement, string root, RootPaths paths, Func<string, bool> exists)
    {
        var located = requirement.RelativePath.Length == 0 ? Outcome<string>.Success(root) : paths.Of(requirement.RelativePath);
        return located.Match(path => Verdict(requirement, root, path, exists(path)), reason => reason);
    }

    private static string Verdict(RootRequirement requirement, string root, string path, bool present) =>
        (requirement.MustExist, present, path == root) switch
        {
            (true, false, true) => $"{root} does not exist — `model-arms init {root}` creates it",
            (true, false, false) => $"{path} is missing — `model-arms {requirement.Producer}` writes it",
            (false, true, true) => $"{root} already exists — init creates a new root and never writes into an existing one",
            (false, true, false) => $"{path} already exists — {requirement.Producer} measures a baseline once; move it away to measure again",
            _ => string.Empty,
        };
}
