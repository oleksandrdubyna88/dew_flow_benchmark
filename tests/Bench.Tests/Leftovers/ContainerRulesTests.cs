using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Leftovers;

/// <summary>
/// Scenario (7), E6 S3: no <c>.cs</c> file of the repository switches the Testcontainers reaper (Ryuk) off, in any spelling the guards miss. Neither guard reads
/// <c>Environment.SetEnvironmentVariable(&lt;the name&gt;, "true")</c> (their <c>ryuk-off</c> wants a <c>:</c> or <c>=</c> after the name inside the text), so this test is that
/// spelling's ONLY reader. Per FILE (not per line, so a join or an assignment that a formatter breaks over lines is read), after the string literals joined with <c>+</c> are joined (the text between a literal's closing quote, a <c>+</c> and the next literal's
/// opening quote, <c>@</c> and <c>$</c> prefixes included, is removed, across line breaks too), it is red on two forms: <b>(a)</b> the variable named, in any case, whole or assembled from split
/// literals; <b>(b)</b> an assignment of <c>false</c> to the setting that Testcontainers acts on (an equals sign that is not <c>==</c>, any whitespace around it, the
/// <c>TestcontainersSettings.</c> qualifier optional).
/// <para>
/// The files of <c>template-manifest.json</c> are skipped: the copy check holds them to the template's bytes, and five lines of the copied guard's own test data name the variable
/// today. So benchmark's own files never assemble the name from <c>+</c>-joined literals (<see cref="ContainerPass.RyukVariable"/> is built with one <c>string.Concat</c>), and the
/// planted lines below are built the same way.
/// </para>
/// <para>
/// What it does NOT read: a name assembled from identifiers or through <c>string.Concat</c>, and a setter reached by reflection. A builder's <c>WithCleanUp(false)</c> is read
/// by nothing here; the fixture's own builder is covered by scenario (1)'s run-time proof.
/// </para></summary>
public sealed class ContainerRulesTests
{
    private const string VariableForm = "the reaper's variable is named";

    private const string SettingForm = "the reaper's setting is assigned false";

    private static readonly Regex JoinedLiterals = new("\"\\s*\\+\\s*[@$]*\"", RegexOptions.CultureInvariant);

    private static readonly Regex SettingAssigned = new(@"ResourceReaperEnabled\s*=(?!=)\s*false\b", RegexOptions.CultureInvariant);

