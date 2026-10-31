using Bench.Cli;
using ModelArms.Adapters;
using ModelArms.Core.Experiment;

namespace ModelArms.Verbs;

/// <summary>What every verb that LAUNCHES a program asks first (plan D17 c, d; D26 b, c; E3 §3): the PATH the children inherit — an empty, relative or forbidden entry is refused — and the two folders git and claude read their configuration from, <c>HOME</c> and <c>XDG_CONFIG_HOME</c>, judged by one helper
/// (<see cref="ConfigFolders"/>); then each executable resolved, ONCE, to the REAL path the verb prints and launches. Extracted from <c>run-arm</c>'s admission, which now calls it, so the rule has one owner: <c>make</c> and <c>checks</c> call the same two methods, and <c>validate</c> — whose
/// <see cref="HostChecks"/> judge the same things for <c>prep-base</c> — reads the same entries. The judgement is by the environment map the verb was handed, never the process's.
/// <para>
/// The one launch admission itself makes is <c>git --version</c> of the resolved git (<see cref="FloorAsync"/>, D23, E4.S0), from the experiment root, under the hardened environment (<see cref="ArmGit"/>: pinned, sealed, no repository read), right after the resolution and before anything else — so a git below <see cref="GitFloor"/> runs no repository question at all. D26 (a)–(f) for the floor: (a) no path is judged here, the
/// resolution did; (b) the REAL path, <see cref="ArmGit"/>'s environment, the experiment root as cwd; (c) it follows the PATH and configuration-folder judgement; (d)–(f) as <c>validate</c>'s.
/// </para></summary>
internal static class LaunchAdmission
{
    /// <summary>The first refusal of what the children inherit: a PATH entry, then <c>HOME</c> or <c>XDG_CONFIG_HOME</c> (D26 c), else the empty text. The PATH is judged before any executable is looked up in it: git and claude look their own helpers up there for hours.</summary>
    public static string FirstRefusal(IReadOnlyDictionary<string, string> environment, IReadOnlyList<string> forbiddenRoots) =>
        ExecutableResolution.PathEntries(environment).Select(entry => ExecutableResolution.PathEntryRefusal(entry, forbiddenRoots, RootFacts.Host)).FirstOrDefault(sentence => sentence.Length > 0)
        ?? ConfigFolders.Refusals(environment, forbiddenRoots).Select(home => home.Refusal).FirstOrDefault()
        ?? string.Empty;

    /// <summary>The REAL path of <paramref name="name"/> on the map's PATH: found → the path; absent → exit 3 (the host lacks the program); refused → exit 4 (D17 names a wrong configuration). The sentence is prefixed <c>model-arms &lt;verb&gt;:</c>.</summary>
    public static Passage<string> Resolve(string verb, string name, IReadOnlyDictionary<string, string> environment, IReadOnlyList<string> forbiddenRoots) =>
        ExecutableResolution.Resolve(name, ExecutableResolution.PathEntries(environment), forbiddenRoots, RootFacts.Host, OperatingSystem.IsWindows()) switch
        {
            ExecutableAnswer.Found found => new Passage<string>.Through(found.Real),
            ExecutableAnswer.Absent absent => Passage<string>.Stop(ExitCodes.Environment, $"model-arms {verb}: {absent.Reason}"),
            ExecutableAnswer.Refused refused => Passage<string>.Stop(ExitCodes.Configuration, $"model-arms {verb}: {refused.Reason}"),
            _ => throw new InvalidOperationException("unreachable"),
        };

    /// <summary>D23, E4.S0: the version of the resolved <paramref name="git"/> read by <see cref="ArmGit.VersionAsync"/> from <paramref name="cwd"/> (the experiment root) and held against <see cref="GitFloor"/>. Below the floor, unparseable, or a git that does not answer is exit 3 prefixed <c>model-arms &lt;verb&gt;:</c>; nothing else has been launched.</summary>
    public static async Task<Passage<GitVersion>> FloorAsync(string verb, string git, IReadOnlyDictionary<string, string> environment, string cwd, CancellationToken ct) =>
        (await new ArmGit(git, cwd, environment).VersionAsync(ct)).Match(
            text => GitFloor.Admit(text).Match(
                version => new Passage<GitVersion>.Through(version),
                reason => Passage<GitVersion>.Stop(ExitCodes.Environment, $"model-arms {verb}: {reason}")),
            reason => Passage<GitVersion>.Stop(ExitCodes.Environment, $"model-arms {verb}: git --version at {git} could not be read: {reason}"));
}
