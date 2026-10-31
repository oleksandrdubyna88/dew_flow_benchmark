using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Tasks;

/// <summary>One task of the split's plan. <see cref="Brief"/> is a file NAME in <c>work/</c>: the harness never reads a brief, it asks for its presence by name.</summary>
public sealed record PlannedTask(string Id, IReadOnlyList<string> Files, string Brief, IReadOnlyList<string> Depends, IReadOnlyList<string> Scenarios);

/// <summary>The batches the tasks run in, and the tasks in the order the list gave them.</summary>
public sealed record TaskBatches(IReadOnlyList<IReadOnlyList<string>> Batches, IReadOnlyList<PlannedTask> Tasks)
{
    /// <summary>The task with this id; asking for one the plan does not hold is a caller's bug.</summary>
    public PlannedTask Task(string id) =>
        Tasks.FirstOrDefault(task => task.Id == id) ?? throw new KeyNotFoundException($"the plan holds no task '{id}'");

    /// <summary>The ids of every task, in batch order.</summary>
    public IReadOnlyList<string> TaskIds => [.. Batches.SelectMany(batch => batch)];
}

/// <summary>The checker the shell ran over the split's <c>work/tasks.json</c> (<c>tasks-plan.mjs:16-49</c>) and the batches it computed (<c>:44-49</c>), as pure functions: every problem is reported at once, in the shell's order, one <c>- &lt;sentence&gt;</c> line each —
/// the text <c>out/tasks-plan.log</c> holds and the split fix's <c>PROBLEMS</c> is filled with. Values print as JavaScript's template literals print them (<see cref="JsText"/>). The shell's script is the oracle (<c>TasksPlanTests</c>).
/// <para>
/// Differences from the shell, each a test: a <c>files</c>, <c>depends</c> or <c>scenarios</c> that is not a list is one sentence, not one problem per character; a <c>brief</c> that is not ONE file name is a missing brief and is never probed (the shell joined the model's text into a path);
/// the bounds and the batch width come from <see cref="TaskLimits"/>; a list holding anything but texts, and an id that is not a text, are refused (the batches hold texts; the shell let an id <c>["t2"]</c> through because JavaScript prints it as <c>t2</c>).
/// </para></summary>
public static class TasksPlan
{
    /// <summary>The <c>brief</c> values that are ONE file name, each once, in list order: the names the caller probes in <c>work/</c>. Empty when the text is not JSON.</summary>
    public static IReadOnlyList<string> BriefNames(string tasksJson) =>
        TaskDrafts.Read(tasksJson).Match<IReadOnlyList<string>>(
            drafts => [.. drafts.Select(draft => draft.Brief.AsString).OfType<string>().Where(IsFileName).Distinct(StringComparer.Ordinal)],
            _ => []);

    /// <summary>The plan, or every problem. <paramref name="workFiles"/> is the set of <see cref="BriefNames"/> whose <c>work/&lt;name&gt;</c> exists; <paramref name="planPath"/> is the experiment's <c>PLAN</c> and <paramref name="scenarioIds"/> its scenarios;
    /// <paramref name="workFolder"/> is the folder the sentences name.</summary>
    public static Outcome<TaskBatches> Validate(string tasksJson, string planPath, IReadOnlyList<string> scenarioIds, TaskLimits limits, IReadOnlySet<string> workFiles, string workFolder) =>
        TaskDrafts.Read(tasksJson).Match(
            drafts => Judged(drafts, new PlanFacts(planPath, scenarioIds, limits, workFiles, workFolder)),
            reason => Outcome<TaskBatches>.Failure("- tasks.json is missing or not JSON: " + reason));

    /// <summary><c>ok: 5 tasks in 2 batches: [t1 t2 t3 t4] → [t5]</c> (<c>tasks-plan.mjs:51</c>).</summary>
    public static string Ok(TaskBatches batches) =>
        $"ok: {batches.Tasks.Count} tasks in {batches.Batches.Count} batches: {string.Join(" → ", batches.Batches.Select(batch => $"[{string.Join(' ', batch)}]"))}";

    private sealed record PlanFacts(string PlanPath, IReadOnlyList<string> ScenarioIds, TaskLimits Limits, IReadOnlySet<string> WorkFiles, string WorkFolder);

