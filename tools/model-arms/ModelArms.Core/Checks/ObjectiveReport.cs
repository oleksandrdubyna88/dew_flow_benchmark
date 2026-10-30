using System.Text.Json;
using Bench.Domain.Trace;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Checks;

/// <summary>The scenario table of the report: each scenario of <c>scenarios.json</c>, whether the target's tests name it, and what the suites say of it.</summary>
internal sealed record ScenarioRow(Scenario Scenario, bool Present, string Passing);

/// <summary>What the report derives from the facts before it writes a member: the parsed logs, the scenario rows, the new failures and the diff.</summary>
internal sealed record ObjectiveReading(
    MtpSummary Mtp,
    NodeTestLog Node,
    IReadOnlyList<ScenarioRow> Scenarios,
    IReadOnlyList<string> McpNew,
    IReadOnlyList<string> McpBaseline,
    IReadOnlyList<string> TsNew,
    IReadOnlyList<NumstatRow> Rows)
{
    public static ObjectiveReading Of(ObjectiveFacts facts)
    {
        var mtp = TestLogSummary.Read(facts.SuiteLog, facts.NamePrefix);
        var node = TsTestLog.Read(facts.ExtensionLog);
        var tests = facts.Ctrf.Match(read => CtrfScenarios.Tests(read).Match(parsed => parsed, _ => []), _ => []);
        var known = facts.Baseline.McpFailed.ToHashSet(StringComparer.Ordinal);
        return new ObjectiveReading(
            mtp,
            node,
            [.. facts.Scenarios.Select(s => new ScenarioRow(s, facts.Present.GetValueOrDefault(s.Id), CtrfScenarios.Passing(s, tests, node)))],
            [.. mtp.Failed.Where(name => !known.Contains(name))],
            [.. mtp.Failed.Where(known.Contains)],
            [.. node.Failed.Where(name => !facts.Baseline.TsFailed.Contains(name, StringComparer.Ordinal))],
            DiffStats.Rows(facts.Numstat));
    }

    public int PresentCount => Scenarios.Count(row => row.Present);

    public int PassingCount => Scenarios.Count(row => row.Passing == "passed");
}

/// <summary><c>objective.json</c> and the line <c>checks</c> prints, in the shell's shape (<c>objective-checks.sh:53-67</c>, §4.9) — the one report the part-6 and part-7 judges read. Every member of the shell's object, in its order, then one
/// additive member, <c>stepsOnDeadline</c>: the steps whose <c>.done</c> says they ended on their deadline (D8), always present, <c>[]</c> when none did. What is not known is <c>null</c>, never a number that was not measured: a count a log
/// does not carry, an exit that was not recorded (a tool killed at its wall, a re-read of logs nobody ran), a commit count git did not give. A build or a format whose exit is not known did not pass (E3 §8 item 10).
/// <para>
/// A member's text is escaped by the writer, whatever an arm named. Reading a file or running git is not here: the caller gathers the facts (E3.S5).
/// </para></summary>
public static class ObjectiveReport
{
    public static string Write(ObjectiveFacts facts)
    {
        var reading = ObjectiveReading.Of(facts);
        return ObjectiveJson.Indented(writer => Report(writer, facts, reading));
    }

    /// <summary>The shell's one line: the arm, the two passes, the count of new failures of each suite, how many scenarios are present and passing and the diff's shortstat, as compact JSON.</summary>
    public static string Summary(ObjectiveFacts facts)
    {
        var reading = ObjectiveReading.Of(facts);
        return ObjectiveJson.OneLine(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("arm", facts.Arm);
            writer.WriteBoolean("build", Passed(facts.Exits.Build));
            writer.WriteBoolean("format", Passed(facts.Exits.Format));
            writer.WriteNumber("mcpNewFailures", reading.McpNew.Count);
            writer.WriteNumber("tsNewFailures", reading.TsNew.Count);
            writer.WriteString("scenarios", $"{reading.PresentCount}/{facts.Scenarios.Count} present, {reading.PassingCount} passing");
            writer.WriteString("stat", Shortstat(facts));
            writer.WriteEndObject();
        });
    }

    private static bool Passed(CapturedCount exit) => exit is { WasCaptured: true, Value: 0 };

    private static string Shortstat(ObjectiveFacts facts) => facts.Shortstat.Trim();

    private static void Report(Utf8JsonWriter writer, ObjectiveFacts facts, ObjectiveReading reading)
    {
        writer.WriteStartObject();
        writer.WriteString("arm", facts.Arm);
        writer.WriteString("head", facts.HeadShort);
        writer.Count("commits", facts.Commits);
        writer.WriteNumber("uncommittedFiles", facts.UncommittedFiles);
        writer.WriteBoolean("build", Passed(facts.Exits.Build));
        writer.WriteBoolean("format", Passed(facts.Exits.Format));
        Mcp(writer, facts, reading);
        Ts(writer, facts, reading);
        Scenarios(writer, reading);
        Diff(writer, facts, reading);
        writer.Strings("stepsOnDeadline", facts.StepsOnDeadline);
        writer.WriteEndObject();
    }

    private static void Mcp(Utf8JsonWriter writer, ObjectiveFacts facts, ObjectiveReading reading)
    {
        writer.WriteStartObject("mcp");
        writer.Count("exit", facts.Exits.Suite);
        writer.Count("total", reading.Mtp.Total);
        writer.Count("passed", reading.Mtp.Succeeded);
        writer.Count("skipped", reading.Mtp.Skipped);
        writer.Strings("failedNew", reading.McpNew);
        writer.Strings("failedBaseline", reading.McpBaseline);
        writer.WriteEndObject();
    }

    private static void Ts(Utf8JsonWriter writer, ObjectiveFacts facts, ObjectiveReading reading)
    {
        writer.WriteStartObject("ts");
        writer.Count("exit", facts.Exits.Extension);
        writer.Count("total", reading.Node.Tests);
        writer.Strings("failedNew", reading.TsNew);
        writer.WriteEndObject();
    }

    private static void Scenarios(Utf8JsonWriter writer, ObjectiveReading reading)
    {
        writer.WriteStartObject("scenarios");
        foreach (var row in reading.Scenarios)
        {
            writer.WriteStartObject(row.Scenario.Id);
            writer.WriteString("name", row.Scenario.Name);
            writer.WriteBoolean("present", row.Present);
            writer.WriteString("passing", row.Passing);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteNumber("scenariosPresentCount", reading.PresentCount);
        writer.WriteNumber("scenariosPassingCount", reading.PassingCount);
    }

    private static void Diff(Utf8JsonWriter writer, ObjectiveFacts facts, ObjectiveReading reading)
    {
        var lines = DiffStats.Split(reading.Rows);
        writer.WriteStartObject("diff");
        writer.WriteString("shortstat", Shortstat(facts));
        writer.WriteStartObject("changedLines");
        writer.WriteNumber("tests", lines.Tests);
        writer.WriteNumber("src", lines.Src);
        writer.WriteNumber("docs", lines.Docs);
        writer.WriteEndObject();
        writer.Strings("files", reading.Rows.Select(row => row.File));
        writer.WriteEndObject();
    }
}
