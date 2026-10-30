using Bench.Application;
using Bench.Domain.Registry;
using Bench.Infrastructure.Models;
using FluentAssertions;
using ModelArms.Core.Experiment;
using ModelArms.Core.Launch;
using Xunit;

namespace Bench.Tests.ModelArms;

/// <summary>The three launch shapes of a step — write, read and the gate's — as the shell harness's claude argv spelled them
/// (<c>run-arm.sh:86-89</c> and <c>:106-108</c>), read through the one <c>CliArgv</c>. The expected flag groups are written out by hand
/// per mode; only the tool lists come from the worked example, and the test checks they are not empty so a shape cannot agree with itself
/// over nothing.
/// <para>
/// What this does NOT prove: that claude accepts the argv, that the order of the flags is the shell's (a multiset of groups is compared —
/// claude reads flags in any order), or that anything is launched; no process starts here.
/// </para></summary>
public sealed class StepLaunchTests
{
    private static readonly ExperimentLocation Loc = new("/x/root", "/x/home", string.Empty, "/x/repo");
    private static readonly ArmPaths Arm = new("/e", "tok");
    private static readonly Guid Session = new("3f2b6f9e-2a4c-4f0e-9d1a-0c6b5a7e8d10");

    /// <summary>A literal set with every list non-empty and every name distinct, so a launch built from it needs no file and no skip.</summary>
    private static readonly ToolSets Tools = new(
        Write: ["Read", "Edit", "Bash(git *)"],
        Read: ["Read", "Grep"],
        Gate: ["mcp__coai__open", "mcp__coai__review_code"],
        Deny: ["Read(//home/op/.claude/**)", "Read(//proc/**)"]);

