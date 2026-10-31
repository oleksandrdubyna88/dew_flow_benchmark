using Bench.Domain.Gate;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Launch;

/// <summary>The environment the TARGET's tools run under — the build, the suite, the extension's tests, <c>npm</c>, the formatter (D17 d, D26 b; E3 §8 item 8): the parent's variables minus the NAMES this rule lists, and nothing more is claimed. Stripped, whatever the case of the name: this repository's own
/// (<c>BENCH_*</c>, <c>COAI_*</c>, <c>ConnectionStrings__*</c>); every secret-NAMED variable (<see cref="CoaiEnvironment.IsSecretName"/>: <c>*_KEY</c>, <c>*_TOKEN</c>, <c>*_SECRET</c>, <c>*_PASSWORD</c>, the creds key) with no <c>CLAUDE_*</c> / <c>ANTHROPIC_*</c> exception, unlike <see cref="ArmChildEnvironment"/>, because the target's code has
/// no login to keep and an arm wrote that code; and, by this class's OWN list (the shared one is not widened, below), <c>PGPASSWORD</c>, <c>DATABASE_URL</c>, <c>*_PAT</c>, <c>*_APIKEY</c>, <c>*_API_KEY</c>, <c>*_CREDENTIALS</c>, <c>*_CREDENTIAL</c>, <c>*_AUTH</c>, <c>*_AUTHTOKEN</c>, <c>*_AUTH_TOKEN</c>, <c>*_AUTH_IDENT</c>, <c>*_PWD</c>, <c>*_PASSWD</c>, <c>*_PASS</c>, <c>*_PASSPHRASE</c>, <c>*_SECRETS</c>, <c>*_KEYS</c>, <c>*_CONNECTION_STRING</c>, <c>*_CONNECTIONSTRING</c>, <c>DOCKER_AUTH_CONFIG</c> and every <c>npm_config_*</c> whose name holds <c>_auth</c>
/// (<c>npm_config__auth</c>, <c>npm_config__authToken</c>, <c>npm_config_//registry/:_authToken</c>; <c>NODE_AUTH_TOKEN</c> and <c>NPM_TOKEN</c> were stripped already, by <c>*_TOKEN</c>); every <c>GIT_*</c>; the two scout flags; and <see cref="AgentSocket"/> — the agent's socket is a capability (a planted test could sign or push as the operator through it, passphrase and hardware key notwithstanding) and no target tool needs it: the base and the rules
/// submodule come from a path. Nothing is added; <c>PATH</c>, <c>HOME</c>, <c>TMPDIR</c>, <c>DOTNET_*</c> and <c>NUGET_*</c> pass. The baseline and every arm's checks run under this one rule, so the two compare.
/// <para>
/// <b>What this does NOT do, and why.</b> <c>HOME</c> passes, so the operator's files under it (<c>~/.ssh</c>, <c>~/.claude/.credentials.json</c>, <c>~/.npmrc</c>, <c>~/.config/gh</c>, <c>~/.nuget</c>) are readable by the target's code, which runs with the operator's rights by design (E3 §8 item 17); a scratch <c>HOME</c> would lose the NuGet and npm caches,
/// dotnet's first-run state and the operator's <c>NuGet.Config</c> feeds, and is the isolation boundary item 17 leaves to a later epic. The rule is by NAME, as <see cref="CoaiEnvironment"/>'s is: a secret under an innocent name (<c>KEEP_ME=hunter2</c>, a bare <c>PASSWD</c> or <c>PASS</c>, a connection string called <c>DB</c>), a secret whose name neither list ends with, a <c>CLAUDE_CODE_*</c> steering variable that is not secret-named, and what a restore or a build NEEDS all pass: a proxy or an index url that may carry userinfo (<c>HTTP_PROXY</c>, <c>HTTPS_PROXY</c>, <c>PIP_INDEX_URL</c>), <c>NuGetPackageSourceCredentials_*</c> and <c>VSS_NUGET_EXTERNAL_FEED_ENDPOINTS</c> (E3 §8 item 8's policy: stripping them fails every restore behind a proxy or a private feed, in the baseline and in every arm alike) — every list of names has a residual, and this is it. <b>The arm's own claude session is not covered by this rule</b> — <see cref="ArmChildEnvironment"/> still uses the shared <c>IsSecretName</c> alone, as the parent's D19 defines it, so the model under test sees <c>PGPASSWORD</c>, <c>DATABASE_URL</c> and the rest of this list when the operator's shell holds them (E3 §8 item 50: the owner's decision, a change to D19);
/// <see cref="CoaiEnvironment.IsSecretName"/> is not widened here because it is a <c>Bench.Domain</c> change with three consumers — its own task, not this one.
/// </para></summary>
public static class TargetEnvironment
{
    /// <summary>The ssh agent's socket: a capability, stripped.</summary>
    public const string AgentSocket = "SSH_AUTH_SOCK";

