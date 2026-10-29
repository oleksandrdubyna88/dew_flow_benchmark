using Bench.Infrastructure.Git;
using ModelArms.Core.Experiment;

namespace ModelArms.Adapters;

/// <summary>What git says about the target's source: whether it is a checkout, and whether it holds the plan branch, the base commit and the plan at that
/// commit. A question git cannot answer (an exit other than 0 and 1: a repository it refuses as unsafe, a timeout) is <c>false</c>, the same as a "no" —
/// the sentence that names the fact is the answer, not a guess about why.</summary>
internal sealed record SourceFacts(bool Exists, bool IsGit, bool PlanBranch, bool Base, bool PlanAtBase)
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    private static SourceFacts Absent { get; } = new(false, false, false, false, false);

    public static async Task<SourceFacts> ReadAsync(TargetFacts target, CancellationToken ct)
    {
        if (!Directory.Exists(target.Source))
        {
            return Absent;
        }

        var isGit = await GitCommand.RunAsync(target.Source, Budget, ct, "rev-parse", "--git-dir") is Bench.Domain.Outcome<string>.Ok;
        if (!isGit)
        {
            return Absent with { Exists = true };
        }

        var branch = await AskAsync(target.Source, "refs/heads/" + target.PlanBranch, ct);
        var baseExists = await AskAsync(target.Source, target.Base.Value + "^{commit}", ct);
        var plan = baseExists && await AskAsync(target.Source, target.Base.Value + ":" + target.Plan, ct);
        return new SourceFacts(true, true, branch, baseExists, plan);
    }

    /// <summary><c>rev-parse --verify --quiet</c> asks the question with exit 1 as the "no" (<c>cat-file -e</c> answers 128 for a missing object, which
    /// <see cref="GitCommand.AskAsync"/> would turn into a failure).</summary>
    private static async Task<bool> AskAsync(string source, string spec, CancellationToken ct) =>
        (await GitCommand.AskAsync(source, Budget, ct, "rev-parse", "--verify", "--quiet", spec)).Match(answer => answer, _ => false);
}
