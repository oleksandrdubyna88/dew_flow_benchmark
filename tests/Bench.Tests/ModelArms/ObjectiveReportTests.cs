using System.Security.Cryptography;
using System.Text.Json;
using Bench.Domain;
using Bench.Domain.Trace;
using FluentAssertions;
using ModelArms.Core.Checks;
using ModelArms.Core.Experiment;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The report's text (C13, <c>objective-checks.sh:53-67</c>). The proof of "the shell's shape" is the shell's own reader, run as an oracle over a repository and the three recorded logs and compared byte for byte
/// (<see cref="ObjectiveBench"/>); the part-6/7 judges read only <c>objective.json</c>, so that round trip stands for them (D28). The shape test reads part 6's real C1 report for its member paths.
/// <para>
/// What this does NOT prove: that the shell's report and the port's agree where the shell's own code is wrong for a name — a scenario name holding a pattern character (the port matches it as a literal, E3 §8 item 13), a key the
/// JavaScript object would re-order (an id that reads as an array index) or a lone surrogate (the CTRF reader refuses one; the writer would quietly write U+FFFD for one that reached it); that the facts <c>checks</c> will gather (E3.S5) are the facts this test gathers.
/// </para></summary>
public sealed class ObjectiveReportTests
{
    private const string PartSixSha256 = "4b051e3e378c7b52f0353d4e01c8cd218a3f44a4f5ec6499fadc361b864bfa26";

    private static string Shaped() => ObjectiveReport.Write(PartSixLikeFacts());

    /// <summary>Facts with every member the part-6 report has a value of the same JSON kind for: counts captured, a scenario, files, a stat.</summary>
    private static ObjectiveFacts PartSixLikeFacts() =>
        ObjectiveFactsKit.Facts() with
        {
            SuiteLog = "  total: 3\n  succeeded: 2\n  skipped: 1\n",
            ExtensionLog = "ℹ tests 5\n",
            Scenarios = [new Scenario("S1", "cs", "A")],
            Present = new Dictionary<string, bool> { ["S1"] = true },
            Numstat = "1\t2\tsrc_mcp/src/A.cs\n",
            Shortstat = "1 file changed, 1 insertion(+), 2 deletions(-)",
        };

    [Fact]
    public async Task The_report_of_a_real_arm_round_trips_the_shells_shape()
    {
        using var bench = await ObjectiveBench.CreateAsync();
        var facts = await bench.FactsAsync();

        var oracle = await bench.RunOracleAsync(TestContext.Current.CancellationToken);
        var written = ObjectiveReport.Write(facts);

        oracle.Report.Should().EndWith("\n}", "the shell writes no trailing newline");
        var expected = oracle.Report[..^2] + ",\n  \"stepsOnDeadline\": []\n}";
        written.Should().Be(expected, "the port's text is the shell's, byte for byte, with the one additive member last");
        JsonDocument.Parse(oracle.Report).RootElement.GetProperty("scenarios").GetProperty("S3").GetProperty("passing").GetString().Should().Be("passed+failed", "the oracle's world holds every outcome");
    }

