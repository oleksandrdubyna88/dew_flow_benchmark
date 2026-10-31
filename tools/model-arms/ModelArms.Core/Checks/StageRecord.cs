using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Trace;
using ModelArms.Core.Steps;

namespace ModelArms.Core.Checks;

/// <summary>How one stage of <c>checks</c> ended: the tool's exit code (<c>Unavailable</c> when the wall killed it or it was never started — never a zero), the whole seconds it ran, and whether the wall killed it. A format stage over no changed file ran nothing: exit 0, no seconds, not killed.</summary>
public sealed record StageRun(CapturedCount Exit, long Seconds, bool Killed);

/// <summary>The four stages in the order <c>checks</c> runs them: the build, the main suite, the formatter and the extension's tests.</summary>
public sealed record StageRuns(StageRun Build, StageRun Suite, StageRun Format, StageRun Extension);

/// <summary><c>out/objective/stages.json</c> (plan C17, E3.S5): how each stage ended, written by a full run and read by <c>--parse-only</c> — <c>{"build":{"exit":0,"seconds":65,"killed":false},"suite":…,"format":…,"extension":…}</c>, compact, members in that order, <c>exit</c> JSON <c>null</c> when
/// it was not captured. Pure: the text in, the text out. The shell kept no such record and re-read its logs with every exit forced to 0 (<c>PARSE_ONLY</c>, <c>objective-checks.sh:14</c>), which claimed a build passed; here a record nobody made is four exits that are <see cref="Unrecorded"/> (E3 §8 item 10).</summary>
public static class StageRecord
{
    private const string NotRecordedReason = "no stage record was made for this arm — stages.json is absent";

    private static readonly string[] Members = ["build", "suite", "format", "extension"];

    /// <summary>Four exits nobody recorded: <c>--parse-only</c> over an arm that has no <c>stages.json</c>.</summary>
    public static StageRuns Unrecorded { get; } = new(Nothing(), Nothing(), Nothing(), Nothing());

    /// <summary>The four exits the report reads, in its own shape.</summary>
    public static StageExits Exits(StageRuns runs) => new(runs.Build.Exit, runs.Suite.Exit, runs.Format.Exit, runs.Extension.Exit);

    public static string Write(StageRuns runs) =>
        JsonText.Write(
            new JsonWriterOptions { Indented = false },
            writer =>
            {
                writer.WriteStartObject();
                Stage(writer, Members[0], runs.Build);
                Stage(writer, Members[1], runs.Suite);
                Stage(writer, Members[2], runs.Format);
                Stage(writer, Members[3], runs.Extension);
                writer.WriteEndObject();
            });

    /// <summary>The record <paramref name="json"/> holds, or the first thing wrong with it: not JSON, not an object, a stage missing or not an object, an <c>exit</c> that is neither a whole number nor <c>null</c> (a stage with NO <c>exit</c>, or a JSON <c>null</c>, is read as not captured — never as 0), <c>seconds</c> or <c>killed</c> missing or of the wrong kind. A torn record is refused, never read as zeros.</summary>
    public static Outcome<StageRuns> Read(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? Stages(document.RootElement)
                : Outcome<StageRuns>.Failure("stages.json is not a JSON object");
        }
        catch (JsonException ex)
        {
            return Outcome<StageRuns>.Failure($"stages.json is not JSON: {ex.Message}");
        }
    }

    private static StageRun Nothing() => new(CapturedCount.Unavailable(NotRecordedReason), 0, Killed: false);

    private static void Stage(Utf8JsonWriter writer, string name, StageRun run)
    {
        writer.WriteStartObject(name);
        if (run.Exit.WasCaptured)
        {
            writer.WriteNumber("exit", run.Exit.Value);
        }
        else
        {
            writer.WriteNull("exit");
        }

        writer.WriteNumber("seconds", run.Seconds);
        writer.WriteBoolean("killed", run.Killed);
        writer.WriteEndObject();
    }

    private static Outcome<StageRuns> Stages(JsonElement top)
    {
        var read = new List<StageRun>();
        foreach (var name in Members)
        {
            var one = One(top, name);
            if (one is Outcome<StageRun>.Fail failed)
            {
                return Outcome<StageRuns>.Failure(failed.Reason);
            }

            read.Add(((Outcome<StageRun>.Ok)one).Value);
        }

        return Outcome<StageRuns>.Success(new StageRuns(read[0], read[1], read[2], read[3]));
    }

    private static Outcome<StageRun> One(JsonElement top, string name)
    {
        if (!top.TryGetProperty(name, out var stage) || stage.ValueKind != JsonValueKind.Object)
        {
            return Outcome<StageRun>.Failure($"stages.json: '{name}' must be an object holding exit, seconds and killed");
        }

        var exit = Exit(stage);
        var seconds = stage.TryGetProperty("seconds", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var whole) && whole >= 0 ? whole : -1;
        var killed = stage.TryGetProperty("killed", out var k) && k.ValueKind is JsonValueKind.True or JsonValueKind.False ? k.GetBoolean() : (bool?)null;
        return exit is null || seconds < 0 || killed is null
            ? Outcome<StageRun>.Failure($"stages.json: '{name}' must hold exit (a whole number or null), seconds (a whole number) and killed (true or false)")
            : Outcome<StageRun>.Success(new StageRun(exit, seconds, killed.Value));
    }

    /// <summary>The exit: a whole number, or not captured when absent or <c>null</c>; <c>null</c> (the C# one) for anything else.</summary>
    private static CapturedCount? Exit(JsonElement stage) =>
        !stage.TryGetProperty("exit", out var exit) || exit.ValueKind == JsonValueKind.Null ? CapturedCount.Unavailable(TimingLines.NotRecorded)
        : exit.ValueKind == JsonValueKind.Number && exit.TryGetInt64(out var number) ? CapturedCount.Number(number)
        : null;
}