    private static readonly Regex VariableNamed = new(Regex.Escape(ContainerPass.RyukVariable), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The forms a file's text shows, each with the line (1-based, in the ORIGINAL text) where its match starts. Pure: the planted texts of the theory go through the same function as the repository's files.</summary>
    internal static IReadOnlyList<(int Line, string Form)> ScanText(string text)
    {
        var (joined, removed) = JoinLiterals(text);
        return [.. Matches(VariableNamed, joined, VariableForm, text, removed), .. Matches(SettingAssigned, joined, SettingForm, text, removed)];
    }

    internal static IReadOnlyList<string> FormsOf(string text) => [.. ScanText(text).Select(finding => finding.Form)];

    /// <summary>The text with every <c>"a" + "b"</c> joined to <c>"ab"</c>, and where the cuts were: (position in the joined text, length removed there).</summary>
    private static (string Joined, List<(int At, int Length)> Removed) JoinLiterals(string text)
    {
        var joined = new System.Text.StringBuilder();
        var removed = new List<(int At, int Length)>();
        var from = 0;
        foreach (Match cut in JoinedLiterals.Matches(text))
        {
            joined.Append(text, from, cut.Index - from);
            removed.Add((joined.Length, cut.Length));
            from = cut.Index + cut.Length;
        }

        joined.Append(text, from, text.Length - from);
        return (joined.ToString(), removed);
    }

    private static IEnumerable<(int Line, string Form)> Matches(Regex pattern, string joined, string form, string original, List<(int At, int Length)> removed) =>
        pattern.Matches(joined).Select(match => (LineOf(original, match.Index + removed.Where(cut => cut.At <= match.Index).Sum(cut => cut.Length)), form));

    private static int LineOf(string text, int index) => 1 + text.AsSpan(0, Math.Min(index, text.Length)).Count('\n');

    [Fact]
    public void No_cs_file_switches_the_reaper_off()
    {
        var files = CSharpFiles();
        files.Should().NotBeEmpty("scanned nothing — discovery broke");
        var skipped = ManifestTargets();

        var findings = files.Where(file => !skipped.Contains(file.Relative)).SelectMany(Findings).ToList();

        findings.Should().BeEmpty("no .cs file outside the manifest's targets may switch Ryuk off (rule §5): every line below is a switch");
    }

    [Fact]
    public void The_scan_still_reads_the_known_instances_it_skips()
    {
        var skipped = ManifestTargets();

        var inTargets = CSharpFiles().Where(file => skipped.Contains(file.Relative)).SelectMany(Findings).ToList();

        inTargets.Should().NotBeEmpty("the copied guard's own test data names the variable: a scan that finds nothing there reads nothing");
        inTargets.Should().OnlyContain(finding => finding.Contains("tests/Bench.Tests/Leftovers/LeftoversGuard", StringComparison.Ordinal), "only the copied guard's files name it, and they are skipped by the manifest");
    }

    public static TheoryData<string> PlantedForms() => new(Plants().Keys);

    [Theory]
    [MemberData(nameof(PlantedForms))]
    public void The_reaper_check_finds_each_planted_form(string plant)
    {
        var (line, expected) = Plants()[plant];

        FormsOf(line).Should().Equal(expected);
    }

    [Fact]
    public void A_finding_names_the_line_where_a_multi_line_match_starts()
    {
        var name = ContainerPass.RyukVariable;
        var text = string.Concat("class C\n{\n    string x =\n        \"", name[..20], "\" +\n        \"", name[20..], "\";\n    bool y = true;\n    void M() => TestcontainersSettings.ResourceReaperEnabled\n        = false;\n}\n");

        ScanText(text).Should().Equal([(4, VariableForm), (7, SettingForm)], "the variable's match starts on line 4 and the assignment's on line 7, in the original text");
    }

    private static Dictionary<string, (string Line, string[] Forms)> Plants()
    {
        var name = ContainerPass.RyukVariable;
        var (head, tail) = (name[..20], name[20..]);
        var plus = new string([' ', '+', ' ']);
        var setting = "ResourceReaperEnabled";
        return new Dictionary<string, (string, string[])>
        {
            ["a SetEnvironmentVariable call"] = (string.Concat("Environment.SetEnvironmentVariable(\"", name, "\", \"true\");"), [VariableForm]),
            ["the name in lower case"] = (string.Concat("var x = \"", name.ToLowerInvariant(), "\";"), [VariableForm]),
            ["two literals joined"] = (string.Concat("var x = \"", head, "\"", plus, "\"", tail, "\";"), [VariableForm]),
            ["a verbatim literal joined to one"] = (string.Concat("var x = @\"", head, "\"", plus, "\"", tail, "\";"), [VariableForm]),
            ["three literals joined"] = (string.Concat("var x = \"", name[..14], "\"", plus, "\"", name[14..20], "\"", plus, "\"", tail, "\";"), [VariableForm]),
            ["an interpolated literal joined to one"] = (string.Concat("var x = $\"", head, "\"", plus, "$\"", tail, "\";"), [VariableForm]),
            ["the setting assigned false"] = (string.Concat("TestcontainersSettings.", setting, " = false;"), [SettingForm]),
            ["the setting assigned false, no spaces"] = (string.Concat(setting, "=false"), [SettingForm]),
            ["the setting assigned false, unqualified, tabbed"] = (string.Concat("    ", setting, "\t=\tfalse;"), [SettingForm]),
            ["two literals joined across a line break"] = (string.Concat("var x = \"", head, "\"", plus.TrimEnd(), "\n        \"", tail, "\";"), [VariableForm]),
            ["a verbatim literal joined across a line break, the plus first"] = (string.Concat("var x = @\"", head, "\"\n            ", plus.TrimStart(), "@\"", tail, "\";"), [VariableForm]),
            ["the setting assigned false across a line break"] = (string.Concat("TestcontainersSettings.", setting, " =\n    false;"), [SettingForm]),
            ["the setting with the equals sign on the next line"] = (string.Concat("TestcontainersSettings.", setting, "\n    = false;"), [SettingForm]),
            ["the setting compared"] = (string.Concat("if (", setting, " == false)"), []),
            ["the setting compared with !="] = (string.Concat("if (", setting, " != false)"), []),
            ["the setting assigned true"] = (string.Concat("TestcontainersSettings.", setting, " = true;"), []),
            ["the setting read"] = (string.Concat("ContainerPass.RequireReaper(TestcontainersSettings.", setting, ");"), []),
            ["the name built from identifiers"] = (string.Concat("Environment.SetEnvironmentVariable(", "left", plus, "right", ", \"true\");"), []),
            ["the name in two literals with no join"] = (string.Concat("var x = (\"", head, "\", \"", tail, "\");"), []),
        };
    }

    private sealed record Source(string Relative, string Absolute);

    private static List<Source> CSharpFiles() =>
        [.. GuardFiles.Walk(RepositoryRoot.Path)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(path => new Source(GuardFiles.RelativeTo(RepositoryRoot.Path, path), path))
            .OrderBy(file => file.Relative, StringComparer.Ordinal)];

    private static IEnumerable<string> Findings(Source file) =>
        ScanText(File.ReadAllText(file.Absolute)).Select(finding => $"{file.Relative}:{finding.Line} — {finding.Form}");

    private static HashSet<string> ManifestTargets()
    {
        var path = Path.Combine(RepositoryRoot.Path, "tests", "Bench.Tests", "Leftovers", "template-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        return manifest.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("target").GetString()!).ToHashSet(StringComparer.Ordinal);
    }
}
