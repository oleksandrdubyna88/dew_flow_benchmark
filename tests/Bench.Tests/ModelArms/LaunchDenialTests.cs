using Bench.Cli;
using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Experiment;
using Xunit;
using static Bench.Tests.ModelArms.RunArmKit;

namespace Bench.Tests.ModelArms;

/// <summary>The delegation check every <c>run-arm</c> test passes through (<see cref="LaunchDenial"/>) is itself checked: it fails on each way a launch can lack the deny, it fails on a launch it cannot place rather than skipping it, it
/// really saw the launches of a run of each kind the two run-arm classes make, and those two classes still reach <c>run-arm</c> only through the kit that carries it. Without these a refactor that made the check see nothing would leave
/// every test green and the plan's box ("every launch of <c>RunArmSchemeTests</c> and <c>RunArmSrsTests</c> carries <c>--disallowedTools Agent Task</c> unless its spec says <c>subagents: true</c>") untrue.
/// <para>
/// What this does NOT prove: that the real claude honours the deny (the module doc measures that); only that the argv the runner built carries it.
/// </para></summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LaunchDenialTests
{
    private static readonly string Opening1Code = Opening(Path.Combine(Cli.Repository.Root, "tools", "model-arms", "briefs", "1-code.template.md"));
    private static readonly Guid Session = new("3f2b6f9e-2a4c-4f0e-9d1a-0c6b5a7e8d10");

    private static StepSpec Spec(bool subagents) =>
        new(new StepId("code"), StepKind.Session, "sonnet", 45, StepMode.Write, subagents, "1-code", [new OutputBinding("CODE_NOTES", "notes/code.md")], "CODE_NOTES", "", "", false, "");

    private static FakeCliCall Fresh(string denied, string allowed = "Read Edit") =>
        new(1, 1, ["--session-id", Session.ToString(), "--allowedTools", .. allowed.Split(' '), .. denied.Length == 0 ? Array.Empty<string>() : ["--disallowedTools", .. denied.Split(' ')], "--print"], "/e", Opening1Code + "x", []);

    private static FakeCliCall Resumed() =>
        new(2, 1, ["--resume", Session.ToString(), "--allowedTools", "Read", "--disallowedTools", "Edit", "--print"], "/e", "Continue.", []);

    private static void Check(FakeCliCall call, bool subagents, params FakeCliCall[] others) =>
        LaunchDenial.Check(call, [call, .. others], [Spec(subagents)]);

    [Fact]
    public void A_launch_that_leads_its_deny_list_with_agent_and_task_passes_when_its_spec_says_no_subagents()
    {
        var launch = Fresh("Agent Task Edit");

        var check = () => Check(launch, subagents: false);

        check.Should().NotThrow();
    }

    [Theory]
    [InlineData("Edit")]
    [InlineData("Task Agent Edit")]
    [InlineData("Edit Agent Task")]
    [InlineData("Agent Edit")]
    [InlineData("")]
    public void A_launch_whose_deny_list_does_not_lead_with_agent_and_task_fails(string denied)
    {
        var launch = Fresh(denied);

        var check = () => Check(launch, subagents: false);

        check.Should().Throw<Exception>().WithMessage("*starts no subagent unless its spec says so*");
    }

    [Theory]
    [InlineData("Read Agent")]
    [InlineData("Task Read")]
    public void A_launch_that_allows_agent_or_task_fails(string allowed)
    {
        var launch = Fresh("Agent Task Edit", allowed);

        var check = () => Check(launch, subagents: false);

        check.Should().Throw<Exception>().WithMessage("*is not allowed one either*");
    }

    [Fact]
    public void A_resume_is_judged_by_the_step_of_the_session_it_continues_and_a_resume_without_a_deny_fails()
    {
        var started = Fresh("Agent Task Edit");

        var check = () => Check(Resumed(), subagents: false, started);

        check.Should().Throw<Exception>().WithMessage("*1-code resumed*").Which.Message.Should().Contain("starts no subagent");
    }

    [Fact]
    public void A_resume_of_a_session_no_logged_launch_started_fails_rather_than_being_skipped()
    {
        var check = () => Check(Resumed(), subagents: false);

        check.Should().Throw<Exception>().WithMessage("*no logged launch*");
    }

    [Fact]
    public void A_launch_whose_brief_no_step_names_fails_rather_than_being_skipped()
    {
        var launch = Fresh("Agent Task Edit");

        var check = () => LaunchDenial.Check(launch, [launch], [Spec(subagents: false) with { Brief = "2-review" }]);

        check.Should().Throw<Exception>().WithMessage("*exactly one step*");
    }

    [Fact]
    public void A_step_that_says_subagents_true_must_not_have_them_denied_and_may_have_no_deny_list_at_all()
    {
        var denies = Fresh("Agent Task Edit");
        var silent = Fresh(string.Empty);

        var withDeny = () => Check(denies, subagents: true);
        var without = () => Check(silent, subagents: true);

        withDeny.Should().Throw<Exception>().WithMessage("*subagents: true*");
        without.Should().NotThrow();
    }

    [Fact]
    public async Task The_check_behind_the_scheme_runs_sees_every_launch_of_a_run()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "C1");

        run.Code.Should().Be(0, run.Error + run.Output);
        fake.Calls().Should().HaveCount(4);
        run.LaunchesChecked.Should().Be(4, "the check read the fake's whole log, so a scheme test cannot pass with it blind");
    }

    [Fact]
    public async Task The_check_behind_the_task_pipeline_sees_every_launch_of_a_run_including_the_wrapped_coders()
    {
        using var kit = await SrsKit.CreateAsync();
        kit.Plan(kit.Five());
        kit.Replay("result-success.jsonl");

        var run = await kit.RunAsync();

        run.Code.Should().Be(0, run.Error + run.Output);
        kit.Fake.Calls().Count(call => call.Prompt.Contains("ONE task, **", StringComparison.Ordinal)).Should().Be(5);
        run.LaunchesChecked.Should().Be(kit.Fake.Calls().Count).And.BeGreaterThan(5, "the split and the five coders at least were all seen");
    }

    [Fact]
    public async Task A_run_that_launches_nothing_checks_nothing_and_says_so()
    {
        using var root = new ExperimentRoot();
        using var fake = new FakeCli();
        await root.MakeArmAsync("C1", fake);
        Replay(fake, "result-success.jsonl");

        var run = await RunAsync(root, "C9");

        run.Code.Should().Be(4, run.Output);
        run.LaunchesChecked.Should().Be(0);
    }

    [Theory]
    [InlineData("RunArmSchemeTests.cs")]
    [InlineData("RunArmSrsTests.cs")]
    public void The_two_run_arm_classes_reach_the_verb_only_through_the_kit_that_checks_its_launches(string file)
    {
        var text = File.ReadAllText(Path.Combine(Cli.Repository.Root, "tests", "Bench.Tests", "ModelArms", file));

        text.Should().Contain("RunAsync(", "the scan read a test file, not nothing");
        foreach (var bypass in new[] { "ExperimentGate" + ".RunAsync(", "Program" + ".RunAsync(", "RunArmVerb" + ".RunAsync(" })
        {
            text.Should().NotContain(bypass, $"{file} must start run-arm through RunArmKit, which checks every launch it logged; a direct call would skip the check");
        }
    }
}
