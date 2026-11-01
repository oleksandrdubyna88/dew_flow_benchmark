using Bench.Domain.Gate;
using Bench.Domain.Probes;

namespace ModelArms.Core.Experiment;

/// <summary>The environment an arm's claude runs under (plan D19): the parent's variables as the shell harness handed them on, minus this
/// repository's own, plus the arm's <c>TMPDIR</c>. An arm reproduces the OPERATOR's conditions, so nothing a measured subject would
/// get is added — no scout flag, no model-guard opt-out — and what claude needs to log in travels untouched.
/// <para>
/// Stripped, whatever the case of the name: <c>BENCH_*</c> and <c>ConnectionStrings__*</c>; the ssh agent's socket <c>SSH_AUTH_SOCK</c> (a capability — a signature or a push as the operator) and every <c>GIT_*</c> (steering — a <c>GIT_DIR</c>, <c>GIT_CONFIG_*</c> or <c>GIT_SSH_COMMAND</c> would redirect git or name a program), through <see cref="IsAgentSocketOrGit"/>, the one rule the target's tools and the harness's git ask as well (E6.S4 a, D19 ledger 3.2) — neither is a secret NAME, so
/// the <c>CLAUDE_*</c> / <c>ANTHROPIC_*</c> exception below is NOT theirs (<c>GIT_CLAUDE_X</c> is stripped, <c>CLAUDE_GIT_X</c> kept); every name
/// <see cref="CoaiEnvironment.IsHarnessVariable"/> accepts; every name <see cref="SecretShaped.IsSecretShaped"/> accepts — the shared <see cref="CoaiEnvironment.IsSecretName"/> and the module's wider lists, the same predicate the target's tools are held to (E4.S0, D19 ledger 2.2: <c>PGPASSWORD</c>, <c>DATABASE_URL</c>, <c>*_PAT</c>, <c>*_CREDENTIALS</c>, npm auth tokens) —
/// <b>unless</b> it starts <c>CLAUDE_</c> or <c>ANTHROPIC_</c> — claude's own login is <c>CLAUDE_CODE_OAUTH_TOKEN</c> or
/// <c>ANTHROPIC_API_KEY</c>, secret-named both, and removing them would launch arms that cannot authenticate (E1 §8 item 1); the exception covers names only the union catches too (<c>CLAUDE_X_PAT</c>, <c>ANTHROPIC_CREDENTIALS</c>): the module cannot tell a login name from a secret one, by D19's design; and
/// <see cref="ProbeChildEnvironment.ModelGuardVariable"/> and <see cref="ProbeChildEnvironment.BackgroundTasksVariable"/> whatever their
/// value, because the arms run under the operator's hooks and settings exactly as part 6 measured them.
/// </para>
/// <para>
/// <c>TMPDIR</c> is the ARM's <see cref="ArmPaths.Tmp"/>, one per arm and shared by the parallel tasks of a batch (D19: part 6 ran them on
/// the per-arm folder without a collision, so a per-task folder would be a condition no run measured); any spelling of it in the parent
/// is replaced. It would otherwise be <c>/tmp/claude-&lt;uid&gt;</c>, which shows every arm's name.
/// </para>
/// <para>
/// <b>Residuals.</b> <c>GOOGLE_APPLICATION_CREDENTIALS</c> (the <c>_CREDENTIALS</c> ending), <c>AWS_SECRET_ACCESS_KEY</c> and <c>AWS_SESSION_TOKEN</c> (<c>*_KEY</c>, <c>*_TOKEN</c>) are stripped — arms that log in through Vertex or Bedrock are not supported (D19, parent §9). A secret under an innocent name passes, as in every rule by name.
/// The agent and git strip (E6.S4 a, ledger 3.2) is NOT a boundary: the session is not HANDED the socket's address or the ambient git variables, nothing more. The model runs as the operator's uid, so a same-uid process can still find the socket — the parent runner holds <c>SSH_AUTH_SOCK</c> (<c>run-all</c> starts it with the full environment) and <c>/proc/$PPID/environ</c> is readable, as are <c>/tmp/ssh-*/agent.*</c> and <c>$XDG_RUNTIME_DIR</c> — and the key FILES: <c>~/.ssh/id_*</c>, <c>~/.ssh/config</c>, <c>~/.gitconfig</c> and <c>~/.gnupg</c> under <c>HOME</c> stay readable, which the owner's trust model leaves open. Also NOT stripped, by design:
/// <c>SSH_AGENT_PID</c>, <c>SSH_ASKPASS</c>, <c>SSH_ASKPASS_REQUIRE</c>, <c>GPG_AGENT_INFO</c>, <c>GPG_TTY</c>, <c>GNUPGHOME</c>, <c>EMAIL</c> (git's identity fallback), <c>PAGER</c>, <c>EDITOR</c> and <c>VISUAL</c> (claude's git calls are non-interactive), <c>XDG_CONFIG_HOME</c> (D26 (c) refuses one under a forbidden root); widening the list is a new ledger entry. The rule is case-insensitive, so a lower-case <c>git_dir</c> or
/// <c>ssh_auth_sock</c> is dropped too: on Linux nothing reads it, so the measured subject loses nothing it could notice, and on Windows it IS <c>GIT_DIR</c> to git. A name that only contains <c>GIT</c> (<c>GITHUB_ACTIONS</c>, <c>MY_GIT_DIR</c>) or resembles the socket (<c>SSH_AUTH_SOCKET</c>) is kept. Runs after E6.S4 differ from part 6's measured conditions, which ran with both.
/// </para></summary>
public static class ArmChildEnvironment
{
    public const string TmpDir = "TMPDIR";

