using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Domain;
using Bench.Domain.Gate;

namespace Bench.Tests.Gate.Import;

/// <summary>The REDACTED calibration fixture (<c>Fixtures/gate-import/calib-phase2.redacted.json</c>): the whole phase-2
/// population of the operator's calibration — 92 <c>runs.jsonl</c> lines (84 cells, 8 of them re-run as a second
/// attempt), a placeholder reply per run carrying the recorded severities, the blinding key and 340 verdicts — over a
/// MADE-UP suite with the real task and seed ids, the four models' public rows, and the other harness's
/// <c>results.json</c> per-model numbers of the same day. No note, cluster text, path, commit sha, repository name or
/// finding text survives; the guard's sample names are the suite's private names.</summary>
internal static class ImportFixture
{
    private static readonly Lazy<JsonObject> Bundle = new(() =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gate-import", "calib-phase2.redacted.json")))!);

    public static JsonObject Root => Bundle.Value;

    /// <summary>Fresh COPIES of the lines — a test edits the one it reads, and the bundle is shared by every test.</summary>
    public static IReadOnlyList<JsonObject> Lines => [.. ((JsonArray)Root["lines"]!).Select(n => (JsonObject)n!.DeepClone())];

    public static JsonObject Line(string id) => Lines.Single(l => (string)l["id"]! == id);

    public static string SuiteJson => Root["suite"]!.ToJsonString();

    public static GateSuite Suite => GateSuiteFile.Parse(SuiteJson).Ok();

    public static PrivateNames Names => GatePrivateNames.Read(SuiteJson).Ok();

    public static IReadOnlyDictionary<string, CalibModel> Models => CalibModels.Parse(Root["models"]!.ToJsonString()).Ok();

    public static IReadOnlyList<JsonObject> Expected => [.. ((JsonArray)Root["expected"]!).Select(n => (JsonObject)n!)];

    public static JsonObject ExpectedFor(string model) => Expected.Single(e => (string)e["model"]! == model);

    /// <summary>Writes the fixture out in the other harness's own layout — <c>runs.jsonl</c>, <c>runs/&lt;id&gt;/reply.json</c>
    /// plus a turn-1 prompt file and a tap facts file per run, <c>assess-key/key.json</c>, <c>assess.jsonl</c> — so the import
    /// under test reads exactly what it reads on the operator's machine.</summary>
    public static void Materialize(string directory, bool withAssessment = true, Func<JsonObject, JsonObject>? edit = null)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "runs.jsonl"), Lines.Select(l => (edit ?? (x => x))((JsonObject)l.DeepClone()).ToJsonString()));

        foreach (var (id, reply) in (JsonObject)Root["replies"]!)
        {
            var run = Path.Combine(directory, "runs", id);
            Directory.CreateDirectory(Path.Combine(run, "answers"));
            Directory.CreateDirectory(Path.Combine(run, "tap"));
            File.WriteAllText(Path.Combine(run, "reply.json"), reply!.ToJsonString());
            File.WriteAllText(Path.Combine(run, "answers", "01-api-FeatureReview-0a.prompt"), $"the turn-1 prompt of {id}");
            File.WriteAllText(Path.Combine(run, "tap", "call-01.json"), """{"call": 1, "status": 200}""");
        }

        if (!withAssessment)
        {
            return;
        }

        Directory.CreateDirectory(Path.Combine(directory, "assess-key"));
        File.WriteAllText(Path.Combine(directory, "assess-key", "key.json"), Root["key"]!.ToJsonString());
        File.WriteAllLines(Path.Combine(directory, "assess.jsonl"), ((JsonArray)Root["assess"]!).Select(n => n!.ToJsonString()));
    }

    /// <summary>Every file under a directory with its bytes' hash — what "the import never writes to its source" is checked
    /// against.</summary>
    public static IReadOnlyDictionary<string, string> Snapshot(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(directory, f), f => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f))));
}
