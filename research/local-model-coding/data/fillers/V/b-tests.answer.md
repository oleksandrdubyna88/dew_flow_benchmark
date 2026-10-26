```csharp
/// <summary>Scenario: Kimi question launch uses shim request in scratch or copy on row home.</summary>
[Fact]
public void AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome()
{
    var consultant = Kimi();
    var none = consultant.Build(Planned("kimi", CapabilityGrant.None, "glm-5-3"));
    none.Request.WorkingDirectory.Should().Be(Scratch);
    none.Request.Arguments.Should().Contain("--ask-acp").And.NotContain("--session");
    none.Role.Should().Be(ConsultantRoles.Question);
    var homeIndex = Array.IndexOf(none.Request.Arguments, "--home");
    var homeValue = none.Request.Arguments[homeIndex + 1];
    homeValue.Should().StartWith(RowFolder + Path.DirectorySeparatorChar);
    none.Request.InheritsEnvironment.Should().BeFalse();
    var refusal = Runners.Kimi.KimiEnvironment.Refusal(none.Request, PersonsPlaces(homeValue));
    refusal.Should().BeEmpty();

    var disk = consultant.Build(Planned("kimi", CapabilityGrant.Disk(RootA, RootB), "glm-5-3"));
    disk.Request.WorkingDirectory.Should().Be(RowTree);
    disk.Request.Arguments.Should().Contain("--ask-acp").And.NotContain("--session");
    disk.Role.Should().Be(ConsultantRoles.Question);
    homeIndex = Array.IndexOf(disk.Request.Arguments, "--home");
    homeValue = disk.Request.Arguments[homeIndex + 1];
    homeValue.Should().StartWith(RowFolder + Path.DirectorySeparatorChar);
    disk.Request.InheritsEnvironment.Should().BeFalse();
    refusal = Runners.Kimi.KimiEnvironment.Refusal(disk.Request, PersonsPlaces(homeValue));
    refusal.Should().BeEmpty();

    foreach (var arg in (string[])none.Request.Arguments.Concat(disk.Request.Arguments))
    {
        arg.Should().NotContain(RootA).And.NotContain(RootB);
    }
}

/// <summary>Scenario: Plan with flags is refused for Kimi.</summary>
[Fact]
public void APlanWithFlags_IsRefusedForKimi()
{
    var plan = Plan("kimi", CapabilityGrant.None) with { Flags = ["--yolo"] };
    var launch = Planned("kimi", CapabilityGrant.None) with { Confinement = new LaunchConfinement.Planned(plan) };
    Action act = () => Kimi().Build(launch);
    act.Should().Throw<ArgumentException>().WithMessage("*shim*");
}
```