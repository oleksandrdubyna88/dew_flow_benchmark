using Bench.Tests.Probes.Driver;
using FluentAssertions;
using ModelArms.Core.Experiment;
using static Bench.Tests.ModelArms.RunArmKit;

namespace Bench.Tests.ModelArms;

/// <summary>The delegation policy, checked on every launch the fake CLI logged for a root (plan model arms §10, E2.S0): a launch whose step spec does not say <c>subagents: true</c> carries <c>--disallowedTools</c> starting with
/// <c>Agent</c> and <c>Task</c> and an <c>--allowedTools</c> holding neither; a launch whose spec says so does not deny them. It sits behind <see cref="RunArmKit.RunAsync"/> and <see cref="RunArmKit.RunWithSeamsAsync"/>, the two
/// ways the <c>run-arm</c> tests run an arm, so a test or a launch added later is covered with no line of its own, a run that ends in a refusal included. It reads the fake's whole log, not the last call: the log holds every launch of
/// the root so far, a resume as well as a fresh start. Which step a launch belongs to is told by its brief (a resume by the session it continues); a launch it cannot place is a failure, never a skip.</summary>
internal static class LaunchDenial
{
    private static readonly string[] SubagentTools = ["Agent", "Task"];

    /// <summary>Checks every launch in the fake's log of <paramref name="root"/> against the step spec it was made for, and returns how many it checked. A root with no fake, or a fake that logged nothing, checks none — the
    /// number is how a test sees that the check ran.</summary>
    public static int Check(ExperimentRoot root)
    {
        if (!root.Environment.TryGetValue("FAKE_CLI_HOME", out var home))
        {
            return 0;
        }

        var calls = FakeCli.CallsIn(home);
        if (calls.Count == 0)
        {
            return 0;
        }

        var steps = root.ExperimentOf().Steps.Values.ToList();
        foreach (var call in calls)
        {
            Check(call, calls, steps);
        }

        return calls.Count;
    }

    /// <summary>One launch against its step's spec.</summary>
    internal static void Check(FakeCliCall call, IReadOnlyList<FakeCliCall> all, IReadOnlyList<StepSpec> steps)
    {
        var spec = SpecOf(call, all, steps);
        var name = Named(call, all);
        var denied = Values(call, "--disallowedTools");
        if (spec.Subagents)
        {
            denied.Should().NotContain(SubagentTools, $"{name}: its step '{spec.Id}' says subagents: true, so the launch does not deny them");
            return;
        }

        denied.Take(2).Should().Equal(SubagentTools, $"{name} starts no subagent unless its spec says so: the deny list of step '{spec.Id}' leads with Agent and Task");
        Values(call, "--allowedTools").Should().NotContain(SubagentTools, $"{name} is not allowed one either");
    }

    private static StepSpec SpecOf(FakeCliCall call, IReadOnlyList<FakeCliCall> all, IReadOnlyList<StepSpec> steps)
    {
        var brief = BriefOf(call);
        if (brief == "resume")
        {
            var session = call.After("--resume");
            var first = all.FirstOrDefault(other => other.Argv.Contains("--session-id") && other.After("--session-id") == session);
            first.Should().NotBeNull($"call {call.Call} resumes session {session}, which no logged launch started: the check cannot tell which step it belongs to");
            brief = BriefOf(first!);
        }

        var found = steps.Where(step => string.Equals(step.Brief, brief, StringComparison.Ordinal)).ToList();
        found.Should().ContainSingle($"call {call.Call} was filled from the brief '{brief}', which exactly one step of the experiment names");
        return found[0];
    }

    /// <summary>The values after <paramref name="flag"/> up to the next flag; none when the launch has no such flag.</summary>
    private static IReadOnlyList<string> Values(FakeCliCall call, string flag) =>
        call.Argv.Contains(flag) ? call.Group(flag) : [];
}
