

// ConfinementPlanner.cs
"kimi" => Kimi(grant, flag),

/// <remarks>
/// M-K6: Read is bounded by the shim; Grep and Glob read the disk directly, so both rows are unconfined and flagged.
/// D6: the roots are copied into the snapshot it stands in.
/// </remarks>
private static Confinement.Planned Kimi(CapabilityGrant grant, AdmissionFlag flag) => grant.Capability switch
{
    Capability.Disk => new("kimi", grant, [], [], CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots },
    _ => new("kimi", grant, [], [], CwdKind.Scratch, [], flag),
};

// ConfinementPlannerTests.cs
[Fact]
public void ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb()
{
    var none = Plan("kimi", CapabilityGrant.None);
    none.Cwd.Should().Be(CwdKind.Scratch, "M-K6: none stands in an empty scratch");
    none.Flags.Should().BeEmpty("no flags ever");
    none.Leading.Should().BeEmpty("no leading args");
    none.AddDirs.Should().BeEmpty("no added dirs");
    none.SnapshotSources.Should().BeEmpty("no snapshot sources for none");

    var disk = Plan("kimi", CapabilityGrant.Disk(RootA, RootB));
    disk.Cwd.Should().Be(CwdKind.Snapshot, "M-K6: disk stands in the snapshot of its roots");
    disk.SnapshotSources.Should().Equal([RootA, RootB], "the roots it reads through their copies");
    disk.Flags.Should().BeEmpty("no flags ever");
    disk.Leading.Should().BeEmpty("no leading args");
    disk.AddDirs.Should().BeEmpty("no added dirs");

    var web = ConfinementPlanner.Plan("kimi", CapabilityGrant.Web);
    web.Should().BeOfType<Confinement.Refused>()
        .Which.Reason.Should().Contain("not been measured")
        .And.Contain("RESULTS_kimi_confinement.md", "the matrix refuses web as unmeasured");
}

// CapabilityMatrixTests.cs
[Fact]
public void KimiHasThreeRows_EachCitingItsCells()
{
    var noneRow = RuntimeCapabilities.Builtin.Of("kimi", Capability.None);
    var diskRow = RuntimeCapabilities.Builtin.Of("kimi", Capability.Disk);
    var webRow = RuntimeCapabilities.Builtin.Of("kimi", Capability.Web);

    noneRow.Should().NotBeNull();
    diskRow.Should().NotBeNull();
    webRow.Should().NotBeNull();

    noneRow.Standing.Should().Be(Standing.Unconfined, "M-K6: none is unconfined");
    noneRow.MeasuredWith.Cells.Should().Contain("M-K6", "M-K6 cites its cells");

    diskRow.Standing.Should().Be(Standing.Unconfined, "M-K6: disk is unconfined");
    diskRow.MeasuredWith.Cells.Should().Contain(["M-K6", "M-K9"], "M-K6 and M-K9 cite their cells");

    webRow.Standing.Should().Be(Standing.Unmeasured, "M-K6: web is unmeasured");

    foreach (var row in new[] { noneRow, diskRow, webRow })
    {
        row.MeasuredWith.Version.Should().Be("2.1.1", "all rows cite version 2.1.1");
        row.MeasuredWith.ResultRef.Should().Contain("RESULTS_kimi_confinement.md", "all rows cite the result ref");
    }
}

[Fact]
public void AdmissionFlagsBothRows()
{
    var noneAdmission = CapabilityMatrix.Admit("kimi", Capability.None);
    var diskAdmission = CapabilityMatrix.Admit("kimi", Capability.Disk);

    noneAdmission.Should().BeOfType<Admission.Admitted>()
        .Which.Flag.Should().Be(AdmissionFlag.Unconfined, "M-K6: none is admitted unconfined");

    diskAdmission.Should().BeOfType<Admission.Admitted>()
        .Which.Flag.Should().Be(AdmissionFlag.Unconfined, "M-K6: disk is admitted unconfined");
}