Write xUnit tests for a C# repo. Style: C# 14 / .NET 10, xUnit v3 + FluentAssertions, file-scoped namespace, 4-space indent, Allman braces, target-typed `new`, collection expressions `[..]`, no comments beyond one short /// summary per test mentioning the scenario id. Output ONLY the C# test methods (no usings, no class wrapper) in one ```csharp block.

Existing types you can use (already imported): `RuntimeCapabilities.Builtin.Of(string runtime, Capability capability)` returns a nullable row `RuntimeCapabilityRow` with `.Standing` (enum `Standing`; the extension `.Spelled()` returns the string "confined"/"unconfined"/"unmeasured") and `.MeasuredWith` with `.Version` (string), `.ResultRef` (string), `.Cells` (IReadOnlyList<string>). `Capability` enum: None, Disk, Web. `CapabilityMatrix.Admit(string runtime, Capability c)` returns `Admission`: either `Admission.Admitted` (props `Standing`, `Flag` of enum `AdmissionFlag` {None, Unconfined, ...}) or `Admission.Refused` (prop `Reason` string). `ConfinementPlanner.Plan(string runtime, CapabilityGrant grant)` returns `Confinement`: `Confinement.Planned` (props Flags, Leading, AddDirs: lists of string; Cwd: `CwdKind` {Scratch, Snapshot, Root}; SnapshotSources: list; Flag: AdmissionFlag) or `Confinement.Refused` (Reason). `CapabilityGrant.None`, `CapabilityGrant.Web`, `CapabilityGrant.Disk(params string[] roots)`. In ConfinementPlannerTests there is a helper `private static Confinement.Planned Plan(string runtime, CapabilityGrant grant)` and constants RootA, RootB.

Example of the existing style (for Vibe):
```csharp
[Fact]
public void Vibe_None_StandsInAnEmptyScratch_AndDisk_InTheSnapshotOfItsRoots_ReadOnlyBothTimes()
{
    var disk = Plan("vibe", CapabilityGrant.Disk(RootA, RootB));
    disk.Cwd.Should().Be(CwdKind.Snapshot);
    disk.AddDirs.Should().BeEmpty();
    disk.SnapshotSources.Should().Equal([RootA, RootB]);
}

[Fact]
public void TheVibeRows_RestOnTheVibeProbe()
{
    var row = RuntimeCapabilities.Builtin.Of("vibe", Capability.Disk)!;
    row.MeasuredWith.ResultRef.Should().Contain("RESULTS_vibe_confinement.md");
    CapabilityMatrix.Admit("vibe", Capability.Web).Should().BeOfType<Admission.Refused>().Which.Reason.Should().Contain("not been measured");
}
```

Write exactly these three tests, with exactly these method names, each preceded by a line `// FILE: <ClassName>` where ClassName is CapabilityMatrixTests for tests 1 and 3 and ConfinementPlannerTests for test 2:
1. `KimiHasThreeRows_EachCitingItsCells` (CapabilityMatrixTests): Of("kimi", None/Disk/Web) are each not null; standings unconfined, unconfined, unmeasured (compare with .Spelled()); every row's MeasuredWith.Version is "2.1.1"; none row Cells contain "M-K6"; disk row Cells contain "M-K6" and "M-K9"; none and disk ResultRef contain "RESULTS_kimi_confinement.md".
2. `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (ConfinementPlannerTests): Plan("kimi", None) -> Cwd Scratch, Flags/Leading/AddDirs empty; Plan("kimi", Disk(RootA, RootB)) -> Cwd Snapshot, SnapshotSources equal [RootA, RootB], Flags/Leading/AddDirs empty; ConfinementPlanner.Plan("kimi", CapabilityGrant.Web) is Confinement.Refused whose Reason contains "not been measured" and "RESULTS_kimi_confinement.md".
3. `AdmissionFlagsBothRows` (CapabilityMatrixTests): for None and Disk, Admit("kimi", c) is Admission.Admitted with Flag == AdmissionFlag.Unconfined.
