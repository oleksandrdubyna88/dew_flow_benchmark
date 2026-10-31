using System.Text.Json.Nodes;
using Bench.Domain;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Tasks;

namespace Bench.Tests.ModelArms;

/// <summary>What <see cref="TasksPlan.Validate"/> answered over one list, and the work folder it was asked about (the folder itself is gone: only its name is used in the sentences).</summary>
internal sealed record PlanChecked(Outcome<TaskBatches> Outcome, string Work)
{
    /// <summary>The refusal's lines. Asserts the refusal first, so a list wrongly accepted fails on an assertion about that and not on a helper's exception.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            Outcome.Should().BeOfType<Outcome<TaskBatches>.Fail>("the list is refused");
            return Outcome.Reason().Split('\n');
        }
    }

    public TaskBatches Batches => Outcome.Ok();
}

/// <summary>One list the shell's own checker is asked about as well: its text and the brief files that are NOT in the work folder.</summary>
internal sealed record PlanRow(string Name, string Json, IReadOnlyList<string> MissingBriefs)
{
    /// <summary>The shell copies a <c>scenarios</c> that is a text into <c>batches.json</c> as that text; the port writes the list of one.</summary>
    public bool ScenariosAsText { get; init; }
}

/// <summary>The lists of E4.S1's tests, built from a small JSON writer: a valid list of <c>n</c> tasks (<c>t1</c>… each owning <c>src/F&lt;i&gt;.cs</c> with its brief <c>t&lt;i&gt;.md</c>, the first three delivering <c>S1</c>–<c>S3</c>, the last owning
/// the plan and depending on all the others) and the edits that each break one rule. Every refusal test starts from a list the checker ACCEPTS (<c>Batches_are_ready_tasks_in_order_at_most_four_at_a_time</c> asserts it), so a refusal is about the edit and not about the fixture.</summary>
internal static class TasksPlanKit
{
    public const string Plan = "todo/PLAN_x.md";

    public static readonly IReadOnlyList<string> Scenarios = ["S1", "S2", "S3"];

    public static JsonArray Texts(params string[] items) => new([.. items.Select(item => (JsonNode?)JsonValue.Create(item))]);

    public static JsonObject Task(string id, string[] files, string[]? depends = null, string[]? scenarios = null) =>
        new()
        {
            ["id"] = id,
            ["files"] = Texts(files),
            ["brief"] = id + ".md",
            ["depends"] = Texts(depends ?? []),
            ["scenarios"] = Texts(scenarios ?? []),
        };

    /// <summary>A valid list of <paramref name="count"/> tasks (accepted by the limits 4, 7, 4 when 4 to 7).</summary>
    public static List<JsonObject> Valid(int count = 5) =>
        [.. Enumerable.Range(1, count).Select(index => Task(
            $"t{index}",
            index == count ? [$"src/F{index}.cs", Plan] : [$"src/F{index}.cs"],
            index == count ? [.. Enumerable.Range(1, count - 1).Select(other => $"t{other}")] : null,
            index <= 3 ? [$"S{index}"] : null))];

    public static string Text(IEnumerable<JsonObject> tasks) => new JsonObject { ["tasks"] = new JsonArray([.. tasks]) }.ToJsonString();

