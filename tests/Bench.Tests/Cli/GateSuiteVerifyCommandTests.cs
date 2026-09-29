using System.Text.Json.Nodes;
using Bench.Cli;
using Bench.Tests.Gate.Driver;
using Bench.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Cli;

/// <summary><c>bench gate suite verify</c> through <see cref="Program.Run"/>: every task's checkout at its variant head, and
/// its plan either committed there or carried by the suite (<c>planText</c>) at a path the head does not commit. S7.1's
/// record said verify proved every plan committed; tsx2's and php1's were not, and the campaign found it instead.</summary>
public sealed class GateSuiteVerifyCommandTests
{
    [Fact]
    public async Task A_committed_plan_and_a_carried_one_at_an_uncommitted_path_both_verify()
    {
        await using var rig = await GateCloneRig.StartAsync();

        var (code, output, error) = Verify(rig, Task(rig, "cs2", "docs/plan.md", null), Task(rig, "tsx2", "todo/PLAN_synthetic.md", "# Synthetic\n"));

        code.Should().Be(ExitCodes.Pass, error);
        output.Should().Contain("ok             cs2").And.Contain("ok             tsx2").And.Contain("2 of 2 task(s) ready");
    }

    [Fact]
    public async Task A_plan_neither_committed_nor_carried_is_refused_by_name()
    {
        await using var rig = await GateCloneRig.StartAsync();

        var (code, output, _) = Verify(rig, Task(rig, "tsx2", "todo/PLAN_synthetic.md", null));

        code.Should().Be(ExitCodes.Environment);
        output.Should().Contain("refused        tsx2").And.Contain("todo/PLAN_synthetic.md is not committed").And.Contain("planText");
    }

    [Fact]
    public async Task A_carried_plan_at_a_path_the_head_commits_is_refused_by_name()
    {
        await using var rig = await GateCloneRig.StartAsync();

        var (code, output, _) = Verify(rig, Task(rig, "cs2", "docs/plan.md", "# Another plan\n"));

        code.Should().Be(ExitCodes.Environment);
        output.Should().Contain("refused        cs2").And.Contain("docs/plan.md").And.Contain("commits");
    }

    private static JsonObject Task(GateCloneRig rig, string id, string planPath, string? planText)
    {
        var task = new JsonObject
        {
            ["id"] = id,
            ["language"] = "C#",
            ["gates"] = new JsonArray("plan", "code", "feature"),
            ["repository"] = rig.Repo.Root,
            ["base"] = rig.Task.Case.Base.Value,
            ["variantHead"] = rig.Task.Case.VariantHead.Value,
            ["planPath"] = planPath,
        };

        if (planText is not null)
        {
            task["planText"] = planText;
        }

        return task;
    }

    private static (int Code, string Output, string Error) Verify(GateCloneRig rig, params JsonObject[] tasks)
    {
        var dir = rig.Root.Sibling("suite");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"verify-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, new JsonObject { ["id"] = "verify-suite", ["tasks"] = new JsonArray([.. tasks]) }.ToJsonString());

        var output = new StringWriter();
        var error = new StringWriter();
        var code = Program.Run(["gate", "suite", "verify", "--suite-file", file, "--checkout-root", rig.Root.Sibling("cli-checkouts")], output, error);
        return (code, output.ToString(), error.ToString());
    }
}
