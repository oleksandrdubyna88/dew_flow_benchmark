using Bench.Domain.Gate;

namespace ModelArms.Core.Experiment;

/// <summary>The ONE rule that says a variable's NAME is a secret's (plan D19, ledger 2.2; E4.S0): the shared <see cref="CoaiEnvironment.IsSecretName"/> (<c>*_KEY</c>, <c>*_TOKEN</c>, <c>*_SECRET</c>, <c>*_PASSWORD</c>, the creds key) OR this module's own wider lists below. <see cref="Launch.TargetEnvironment"/> (the target's tools: the build, the suite, <c>npm</c>)
/// and <see cref="ArmChildEnvironment"/> (the arm's own claude session) both ask this predicate and hold no list of their own — the decision is written once. <see cref="CoaiEnvironment.IsSecretName"/> itself is a <c>Bench.Domain</c> rule with three consumers and is not widened here.
/// <para>
/// By NAME, whatever the case: a secret under an innocent name (<c>KEEP_ME=hunter2</c>, a bare <c>PASSWD</c> or <c>PASS</c>, a connection string called <c>DB</c>) passes, and so does what a restore or a build needs (<c>HTTP_PROXY</c>, <c>PIP_INDEX_URL</c>, <c>NuGetPackageSourceCredentials_*</c>) — every list of names has a residual, and the target's class doc states it.
/// </para></summary>
public static class SecretShaped
{
    /// <summary>The npm setting prefix: npm reads its settings from <c>npm_config_&lt;name&gt;</c>.</summary>
    public const string NpmConfig = "npm_config_";

    /// <summary>What marks an npm auth setting inside such a name (<c>npm_config__auth</c>, <c>npm_config__authToken</c>, <c>npm_config_//registry/:_authToken</c>).</summary>
    public const string NpmAuth = "_auth";

    /// <summary>Whole names, matched whatever their case, that are secrets by what they are for: a database client's password, a connection string that carries one, the file docker reads registry credentials from.</summary>
    public static IReadOnlyList<string> WholeNames { get; } = ["PGPASSWORD", "DATABASE_URL", "DOCKER_AUTH_CONFIG"];

    /// <summary>Endings, matched whatever the case, of a secret the shared suffix list does not end with (<c>_API_KEY</c> is <c>_KEY</c>'s already and is listed so the rule stands alone): a personal access token, an API key written without its underscore, credentials, an auth value, an auth token and an auth ident, the other spellings of a password (<c>_PWD</c>, <c>_PASSWD</c>, <c>_PASS</c>, <c>_PASSPHRASE</c>), the plural forms (<c>_SECRETS</c>, <c>_KEYS</c>) and a connection string —
    /// <c>NODE_AUTH_TOKEN</c> and <c>NPM_TOKEN</c> are <c>_TOKEN</c>'s already; <c>PWD</c> and <c>OLDPWD</c> have no underscore before the letters and stay. An <c>*_AUTH</c> TOGGLE (<c>DISABLE_AUTH</c>) is stripped too and behaves the same in the baseline and in every arm's checks.</summary>
    public static IReadOnlyList<string> Endings { get; } =
        ["_PAT", "_APIKEY", "_API_KEY", "_CREDENTIALS", "_CREDENTIAL", "_AUTH", "_AUTHTOKEN", "_AUTH_TOKEN", "_AUTH_IDENT", "_PWD", "_PASSWD", "_PASS", "_PASSPHRASE", "_SECRETS", "_KEYS", "_CONNECTION_STRING", "_CONNECTIONSTRING"];

    /// <summary>Whether <paramref name="name"/> is the shared rule's, one of <see cref="WholeNames"/>, ends with one of <see cref="Endings"/>, or is an <c>npm_config_*</c> that names an auth setting — whatever its case.</summary>
    public static bool IsSecretShaped(string name) =>
        CoaiEnvironment.IsSecretName(name)
        || WholeNames.Contains(name, StringComparer.OrdinalIgnoreCase)
        || Endings.Any(ending => name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
        || (name.StartsWith(NpmConfig, StringComparison.OrdinalIgnoreCase) && name.Contains(NpmAuth, StringComparison.OrdinalIgnoreCase));
}
