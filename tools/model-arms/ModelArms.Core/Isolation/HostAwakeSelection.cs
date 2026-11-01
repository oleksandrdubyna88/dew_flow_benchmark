using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Isolation;

/// <summary>Which keep-awake adapter a host gets (D12).</summary>
public enum AwakeKind { Windows, Wsl, None }

/// <summary>The pure choice of the keep-awake adapter from the configured <see cref="AwakeChoice"/> and the host's kind: <c>auto</c> follows the host, a named adapter must match it, <c>none</c> is always allowed.
/// A named adapter on the wrong host fails with <see cref="HostChecks.AwakeProblem"/>'s sentence — the one <c>validate</c> prints, because <c>chain</c> does not run <c>validate</c>.</summary>
public static class HostAwakeSelection
{
    public static Outcome<AwakeKind> For(AwakeChoice choice, HostKind host) =>
        HostChecks.AwakeProblem(choice, host) is { Length: > 0 } problem
            ? Outcome<AwakeKind>.Failure(problem)
            : Outcome<AwakeKind>.Success((choice, host) switch
            {
                (AwakeChoice.None, _) => AwakeKind.None,
                (AwakeChoice.Windows, _) or (AwakeChoice.Auto, HostKind.Windows) => AwakeKind.Windows,
                (AwakeChoice.Wsl, _) or (AwakeChoice.Auto, HostKind.Wsl) => AwakeKind.Wsl,
                _ => AwakeKind.None,
            });
}