    private static Outcome<TaskBatches> Judged(IReadOnlyList<TaskDraft> drafts, PlanFacts facts)
    {
        var deps = Dependencies(drafts);
        IReadOnlyList<string> problems =
        [
            .. Count(drafts, facts),
            .. Twice(drafts),
            .. EachTask(drafts, facts),
            .. Cycles(drafts, deps),
            .. PlanOwner(drafts, deps, facts),
            .. Undelivered(drafts, facts),
        ];
        return problems.Count > 0
            ? Outcome<TaskBatches>.Failure(string.Join('\n', problems.Select(problem => "- " + problem)))
            : Outcome<TaskBatches>.Success(Batched(drafts.Select(draft => Planned(draft)).ToList(), facts.Limits.MaxParallel));
    }

    private static IEnumerable<string> Count(IReadOnlyList<TaskDraft> drafts, PlanFacts facts) =>
        drafts.Count < facts.Limits.Min || drafts.Count > facts.Limits.Max
            ? [$"there are {drafts.Count} tasks; the brief asks for {facts.Limits.Min} to {facts.Limits.Max}"]
            : [];

    /// <summary>Once per later occurrence (<c>ids.indexOf(x) !== i</c>); <c>undefined</c> is equal to <c>undefined</c>, an object to no other object.</summary>
    private static IEnumerable<string> Twice(IReadOnlyList<TaskDraft> drafts)
    {
        var keys = drafts.Select(draft => draft.Id.Key).ToList();
        return drafts.Where((draft, index) => keys.IndexOf(draft.Id.Key) != index).Select(draft => $"task id {draft.Id.Text} is used twice");
    }

    private static IEnumerable<string> EachTask(IReadOnlyList<TaskDraft> drafts, PlanFacts facts)
    {
        var owner = new Dictionary<string, JsValue>(StringComparer.Ordinal);
        var ids = drafts.Select(draft => draft.Id.Key).ToHashSet(StringComparer.Ordinal);
        return drafts.SelectMany(draft => Concat(Shape(draft), Files(draft, owner), DependsOn(draft, ids), Brief(draft, facts)));
    }

    private static IEnumerable<string> Concat(params IEnumerable<string>[] parts) => [.. parts.SelectMany(part => part)];

    private static IEnumerable<string> Shape(TaskDraft draft) =>
    [
        .. When(!(draft.Id.AsString is { } id && TaskIds.IsTaskId(id)), () => $"a task id must look like t1, t2…: {draft.Id.Json}"),
        .. When(draft.Files.Items.Count == 0, () => $"{draft.Id.Text} lists no files"),
    ];

    private static IEnumerable<string> When(bool condition, Func<string> sentence) => condition ? [sentence()] : [];

    /// <summary>The members' own sentences first (a member that is not a list; a list holding something that is not a text), then each file: <c>owner[f]</c> is the id of the last task that listed it, and a falsy owner owns nothing.</summary>
    private static IEnumerable<string> Files(TaskDraft draft, Dictionary<string, JsValue> owner)
    {
        var problems = Member(draft, "files", draft.Files).ToList();
        foreach (var file in draft.Files.Items)
        {
            if (owner.TryGetValue(file.Text, out var first) && first.Truthy && first.Key != draft.Id.Key)
            {
                problems.Add($"{file.Text} belongs to both {first.Text} and {draft.Id.Text}");
            }

            owner[file.Text] = draft.Id;
        }

        return problems;
    }

    private static IEnumerable<string> DependsOn(TaskDraft draft, IReadOnlySet<string> ids) =>
        [.. Member(draft, "depends", draft.Depends), .. draft.Depends.Items.Where(dependency => !ids.Contains(dependency.Key)).Select(dependency => $"{draft.Id.Text} depends on {dependency.Text}, which does not exist")];

    private static IEnumerable<string> Member(TaskDraft draft, string name, ListMember list) =>
    [
        .. When(list.NotAList, () => $"{draft.Id.Text}'s {name} is not a list"),
        .. list.Items.Where(item => item.AsString is null).Select(item => $"{draft.Id.Text}'s {name} holds {item.Json}, which is not a text"),
    ];

    /// <summary>A brief is present when it is ONE file name the caller found in <c>work/</c>; a caller that lists any other name as present is not believed.</summary>
    private static IEnumerable<string> Brief(TaskDraft draft, PlanFacts facts) =>
        draft.Brief.AsString is { } name && IsFileName(name) && facts.WorkFiles.Contains(name)
            ? []
            : [$"{draft.Id.Text}'s brief {draft.Brief.Json} does not exist in {facts.WorkFolder}"];

