namespace ModelArms.Adapters;

/// <summary>Test-only (internal; <c>Bench.Tests</c> sees it through <c>InternalsVisibleTo</c>; nothing in configuration reaches it). <see cref="LazyFetchSwitch"/> <c>false</c> leaves <c>GIT_NO_LAZY_FETCH</c> out of ArmGit's environment — the environment a git before 2.44 effectively sees, which ignores the variable — so a test observes the transport
/// allow-list hold ALONE. <see cref="Real"/> always sets it.</summary>
internal sealed record ArmGitSeams(bool LazyFetchSwitch)
{
    public static readonly ArmGitSeams Real = new(LazyFetchSwitch: true);
}
