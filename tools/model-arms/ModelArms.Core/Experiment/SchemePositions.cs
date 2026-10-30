namespace ModelArms.Core.Experiment;

/// <summary>One output a scheme's entry binds, with where it stands: the position of its entry, the producer (the entry's file name, <c>2-hA</c>; the split's or the split
/// fix's step id; <c>tasks</c> for the block's own outputs) and the binding itself.</summary>
public sealed record PlacedOutput(int Position, string Producer, OutputBinding Binding);

/// <summary>What a scheme's entries bind and where: the one answer the reader's destination rule, the brief rehearsal and the runner all ask, so no two of them fill by
/// two rules.</summary>
public static class SchemePositions
{
    /// <summary>Every output the scheme's entries bind, in scheme order: a step entry gives its step's outputs; a tasks entry gives the split's, then the split fix's, then
    /// the block's own — in that order, so the split fix may append to the split's notes.</summary>
    public static IReadOnlyList<PlacedOutput> Outputs(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) => [];

    /// <summary>The bindings a brief at <paramref name="position"/> for step <paramref name="id"/> may use: every output of an EARLIER position, the step's OWN outputs (the
    /// gate's template names its own status file) and, inside a tasks entry, the block's steps up to and including <paramref name="id"/> — never the block's own
    /// outputs, which are for the steps AFTER it.</summary>
    public static IReadOnlyList<OutputBinding> Visible(Scheme scheme, int position, StepId id, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) => [];
}
