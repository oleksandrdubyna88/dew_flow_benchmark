namespace ModelArms.Core.Tasks;

/// <summary>A commit and its patch-id; <paramref name="PatchId"/> is empty for a commit without a patch (an empty or a merge commit, which has none).</summary>
public sealed record PickSource(string Sha, string PatchId);

/// <summary>What the pick-back does with one source commit.</summary>
public abstract record PickStep
{
    private PickStep()
    {
    }

    /// <summary>Already on the arm branch; <paramref name="By"/> says how it is known — the record's believed completion (<c>the record</c>) is the only way.</summary>
    public sealed record Carried(string Source, string Picked, string By) : PickStep;

    public sealed record Pick(string Source, string PatchId) : PickStep;
}

/// <summary>The steps, in the task's order, and the completions of the record that were not believed.</summary>
public sealed record PickPlan(IReadOnlyList<PickStep> Steps, IReadOnlyList<PickEntry.Picked> NotBelieved);

/// <summary>D22's skip, decided once before the first pick (E4.S2 spec item 8, as changed by E6.S4 b — ledger 3.3). A completion line is BELIEVED when its <c>picked</c> commit is one the arm branch holds since the task's <c>.before</c>; a source with a believed completion is carried by the record, and the record ALONE carries: every other source is picked, whatever its patch-id.
/// The patch-id skip is gone — it carried a source with no line at all whose change the branch already held, a sibling task's identical change or the model's own commit, WITHOUT a commit of its own; now such a change lands as one empty commit with the task's author and message, which git keeps (<c>--keep-redundant-commits</c>). Two identical commits inside one task are both picked: the second becomes an empty commit too.</summary>
public static class PickBackOrder
{
    /// <param name="sources">The task's commits in order, each with its patch-id (which the completion line records, never matches on).</param>
    /// <param name="record">The pick record's valid lines.</param>
    /// <param name="onBranch">The shas of <c>rev-list &lt;before&gt;..HEAD</c> — the commits the arm branch holds since the task's <c>.before</c>.</param>
    public static PickPlan Decide(IReadOnlyList<PickSource> sources, IReadOnlyList<PickEntry> record, IReadOnlyCollection<string> onBranch)
    {
        var onBranchShas = onBranch.ToHashSet(StringComparer.Ordinal);
        var completions = record.OfType<PickEntry.Picked>().ToList();
        var notBelieved = completions.Where(line => !onBranchShas.Contains(line.PickedSha)).ToList();
        var steps = sources.Select(source => StepFor(source, completions, onBranchShas)).ToList();
        return new PickPlan(steps, notBelieved);
    }

    private static PickStep StepFor(PickSource source, List<PickEntry.Picked> completions, HashSet<string> onBranchShas) =>
        completions.FirstOrDefault(line => line.Source == source.Sha && onBranchShas.Contains(line.PickedSha)) is { } believed
            ? new PickStep.Carried(source.Sha, believed.PickedSha, "the record")
            : new PickStep.Pick(source.Sha, source.PatchId);
}
