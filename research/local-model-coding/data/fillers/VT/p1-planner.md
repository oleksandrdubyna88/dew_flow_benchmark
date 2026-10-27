You write C# (.NET 10, latest C#, xUnit v3 + FluentAssertions). Output ONLY code blocks, each preceded by one line naming where it goes. No explanations.

## Task A — production: add one planner arm `Kimi`
Existing code in `ConfinementPlanner` (static class, file ConfinementPlanner.cs):

```csharp
    private static Confinement Fragments(string runtime, CapabilityGrant grant, AdmissionFlag flag) => runtime switch
    {
        "claude" => Claude(grant, flag),
        "codex" => Codex(grant, flag),
        "antigravity" => Antigravity(grant, flag),
        "vibe" => Vibe(grant, flag),
        // A completion has no tools, so it has no sandbox flag: the question alone, from nowhere.
        _ => new Confinement.Planned(runtime, grant, [], [], CwdKind.Scratch, [], flag),
    };

    /// <remarks>
    /// D2's pair on every capability, never <c>--trust</c> or <c>--add-dir</c> ... Vibe reads only its working folder (M-V11, 6 of 6), so a disk row's roots are
    /// COPIED into the snapshot it stands in (D6) and named to it there; a none row stands in an empty folder.
    /// </remarks>
    private static Confinement.Planned Vibe(CapabilityGrant grant, AdmissionFlag flag) => grant.Capability switch
    {
        Capability.Disk => new("vibe", grant, [], VibeReadOnly, CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots },
        // `web` never reaches here: the matrix refuses it (no Vibe cell exercises the web).
        _ => new("vibe", grant, [], VibeReadOnly, CwdKind.Scratch, [], flag),
    };
```
Spec: add the arm `"kimi" => Kimi(grant, flag),` and a method `Kimi` in the style of `Vibe`: `disk` -> `new("kimi", grant, [], [], CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots }`; every other capability -> `new("kimi", grant, [], [], CwdKind.Scratch, [], flag)`. NO flags ever (Kimi's confinement is the shim's, D15, not its argv). `web` never reaches it (the matrix refuses it as unmeasured). Doc comment in the same remarks style, mentioning M-K6 (Read is bounded by the shim; Grep and Glob read the disk directly, so both rows are unconfined and flagged) and D6 (the roots are copied).
Give me the two edits: the new switch arm line, and the new method.

## Task B — tests in class `ConfinementPlannerTests` (file ConfinementPlannerTests.cs)
Existing in that class:
```csharp
    private const string RootA = "D:/projects/alpha";
    private const string RootB = "D:/projects/beta";
    private static Confinement.Planned Plan(string runtime, CapabilityGrant grant) =>
        ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which;
```
`CapabilityGrant.None`, `CapabilityGrant.Disk(RootA, RootB)`, `CapabilityGrant.Web` exist. `Confinement.Refused` has `.Reason`. An example existing test:
```csharp
    [Fact]
    public void Vibe_None_StandsInAnEmptyScratch_AndDisk_InTheSnapshotOfItsRoots_ReadOnlyBothTimes()
    {
        var none = Plan("vibe", CapabilityGrant.None);
        none.Cwd.Should().Be(CwdKind.Scratch);
        none.AddDirs.Should().BeEmpty();
        var disk = Plan("vibe", CapabilityGrant.Disk(RootA, RootB));
        disk.Cwd.Should().Be(CwdKind.Snapshot, "M-V11: ...");
        disk.SnapshotSources.Should().Equal([RootA, RootB], "the roots it reads through their copies");
        none.SnapshotSources.Should().BeEmpty();
    }
```
Write ONE `[Fact]` named exactly `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`:
- `none`: Planned; Cwd Scratch; Flags, Leading, AddDirs, SnapshotSources all empty.
- `disk(RootA, RootB)`: Planned; Cwd Snapshot; SnapshotSources equal [RootA, RootB]; Flags, Leading, AddDirs empty.
- `web`: `ConfinementPlanner.Plan("kimi", CapabilityGrant.Web)` is `Confinement.Refused` whose Reason contains "not been measured" and "RESULTS_kimi_confinement.md".
Use `because` strings like the example.

## Task C — tests in class `CapabilityMatrixTests` (file CapabilityMatrixTests.cs)
Existing example in that class:
```csharp
    [Fact]
    public void TheVibeRows_RestOnTheVibeProbe_AndItsWebIsUnmeasured_SoRefused()
    {
        foreach (var capability in (Capability[])[Capability.None, Capability.Disk])
        {
            var row = RuntimeCapabilities.Builtin.Of("vibe", capability)!;
            row.MeasuredWith.ResultRef.Should().Contain("RESULTS_vibe_confinement.md");
            row.MeasuredWith.Cells.Should().NotBeEmpty($"vibe x {capability} stands on measured cells");
            CapabilityMatrix.Admit("vibe", capability).Should().BeOfType<Admission.Admitted>()
                .Which.Flag.Should().Be(AdmissionFlag.None, "...");
        }
        RuntimeCapabilities.Builtin.Of("vibe", Capability.Disk)!.MeasuredWith.Cells.Should().Contain("M-V6", "...");
        CapabilityMatrix.Admit("vibe", Capability.Web).Should().BeOfType<Admission.Refused>()
            .Which.Reason.Should().Contain("not been measured", "...");
    }
```
Row objects have `.Standing` (enum `Standing` with members including `Unconfined`, `Unmeasured`; check as `Standing.Unconfined`) and `.MeasuredWith.Version`, `.MeasuredWith.Cells` (list of string), `.MeasuredWith.ResultRef`.
Write two `[Fact]`s:
1. `KimiHasThreeRows_EachCitingItsCells`: `RuntimeCapabilities.Builtin.Of("kimi", Capability.None)` / `Disk` / `Web` each not null; none: Standing Unconfined, Cells contain "M-K6"; disk: Unconfined, Cells contain "M-K6" and "M-K9"; web: Unmeasured; every row's MeasuredWith.Version is "2.1.1"; ResultRef of every row contains "RESULTS_kimi_confinement.md".
2. `AdmissionFlagsBothRows`: `CapabilityMatrix.Admit("kimi", Capability.None)` and `(…, Capability.Disk)` are `Admission.Admitted` with `.Flag` equal `AdmissionFlag.Unconfined`.
