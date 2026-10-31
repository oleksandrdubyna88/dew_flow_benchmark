using Bench.Domain.Targets;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The target's argument lists (C14): the lowering wrapper handed the RESOLVED executable and an absolute script, the suite's two CTRF options, one formatter command per verb with each file its own argument, and npm in the folder of each <c>node_modules</c>.
/// <para>
/// What this does NOT prove: that the lists run — they are launched in <c>PrepBaseTests</c> through shims, never through a real <c>dotnet</c> or <c>npm</c> — or that a wrapper script that is not the shell's reads them as the shell's did.
/// </para></summary>
public sealed class TargetArgvTests
{
    private const string Clone = "/root/base";

    private static readonly ToolPaths Tools = new("/real/git", "/real/dotnet", "/real/node", "/real/dotnet", "/real/dotnet", "/real/npm", "/real/npm");

    private static TargetFacts Target(IReadOnlyList<string>? lowPriority = null, IReadOnlyList<string>? nodeModules = null) =>
        new(
            "/src",
            "plan-branch",
            CommitSha.Parse(new string('a', 40)).Ok(),
            "todo/PLAN_x.md",
            "feat/x",
            ".agents/conventions",
            lowPriority ?? ["node", ".agents/conventions/tools/low-priority.mjs"],
            ["dotnet", "build", "X.slnx"],
            new SuiteFacts(["dotnet", "tests/bin/Tgt.Tests.dll", "--report-xunit-ctrf"], "mcp.ctrf.json", "Tgt.Tests."),
            new FormatFacts("X.slnx", ["whitespace", "style"]),
            new ExtensionFacts("ext", ["npm", "test"]),
            nodeModules ?? ["ext/node_modules", ".agents/conventions/node_modules"],
            ["tests"]);

    [Fact]
    public void The_lowering_wrapper_is_absolute_and_hands_on_the_resolved_executable()
    {
        var build = TargetArgv.Build(Target(), Tools, Clone);

        build.Executable.Should().Be("/real/node", "the wrapper is a node script, and the real node runs it");
        build.Arguments.Should().Equal(Path.Combine(Clone, ".agents", "conventions", "tools", "low-priority.mjs"), "/real/dotnet", "build", "X.slnx");
        build.Arguments[1].Should().Be("/real/dotnet", "the wrapper is handed the resolved executable, never its name (E3 §8 item 11)");
        build.WorkingDirectory.Should().Be(Clone);
        build.Wall.Should().Be(TargetWalls.Build);

        var absolute = TargetArgv.Build(Target(lowPriority: ["node", "/opt/wrap/low.mjs", "--quiet"]), Tools, Clone);

        absolute.Arguments.Should().Equal("/opt/wrap/low.mjs", "--quiet", "/real/dotnet", "build", "X.slnx");

        var extension = TargetArgv.Extension(Target(), Tools, Clone);

        extension.Arguments.Should().Equal(Path.Combine(Clone, ".agents", "conventions", "tools", "low-priority.mjs"), "/real/npm", "test");
        extension.WorkingDirectory.Should().Be(Path.Combine(Clone, "ext"));
    }

    [Fact]
    public void The_suite_gets_the_shells_two_ctrf_options_and_format_one_command_per_verb()
    {
        var suite = TargetArgv.Suite(Target(), Tools, Clone, "/root/base-results");

        suite.Arguments.TakeLast(4).Should().Equal("--results-directory", "/root/base-results", "--report-xunit-ctrf-filename", "mcp.ctrf.json");
        suite.Arguments.Should().Contain("--report-xunit-ctrf", "the suite's own words come first, the harness's two options last");
        suite.Wall.Should().Be(TargetWalls.Suite);

        var format = TargetArgv.Format(Target(), Tools, Clone, ["a b.cs", "c.cs"]);

        format.Select(command => command.Arguments[2]).Should().Equal("whitespace", "style");
        foreach (var command in format)
        {
            command.Executable.Should().Be("/real/dotnet", "the formatter is not lowered (objective-checks.sh:21)");
            command.Arguments.Should().Equal("format", "X.slnx", command.Arguments[2], "--verify-no-changes", "--no-restore", "--include", "a b.cs", "c.cs");
            command.WorkingDirectory.Should().Be(Clone);
        }

        TargetArgv.Format(Target(), Tools, Clone, []).Should().BeEmpty("no changed file, no formatter run (the shell passed it)");
        TargetArgv.Format(Target(), Tools, Clone, ["--no-restore.cs"]).First().Arguments.Last().Should().Be("./--no-restore.cs", "a file an arm named is never read by the formatter as an option");
    }

    [Fact]
    public void Npm_runs_in_the_folder_of_each_node_modules_and_ignores_scripts_only_under_the_rules_submodule()
    {
        var rules = TargetArgv.RulesNpmCi(Target(), Tools, Clone).Single();
        var others = TargetArgv.OtherNpmCi(Target(), Tools, Clone).Single();

        rules.Executable.Should().Be("/real/npm", "npm is not lowered (prep-base.sh:12)");
        rules.Arguments.Should().Equal("ci", "--no-audit", "--no-fund", "--ignore-scripts");
        rules.WorkingDirectory.Should().Be(Path.Combine(Clone, ".agents", "conventions"));
        others.Arguments.Should().Equal("ci", "--no-audit", "--no-fund");
        others.WorkingDirectory.Should().Be(Path.Combine(Clone, "ext"));
        TargetArgv.OtherNpmCi(Target(nodeModules: ["node_modules"]), Tools, Clone).Single().WorkingDirectory.Should().Be(Clone, "a bare node_modules restores in the clone itself");
        TargetArgv.RulesNpmCi(Target(nodeModules: ["ext/node_modules", ".agents/conventions-other/node_modules"]), Tools, Clone).Should().BeEmpty("a sibling that shares the prefix is not under the submodule");
    }

    [Theory]
    [InlineData("submodule..agents/conventions.path .agents/conventions", ".agents/conventions", ".agents/conventions")]
    [InlineData("submodule.a.b.c.path x/y\nsubmodule.rules.path .agents/conventions\n", ".agents/conventions", "rules")]
    [InlineData("submodule.rules.path .agents/conventions-old\n", ".agents/conventions", "")]
    [InlineData("", ".agents/conventions", "")]
    [InlineData("submodule..path .agents/conventions", ".agents/conventions", "")]
    public void A_submodule_is_named_by_the_path_line_that_ends_exactly_with_its_path(string output, string path, string expected)
    {
        GitModules.NameOf(output, path).Should().Be(expected);
    }
}
