using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>The folders git reads its GLOBAL config from — <c>HOME</c> and <c>XDG_CONFIG_HOME</c> — judged once, for every verb that starts a git or a claude (D26 c): <c>validate</c> and <c>run-arm</c> both ask here, so the rule has one
/// owner. A config can name a program, a file git appends its trace events to, or a <c>safe.directory</c>; a claude reads <c>&lt;HOME&gt;/.claude/settings.json</c>, whose hooks run programs — so a folder an arm could write to is refused like a PATH entry.
/// A variable the map does not hold is not refused.</summary>
internal static class ConfigFolders
{
    private static readonly string[] Variables = ["HOME", "XDG_CONFIG_HOME"];

    public static IReadOnlyList<(string Variable, string Refusal)> Refusals(IReadOnlyDictionary<string, string> environment, IReadOnlyList<string> forbiddenRoots) =>
        [.. Variables
            .Where(environment.ContainsKey)
            .Select(variable => (Variable: variable, Refusal: ExecutableResolution.ConfigFolderRefusal(variable, environment[variable], forbiddenRoots, RootFacts.Host)))
            .Where(home => home.Refusal.Length > 0)];
}