    /// <summary>The refusal's lines of an edited valid list, or of the text itself.</summary>
    public static PlanChecked Check(string json, TaskLimits? limits = null, IReadOnlyList<string>? missing = null, IReadOnlyList<string>? alsoThere = null)
    {
        using var temp = new TempFolder();
        var work = temp.Folder("work");
        var names = TasksPlan.BriefNames(json).Except(missing ?? []).ToList();
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(work, name), "brief");
        }

        var workFiles = names.Concat(alsoThere ?? []).ToHashSet(StringComparer.Ordinal);
        return new PlanChecked(TasksPlan.Validate(json, Plan, Scenarios, limits ?? TaskLimits.Shell, workFiles, work), work);
    }

    public static PlanChecked Check(IEnumerable<JsonObject> tasks, TaskLimits? limits = null) => Check(Text(tasks), limits);

    /// <summary>The lists the oracle test asks the shell's own checker about: one per sentence of the table (the three deviations left out), and the lists that are accepted.</summary>
    public static IEnumerable<PlanRow> OracleRows()
    {
        yield return Row("three tasks", Valid(3));
        yield return Row("eight tasks", Valid(8));
        yield return Row("an id used twice", Edit(Valid(), tasks => tasks[1]["id"] = "t1"));
        foreach (var (name, literal) in new[] { ("upper case", "\"T1\""), ("a trailing newline", "\"t1\\n\""), ("a word", "\"x\""), ("a number", "1"), ("null", "null"), ("a boolean", "false") })
        {
            yield return Row("the id is " + name, Edit(Valid(), tasks => tasks[1]["id"] = JsonNode.Parse(literal)));
        }

        yield return Row("ids that are numbers of every layout", Edit(Valid(6), tasks =>
        {
            string[] literals = ["1e21", "1.5e-7", "123456789012345680000", "0.000001", "-0", "2.50"];
            for (var index = 0; index < literals.Length; index++)
            {
                tasks[index]["id"] = JsonNode.Parse(literals[index]);
            }
        }));
        yield return Row("the id is absent", Edit(Valid(), tasks => tasks[1].Remove("id")));
        yield return Row("two ids absent", Edit(Valid(), tasks => { tasks[1].Remove("id"); tasks[2].Remove("id"); }));
        yield return Row("an empty files", Edit(Valid(), tasks => tasks[1]["files"] = new JsonArray()));
        yield return Row("an absent files", Edit(Valid(), tasks => tasks[1].Remove("files")));
        yield return Row("a file owned twice", Edit(Valid(), tasks => ((JsonArray)tasks[2]["files"]!).Add("src/F1.cs")));
        yield return Row("a dependency that does not exist", Edit(Valid(), tasks => tasks[1]["depends"] = Texts("t9")));
        yield return Row("a cycle", Edit(Valid(), tasks => { tasks[0]["depends"] = Texts("t2"); tasks[1]["depends"] = Texts("t1"); }));
        yield return Row("no task owns the plan", Edit(Valid(), tasks => tasks[4]["files"] = Texts("src/F5.cs")));
        yield return Row("the owner depends on two only", Edit(Valid(), tasks => tasks[4]["depends"] = Texts("t1", "t2")));
        yield return Row("a scenario nobody delivers", Edit(Valid(), tasks => tasks[1]["scenarios"] = new JsonArray()));
        yield return new PlanRow("a missing brief", Text(Valid()), ["t3.md"]);
        yield return new PlanRow("a brief that is not there and a missing one", Text(Edit(Valid(), tasks => tasks[0].Remove("brief"))), ["t3.md"]);
        yield return new PlanRow("not json", "{\"tasks\": [", []);
        yield return Row("five problems at once", FiveProblems());
        yield return Row("accepted: scenarios given as a text is one scenario", Edit(Valid(), tasks => tasks[1]["scenarios"] = "S2")) with { ScenariosAsText = true };
        yield return Row("accepted: five tasks in a line", Edit(Valid(), tasks => { for (var index = 1; index < 4; index++) { tasks[index]["depends"] = Texts($"t{index}"); } }));
        yield return Row("accepted: seven tasks in three batches", Valid(7));
        yield return Row("accepted: four independent and the owner", Edit(Valid(5), tasks => tasks[1]["depends"] = Texts("t1")));
        yield return Row("accepted: files with quotes, a backslash and non-ASCII text", Edit(Valid(), tasks => ((JsonArray)tasks[0]["files"]!).Add("src/\"q\" \\ é — ё.cs")));
    }

    /// <summary>Three tasks (sentence 2), a duplicated id (3), a task without files (5), a dependency that does not exist (7), a scenario nobody delivers (12): the shell prints all five, in this order.</summary>
    public static List<JsonObject> FiveProblems() =>
        Edit(Valid(3), tasks =>
        {
            tasks[1]["id"] = "t1";
            tasks[1]["files"] = new JsonArray();
            tasks[2]["depends"] = Texts("t1", "t9");
            tasks[2]["scenarios"] = new JsonArray();
        });

    public static List<JsonObject> Edit(List<JsonObject> tasks, Action<List<JsonObject>> edit)
    {
        edit(tasks);
        return tasks;
    }

    private static PlanRow Row(string name, List<JsonObject> tasks) => new(name, Text(tasks), []);
}