    /// <summary>The ssh agent's socket: a capability, stripped from every child of the module — the one spelling of the name, asked through <see cref="IsAgentSocketOrGit"/>.</summary>
    public const string AgentSocket = "SSH_AUTH_SOCK";

    /// <summary>The two scout flags: the one list, asked by <see cref="Stripped"/> here and by <see cref="IsScoutFlag"/> for the target's tools.</summary>
    private static readonly string[] ScoutFlags = [ProbeChildEnvironment.ModelGuardVariable, ProbeChildEnvironment.BackgroundTasksVariable];

    private static readonly string[] ClaudesOwnPrefixes = ["CLAUDE_", "ANTHROPIC_"];

    /// <summary>The child's variables: a new ordinal dictionary, the parent left as it was.</summary>
    public static IReadOnlyDictionary<string, string> Of(IReadOnlyDictionary<string, string> parent, ArmPaths paths) =>
        new Dictionary<string, string>(parent.Where(variable => !Stripped(variable.Key)), StringComparer.Ordinal)
        {
            [TmpDir] = paths.Tmp,
        };

    private static bool Stripped(string name) =>
        IsThisRepositorys(name) || IsSecretNotClaudes(name) || IsAgentSocketOrGit(name) || name.Equals(TmpDir, StringComparison.OrdinalIgnoreCase) || IsScoutFlag(name);

    /// <summary>This repository's own variables — <c>BENCH_*</c>, <c>COAI_*</c>, <c>ConnectionStrings__*</c> — which no child of the module inherits; <see cref="Launch.TargetEnvironment"/> asks the same question.</summary>
    internal static bool IsThisRepositorys(string name) =>
        CoaiEnvironment.IsHarnessVariable(name) || name.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase);

    /// <summary>The two scout flags, whatever their value (a measured subject must not be handed them).</summary>
    internal static bool IsScoutFlag(string name) => ScoutFlags.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>The ssh agent's socket (whole name) and every <c>GIT_*</c> (prefix), whatever the case (ledger 3.2, E6.S4): a capability and steering, neither a secret NAME. The ONE rule — <see cref="Stripped"/> here, <see cref="Launch.TargetEnvironment"/> and <see cref="GitEnvironment"/> ask it —
    /// and asked OUTSIDE <c>IsSecretNotClaudes</c>'s <c>CLAUDE_*</c> / <c>ANTHROPIC_*</c> exception, which is the secret rule's alone: <c>GIT_CLAUDE_X</c> is stripped, <c>CLAUDE_GIT_X</c> is not a <c>GIT_</c> name and stays.</summary>
    internal static bool IsAgentSocketOrGit(string name) =>
        name.Equals(AgentSocket, StringComparison.OrdinalIgnoreCase) || name.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase);

    private static bool IsSecretNotClaudes(string name) =>
        SecretShaped.IsSecretShaped(name) && !ClaudesOwnPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
