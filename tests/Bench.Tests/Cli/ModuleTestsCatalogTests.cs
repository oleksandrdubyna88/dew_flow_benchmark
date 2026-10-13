using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>research/module_tests.md</c> is DERIVED, not typed (the shared rule <c>common.scenario-tests</c>, point 4): the CLI's verbs are read
/// out of <c>Program.cs</c>'s dispatch, the scouts' sub-verbs out of <c>ScoutsCommand.cs</c>, the routes out of <c>src/Bench.Api</c> — and every one
/// must have its row, every row must name a flow that exists, and every row marked <c>covered</c> must name a test class this assembly holds.
/// Each scan has a companion asserting it still finds a known instance, so a reformatted source cannot make it pass by matching nothing.</summary>
public sealed partial class ModuleTestsCatalogTests
{
    private static string Root => Repository.Root;

    private sealed record Row(string Flow, string Status, string Test);

    [Fact]
    public void Every_derived_flow_has_its_row_and_every_row_names_a_flow_that_exists()
    {
        var derived = DerivedFlows();
        var rows = Rows().Select(r => r.Flow).ToList();

        derived.Except(rows).Should().BeEmpty("a verb or route added without its catalogue row is the gap the rule exists to make visible");
        rows.Except(derived).Should().BeEmpty("a row for a flow that no longer exists is a claim the catalogue cannot point at");
        rows.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_row_marked_covered_names_a_test_class_that_exists_and_every_other_row_says_why_not()
    {
        var classes = typeof(ModuleTestsCatalogTests).Assembly.GetTypes().Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        Rows().Where(r => r.Status == "covered").Should().OnlyContain(r => classes.Contains(r.Test), "an entry marked covered names its test, or it is not covered");
        Rows().Should().OnlyContain(r => r.Status == "covered" || r.Status == "not covered");
        Rows().Where(r => r.Flow.StartsWith("bench scouts ", StringComparison.Ordinal)).Should().OnlyContain(r => r.Status == "covered", "every scouts flow shipped with its scenario");
    }

    [Fact]
    public void The_scans_still_find_known_flows_in_each_source()
    {
        var derived = DerivedFlows();

        derived.Should().Contain(["bench scouts", "bench probes", "bench version", "bench scouts assess", "bench scouts tasks",
            "GET /api/bench/probes/runs/{id:guid}", "POST /api/bench/sessions/events", "GET /api/bench/gate/{gate}/runs"]);
        Rows().Should().Contain(r => r.Flow == "bench scouts report" && r.Test == "ScoutsScenarioTests");
    }

    private static IReadOnlyList<string> DerivedFlows() => [.. Verbs(), .. ScoutVerbs(), .. Routes()];

    /// <summary><c>"verb" =&gt;</c> inside <c>Program.Dispatch</c>.</summary>
    private static IEnumerable<string> Verbs()
    {
        var program = File.ReadAllText(Path.Combine(Root, "hosts", "Cli", "Program.cs"));
        var dispatch = program[program.IndexOf("private static int Dispatch(", StringComparison.Ordinal)..];
        dispatch = dispatch[..dispatch.IndexOf("\n    }", StringComparison.Ordinal)];

        return Arm().Matches(dispatch).Select(m => $"bench {m.Groups["verb"].Value}");
    }

    /// <summary><c>"sub-verb" =&gt; await</c> inside the scouts dispatch.</summary>
    private static IEnumerable<string> ScoutVerbs()
    {
        var scouts = File.ReadAllText(Path.Combine(Root, "hosts", "Cli", "ScoutsCommand.cs"));

        return ScoutArm().Matches(scouts).Select(m => $"bench scouts {m.Groups["verb"].Value}");
    }

    /// <summary><c>var x = app.MapGroup("/prefix")</c> then <c>x.MapGet("/route", …)</c>, per file of <c>src/Bench.Api</c>.</summary>
    private static IEnumerable<string> Routes() =>
        Directory.EnumerateFiles(Path.Combine(Root, "src", "Bench.Api"), "*.cs").Order(StringComparer.Ordinal).SelectMany(file =>
        {
            var text = File.ReadAllText(file);
            var groups = Group().Matches(text).GroupBy(m => m.Groups["var"].Value).ToDictionary(g => g.Key, g => g.First().Groups["prefix"].Value);

            return Map().Matches(text)
                .Where(m => groups.ContainsKey(m.Groups["var"].Value))
                .Select(m => $"{m.Groups["method"].Value.ToUpperInvariant()} {groups[m.Groups["var"].Value]}{m.Groups["route"].Value}");
        });

    private static IReadOnlyList<Row> Rows() =>
        [.. File.ReadAllLines(Path.Combine(Root, "research", "module_tests.md"))
            .Select(line => CatalogueRow().Match(line))
            .Where(m => m.Success)
            .Select(m => new Row(m.Groups["flow"].Value, m.Groups["status"].Value.Trim(), m.Groups["test"].Value))];

    /// <summary>Not anchored to the line start: the help arm is spelled <c>"" or "help" =&gt;</c>.</summary>
    [GeneratedRegex("""\"(?<verb>[a-z][a-z-]*)" =>""")]
    private static partial Regex Arm();

    [GeneratedRegex("""^\s*"(?<verb>[a-z][a-z-]*)" => await""", RegexOptions.Multiline)]
    private static partial Regex ScoutArm();

    [GeneratedRegex("""var (?<var>\w+) = app\.MapGroup\("(?<prefix>[^"]*)"\)""")]
    private static partial Regex Group();

    [GeneratedRegex(@"(?<var>\w+)\.Map(?<method>Get|Post|Put|Delete|Patch)\(""(?<route>[^""]*)""")]
    private static partial Regex Map();

    [GeneratedRegex(@"^\| `(?<flow>(?:bench [a-z -]+|(?:GET|POST|PUT|DELETE|PATCH) /[^`]*))` \| (?<status>[a-z ]+) \| (?:`(?<test>\w+)`|—) \|")]
    private static partial Regex CatalogueRow();
}
