using System.Text.Json.Nodes;
using FluentAssertions;
using ModelArms.Core.Steps;

namespace Bench.Tests.ModelArms;

/// <summary>What the fake arms of <see cref="ConsumerArms"/> hold, read back from the MODULE's own files for a test to compare a reader's report with (<c>TimingOf</c>, the arm's streams, <c>notes/review.json</c>, <c>objective.json</c>): never a number typed from the fixtures, so a fixture that changed changes the expectation with it.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class ConsumerFacts
{
    /// <summary>The coai round the gate stream of C1's first gate attempt holds: its verdict, its token counts and the decision the gate agent recorded for its one finding.</summary>
    internal sealed record GateRound(string Verdict, long TokensIn, long TokensOut, string Action, string Reason, int Findings);

    /// <summary>The distinct steps an arm STARTED, in the order they first started: what <c>metrics.mjs</c> names a step.</summary>
    public static IReadOnlyList<string> StepsOf(ConsumerArms arms, string arm) =>
        [.. arms.Root.TimingOf(arm).OfType<TimingLine.Start>().Select(start => start.Step).Distinct(StringComparer.Ordinal)];

    public static IReadOnlyList<TimingLine.Start> Starts(ConsumerArms arms, string arm, string step) =>
        [.. arms.Root.TimingOf(arm).OfType<TimingLine.Start>().Where(start => start.Step == step)];

    public static TimingLine.End EndOf(ConsumerArms arms, string arm, string step, int attempt) =>
        arms.Root.TimingOf(arm).OfType<TimingLine.End>().Single(end => end.Step == step && end.Attempt == attempt);

    /// <summary>The wall of every attempt of every step of the arm, summed as the module wrote it.</summary>
    public static long WallOf(ConsumerArms arms, string arm) => arms.Root.TimingOf(arm).OfType<TimingLine.End>().Sum(end => end.WallSeconds.Value);

    /// <summary>The result events of a stream, in order.</summary>
    public static IReadOnlyList<JsonNode> Results(string stream) =>
        [.. File.ReadAllLines(stream).Where(line => line.StartsWith('{')).Select(line => JsonNode.Parse(line)!).Where(node => node["type"]?.GetValue<string>() == "result")];

    /// <summary>The host's cost of an attempt: <c>total_cost_usd</c> of its last result, 0 when the result carries none (<c>result-success.jsonl</c> has no cost: E2 §8 item 62) - a test that needs a cost asserts it is above 0.</summary>
    public static double CostOf(ConsumerArms arms, string arm, string step, int attempt) =>
        Results(arms.PathsOf(arm).Stream(step, attempt)).Last()["total_cost_usd"]?.GetValue<double>() ?? 0;

    public static string TerminalOf(ConsumerArms arms, string arm, string step, int attempt) =>
        Results(arms.PathsOf(arm).Stream(step, attempt)).Last()["terminal_reason"]!.GetValue<string>();

    /// <summary>The verdict of the arm's <c>notes/review.json</c>, and its findings.</summary>
    public static JsonNode ReviewOf(ConsumerArms arms, string arm) => JsonNode.Parse(File.ReadAllText(Path.Combine(arms.PathsOf(arm).Notes, "review.json")))!;

    public static JsonNode ObjectiveOf(ConsumerArms arms, string arm) => JsonNode.Parse(File.ReadAllText(arms.PathsOf(arm).Objective))!;

    /// <summary>The transcript fixture the interrupted attempt was given: its model and the four counts of its one message.</summary>
    public static (string Model, JsonNode Usage) TranscriptFixture()
    {
        var message = JsonNode.Parse(ModelArmsFixtures.Read("consumers/transcript-interrupted.jsonl").Split('\n')[0])!["message"]!;
        return (message["model"]!.GetValue<string>(), message["usage"]!);
    }

    /// <summary>The gate round of the coai stream C1's first gate attempt wrote (its <c>tool_use</c> of <c>review_code</c>, the reply, the <c>resolve</c> with its decisions as a JSON STRING).</summary>
    public static GateRound GateRoundOf(ConsumerArms arms)
    {
        File.Exists(arms.PathsOf("C1").Stream("4-gate", 1)).Should().BeTrue("C1's gate left its first attempt's stream");
        var events = File.ReadAllLines(arms.PathsOf("C1").Stream("4-gate", 1)).Where(line => line.StartsWith('{')).Select(line => JsonNode.Parse(line)!).ToList();
        var contents = events.SelectMany(each => each["message"]?["content"]?.AsArray() ?? []).Where(part => part is not null).Select(part => part!).ToList();
        var reply = JsonNode.Parse(contents.First(part => part["type"]?.GetValue<string>() == "tool_result" && part["tool_use_id"]?.GetValue<string>() == "toolu_review")["content"]![0]!["text"]!.GetValue<string>())!;
        var decision = JsonNode.Parse(contents.First(part => part["type"]?.GetValue<string>() == "tool_use" && part["name"]?.GetValue<string>() == "mcp__coai__resolve")["input"]!["decisions"]!.GetValue<string>())![0]!;
        return new GateRound(reply["verdict"]!.GetValue<string>(), reply["cost"]!["tokensIn"]!.GetValue<long>(), reply["cost"]!["tokensOut"]!.GetValue<long>(), decision["action"]!.GetValue<string>(), decision["reason"]!.GetValue<string>(), reply["findings"]!.AsArray().Count);
    }

    /// <summary>The text of one section of <c>tables.md</c>: from the heading that starts <paramref name="heading"/> to the next heading.</summary>
    public static string Section(string tables, string heading)
    {
        var from = tables.IndexOf("\n" + heading, StringComparison.Ordinal);
        if (from < 0)
        {
            throw new InvalidOperationException($"tables.md has no section '{heading}'");
        }

        var next = tables.IndexOf("\n## ", from + heading.Length, StringComparison.Ordinal);
        return next < 0 ? tables[from..] : tables[from..next];
    }

    /// <summary>The row of a table section that starts <c>| &lt;arm&gt; |</c>.</summary>
    public static string RowOf(string section, string listed) =>
        section.Split('\n').Single(line => line.StartsWith($"| {listed} |", StringComparison.Ordinal));
}
