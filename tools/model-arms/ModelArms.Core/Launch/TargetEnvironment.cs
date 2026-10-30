using Bench.Domain.Gate;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Launch;

/// <summary>The environment the TARGET's tools run under — the build, the suite, the extension's tests, <c>npm</c>, the formatter (plan D17 d, D26 b; E3 §8 item 8): the parent's variables minus everything a
/// code an arm WROTE must not read or be steered by. Stripped, whatever the case of the name: this repository's own (<c>BENCH_*</c>, <c>COAI_*</c>, <c>ConnectionStrings__*</c>); every secret-named variable
/// (<see cref="CoaiEnvironment.IsSecretName"/>) <b>with no <c>CLAUDE_*</c> / <c>ANTHROPIC_*</c> exception</b> — unlike <see cref="ArmChildEnvironment"/>, because the target's code has no login to keep and an arm wrote
/// that code; every <c>GIT_*</c> (a parent's <c>GIT_DIR</c> would point a test's own git at another repository); and the two scout flags. Nothing is added; <c>PATH</c>, <c>HOME</c>, <c>TMPDIR</c>,
/// <c>DOTNET_*</c> and <c>NUGET_*</c> pass as the parent's. The baseline and every arm's checks run under this one rule, so the two compare.
/// <para>
/// What this does not do: judge a variable whose NAME is innocent and whose VALUE is a secret (<c>KEEP_ME=hunter2</c>) — the rule is by name, as <see cref="CoaiEnvironment"/>'s is.
/// </para></summary>
public static class TargetEnvironment
{
    /// <summary>The child's variables: a new ordinal dictionary, the parent left as it was.</summary>
    public static IReadOnlyDictionary<string, string> Of(IReadOnlyDictionary<string, string> parent) =>
        new Dictionary<string, string>(parent.Where(variable => !Stripped(variable.Key)), StringComparer.Ordinal);

    private static bool Stripped(string name) =>
        ArmChildEnvironment.IsThisRepositorys(name) || CoaiEnvironment.IsSecretName(name) || IsGit(name) || ArmChildEnvironment.IsScoutFlag(name);

    private static bool IsGit(string name) => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase);
}
