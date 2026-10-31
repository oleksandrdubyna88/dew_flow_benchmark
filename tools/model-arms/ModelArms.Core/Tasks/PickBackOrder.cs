namespace ModelArms.Core.Tasks;

/// <summary>A commit and its patch-id; <paramref name="PatchId"/> is empty for a commit without a patch (an empty or a merge commit, which has none).</summary>
public sealed record PickSource(string Sha, string PatchId);

/// <summary>What the pick-back does with one source commit.</summary>
public abstract record PickStep
{
    private PickStep()
    {
    }

    /// <summary>Already on the arm branch; <paramref name="By"/> says how it is known (<c>the record</c> or <c>patch-id of &lt;sha&gt;</c>).</summary>
    public sealed record Carried(string Source, string Picked, string By) : PickStep;

    public sealed record Pick(string Source, string PatchId) : PickStep;
}

/// <summary>The steps, in the task's order, and the completions of the record that were not believed.</summary>
public sealed record PickPlan(IReadOnlyList<PickStep> Steps, IReadOnlyList<PickEntry.Picked> NotBelieved);

/// <summary>D22's skip, decided once before the first pick (E4.S2 spec item 8). A completion line is BELIEVED when its <c>picked</c> commit is one the arm branch holds since the task's <c>.before</c>; a source with a believed completion is carried by the record. The patch-id skip is NARROWED
/// to a source with no line at all: with the intent recorded, every commit this pipeline picked is explained by the record, so the only commit a patch-id can still match is one that reached the branch by another hand (a sibling task's identical change, the model's own commit) — the residual,
/// said in the module doc. Two identical commits inside one task are both picked: the second becomes an empty commit.</summary>
public static class PickBackOrder
{
    public static PickPlan Decide(IReadOnlyList<PickSource> sources, IReadOnlyList<PickEntry> record, IReadOnlyList<PickSource> onBranch)
    {
        var onBranchShas = onBranch.Select(commit => commit.Sha).ToHashSet(StringComparer.Ordinal);
        var completions = record.OfType<PickEntry.Picked>().ToList();
        var notBelieved = completions.Where(line => !onBranchShas.Contains(line.PickedSha)).ToList();
        var steps = sources.Select(source => StepFor(source, record, completions, onBranch, onBranchShas)).ToList();
        return new PickPlan(steps, notBelieved);
    }

    private static PickStep StepFor(PickSource source, IReadOnlyList<PickEntry> record, List<PickEntry.Picked> completions, IReadOnlyList<PickSource> onBranch, HashSet<string> onBranchShas)
    {
        var believed = completions.FirstOrDefault(line => line.Source == source.Sha && onBranchShas.Contains(line.PickedSha));
        if (believed is not null)
        {
            return new PickStep.Carried(source.Sha, believed.PickedSha, "the record");
        }

        var hasLine = record.Any(entry => entry is PickEntry.Intent intent ? intent.Source == source.Sha : ((PickEntry.Picked)entry).Source == source.Sha);
        if (!hasLine && source.PatchId.Length > 0 && onBranch.FirstOrDefault(commit => commit.PatchId == source.PatchId) is { } same)
        {
            return new PickStep.Carried(source.Sha, same.Sha, $"patch-id of {same.Sha}");
        }

        return new PickStep.Pick(source.Sha, source.PatchId);
    }
}
