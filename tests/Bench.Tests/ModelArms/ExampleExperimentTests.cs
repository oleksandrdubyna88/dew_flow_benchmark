using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;
using ArmsProgram = ModelArms.Program;
using ModelArmsApp = ModelArms;

namespace Bench.Tests.ModelArms;

/// <summary>The worked example (D24) and the <c>init</c> template derived from it (D18): the example is the one source, <c>validate</c> accepts it once its paths
/// exist, and the template is that file with its values blanked — same members, same array lengths, the comments kept.
/// <para>
/// What this does NOT prove: that a template filled by hand is accepted (<c>InitVerbTests</c> fills it with the fixture's text and validates), that the example
/// is a GOOD experiment (only that <c>validate</c> accepts its shape and its paths on a fixture host), or anything on Windows, where <c>isolation</c> is refused.
/// </para></summary>
public sealed partial class ExampleExperimentTests
{
    private static readonly string[] KeptMembers = ["schema", "scenarios", "baseline"];

    private static string SourceExample => Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json");

    [Fact]
    public async Task The_shipped_part6_example_validates_over_a_fixture_root_once_its_paths_exist()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the example's isolation section is refused on Windows, by design");
        using var root = await new ExperimentRoot().WithTargetAsync();
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await ArmsProgram.RunAsync(["validate", "--experiment", root.Root], output, error, root.Environment, TestContext.Current.CancellationToken);

        code.Should().Be(0, error.ToString());
        output.ToString().Should().Contain("is valid on this host");
    }

    [Fact]
    public void The_example_travels_beside_the_binary_as_the_file_the_source_tree_holds()
    {
        var beside = ModelArmsApp.ModuleFiles.Beside(AppContext.BaseDirectory).ExampleFile;

        File.Exists(beside).Should().BeTrue("the content item copies the example to the output folder, and init reads it from there");
        File.ReadAllText(beside).Should().Be(File.ReadAllText(SourceExample));
    }

    [Fact]
    public void The_init_template_is_the_example_with_its_values_blanked()
    {
        var example = File.ReadAllText(SourceExample);

        var template = ExperimentTemplate.From(example);

        Shape(template).Should().Equal(Shape(example), "the template has every member of the example, at the same path, of the same kind and with the same array lengths");
        Violations(template, example).Should().BeEmpty("every string is <fill me> except schema, scenarios and baseline, and every number is 0");
        Comments(example).Should().NotBeEmpty("a scan that finds no comment passes forever");
        Comments(example).Where(comment => !template.Contains(comment, StringComparison.Ordinal)).Should().BeEmpty("the example's comments are the template's instructions");
    }

    [Fact]
    public void A_comment_sits_after_the_name_of_the_member_it_describes_not_after_the_value_before_it()
    {
        var template = ExperimentTemplate.From(File.ReadAllText(SourceExample));
        var comment = Comments(File.ReadAllText(SourceExample)).First(text => text.StartsWith("The repository the arms work on", StringComparison.Ordinal));

        var named = template.IndexOf("\"target\":", StringComparison.Ordinal);
        var commented = template.IndexOf(comment, StringComparison.Ordinal);
        var firstInside = template.IndexOf("\"source\":", StringComparison.Ordinal);

        named.Should().BeGreaterThan(0, "a scan that finds no member passes forever");
        commented.Should().BeGreaterThan(named, "the writer puts a comment after the value written before it, which is the previous member's; the template moves it behind the name it describes");
        commented.Should().BeLessThan(firstInside);
    }

    [Fact]
    public void A_template_is_read_back_by_the_experiment_reader_with_its_comments_and_every_required_member()
    {
        var template = ExperimentTemplate.From(File.ReadAllText(SourceExample));

        var read = ExperimentFile.Read(template, new ExperimentLocation(Path.GetFullPath("/example/root"), string.Empty, string.Empty, string.Empty));

        read.Match(_ => string.Empty, reason => reason).Split('\n').Should().NotContain(line => line.Contains("is missing", StringComparison.Ordinal));
    }

    /// <summary>One line per node of the document: its path, its kind and, for an array, its length. Comments are skipped.</summary>
    private static IReadOnlyList<string> Shape(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        return [.. Nodes(document.RootElement, string.Empty).Select(node => $"{node.Path}:{node.Element.ValueKind}{Length(node.Element)}")];
    }

    private static string Length(JsonElement element) => element.ValueKind == JsonValueKind.Array ? $"[{element.GetArrayLength()}]" : string.Empty;

    /// <summary>The leaves that are not what the template rule says: a string other than <c>&lt;fill me&gt;</c> where the example's value is not kept, a kept
    /// member that differs from the example, a number other than 0.</summary>
    private static IReadOnlyList<string> Violations(string template, string example)
    {
        using var blanked = JsonDocument.Parse(template, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        using var original = JsonDocument.Parse(example, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var originals = Nodes(original.RootElement, string.Empty).ToDictionary(node => node.Path, node => node.Element);
        return [.. Nodes(blanked.RootElement, string.Empty).Select(node => Violation(node, originals)).Where(text => text.Length > 0)];
    }

    private static string Violation((string Path, JsonElement Element) node, Dictionary<string, JsonElement> originals) =>
        node.Element.ValueKind switch
        {
            JsonValueKind.String when KeptMembers.Contains(node.Path) => node.Element.GetString() == originals[node.Path].GetString() ? string.Empty : $"{node.Path} was not kept",
            JsonValueKind.String => node.Element.GetString() == "<fill me>" ? string.Empty : $"{node.Path} is '{node.Element.GetString()}', not <fill me>",
            JsonValueKind.Number => node.Element.GetDouble() == 0 ? string.Empty : $"{node.Path} is {node.Element}, not 0",
            _ => string.Empty,
        };

    private static IEnumerable<(string Path, JsonElement Element)> Nodes(JsonElement element, string path)
    {
        yield return (path, element);
        var children = element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Select(property => (Path: path.Length == 0 ? property.Name : $"{path}.{property.Name}", property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select((item, index) => (Path: $"{path}[{index}]", Value: item)),
            _ => [],
        };
        foreach (var child in children.SelectMany(child => Nodes(child.Value, child.Path)))
        {
            yield return child;
        }
    }

    /// <summary>The text of each whole-line <c>//</c> comment of the example, trimmed.</summary>
    private static IReadOnlyList<string> Comments(string example) =>
        [.. WholeLineComment().Matches(example).Select(match => match.Groups["text"].Value.Trim())];

    [GeneratedRegex(@"^\s*//(?<text>.*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex WholeLineComment();
}
