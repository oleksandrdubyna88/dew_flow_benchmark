```csharp
// FILE: CapabilityMatrixTests
[Fact]
public void KimiHasThreeRows_EachCitingItsCells()
{
    var none = RuntimeCapabilities.Builtin.Of("kimi", Capability.None)!;
    var disk = RuntimeCapabilities.Builtin.Of("kimi", Capability.Disk)!;
    var web = RuntimeCapabilities.Builtin.Of("kimi", Capability.Web)!;

    none.Should().NotBeNull();
    disk.Should().NotBeNull();
    web.Should().NotBeNull();

    none.Standing.Spelled().Should().Be("unconfined");
    disk.Standing.Spelled().Should().Be("unconfined");
    web.Standing.Spelled().Should().Be("unmeasured");

    none.MeasuredWith.Version.Should().Be("2.1.1");
    disk.MeasuredWith.Version.Should().Be("2.1.1");
    web.MeasuredWith.Version.Should().Be("2.1.1");

    none.Cells.Should().Contain("M-K6");
    disk.Cells.Should().Contain("M-K6");
    disk.Cells.Should().Contain("M-K9");

    none.ResultRef.Should().Contain("RESULTS_kimi_confinement.md");
    disk.ResultRef.Should().Contain("RESULTS_kimi_confinement.md");
}

// FILE: ConfinementPlannerTests
[Fact]
public void ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb()
{
    var nonePlan = Plan("kimi", CapabilityGrant.None);
    nonePlan.Cwd.Should().Be(CwdKind.Scratch);
    nonePlan.Flags.Should().BeEmpty();
    nonePlan.Leading.Should().BeEmpty();
    nonePlan.AddDirs.Should().BeEmpty();

    var diskPlan = Plan("kimi", CapabilityGrant.Disk(RootA, RootB));
    diskPlan.Cwd.Should().Be(CwdKind.Snapshot);
    diskPlan.SnapshotSources.Should().Equal([RootA, RootB]);
    diskPlan.Flags.Should().BeEmpty();
    diskPlan.Leading.Should().BeEmpty();
    diskPlan.AddDirs.Should().BeEmpty();

    var webPlan = ConfinementPlanner.Plan("kimi", CapabilityGrant.Web);
    webPlan.Should().BeOfType<Confinement.Refused>().Which.Reason.Should().Contain("not been measured");
    webPlan.Should().BeOfType<Confinement.Refused>().Which.Reason.Should().Contain("RESULTS_kimi_confinement.md");
}

// FILE: CapabilityMatrixTests
[Fact]
public void AdmissionFlagsBothRows()
{
    var noneAdmission = CapabilityMatrix.Admit("kimi", Capability.None);
    noneAdmission.Should().BeOfType<Admission.Admitted>().Which.Flag.Should().Be(AdmissionFlag.Unconfined);

    var diskAdmission = CapabilityMatrix.Admit("kimi", Capability.Disk);
    diskAdmission.Should().BeOfType<Admission.Admitted>().Which.Flag.Should().Be(AdmissionFlag.Unconfined);
}
```