    /// <summary>Empty, <c>.</c>, <c>..</c>, anything with a separator or a NUL, and anything rooted is not the name of a file in <c>work/</c>.</summary>
    private static bool IsFileName(string name) =>
        name.Length > 0 && name is not ("." or "..") && name.IndexOfAny(['/', '\\', '\0']) < 0 && !Path.IsPathRooted(name);

    /// <summary><c>deps = Object.fromEntries(tasks.map(t =&gt; [t.id, new Set(t.depends)]))</c>: keyed by the id's text, the later of two equal keys wins.</summary>
    private static Dictionary<string, IReadOnlyList<JsValue>> Dependencies(IReadOnlyList<TaskDraft> drafts)
    {
        var deps = new Dictionary<string, IReadOnlyList<JsValue>>(StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            deps[draft.Id.Text] = [.. draft.Depends.Items.DistinctBy(item => item.Key)];
        }

        return deps;
    }

    /// <summary>Everything reachable from <paramref name="start"/> through <c>depends</c>, as strict-equality keys.</summary>
    private static HashSet<string> Closure(JsValue start, IReadOnlyDictionary<string, IReadOnlyList<JsValue>> deps)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<JsValue>([start]);
        while (pending.Count > 0)
        {
            foreach (var next in deps.GetValueOrDefault(pending.Pop().Text) ?? [])
            {
                if (seen.Add(next.Key))
                {
                    pending.Push(next);
                }
            }
        }

        return seen;
    }

    private static IEnumerable<string> Cycles(IReadOnlyList<TaskDraft> drafts, IReadOnlyDictionary<string, IReadOnlyList<JsValue>> deps) =>
        drafts.Where(draft => Closure(draft.Id, deps).Contains(draft.Id.Key)).Select(draft => $"{draft.Id.Text} depends on itself through a cycle");

    /// <summary>The plan's owner is the last task that lists it; the last task must reach every other.</summary>
    private static IEnumerable<string> PlanOwner(IReadOnlyList<TaskDraft> drafts, IReadOnlyDictionary<string, IReadOnlyList<JsValue>> deps, PlanFacts facts)
    {
        var owner = drafts.LastOrDefault(draft => draft.Files.Items.Any(file => file.Text == facts.PlanPath))?.Id;
        if (owner is not { Truthy: true })
        {
            return [$"no task owns {facts.PlanPath} (the last task must)"];
        }

        var reached = Closure(owner, deps);
        var missing = drafts.Select(draft => draft.Id).Where(id => id.Key != owner.Key && !reached.Contains(id.Key)).ToList();
        return missing.Count > 0 ? [$"{owner.Text} owns the plan but does not depend on {string.Join(", ", missing.Select(id => id.Joined))}"] : [];
    }

    private static IEnumerable<string> Undelivered(IReadOnlyList<TaskDraft> drafts, PlanFacts facts)
    {
        var covered = drafts.SelectMany(draft => draft.Scenarios.Items).Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var missing = facts.ScenarioIds.Where(id => !covered.Contains("s:" + id)).ToList();
        return
        [
            .. drafts.SelectMany(draft => Member(draft, "scenarios", draft.Scenarios)),
            .. When(missing.Count > 0, () => $"no task delivers {string.Join(", ", missing)}"),
        ];
    }

    /// <summary>Only a list the checker accepted gets here: every id and every item is a text, every dependency exists and none is a cycle.</summary>
    private static PlannedTask Planned(TaskDraft draft) =>
        new(draft.Id.AsString!, Texts(draft.Files), draft.Brief.AsString!, Texts(draft.Depends), Texts(draft.Scenarios));

    private static string[] Texts(ListMember list) => [.. list.Items.Select(item => item.AsString!)];

    /// <summary>Each batch is the READY tasks (not done, every dependency done) in list order, at most <paramref name="width"/> of them (<c>tasks-plan.mjs:44-49</c>).</summary>
    private static TaskBatches Batched(IReadOnlyList<PlannedTask> tasks, int width)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        var batches = new List<IReadOnlyList<string>>();
        while (done.Count < tasks.Count)
        {
            var batch = tasks.Where(task => !done.Contains(task.Id) && task.Depends.All(done.Contains)).Select(task => task.Id).Take(width).ToList();
            batches.Add(batch);
            done.UnionWith(batch);
        }

        return new TaskBatches(batches, tasks);
    }
}