    [Fact]
    public void The_shape_is_the_part6_arms_every_member_in_its_order()
    {
        var recorded = ModelArmsFixtures.Path("objective/objective-part6-C1.json");
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(recorded))).Should().Be(PartSixSha256, "the recorded report is copied as it was, and no token is in it");
        var expected = ObjectiveFactsKit.Paths(ObjectiveFactsKit.Parse(File.ReadAllText(recorded))).Append("stepsOnDeadline: array");

        var paths = ObjectiveFactsKit.Paths(ObjectiveFactsKit.Parse(Shaped()));

        paths.Should().Equal(expected);
        paths.Should().Contain("scenarios.*.passing: string", "the scenario map's entries are read once, so the map is not skipped");
    }

    [Fact]
    public void Steps_that_ended_on_a_deadline_are_listed_under_stepsOnDeadline()
    {
        ObjectiveReport.Write(ObjectiveFactsKit.Facts() with { StepsOnDeadline = ["3-fix"] }).Should().EndWith("\"stepsOnDeadline\": [\n    \"3-fix\"\n  ]\n}");
        ObjectiveReport.Write(ObjectiveFactsKit.Facts()).Should().EndWith("\"stepsOnDeadline\": []\n}", "an empty list says none ended on its deadline; the member is always there");
    }

    [Fact]
    public async Task The_summary_line_is_the_shells()
    {
        using var bench = await ObjectiveBench.CreateAsync();
        var facts = await bench.FactsAsync();

        var oracle = await bench.RunOracleAsync(TestContext.Current.CancellationToken);

        oracle.SummaryLine.Should().StartWith("{\"arm\":\"C1\",\"build\":true");
        ObjectiveReport.Summary(facts).Should().Be(oracle.SummaryLine);
    }

    [Fact]
    public void Arm_written_strings_are_escaped_and_parse_back_exactly()
    {
        const string Hostile = "\"},\"x\":{\"";
        string[] scenarioNames = ["a \"quoted\" name", "back\\slash", "new\nline", "tab\there", "control\u0001char", "ёлка", "emoji 😀 name", Hostile];
        string[] oneWord = ["q\"uote", "back\\slash", "ctl\u0001", "ёлка", "😀", Hostile];
        string[] paths = ["dir/q\"uote.cs", "dir/back\\slash.cs", "dir/ctl\u0001.cs", "dir/ёлка.cs", "dir/😀.cs", $"dir/{Hostile}.cs", "dir/with space.cs"];
        var scenarios = scenarioNames.Select((name, i) => new Scenario($"S{i}{(i == 7 ? Hostile : string.Empty)}", i % 2 == 0 ? "cs" : "node", name)).ToList();
        var facts = ObjectiveFactsKit.Facts() with
        {
            Scenarios = scenarios,
            Present = scenarios.ToDictionary(s => s.Id, _ => true),
            SuiteLog = string.Join('\n', oneWord.Select(name => $"failed {ObjectiveFactsKit.Prefix}{name} (1ms)")) + "\n  total: 3\n  succeeded: 2\n  skipped: 1\n",
            ExtensionLog = string.Join('\n', scenarioNames.Where(name => !name.Contains('\n')).Select(name => $"✖ {name} (1ms)")) + "\nℹ tests 5\n",
            Numstat = string.Join('\n', paths.Select(path => $"1\t1\t{path}")),
            StepsOnDeadline = ["3-fix \"late\""],
        };

        var text = ObjectiveReport.Write(facts);

        var report = ObjectiveFactsKit.Parse(text);
        ObjectiveFactsKit.Paths(report).Should().Equal(ObjectiveFactsKit.Paths(ObjectiveFactsKit.Parse(Shaped())), "an awkward string is a value and never a member");
        scenarios.Select(s => ObjectiveFactsKit.Value(report, "scenarios", s.Id, "name")).Should().Equal(scenarioNames);
        ObjectiveFactsKit.Strings(report, "diff", "files").Should().Equal(paths);
        ObjectiveFactsKit.Strings(report, "mcp", "failedNew").Should().Equal(oneWord);
        ObjectiveFactsKit.Strings(report, "ts", "failedNew").Should().Equal(scenarioNames.Where(name => !name.Contains('\n')));
        ObjectiveFactsKit.Strings(report, "stepsOnDeadline").Should().Equal("3-fix \"late\"");
    }

    [Fact]
    public void Unknown_exits_are_null_and_never_pass()
    {
        var unknown = ObjectiveReport.Write(ObjectiveFactsKit.Facts() with { Exits = ObjectiveFactsKit.UnknownExits(), Commits = CapturedCount.Unavailable("x") });
        var known = ObjectiveReport.Write(ObjectiveFactsKit.Facts());

        var report = ObjectiveFactsKit.Parse(unknown);
        ObjectiveFactsKit.Value(report, "build").Should().Be("false", "a build that did not report an exit did not pass");
        ObjectiveFactsKit.Value(report, "format").Should().Be("false");
        ObjectiveFactsKit.Value(report, "mcp", "exit").Should().Be("null");
        ObjectiveFactsKit.Value(report, "ts", "exit").Should().Be("null");
        ObjectiveFactsKit.Value(report, "commits").Should().Be("null");
        unknown.Should().Contain("\"exit\": null");

        var passed = ObjectiveFactsKit.Parse(known);
        ObjectiveFactsKit.Value(passed, "build").Should().Be("true", "the same facts with exits of 0 pass");
        ObjectiveFactsKit.Value(passed, "mcp", "exit").Should().Be("0");
    }
}
