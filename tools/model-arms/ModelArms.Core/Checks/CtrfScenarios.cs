using System.Text.Json;
using System.Text.RegularExpressions;
using Bench.Domain;
using ModelArms.Core.Experiment;

namespace ModelArms.Core.Checks;

/// <summary>One test of a CTRF report: its full name and its status word (<c>passed</c>, <c>failed</c>, <c>skipped</c>, <c>pending</c>, <c>other</c>).</summary>
public sealed record CtrfTest(string Name, string Status);

/// <summary>The suite's CTRF report (<c>mcp.ctrf.json</c>, written by the target's own test run) read for the scenarios (<c>objective-checks.sh:38-47</c>).</summary>
public static class CtrfScenarios
{
    private const string NotPassing = "absent";

    /// <summary>The tests of the report, in its order, or why it is not a CTRF report: not JSON (or cut short), not an object, no <c>results</c> object, no <c>tests</c> array, or a test without a string <c>name</c> and <c>status</c>. The shell's
    /// <c>JSON.parse</c> crashed on the first of these and read a missing <c>results</c> as no tests; here a report that is not one is a refusal with its reason, and the caller reads no tests and SAYS why (E3 §8 item 14).</summary>
    public static Outcome<IReadOnlyList<CtrfTest>> Tests(string ctrf)
    {
        try
        {
            using var document = JsonDocument.Parse(ctrf);
            return TestsOf(document.RootElement);
        }
        catch (JsonException cut)
        {
            return Outcome<IReadOnlyList<CtrfTest>>.Failure($"the report is not valid JSON ({cut.Message})");
        }
    }

    /// <summary>What a scenario's tests say. A <c>node</c> scenario is read from the extension's log: <c>passed</c>, <c>failed</c>, or <c>absent</c> when the log names it as neither. Any other kind is read from the CTRF: the tests whose name holds the scenario's
    /// name as a whole word — none is <c>absent</c>, all of them passed is <c>passed</c>, otherwise the distinct statuses in the order they appear, joined by <c>+</c> (<c>failed</c>, <c>passed+failed</c>). The name is a literal and never a pattern (E3 §8 item 13).</summary>
    public static string Passing(Scenario scenario, IReadOnlyList<CtrfTest> tests, NodeTestLog node) =>
        scenario.Kind == "node" ? FromNode(scenario.Name, node) : FromCtrf(scenario.Name, tests);

    private static string FromNode(string name, NodeTestLog node) =>
        node.Passed.Contains(name) ? "passed" : node.Failed.Contains(name) ? "failed" : NotPassing;

    private static string FromCtrf(string name, IReadOnlyList<CtrfTest> tests)
    {
        var word = new Regex($@"\b{Regex.Escape(name)}\b", RegexOptions.ECMAScript);
        var statuses = tests.Where(test => word.IsMatch(test.Name)).Select(test => test.Status).Distinct(StringComparer.Ordinal).ToList();
        return statuses switch
        {
            [] => NotPassing,
            ["passed"] => "passed",
            _ => string.Join('+', statuses),
        };
    }

    private static Outcome<IReadOnlyList<CtrfTest>> TestsOf(JsonElement top)
    {
        if (top.ValueKind != JsonValueKind.Object)
        {
            return Fail($"the report is a JSON {top.ValueKind.ToString().ToLowerInvariant()}, not an object — a CTRF report is an object with results.tests");
        }

        if (Member(top, "results", JsonValueKind.Object) is not { } results)
        {
            return Fail("the report has no \"results\" object — a CTRF report keeps its tests in results.tests");
        }

        return Member(results, "tests", JsonValueKind.Array) is { } tests ? Listed(tests) : Fail("the report's \"results\" has no \"tests\" array");
    }

    private static JsonElement? Member(JsonElement parent, string name, JsonValueKind kind) =>
        parent.TryGetProperty(name, out var found) && found.ValueKind == kind ? found : null;

    private static Outcome<IReadOnlyList<CtrfTest>> Listed(JsonElement tests)
    {
        var read = new List<CtrfTest>(tests.GetArrayLength());
        foreach (var test in tests.EnumerateArray())
        {
            if (Text(test, "name") is not { } name || Text(test, "status") is not { } status)
            {
                return Fail($"results.tests[{read.Count}] has no string \"name\" and \"status\"");
            }

            read.Add(new CtrfTest(name, status));
        }

        return Outcome<IReadOnlyList<CtrfTest>>.Success(read);
    }

    private static string? Text(JsonElement test, string member) =>
        test.ValueKind == JsonValueKind.Object && test.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Outcome<IReadOnlyList<CtrfTest>> Fail(string reason) => Outcome<IReadOnlyList<CtrfTest>>.Failure(reason);
}
