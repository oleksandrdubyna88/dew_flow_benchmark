

src_mcp/runners/Kimi/KimiConsultant.cs
```csharp
namespace CoaiMcp.Runners.Kimi;

using System;
using CoaiMcp.Runners.Consultation;
using CoaiMcp.Runners.Reviewers;

public class KimiConsultant
{
    private readonly ConsultantBuilder inner;

    public KimiConsultant(ConsultantBuilder inner) => this.inner = inner;

    public ReviewerInvocation Build(ConsultantLaunch launch)
    {
        ConsultantLaunches.MustBeLaunchable(launch);
        MustCarryNoFlags(launch);
        var settings = launch.Settings with { OwnedScratch = OwnedFolder(launch), Model = ModelOf(launch.Settings) };
        var built = inner.Build(ConsultantLaunches.RoleOf(launch), launch.Prompt, Cwd(launch), string.Empty, launch.OutputDir, settings);
        var request = built.Request with { Arguments = [.. built.Request.Arguments, .. SessionOf(launch)] };
        ConsultantLaunches.MustCarryNoLineBreak(request.Arguments);

        return built with { Request = launch.Confinement is LaunchConfinement.Planned ? ConsultantLaunches.ForQuestion(request) : request };
    }

    private static string Cwd(ConsultantLaunch launch) =>
        launch.Confinement is LaunchConfinement.Planned planned ? ConsultantLaunches.Cwd(launch, planned.Plan) : SnapshotOf(launch);

    private static void MustCarryNoFlags(ConsultantLaunch launch)
    {
        if (launch.Confinement is LaunchConfinement.Planned { Flags.Count: > 0 or > 0, Leading.Count: > 0 or > 0 } _)
            throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
    }
}
```

tests/CoaiMcp.Tests/KimiQuestionLaunchTests.cs
```csharp
namespace CoaiMcp.Tests;

using System;
using System.IO;
using System.Linq;
using CoaiMcp.Core.QuestionConsult;
using CoaiMcp.Runners.Acp;
using CoaiMcp.Runners.Consultation;
using CoaiMcp.Runners.Kimi;
using CoaiMcp.Runners.Platform;
using CoaiMcp.Runners.Reviewers;
using FluentAssertions;
using Xunit;

public class KimiQuestionLaunchTests
{
    private const string RootA = "D:/projects/alpha";
    private const string RootB = "D:/projects/beta";
    private static readonly string RowFolder = Path.Combine(Path.GetTempPath(), "coai-question-" + Guid.NewGuid().ToString("N"));
    private static readonly string RowTree = Path.Combine(RowFolder, "tree");
    private static readonly string RowCwd = Path.Combine(RowFolder, "cwd");

    private static ConsultantLaunch Planned(string runtime, CapabilityGrant grant, string model = "") => new(
        "repo", "the question", string.Empty, "answers", new ReviewerSettings(runtime) { Model = model, OwnedScratch = RowFolder })
    {
        Confinement = Plan(runtime, grant),
        Workdir = RowTree,
        Scratch = RowCwd,
    };

    private static Confinement.Planned Plan(string runtime, CapabilityGrant grant) =>
        ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which;

    private static KimiConsultant Consultant() => new(new KimiRuntime("kimi", HostKind.Linux,
        new KimiSeams(root => KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin")), () => ("coai-mcp", Array.Empty<string>()))));

    [Fact]
    public void AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome()
    {
        var noneLaunch = Planned("kimi", CapabilityGrant.None);
        var diskLaunch = Planned("kimi", CapabilityGrant.Disk(RootA, RootB));

        var noneInv = Consultant().Build(noneLaunch);
        var diskInv = Consultant().Build(diskLaunch);

        noneInv.Request.WorkingDirectory.Should().Be(RowCwd);
        diskInv.Request.WorkingDirectory.Should().Be(RowTree);

        var GetHomeParent = (string arg) => Path.GetDirectoryName(arg.Substring("--home=".Length));
        GetHomeParent(noneInv.Request.Arguments.First(a => a.StartsWith("--home="))).Should().Be(RowFolder);
        GetHomeParent(diskInv.Request.Arguments.First(a => a.StartsWith("--home="))).Should().Be(RowFolder);

        noneInv.Request.Arguments.Should().NotContain("--session");
        diskInv.Request.Arguments.Should().NotContain("--session");

        noneInv.Request.Arguments.Should().NotContain(a => a == RootA || a.Contains(RootA));
        noneInv.Request.Arguments.Should().NotContain(a => a == RootB || a.Contains(RootB));
        diskInv.Request.Arguments.Should().NotContain(a => a == RootA || a.Contains(RootA));
        diskInv.Request.Arguments.Should().NotContain(a => a == RootB || a.Contains(RootB));

        noneInv.Request.InheritsEnvironment.Should().BeFalse();
        diskInv.Request.InheritsEnvironment.Should().BeFalse();
    }

    [Fact]
    public void APlanWithFlags_IsRefusedForKimi()
    {
        var nonePlan = Plan("kimi", CapabilityGrant.None);
        var yoloLaunch = Planned("kimi", CapabilityGrant.None) with { Confinement = nonePlan with { Flags = new[] { "--yolo" } } };
        var searchLaunch = Planned("kimi", CapabilityGrant.None) with { Confinement = nonePlan with { Leading = new[] { "--search" } } };

        Consultant().Build(yoloLaunch).Should().Throw<ArgumentException>().WithMessage("*shim*");
        Consultant().Build(searchLaunch).Should().Throw<ArgumentException>().WithMessage("*shim*");
    }
}
```