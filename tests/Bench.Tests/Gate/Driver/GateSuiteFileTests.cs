using System.Text.Json.Nodes;
using Bench.Application.Gate;
using Bench.Cli;
using FluentAssertions;
using Xunit;

namespace Bench.Tests.Gate.Driver;

/// <summary>The operator's suite file and <c>--set</c> extras, read strictly: a word nobody recognises is refused by name,
/// never silently dropped — a mistyped gate would otherwise run a campaign that omits the gate it was asked for.</summary>
public sealed class GateSuiteFileTests
{
    [Fact]
    public void A_suite_task_naming_an_unknown_gate_is_refused_by_name()
    {
        var suite = Suite(new JsonArray("plan", "code", "featre"));

        GateSuiteFile.Parse(suite).Reason().Should().Contain("featre").And.Contain("cs2");
    }

    [Fact]
    public void A_well_formed_suite_freezes_with_its_gates_and_seeds()
    {
        var frozen = GateSuiteFile.Parse(Suite(new JsonArray("plan", "feature"))).Ok();

        frozen.Tasks.Should().ContainSingle().Which.Hosts.Canonical.Should().Be("plan,feature");
        frozen.Tasks[0].Seeds.Should().ContainSingle();
        frozen.PrivateNames.Should().Equal(["contoso-orders"]);
    }

    [Fact]
    public void A_run_setting_named_twice_is_refused_rather_than_crashing_or_guessing()
    {
        var command = CommandLine.Parse(["gate", "run", "--set", "COAI_ROUNDS_PLANCRITIQUE=2,coai_rounds_plancritique=3"]);

        GateCliInputs.Extras(command).Reason().Should().Contain("COAI_ROUNDS_PLANCRITIQUE").And.Contain("twice");
        GateCliInputs.Extras(CommandLine.Parse(["gate", "run", "--set", "COAI_X=1"])).Ok().Should().ContainKey("COAI_X");
    }

    private static string Suite(JsonArray gates) => new JsonObject
    {
        ["id"] = "unit-suite",
        ["privateNames"] = new JsonArray("contoso-orders"),
        ["tasks"] = new JsonArray(new JsonObject
        {
            ["id"] = "cs2",
            ["language"] = "C#",
            ["gates"] = gates,
            ["repository"] = "file:///repos/cs2",
            ["base"] = new string('a', 40),
            ["variantHead"] = new string('b', 40),
            ["planPath"] = "docs/plan.md",
            ["epics"] = new JsonArray(new JsonObject { ["title"] = "Epic 1", ["summary"] = "s" }),
            ["lessons"] = new JsonObject { ["pitfalls"] = new JsonArray("none") },
            ["seeds"] = new JsonArray(new JsonObject
            {
                ["id"] = "cs2-S1",
                ["file"] = "src/A.cs",
                ["old"] = "a",
                ["new"] = "b",
                ["what"] = "w",
                ["trigger"] = "t",
                ["mechanism"] = "m",
                ["consequence"] = "c",
            }),
        }),
    }.ToJsonString();
}
