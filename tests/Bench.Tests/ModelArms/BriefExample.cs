using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;
using ModelArms.Core.Briefs;
using ModelArms.Core.Experiment;

namespace Bench.Tests.ModelArms;

/// <summary>What the brief suites share: the worked example read at one location, the arm paths and facts a brief is filled with, and every (scheme, position, step id) the example
/// places — so a field list is asked for one way, <see cref="BriefFields.For"/> over <see cref="SchemePositions.Visible"/>, as the rehearsal and the runner ask it.</summary>
internal static class BriefExample
{
    public const string Own = "00000000000000c1";
    public const string Other = "00000000000000c2";
    public const string Root = "/e";

    public static readonly ExperimentLocation Location = new("/x/root", "/x/home", string.Empty, "/x/repo");

    public static readonly ArmPaths Paths = new(Root, Own);

    public static readonly BriefFacts Facts = new(
        Plan: "/e/base/todo/PLAN_x.md",
        Base: "1111111111111111111111111111111111111111",
        Branch: "feat/x",
        ScenarioCount: 12,
        Deadline: "12:30",
        Known: "the known failures are listed in the plan",
        Task: new TaskFacts("t3", "/e/r/" + Own + "/work/t3.md", "src/A.cs src/B.cs", "2222222222222222222222222222222222222222"),
        Problems: "the plan splits one file across two tasks");

    public static Experiment Experiment => ExperimentFile.Read(ExampleText(), Location).Ok();

    public static string ShippedFolder => System.IO.Path.Combine(Cli.Repository.Root, "tools", "model-arms", "briefs");

    public static string ExampleText() =>
        File.ReadAllText(System.IO.Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json"));

    /// <summary>The example as a node tree, edited and written back.</summary>
    public static string Edited(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(ExampleText(), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    public static Scheme Scheme(string name) => Experiment.Schemes.Single(scheme => scheme.Name == name);

    /// <summary>The fields of the brief of step <paramref name="id"/> at <paramref name="position"/> of <paramref name="scheme"/>.</summary>
    public static IReadOnlyDictionary<string, string> Fields(string scheme, int position, string id, ArmPaths? paths = null, BriefFacts? facts = null)
    {
        var experiment = Experiment;
        var spec = experiment.Steps[new StepId(id)];
        var visible = SchemePositions.Visible(Scheme(scheme), position, spec.Id, experiment.Steps, experiment.Tasks);
        return BriefFields.For(spec, visible, experiment.Tasks, paths ?? Paths, facts ?? Facts);
    }

    /// <summary>Every (scheme, position, step id) the example places, a tasks entry counting its three steps in order.</summary>
    public static IReadOnlyList<(string Scheme, int Position, string Id)> Placements()
    {
        var experiment = Experiment;
        return
        [
            .. experiment.Schemes.SelectMany(scheme => scheme.Entries.SelectMany((entry, position) => IdsOf(entry, experiment.Tasks).Select(id => (scheme.Name, position, id)))),
        ];
    }

    private static IEnumerable<string> IdsOf(SchemeEntry entry, TaskLimits tasks) =>
        entry switch
        {
            SchemeEntry.Step step => [step.Id.Value],
            _ => [tasks.Split.Value, tasks.SplitFix.Value, tasks.TaskCode.Value],
        };

    /// <summary>The shipped templates by stem, read as the console reads them.</summary>
    public static IReadOnlyDictionary<string, Outcome<string>> Templates() =>
        BriefTemplates.Shipped.ToDictionary(
            stem => stem,
            stem => Outcome<string>.Success(File.ReadAllText(System.IO.Path.Combine(ShippedFolder, BriefTemplates.NameOf(stem).Ok()))));
}
