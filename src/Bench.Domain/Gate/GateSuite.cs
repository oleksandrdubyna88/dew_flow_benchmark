namespace Bench.Domain.Gate;

/// <summary>A frozen, hashed set of seeded tasks — the gate benchmark's <see cref="Suites.Suite"/>.
/// <para>
/// The stamp is the hash of the tasks' canonical forms, which carry every fact a run is measured against
/// (the case, the seeds, what each task hosts) and NOT where the clones are — so the operator's suite file can
/// name absolute paths on this machine and still stamp identically on another. <see cref="PrivateNames"/> is
/// likewise outside the hash: it is an input to the publication guard, not to the measurement.
/// </para></summary>
public sealed record GateSuite
{
    private GateSuite(string id, IReadOnlyList<GateTask> tasks, IReadOnlyList<string> privateNames, string hash)
    {
        Id = id;
        Tasks = tasks;
        PrivateNames = privateNames;
        Hash = hash;
    }

    public string Id { get; }

    public IReadOnlyList<GateTask> Tasks { get; }

    /// <summary>Repository and company names the operator lists so the publication guard can refuse any row
    /// that carries one. Never hashed, never stored.</summary>
    public IReadOnlyList<string> PrivateNames { get; }

    public string Hash { get; }

    /// <summary>What every run quotes: the id plus enough of the hash to prove which file it was.</summary>
    public string Stamp => $"{Id}#{HashText.Short(Hash)}";

    public static Outcome<GateSuite> Freeze(string? id, IReadOnlyList<GateTask> tasks, IReadOnlyList<string> privateNames)
    {
        var suiteId = Slug.Clean(id);
        var duplicate = tasks.GroupBy(t => t.Id.Value, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        var refusal = (Slug.IsValid(suiteId), tasks.Count, duplicate, privateNames.Any(string.IsNullOrWhiteSpace)) switch
        {
            (false, _, _, _) => $"'{suiteId}' is not a usable suite id — {Slug.Rule}",
            (_, 0, _, _) => $"suite '{suiteId}' has no tasks — an empty suite is not worth a stamp",
            (_, _, not null, _) => $"suite '{suiteId}' lists task '{duplicate.Key}' twice",
            (_, _, _, true) => $"suite '{suiteId}' lists a blank private name — a blank would match every row and refuse the whole export",
            _ => string.Empty,
        };

        return refusal.Length > 0
            ? Outcome<GateSuite>.Failure(refusal)
            : Outcome<GateSuite>.Success(Frozen(suiteId, [.. tasks], [.. privateNames.Select(n => n.Trim())]));
    }

    /// <summary>The suite over SNAPSHOTS: the task list is copied here and every task already holds its own
    /// copy of its seeds (<see cref="GateTask.Of"/>), so a caller that keeps and edits its lists after freezing
    /// cannot change what a stamped suite contains.</summary>
    private static GateSuite Frozen(string id, IReadOnlyList<GateTask> tasks, IReadOnlyList<string> privateNames) =>
        new(id, tasks, privateNames, HashOf(id, tasks));

    /// <summary>The tasks a run of <paramref name="gate"/> may plan over. Refused when none hosts it, because a
    /// matrix over zero tasks is refused one step later with less to say.</summary>
    public Outcome<IReadOnlyList<GateTask>> TasksFor(GateKind gate)
    {
        var hosting = Tasks.Where(t => t.Hosts.Hosts(gate)).ToList();

        return hosting.Count > 0
            ? Outcome<IReadOnlyList<GateTask>>.Success(hosting)
            : Outcome<IReadOnlyList<GateTask>>.Failure($"no task in suite '{Id}' hosts the {Name(gate)} gate");
    }

    /// <summary><see cref="TasksFor(GateKind)"/> narrowed to <paramref name="only"/> when it names any — kept in the SUITE's
    /// order, so the matrix nests as in every run. Each named task must exist and host the gate, refused by name as
    /// <see cref="Task"/> refuses it. Naming none narrows nothing.</summary>
    public Outcome<IReadOnlyList<GateTask>> TasksFor(GateKind gate, IReadOnlyList<GateTaskId> only)
    {
        var refusals = only.Select(id => Task(id, gate)).OfType<Outcome<GateTask>.Fail>().Select(f => f.Reason).ToList();

        return (only.Count, refusals.Count) switch
        {
            (0, _) => TasksFor(gate),
            (_, > 0) => Outcome<IReadOnlyList<GateTask>>.Failure(string.Join("; ", refusals)),
            _ => Outcome<IReadOnlyList<GateTask>>.Success(Named(only)),
        };
    }

    /// <summary>The suite's tasks whose id is named, in the suite's order — one set lookup per task.</summary>
    private List<GateTask> Named(IReadOnlyList<GateTaskId> only)
    {
        var named = only.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
        return [.. Tasks.Where(t => named.Contains(t.Id.Value))];
    }

    /// <summary>One task, for one gate — refused by name when the task cannot host that gate.</summary>
    public Outcome<GateTask> Task(GateTaskId id, GateKind gate)
    {
        var task = Tasks.FirstOrDefault(t => t.Id.Value == id.Value);

        return (task, task?.Hosts.Hosts(gate)) switch
        {
            (null, _) => Outcome<GateTask>.Failure(
                $"suite '{Id}' has no task '{id}' — it has: {string.Join(", ", Tasks.Select(t => t.Id.Value))}"),
            (_, false) => Outcome<GateTask>.Failure(
                $"task '{id}' does not host the {Name(gate)} gate — it hosts {task.Hosts.Canonical}; a plan-only task has no diff and no feature to review"),
            _ => Outcome<GateTask>.Success(task),
        };
    }

    /// <summary>The seeds a seeded-recall column is computed against. A task with no seeds cannot have one, and
    /// says so rather than reporting zero of zero.</summary>
    public Outcome<IReadOnlyList<SeedSpec>> SeedsOf(GateTaskId id)
    {
        var task = Tasks.FirstOrDefault(t => t.Id.Value == id.Value);

        return (task, task?.IsSeeded) switch
        {
            (null, _) => Outcome<IReadOnlyList<SeedSpec>>.Failure($"suite '{Id}' has no task '{id}'"),
            (_, false) => Outcome<IReadOnlyList<SeedSpec>>.Failure(
                $"task '{id}' carries no seeds — a seeded-recall column cannot be computed for it; a real plan's ground truth is agreement and the blinded value verdicts only"),
            _ => Outcome<IReadOnlyList<SeedSpec>>.Success(task.Seeds),
        };
    }

    private static string Name(GateKind gate) => gate.ToString().ToLowerInvariant();

    private static string HashOf(string id, IReadOnlyList<GateTask> tasks) =>
        StableHash.Of(CanonicalFields.Of(
        [
            "gate-suite", id, tasks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            .. tasks.OrderBy(t => t.Id.Value, StringComparer.Ordinal).Select(t => t.Canonical),
        ]));
}
