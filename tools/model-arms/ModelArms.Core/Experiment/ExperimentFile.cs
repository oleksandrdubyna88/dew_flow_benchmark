using System.Text.Json;
using System.Text.Json.Nodes;
using Bench.Domain;

namespace ModelArms.Core.Experiment;

/// <summary>The experiment's facts, read from the text of <c>experiment.json</c>. Reading is strict and says everything at once:
/// an unknown member is refused by name (never ignored), a missing one is named, and every problem of the file comes back in one
/// answer, one per line. The text arrives as a string; the console owns the file, so Core opens nothing.</summary>
public static class ExperimentFile
{
    public const string Schema = "model-arms/experiment-v1";

    private static readonly string[] Required =
        ["schema", "target", "scenarios", "baseline", "known", "steps", "schemes", "rounds", "limits", "tools", "awake"];

    private static readonly string[] Known = [.. Required, "tasks", "coai", "isolation"];
    private static readonly string[] LimitMembers = ["waitSeconds", "tries", "gateWaitSeconds", "gateTries", "minimumRemainingSeconds", "killGraceSeconds"];
    private static readonly string[] TaskMembers = ["min", "max", "maxParallel"];
    private static readonly string[] ToolMembers = ["write", "read", "gate", "deny"];
    private static readonly string[] CoaiMembers = ["binary", "settings", "side"];
    private static readonly string[] IsolationMembers = ["closed", "unlisted"];

    private static readonly (string Key, AwakeChoice Value)[] AwakeChoices =
        [("auto", AwakeChoice.Auto), ("windows", AwakeChoice.Windows), ("wsl", AwakeChoice.Wsl), ("none", AwakeChoice.None)];

    /// <summary>The facts, or every problem of the file one per line, the placement problems first.</summary>
    public static Outcome<Experiment> Read(string json, ExperimentLocation location)
    {
        var reader = new StrictJson();
        foreach (var problem in Placement(location))
        {
            reader.Add(problem);
        }

        var experiment = Parse(json, reader) is { } root ? Walk(reader, root, location) : null;
        return experiment is not null && reader.Problems.Count == 0
            ? Outcome<Experiment>.Success(experiment)
            : Outcome<Experiment>.Failure(string.Join('\n', reader.Problems));
    }

    /// <summary>D4's two root refusals: a root lives outside this repository and outside every git checkout, or the arms' clones would nest in it.</summary>
    public static IReadOnlyList<string> Placement(ExperimentLocation location)
    {
        var problems = new List<string>();
        if (location.ThisRepository.Length > 0 && PathScope.IsEqualOrUnder(location.Root, location.ThisRepository))
        {
            problems.Add($"the experiment root {location.Root} is inside this repository ({location.ThisRepository}) — a root lives outside every checkout");
        }

        if (location.EnclosingGitTop.Length > 0)
        {
            problems.Add($"the experiment root {location.Root} is inside the git checkout {location.EnclosingGitTop} — the arms' clones would nest in it");
        }

        return problems;
    }

