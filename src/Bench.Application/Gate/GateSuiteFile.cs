using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Gate;
using Bench.Domain.Targets;

namespace Bench.Application.Gate;

/// <summary>Reads the operator's private suite file into a frozen <see cref="GateSuite"/>. The file lives outside git
/// (the artefact root, by §9 Q6); <c>samples/gate-suite.sample.json</c> documents the shape with made-up names.
/// <code>
/// { "id": "gate-seeded", "privateNames": ["..."],
///   "tasks": [ { "id": "cs2", "language": "C#", "gates": ["plan","code","feature"], "calibration": false,
///                "repository": "&lt;local path or url&gt;", "base": "&lt;40 hex&gt;", "variantHead": "&lt;40 hex&gt;",
///                "planPath": "docs/plan.md", "epics": [ ... ], "lessons": { ... }, "absentSubmodules": [ ... ],
///                "seeds": [ { "id": "cs2-S1", "file": "...", "old": "...", "new": "...", "what": "...",
///                             "trigger": "...", "mechanism": "...", "consequence": "...", "crossEpic": true } ] } ] }
/// </code>
/// <c>epics</c> and <c>lessons</c> may be JSON values or strings; either way the TEXT sent to <c>review_feature</c> is
/// what is hashed into the stamp. <c>planText</c> (optional) carries a plan the variant head does not commit
/// (<see cref="GateCase.PlanText"/>); <c>absentSubmodules</c> (optional) names submodules the task is measured without
/// (<see cref="GateCase.AbsentSubmodules"/>). Every refusal names the task and the field.</summary>
public static class GateSuiteFile
{
    public static Outcome<GateSuite> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            {
                return Outcome<GateSuite>.Failure("the suite file is not an object with a 'tasks' array");
            }

            var parsed = new List<GateTask>();
            foreach (var task in tasks.EnumerateArray())
            {
                var one = Task(task);
                if (one is Outcome<GateTask>.Fail fail)
                {
                    return Outcome<GateSuite>.Failure(fail.Reason);
                }

                parsed.Add(((Outcome<GateTask>.Ok)one).Value);
            }

