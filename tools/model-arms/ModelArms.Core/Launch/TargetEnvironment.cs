using Bench.Domain.Gate;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Launch;

/// <summary>The environment the TARGET's tools run under — the build, the suite, the extension's tests, <c>npm</c>, the formatter (D17 d, D26 b; E3 §8 item 8): the parent's variables minus the NAMES this rule lists, and nothing more is claimed. Stripped, whatever the case of the name: this repository's own
/// (<c>BENCH_*</c>, <c>COAI_*</c>, <c>ConnectionStrings__*</c>); every secret-NAMED variable (<see cref="CoaiEnvironment.IsSecretName"/>: <c>*_KEY</c>, <c>*_TOKEN</c>, <c>*_SECRET</c>, <c>*_PASSWORD</c>, the creds key) with no <c>CLAUDE_*</c> / <c>ANTHROPIC_*</c> exception, unlike <see cref="ArmChildEnvironment"/>, because the target's code has
/// no login to keep and an arm wrote that code; every <c>GIT_*</c>; the two scout flags; and <see cref="AgentSocket"/> — the agent's socket is a capability (a planted test could sign or push as the operator through it, passphrase and hardware key notwithstanding) and no target tool needs it: the base and the rules
/// submodule come from a path. Nothing is added; <c>PATH</c>, <c>HOME</c>, <c>TMPDIR</c>, <c>DOTNET_*</c> and <c>NUGET_*</c> pass. The baseline and every arm's checks run under this one rule, so the two compare.
/// <para>
/// <b>What this does NOT do, and why.</b> <c>HOME</c> passes, so the operator's files under it (<c>~/.ssh</c>, <c>~/.claude/.credentials.json</c>, <c>~/.npmrc</c>, <c>~/.config/gh</c>, <c>~/.nuget</c>) are readable by the target's code, which runs with the operator's rights by design (E3 §8 item 17); a scratch <c>HOME</c> would lose the NuGet and npm caches,
/// dotnet's first-run state and the operator's <c>NuGet.Config</c> feeds, and is the isolation boundary item 17 leaves to a later epic. The rule is by NAME, as <see cref="CoaiEnvironment"/>'s is: a secret whose name the suffix list does not end with (<c>PGPASSWORD</c>, <c>*_PAT</c>, <c>*_APIKEY</c>, <c>*_CREDENTIALS</c>, <c>DATABASE_URL</c>,
/// <c>npm_config__authToken</c>) passes, as does one under an innocent name (<c>KEEP_ME=hunter2</c>) and a <c>CLAUDE_CODE_*</c> steering variable that is not secret-named; widening <c>IsSecretName</c> is a <c>Bench.Domain</c> change with three consumers — its own task, not this one.
/// </para></summary>
public static class TargetEnvironment
{
    /// <summary>The ssh agent's socket: a capability, stripped.</summary>
    public const string AgentSocket = "SSH_AUTH_SOCK";

    /// <summary>The child's variables: a new ordinal dictionary, the parent left as it was.</summary>
    public static IReadOnlyDictionary<string, string> Of(IReadOnlyDictionary<string, string> parent) =>
        new Dictionary<string, string>(parent.Where(variable => !Stripped(variable.Key)), StringComparer.Ordinal);

    private static bool Stripped(string name) =>
        ArmChildEnvironment.IsThisRepositorys(name) || CoaiEnvironment.IsSecretName(name) || IsGit(name) || ArmChildEnvironment.IsScoutFlag(name) || name.Equals(AgentSocket, StringComparison.OrdinalIgnoreCase);

    private static bool IsGit(string name) => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase);
}
