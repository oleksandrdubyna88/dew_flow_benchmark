using Bench.Domain.Gate;
using Bench.Domain.Probes;

namespace ModelArms.Core.Experiment;

/// <summary>The environment an arm's claude runs under (plan D19): the parent's variables as the shell harness handed them on, minus this
/// repository's own, plus the arm's <c>TMPDIR</c>. An arm reproduces the OPERATOR's conditions, so nothing a measured subject would
/// get is added — no scout flag, no model-guard opt-out — and what claude needs to log in travels untouched.
/// <para>
/// Stripped, whatever the case of the name: <c>BENCH_*</c> and <c>ConnectionStrings__*</c>; every name
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
/// <b>Residuals.</b> <c>GOOGLE_APPLICATION_CREDENTIALS</c> (the <c>_CREDENTIALS</c> ending), <c>AWS_SECRET_ACCESS_KEY</c> and <c>AWS_SESSION_TOKEN</c> (<c>*_KEY</c>, <c>*_TOKEN</c>) are stripped — arms that log in through Vertex or Bedrock are not supported (D19, parent §9). <c>SSH_AUTH_SOCK</c> and every inherited <c>GIT_*</c> pass — not secret-shaped, the owner's open question (parent §9);
/// until it is answered, start the arms from a shell without an ssh agent. A secret under an innocent name passes, as in every rule by name.
/// </para></summary>
public static class ArmChildEnvironment
{
    public const string TmpDir = "TMPDIR";

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
        IsThisRepositorys(name) || IsSecretNotClaudes(name) || name.Equals(TmpDir, StringComparison.OrdinalIgnoreCase) || IsScoutFlag(name);

    /// <summary>This repository's own variables — <c>BENCH_*</c>, <c>COAI_*</c>, <c>ConnectionStrings__*</c> — which no child of the module inherits; <see cref="Launch.TargetEnvironment"/> asks the same question.</summary>
    internal static bool IsThisRepositorys(string name) =>
        CoaiEnvironment.IsHarnessVariable(name) || name.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase);

    /// <summary>The two scout flags, whatever their value (a measured subject must not be handed them).</summary>
    internal static bool IsScoutFlag(string name) => ScoutFlags.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsSecretNotClaudes(string name) =>
        SecretShaped.IsSecretShaped(name) && !ClaudesOwnPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