    private static ToolSets ExampleTools()
    {
        PosixPaths();
        var text = File.ReadAllText(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "examples", "part6.experiment.json"));
        return ExperimentFile.Read(text, Loc).Ok().Tools;
    }

    private static void PosixPaths() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the worked example and the location use POSIX absolute paths");

    /// <summary>Flag groups: a token starting <c>-</c> opens a group, the tokens after it are its values.</summary>
    private static List<string> Groups(IReadOnlyList<string> argv)
    {
        var groups = new List<List<string>>();

        foreach (var token in argv)
        {
            if (token.StartsWith('-'))
            {
                groups.Add([token]);
            }
            else
            {
                groups[^1].Add(token);
            }
        }

        return [.. groups.Select(g => string.Join('\u001f', g))];
    }

    private static string Group(params string[] tokens) => string.Join('\u001f', tokens);

    private static string Group(string flag, IEnumerable<string> values) => string.Join('\u001f', values.Prepend(flag));

    private static string Spelled(StepMode mode, ToolSets tools)
    {
        var argv = CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", StepLaunch.Options(mode, tools, Arm, new StepSession.Fresh(Session)), []).Ok();
        return string.Join(" ", argv);
    }

    private static StepLaunchRequest Request(StepSession session, IReadOnlyDictionary<string, string>? parent = null) =>
        new("/usr/bin/claude", new StepSpec(StepKind.Code, "sonnet", 45, StepMode.Write), Tools, Arm, session, "/e/r/tok/arm", "the brief", TimeSpan.FromMinutes(45),
            parent ?? new Dictionary<string, string> { ["HOME"] = "/home/op" });

    [Theory]
    [InlineData(StepMode.Write)]
    [InlineData(StepMode.Read)]
    [InlineData(StepMode.Gate)]
    public void Write_read_and_gate_modes_produce_the_shells_three_argv_shapes(StepMode mode)
    {
        var tools = ExampleTools();
        tools.Write.Should().NotBeEmpty();
        tools.Read.Should().NotBeEmpty();
        tools.Gate.Should().NotBeEmpty();
        tools.Deny.Should().NotBeEmpty("an empty deny list would leave the order of --disallowedTools unobserved");

        var (permission, allowed, denied, mcp) = mode switch
        {
            StepMode.Write => ("acceptEdits", tools.Write, tools.Deny, Arm.McpNone),
            StepMode.Read => ("default", tools.Read, ["Edit", "Write", "NotebookEdit", .. tools.Deny], Arm.McpNone),
            _ => ("acceptEdits", [.. tools.Write, .. tools.Gate], tools.Deny, Arm.McpCoai),
        };

        var argv = CliArgv.For(ModelRuntimeKind.CliClaude, "sonnet", StepLaunch.Options(mode, tools, Arm, new StepSession.Fresh(Session)), []).Ok();

        Groups(argv).Should().BeEquivalentTo(
        [
            Group("-p"),
            Group("--model", "sonnet"),
            Group("--output-format", "stream-json"),
            Group("--verbose"),
            Group("--strict-mcp-config"),
            Group("--mcp-config", mcp),
            Group("--session-id", Session.ToString("D")),
            Group("--permission-mode", permission),
            Group("--disallowedTools", denied),
            Group("--add-dir", Arm.Notes),
            Group("--add-dir", Arm.Work),
            Group("--allowedTools", allowed),
        ]);
        Spelled(mode, tools).Should().Be(string.Join(" ", argv), "the options build the same argv every time");
    }

    [Fact]
    public void The_child_environment_is_ArmChildEnvironment_plus_its_working_directory_passed_as_a_replaced_set()
    {
        var parent = new Dictionary<string, string> { ["HOME"] = "/home/op", ["BENCH_DB"] = "Host=db", ["TMPDIR"] = "/tmp/claude-1000", ["CLAUDE_CODE_OAUTH_TOKEN"] = "t" };

        var ask = StepLaunch.Ask(Request(new StepSession.Fresh(Session), parent));

        ask.Environment.IsReplaced.Should().BeTrue("the launcher must REPLACE the environment, never merge it into the harness's own");
        ask.Environment.Variables.Should().BeEquivalentTo(
            new Dictionary<string, string>(ArmChildEnvironment.Of(parent, Arm)) { ["PWD"] = "/e/r/tok/arm" },
            "ArmChildEnvironment.Of, and the one named addition: PWD");
        ask.Environment.Variables.Should().NotContainKey("BENCH_DB").And.Contain("TMPDIR", Arm.Tmp, "the comparison above must not be two empty things");
    }

    [Fact]
    public void The_child_sees_its_own_folder_as_PWD_and_no_trace_of_the_runners()
    {
        var parent = new Dictionary<string, string> { ["HOME"] = "/home/op", ["PWD"] = "/home/op/git/dew_flow_benchmark", ["OLDPWD"] = "/home/op" };

        var variables = StepLaunch.Ask(Request(new StepSession.Fresh(Session), parent)).Environment.Variables;

        variables["PWD"].Should().Be("/e/r/tok/arm", "the shell ran claude after cd into its folder, so PWD was that folder, not the runner's");
        variables.Keys.Should().NotContain("OLDPWD", "the runner's previous folder is the runner's, and names where the experiment was started from");
        variables.Should().Contain("HOME", "/home/op", "everything else the parent held still passes");
    }

    [Fact]
    public void A_parent_without_PWD_still_gets_the_working_directory_as_PWD()
    {
        StepLaunch.Ask(Request(new StepSession.Fresh(Session))).Environment.Variables["PWD"].Should().Be("/e/r/tok/arm");
    }

    [Fact]
    public void A_resume_carries_the_same_options_with_resume_in_place_of_session_id()
    {
        var tools = Tools;

        foreach (var mode in Enum.GetValues<StepMode>())
        {
            var fresh = StepLaunch.Options(mode, tools, Arm, new StepSession.Fresh(Session));
            var resumed = StepLaunch.Options(mode, tools, Arm, new StepSession.Resume(Session));

            fresh.SessionId.Should().Be(Session);
            resumed.ResumeSessionId.Should().Be(Session);
            resumed.SessionId.Should().Be(Guid.Empty);
            resumed.Should().BeEquivalentTo(fresh, o => o.Excluding(x => x.SessionId).Excluding(x => x.ResumeSessionId), $"{mode}: every other option is the fresh one's");
        }
    }

    [Fact]
    public void The_working_directory_model_wall_and_prompt_are_the_requests()
    {
        var request = Request(new StepSession.Fresh(Session));

        var ask = StepLaunch.Ask(request);

        ask.WorkingDirectory.Should().Be("/e/r/tok/arm");
        ask.ModelId.Should().Be("sonnet");
        ask.Wall.Should().Be(TimeSpan.FromMinutes(45));
        ask.Prompt.Should().Be("the brief");
        ask.Executable.Should().Be("/usr/bin/claude");
        ask.Runtime.Should().Be(ModelRuntimeKind.CliClaude);
        ask.Options.Should().BeEquivalentTo(StepLaunch.Options(StepMode.Write, request.Tools, Arm, request.Session));
    }
}