    /// <summary>The child's variables: a new ordinal dictionary, the parent left as it was.</summary>
    public static IReadOnlyDictionary<string, string> Of(IReadOnlyDictionary<string, string> parent) =>
        new Dictionary<string, string>(parent.Where(variable => !Stripped(variable.Key)), StringComparer.Ordinal);

    /// <summary>Whole names, matched whatever their case, that are secrets by what they are for: a database client's password, a connection string that carries one, the file docker reads registry credentials from.</summary>
    private static readonly string[] SecretNames = ["PGPASSWORD", "DATABASE_URL", "DOCKER_AUTH_CONFIG"];

    /// <summary>Endings, matched whatever the case, of a secret the shared suffix list does not end with (<c>_API_KEY</c> is <c>_KEY</c>'s already and is listed so this rule stands alone): a personal access token, an API key written without its underscore, credentials, an auth value, an auth token and an auth ident, the other spellings of a password (<c>_PWD</c>, <c>_PASSWD</c>, <c>_PASS</c>, <c>_PASSPHRASE</c>), the plural forms (<c>_SECRETS</c>, <c>_KEYS</c>) and a connection string — <c>NODE_AUTH_TOKEN</c> and <c>NPM_TOKEN</c> are <c>_TOKEN</c>'s already; <c>PWD</c> and <c>OLDPWD</c> have no underscore before the letters and stay. An <c>*_AUTH</c> TOGGLE (<c>DISABLE_AUTH</c>) is stripped too and behaves the same in the baseline and in every arm's checks.</summary>
    private static readonly string[] SecretSuffixes = ["_PAT", "_APIKEY", "_API_KEY", "_CREDENTIALS", "_CREDENTIAL", "_AUTH", "_AUTHTOKEN", "_AUTH_TOKEN", "_AUTH_IDENT", "_PWD", "_PASSWD", "_PASS", "_PASSPHRASE", "_SECRETS", "_KEYS", "_CONNECTION_STRING", "_CONNECTIONSTRING"];

    /// <summary>npm reads its settings from <c>npm_config_&lt;name&gt;</c>, and an auth setting is spelled with <c>_auth</c> in it (<c>npm_config__auth</c>, <c>npm_config__authToken</c>, <c>npm_config_//registry/:_authToken</c>).</summary>
    private const string NpmConfig = "npm_config_";

    private const string NpmAuth = "_auth";

    private static bool Stripped(string name) =>
        ArmChildEnvironment.IsThisRepositorys(name) || CoaiEnvironment.IsSecretName(name) || IsSecretShaped(name) || IsGit(name) || ArmChildEnvironment.IsScoutFlag(name) || name.Equals(AgentSocket, StringComparison.OrdinalIgnoreCase);

    /// <summary>This rule's own list, beside <see cref="CoaiEnvironment.IsSecretName"/>, which is a <c>Bench.Domain</c> rule with three consumers and is not widened here: the names above, the endings above, and any <c>npm_config_*</c> that names an auth setting.</summary>
    private static bool IsSecretShaped(string name) =>
        SecretNames.Contains(name, StringComparer.OrdinalIgnoreCase)
        || SecretSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        || (name.StartsWith(NpmConfig, StringComparison.OrdinalIgnoreCase) && name.Contains(NpmAuth, StringComparison.OrdinalIgnoreCase));

    private static bool IsGit(string name) => name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase);
}
