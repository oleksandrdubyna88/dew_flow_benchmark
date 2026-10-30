using Bench.Domain;
using ModelArms.Core.Steps;

namespace ModelArms.Adapters;

/// <summary>The one writer of a step's derived outputs (plan D21). STUB.</summary>
public static class DerivedOutputs
{
    public static Task<Outcome<IReadOnlyList<DerivedOutput>>> WriteAsync(StepContext context, IReadOnlyList<DerivedOutput> outputs, ArmFiles files, ArmGit git, CancellationToken ct) =>
        Task.FromResult(Outcome<IReadOnlyList<DerivedOutput>>.Success([]));
}
