namespace ModelArms.Core.Experiment;

/// <summary>The rules a scheme answers for, each over <see cref="SchemePositions"/> so the reader, the rehearsal and the runner agree on what a position may see: a name or a tasks
/// entry placed twice, a <c>notes</c> or a <c>diffFrom</c> that does not hold at the position the step is placed, and a destination written by two producers.</summary>
internal static class SchemeChecks
{
    public static IReadOnlyList<string> Problems(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks, IReadOnlySet<string> noteable) =>
    [
        .. Placement(scheme),
        .. scheme.Entries.SelectMany((entry, index) => TaskCodeAlone(scheme, entry, index, steps)),
        .. SchemePositions.Steps(scheme, tasks).SelectMany(placed => Position(scheme, placed, steps, tasks, noteable)),
        .. PathsWrittenTwice(scheme, steps, tasks),
        .. PipelineFiles(scheme, steps, tasks),
        .. FieldsBoundTwice(scheme, steps, tasks),
    ];

    /// <summary>The fields a <c>notes</c> may name and a scheme position may judge: every field some step or the tasks block binds, but no verdict (the runner writes it; the map-level
    /// check says so) and none already refused (one cause, one sentence).</summary>
    public static IReadOnlySet<string> Noteable(IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks, IReadOnlySet<string> refused) =>
        AllFields(steps, tasks).Except(steps.Values.Select(spec => spec.Verdict)).Except(refused).ToHashSet(StringComparer.Ordinal);

    /// <summary>Every field some step or the tasks block binds: a <c>notes</c> naming anything else is one cause, said once, by the map-level check.</summary>
    public static IReadOnlyList<string> AllFields(IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        [.. steps.Values.SelectMany(spec => spec.Outputs).Concat(tasks.Outputs).Select(binding => binding.Field).Distinct(StringComparer.Ordinal)];

    private static IEnumerable<string> Placement(Scheme scheme) =>
    [
        .. scheme.Entries.OfType<SchemeEntry.Step>().GroupBy(step => step.Name, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => $"schemes.{scheme.Name} — '{group.Key}' is placed twice"),
        .. scheme.Entries.OfType<SchemeEntry.Tasks>().Skip(1).Take(1).Select(_ => $"schemes.{scheme.Name} — tasks is placed twice"),
    ];

    /// <summary>The task coder is launched per task in its worktree by the pipeline: placed as a plain entry it would run once in the arm's clone, as a session that is not one.</summary>
    private static IEnumerable<string> TaskCodeAlone(Scheme scheme, SchemeEntry entry, int index, IReadOnlyDictionary<StepId, StepSpec> steps) =>
        entry is SchemeEntry.Step step && steps.TryGetValue(step.Id, out var spec) && spec.Kind == StepKind.TaskCode
            ? [$"schemes.{scheme.Name}[{index}] ({step.Name}): a taskCode step runs only inside a tasks entry"]
            : [];

    private static IEnumerable<string> Position(Scheme scheme, PlacedStep placed, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks, IReadOnlySet<string> noteable) =>
        steps.TryGetValue(placed.Id, out var spec)
            ? [.. Notes(scheme, placed, spec, steps, tasks, noteable), .. DiffFrom(scheme, placed, spec, steps, tasks)]
            : [];

    private static string Where(Scheme scheme, PlacedStep placed) => $"schemes.{scheme.Name}[{placed.Position}] ({placed.Label}):";

    private static IEnumerable<string> Notes(Scheme scheme, PlacedStep placed, StepSpec spec, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks, IReadOnlySet<string> noteable)
    {
        var visible = SchemePositions.Visible(scheme, placed.Position, placed.Id, steps, tasks).Select(binding => binding.Field).ToList();
        return noteable.Contains(spec.Notes) && !visible.Contains(spec.Notes)
            ? [$"{Where(scheme, placed)} notes {spec.Notes} is bound by no step at or before this position (visible: {string.Join(", ", visible)})"]
            : [];
    }

    private static IEnumerable<string> DiffFrom(Scheme scheme, PlacedStep placed, StepSpec spec, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        spec.DiffFrom.Length > 0 && steps.TryGetValue(new StepId(spec.DiffFrom), out var source) ? Misplaced(scheme, placed, spec, source, tasks) : [];

    private static IEnumerable<string> Misplaced(Scheme scheme, PlacedStep placed, StepSpec spec, StepSpec source, TaskLimits tasks)
    {
        var all = SchemePositions.Steps(scheme, tasks);
        var before = all.Take(all.ToList().IndexOf(placed) + 1).Any(other => other.Id == source.Id);
        return !before
            ? [$"{Where(scheme, placed)} diffFrom {spec.DiffFrom} is not placed at or before this position"]
            : !source.Snapshot ? [$"{Where(scheme, placed)} diffFrom {spec.DiffFrom} has no snapshot"] : [];
    }

    /// <summary>Two producers of one file would make a consumer read it twice as two things: a fixer briefed with two verdicts that are one file. Compared without regard to case, so
    /// it fails closed on a host whose file system ignores it; per scheme, because two steps no scheme places together may write one path.</summary>
    private static IEnumerable<string> PathsWrittenTwice(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks)
    {
        var placed = SchemePositions.Outputs(scheme, steps, tasks);
        return placed.SelectMany((first, index) => placed.Skip(index + 1)
            .Where(second => second.Producer != first.Producer && string.Equals(second.Binding.RelativePath, first.Binding.RelativePath, StringComparison.OrdinalIgnoreCase))
            .Select(second => $"schemes.{scheme.Name} — {first.Binding.RelativePath} is written by two of its producers: {first.Producer} ({first.Binding.Field}) and {second.Producer} ({second.Binding.Field})"));
    }

    /// <summary>A scheme that runs the task pipeline leaves it its files (<see cref="ArmPaths.IsPipelineFile"/>): a binding that names one would have the model, or the runner, write over what the pipeline reads.</summary>
    private static IEnumerable<string> PipelineFiles(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        !scheme.Entries.OfType<SchemeEntry.Tasks>().Any()
            ? []
            : SchemePositions.Outputs(scheme, steps, tasks).Where(placed => ArmPaths.IsPipelineFile(placed.Binding.RelativePath))
                .Select(placed => $"schemes.{scheme.Name} — {placed.Binding.RelativePath} is a file of the task pipeline, which writes it: {placed.Producer} ({placed.Binding.Field}) cannot bind it");

    /// <summary>The tasks block's own outputs may repeat a step's field only where no scheme places both (the example's <c>CODE_NOTES</c>): a scheme placing both is refused.</summary>
    private static IEnumerable<string> FieldsBoundTwice(Scheme scheme, IReadOnlyDictionary<StepId, StepSpec> steps, TaskLimits tasks) =>
        !scheme.Entries.OfType<SchemeEntry.Tasks>().Any()
            ? []
            : tasks.Outputs.SelectMany(own => SchemePositions.Steps(scheme, tasks).Select(placed => placed.Id).Distinct()
                .Where(id => steps.TryGetValue(id, out var spec) && spec.Outputs.Any(binding => binding.Field == own.Field))
                .Select(id => $"schemes.{scheme.Name} — {own.Field} is bound by step {id} and by the tasks block"));
}