    private static JsonObject? Parse(string json, StrictJson reader)
    {
        try
        {
            var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = false, AllowDuplicateProperties = false };
            var node = JsonNode.Parse(json, default, options);
            if (node is not JsonObject)
            {
                reader.Add("experiment.json must be a JSON object");
            }

            return node as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            // JsonNode.Parse reports a malformed text as JsonException and a member written twice as ArgumentException; both are the
            // operator's file being wrong, an expected answer rather than a crash.
            reader.Add($"experiment.json is not JSON: {ex.Message}");
            return null;
        }
    }

    private static Experiment Walk(StrictJson json, JsonObject top, ExperimentLocation location)
    {
        json.Object(top, string.Empty, Known, Required);
        CheckSchema(json, top);
        var steps = FlowSection.Steps(json, top);
        var schemes = FlowSection.Schemes(json, top, steps);
        return new Experiment(
            location.Root,
            TargetSection.Read(json, top, location),
            PathFields.Relative(json, top, string.Empty, "scenarios", "the experiment root"),
            PathFields.Relative(json, top, string.Empty, "baseline", "the experiment root"),
            json.Text(top, string.Empty, "known"),
            steps,
            schemes,
            FlowSection.Rounds(json, top, schemes),
            Limits(json, top),
            Tasks(json, top),
            Tools(json, top),
            Coai(json, top),
            Isolation(json, top, location),
            json.Choice(top, string.Empty, "awake", AwakeChoices, AwakeChoice.Auto));
    }

    private static void CheckSchema(StrictJson json, JsonObject top)
    {
        var schema = json.Text(top, string.Empty, "schema");
        if (schema.Length > 0 && schema != Schema)
        {
            json.Add($"schema is '{schema}' — this build reads exactly '{Schema}'");
        }
    }

    private static Limits Limits(StrictJson json, JsonObject top)
    {
        var limits = json.Object(top["limits"], "limits", LimitMembers, LimitMembers);
        return new Limits(
            json.Positive(limits, "limits", "waitSeconds"),
            json.Positive(limits, "limits", "tries"),
            json.Positive(limits, "limits", "gateWaitSeconds"),
            json.Positive(limits, "limits", "gateTries"),
            json.Positive(limits, "limits", "minimumRemainingSeconds"),
            json.Positive(limits, "limits", "killGraceSeconds"));
    }

    private static TaskLimits Tasks(StrictJson json, JsonObject top)
    {
        if (!top.ContainsKey("tasks"))
        {
            return TaskLimits.Shell;
        }

        var tasks = json.Object(top["tasks"], "tasks", TaskMembers, TaskMembers);
        var limits = new TaskLimits(json.Positive(tasks, "tasks", "min"), json.Positive(tasks, "tasks", "max"), json.Positive(tasks, "tasks", "maxParallel"));
        if (limits.Min > limits.Max && limits.Max > 0)
        {
            json.Add($"tasks.min ({limits.Min}) is greater than tasks.max ({limits.Max}) — the split plans at least min and at most max tasks");
        }

        return limits;
    }

    private static ToolSets Tools(StrictJson json, JsonObject top)
    {
        var tools = json.Object(top["tools"], "tools", ToolMembers, ToolMembers);
        return new ToolSets(
            json.Texts(tools, "tools", "write"),
            json.Texts(tools, "tools", "read"),
            json.Texts(tools, "tools", "gate"),
            json.Texts(tools, "tools", "deny"));
    }

    private static CoaiFacts Coai(StrictJson json, JsonObject top)
    {
        if (!top.ContainsKey("coai"))
        {
            return CoaiFacts.None;
        }

        var coai = json.Object(top["coai"], "coai", CoaiMembers, CoaiMembers);
        return new CoaiFacts(
            PathFields.Absolute(json, coai, "coai", "binary"),
            PathFields.Absolute(json, coai, "coai", "settings"),
            Side(json, coai));
    }

    private static string Side(StrictJson json, JsonObject coai)
    {
        var side = json.Text(coai, "coai", "side");
        if (side.Contains('/') || side.Contains('\\') || side.Contains(".."))
        {
            json.Add($"coai.side is '{side}' — it names one folder: no '/' or '\\' and no '..'");
        }

        return side;
    }

    private static IsolationFacts Isolation(StrictJson json, JsonObject top, ExperimentLocation location)
    {
        if (!top.ContainsKey("isolation"))
        {
            return IsolationFacts.None;
        }

        var isolation = json.Object(top["isolation"], "isolation", IsolationMembers, IsolationMembers);
        return new IsolationFacts(
            Expanded(json, isolation, "closed", location),
            Expanded(json, isolation, "unlisted", location));
    }

    private static IReadOnlyList<string> Expanded(StrictJson json, JsonObject isolation, string name, ExperimentLocation location) =>
        [.. json.Texts(isolation, "isolation", name).Select((entry, index) => Expand(json, entry, $"isolation.{name}[{index}]", location))];

    /// <summary>One isolation entry with its placeholders expanded: <c>&lt;root&gt;</c> is the experiment root, a leading <c>~</c> the home directory.</summary>
    private static string Expand(StrictJson json, string entry, string where, ExperimentLocation location) =>
        entry switch
        {
            "" => entry,
            "<root>" => location.Root,
            _ when entry.StartsWith("<root>/", StringComparison.Ordinal) => Path.Combine(location.Root, entry["<root>/".Length..]),
            _ when entry.StartsWith("~/", StringComparison.Ordinal) => Path.Combine(location.Home, entry["~/".Length..]),
            _ when Path.IsPathFullyQualified(entry) => entry,
            _ => Refused(json, entry, where),
        };

    private static string Refused(StrictJson json, string entry, string where)
    {
        json.Add($"{where} is '{entry}' — it must be <root>, start with <root>/ or ~/, or be an absolute path");
        return entry;
    }
}
