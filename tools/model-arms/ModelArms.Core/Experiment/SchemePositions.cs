namespace ModelArms.Core.Experiment;

/// <summary>One output a scheme's entry binds, with where it stands: the position of its entry, the producer (the entry's file name, <c>2-hA</c>; the split's or the split
/// fix's step id; <c>tasks</c> for the block's own outputs) and the binding itself.</summary>
public sealed record PlacedOutput(int Position, string Producer, OutputBinding Binding);

/// <summary>One step a scheme places: the position of its entry, the label a sentence names it by (the entry's file name, or the step id for a step of a tasks block) and its id.</summary>
public sealed record PlacedStep(int Position, string Label, StepId Id);

/// <summary>What a scheme's entries bind and where: the one answer the reader's destination rule, the brief rehearsal and the runner all ask, so no two of them fill by
/// two rules.</summary>
public static class SchemePositions
{
    /// <summary>Every step the scheme places, in scheme order; a tasks entry places its split, its split fix and its task coder, in that order.</summary>
    public static IReadOnlyList<PlacedStep> Steps(Scheme scheme, TaskLimits tasks) =>
        [.. scheme.Entries.SelectMany((entry, position) => StepsOf(entry, position, tasks))];

    /// <summary>Every output the scheme's entries bind, in scheme order: a step entry gives its step's outputs; a tasks entry gives the split's, then the split fix's, then
    /// the block's own — in that order, so the split fix may append to the split's notes.</summary>
    public static IReadOnlyList<PlacedOutput> Outputs(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        [.. scheme.Entries.SelectMany((entry, position) => OutputsOf(entry, position, steps, tasks))];

    /// <summary>The bindings a brief at <paramref name="position"/> for step <paramref name="id"/> may use: every output of an EARLIER position, the step's OWN outputs (the
    /// gate's template names its own status file) and, inside a tasks entry, the block's steps up to and including <paramref name="id"/> — never the block's own
    /// outputs, which are for the steps AFTER it.</summary>
    public static IReadOnlyList<OutputBinding> Visible(Scheme scheme, int position, StepId id, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
    [
        .. Outputs(scheme, steps, tasks).Where(placed => placed.Position < position).Select(placed => placed.Binding),
        .. Own(scheme.Entries.ElementAtOrDefault(position), id, steps, tasks),
    ];

    private static IEnumerable<PlacedStep> StepsOf(SchemeEntry entry, int position, TaskLimits tasks) =>
        entry switch
        {
            SchemeEntry.Step step => [new PlacedStep(position, step.Name, step.Id)],
            _ => [.. Block(tasks).Select(id => new PlacedStep(position, id.Value, id))],
        };

    private static IEnumerable<PlacedOutput> OutputsOf(SchemeEntry entry, int position, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        entry switch
        {
            SchemeEntry.Step step => Placed(position, step.Name, Declared(steps, step.Id)),
            _ =>
            [
                .. Placed(position, tasks.Split.Value, Declared(steps, tasks.Split)),
                .. Placed(position, tasks.SplitFix.Value, Declared(steps, tasks.SplitFix)),
                .. Placed(position, "tasks", tasks.Outputs),
            ],
        };

    private static IEnumerable<OutputBinding> Own(SchemeEntry? entry, StepId id, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        entry switch
        {
            SchemeEntry.Step => Declared(steps, id),
            SchemeEntry.Tasks => BlockUpTo(id, steps, tasks),
            _ => [],
        };

    /// <summary>The outputs of the block's steps up to and including <paramref name="id"/>: split before split fix before the task coder. An id that is none of the three has none.</summary>
    private static IEnumerable<OutputBinding> BlockUpTo(StepId id, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks)
    {
        var block = Block(tasks);
        var index = block.ToList().IndexOf(id);
        return index < 0 ? [] : block.Take(index + 1).SelectMany(member => Declared(steps, member));
    }

    private static IEnumerable<PlacedOutput> Placed(int position, string producer, IEnumerable<OutputBinding> outputs) =>
        outputs.Select(binding => new PlacedOutput(position, producer, binding));

    private static IReadOnlyList<OutputBinding> Declared(IReadOnlyDictionary<StepId, StepSpec> steps, StepId id) =>
        steps.TryGetValue(id, out var spec) ? spec.Outputs : [];

    /// <summary>The three steps a tasks entry runs, in the order they run.</summary>
    private static IReadOnlyList<StepId> Block(TaskLimits tasks) => [tasks.Split, tasks.SplitFix, tasks.TaskCode];
}