            return GateSuite.Freeze(Text(root, "id"), parsed, Names(root));
        }
        catch (JsonException ex)
        {
            return Outcome<GateSuite>.Failure($"the suite file is not JSON — {ex.Message}");
        }
    }

    /// <summary>A frozen suite written back in the file's own shape — what an import that BUILDS a suite (the coai-bench
    /// cases) leaves in the artefact root, so its runs can be reported by suite file like any other. Parsing the text
    /// gives back the same stamp: the clone location is the only field not in the stamp, and the caller names it.</summary>
    public static string Json(GateSuite suite, Func<GateTask, string> repository) =>
        new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = suite.Id,
            ["privateNames"] = new System.Text.Json.Nodes.JsonArray([.. suite.PrivateNames.Select(n => (System.Text.Json.Nodes.JsonNode)n)]),
            ["tasks"] = new System.Text.Json.Nodes.JsonArray([.. suite.Tasks.Select(t => (System.Text.Json.Nodes.JsonNode)TaskJson(t, repository(t)))]),
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static System.Text.Json.Nodes.JsonObject TaskJson(GateTask task, string repository)
    {
        var json = TaskFields(task, repository);
        if (task.Case.PlanText.Length > 0)
        {
            json["planText"] = task.Case.PlanText;
        }

        if (task.Case.AbsentSubmodules.Count > 0)
        {
            json["absentSubmodules"] = new System.Text.Json.Nodes.JsonArray([.. task.Case.AbsentSubmodules.Select(p => (System.Text.Json.Nodes.JsonNode)p)]);
        }

        return json;
    }

    private static System.Text.Json.Nodes.JsonObject TaskFields(GateTask task, string repository) => new()
    {
        ["id"] = task.Id.Value,
        ["language"] = task.Language,
        ["gates"] = new System.Text.Json.Nodes.JsonArray([.. task.Hosts.Kinds.Select(k => (System.Text.Json.Nodes.JsonNode)k.ToString().ToLowerInvariant())]),
        ["calibration"] = task.IsCalibration,
        ["repository"] = repository,
        ["base"] = task.Case.Base.Value,
        ["variantHead"] = task.Case.VariantHead.Value,
        ["planPath"] = task.Case.PlanPath,
        ["epics"] = task.Case.Epics,
        ["lessons"] = task.Case.Lessons,
        ["seeds"] = new System.Text.Json.Nodes.JsonArray([.. task.Seeds.Select(s => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = s.Id.Value, ["file"] = s.File, ["old"] = s.Old, ["new"] = s.New, ["what"] = s.What,
            ["trigger"] = s.Trigger, ["mechanism"] = s.Mechanism, ["consequence"] = s.Consequence, ["crossEpic"] = s.CrossEpic,
        })]),
    };

    private static Outcome<GateTask> Task(JsonElement task)
    {
        var id = Text(task, "id");

        return GateTaskId.Parse(id).Match(
            taskId => Fields(taskId, task).Match(Outcome<GateTask>.Success, reason => Outcome<GateTask>.Failure($"task '{id}': {reason}")),
            Outcome<GateTask>.Failure);
    }

    private static Outcome<GateTask> Fields(GateTaskId id, JsonElement task)
    {
        var unknown = Strings(task, "gates").Where(w => !Gate(w).Any()).ToList();
        if (unknown.Count > 0)
        {
            return Outcome<GateTask>.Failure($"gate '{unknown[0]}' is not a gate — plan, code or feature; a word nobody recognises is refused, never dropped");
        }

        var hosts = HostedGates.Of([.. Strings(task, "gates").SelectMany(Gate)]);
        var @case = Case(task);
        var seeds = Seeds(task);
        var clone = CloneLocation.Parse(Text(task, "repository"));

        return (hosts, @case, seeds, clone) switch
        {
            (Outcome<HostedGates>.Fail f, _, _, _) => Outcome<GateTask>.Failure(f.Reason),
            (_, Outcome<GateCase>.Fail f, _, _) => Outcome<GateTask>.Failure(f.Reason),
            (_, _, Outcome<IReadOnlyList<SeedSpec>>.Fail f, _) => Outcome<GateTask>.Failure(f.Reason),
            (_, _, _, Outcome<CloneLocation>.Fail f) => Outcome<GateTask>.Failure(f.Reason),
            (Outcome<HostedGates>.Ok h, Outcome<GateCase>.Ok c, Outcome<IReadOnlyList<SeedSpec>>.Ok s, Outcome<CloneLocation>.Ok l) =>
                GateTask.Of(id, Text(task, "language"), h.Value, Flag(task, "calibration"), c.Value, s.Value, l.Value),
            _ => throw new InvalidOperationException("unreachable"),
        };
    }

    private static Outcome<GateCase> Case(JsonElement task) =>
        CommitSha.Parse(Text(task, "base")).Match(
            @base => CommitSha.Parse(Text(task, "variantHead")).Match(
                head => GateCase.Of(@base, head, Text(task, "planPath"), Raw(task, "epics"), Raw(task, "lessons"), Strings(task, "absentSubmodules"), Text(task, "planText")),
                reason => Outcome<GateCase>.Failure($"variantHead: {reason}")),
            reason => Outcome<GateCase>.Failure($"base: {reason}"));

    private static Outcome<IReadOnlyList<SeedSpec>> Seeds(JsonElement task)
    {
        var seeds = new List<SeedSpec>();

        if (!task.TryGetProperty("seeds", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return Outcome<IReadOnlyList<SeedSpec>>.Success(seeds);
        }

        foreach (var s in list.EnumerateArray())
        {
            var seed = SeedSpec.Of(Text(s, "id"), Text(s, "file"), Text(s, "old"), Text(s, "new"), Text(s, "what"),
                Text(s, "trigger"), Text(s, "mechanism"), Text(s, "consequence"), Flag(s, "crossEpic"));

            if (seed is Outcome<SeedSpec>.Fail fail)
            {
                return Outcome<IReadOnlyList<SeedSpec>>.Failure($"seed '{Text(s, "id")}': {fail.Reason}");
            }

            seeds.Add(((Outcome<SeedSpec>.Ok)seed).Value);
        }

        return Outcome<IReadOnlyList<SeedSpec>>.Success(seeds);
    }

    private static IEnumerable<GateKind> Gate(string word) => Enum.TryParse<GateKind>(word, ignoreCase: true, out var gate) ? [gate] : [];

    private static IReadOnlyList<string> Names(JsonElement root) => Strings(root, "privateNames");

    private static IReadOnlyList<string> Strings(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? [.. v.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString() ?? string.Empty)]
            : [];

    private static string Text(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    /// <summary>A JSON value as the text <c>review_feature</c> is sent: a string as itself, anything else compacted.</summary>
    private static string Raw(JsonElement o, string name) =>
        !o.TryGetProperty(name, out var v) ? string.Empty
        : v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty
        : JsonSerializer.Serialize(v);

    private static bool Flag(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
