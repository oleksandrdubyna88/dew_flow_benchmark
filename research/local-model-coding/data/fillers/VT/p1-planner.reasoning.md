Here's a thinking process:

1.  **Analyze User Input:**
   - **Language/Framework:** C# (.NET 10, latest C#, xUnit v3 + FluentAssertions)
   - **Output Format:** ONLY code blocks, each preceded by one line naming where it goes. No explanations.
   - **Task A:** Production code edit for `ConfinementPlanner.cs`
     - Add switch arm: `"kimi" => Kimi(grant, flag),`
     - Add method `Kimi` in style of `Vibe`:
       - `disk` -> `new("kimi", grant, [], [], CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots }`
       - others -> `new("kimi", grant, [], [], CwdKind.Scratch, [], flag)`
     - NO flags ever (confinement is shim's, D15, not argv)
     - `web` never reaches it
     - Doc comment in same remarks style, mentioning M-K6 (Read bounded by shim; Grep/Glob read disk directly, so both rows unconfined and flagged) and D6 (roots copied)
     - Output: two edits (switch arm line, new method)
   - **Task B:** Tests in `ConfinementPlannerTests.cs`
     - Existing setup provided
     - Write ONE `[Fact]` named exactly `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`
     - Checks for `none`, `disk`, `web` as specified
     - Use `because` strings like example
   - **Task C:** Tests in `CapabilityMatrixTests.cs`
     - Existing setup provided
     - Write TWO `[Fact]`s:
       1. `KimiHasThreeRows_EachCitingItsCells`: checks None/Disk/Web rows properties
       2. `AdmissionFlagsBothRows`: checks Admitted with Unconfined flag for None/Disk

2.  **Deconstruct Task A (Production Code):**
   - Need to output exactly the switch arm line and the method.
   - Switch arm: `"kimi" => Kimi(grant, flag),`
   - Method signature: `private static Confinement.Planned Kimi(CapabilityGrant grant, AdmissionFlag flag)`
   - Implementation:
     ```csharp
     private static Confinement.Planned Kimi(CapabilityGrant grant, AdmissionFlag flag) => grant.Capability switch
     {
         Capability.Disk => new("kimi", grant, [], [], CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots },
         _ => new("kimi", grant, [], [], CwdKind.Scratch, [], flag),
     };
     ```
   - Doc comment: Needs to match the style of the `Vibe` remarks. Mention M-K6, Grep/Glob, D6.
     Let's draft it carefully based on the prompt:
     ```csharp
     /// <remarks>
     /// M-K6: Read is bounded by the shim; Grep and Glob read the disk directly, so both rows are unconfined and flagged.
     /// D6: the roots are copied into the snapshot it stands in.
     /// </remarks>
     ```
     Wait, the prompt says: "Doc comment in the same remarks style, mentioning M-K6 (Read is bounded by the shim; Grep and Glob read the disk directly, so both rows are unconfined and flagged) and D6 (the roots are copied)."
     I'll format it exactly like the existing `Vibe` remarks but adapted for Kimi.
     Let's craft it:
     ```csharp
     /// <remarks>
     /// M-K6: Read is bounded by the shim; Grep and Glob read the disk directly, so both rows are unconfined and flagged.
     /// D6: the roots are copied into the snapshot it stands in.
     /// </remarks>
     ```
     Actually, the prompt says "in the same remarks style". I'll keep it concise and matching the tone.
     I will output exactly the two requested pieces.

   Check constraints: "NO flags ever (Kimi's confinement is the shim's, D15, not its argv)." -> This means the `flag` parameter is passed but maybe ignored? Or just that we don't modify it. The prompt says `new(..., flag)` which matches the existing pattern. I'll just pass `flag` as is.

   Let's assemble Task A output:
   ```csharp
   // ConfinementPlanner.cs
   "kimi" => Kimi(grant, flag),
   ```
   Wait, the prompt says "Give me the two edits: the new switch arm line, and the new method." I will provide them clearly.

   Actually, I'll just output the exact lines/method as requested.

   Draft for Task A:
   ```csharp
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
   ```

3.  **Deconstruct Task B (Tests):**
   - Class: `ConfinementPlannerTests`
   - Fact name: `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`
   - Checks:
     - `none`: Planned; Cwd Scratch; Flags, Leading, AddDirs, SnapshotSources all empty.
     - `disk(RootA, RootB)`: Planned; Cwd Snapshot; SnapshotSources equal [RootA, RootB]; Flags, Leading, AddDirs empty.
     - `web`: `ConfinementPlanner.Plan("kimi", CapabilityGrant.Web)` is `Confinement.Refused` whose Reason contains "not been measured" and "RESULTS_kimi_confinement.md".
   - Use `because` strings.
   - I need to write the full fact method.

   Draft:
   ```csharp
   [Fact]
   public void ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb()
   {
       var none = Plan("kimi", CapabilityGrant.None);
       none.Should().BeOfType<Confinement.Planned>();
       none.Cwd.Should().Be(CwdKind.Scratch, "M-K6: none stands in an empty scratch");
       none.Flags.Should().BeEmpty("no flags ever");
       none.Leading.Should().BeEmpty("no leading args");
       none.AddDirs.Should().BeEmpty("no added dirs");
       none.SnapshotSources.Should().BeEmpty("no snapshot sources for none");

       var disk = Plan("kimi", CapabilityGrant.Disk(RootA, RootB));
       disk.Should().BeOfType<Confinement.Planned>();
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
   ```
   Check against constraints: "Use `because` strings like the example." The example uses `Should().Be(..., "...")`. I'll adjust to match that style more closely if needed, but FluentAssertions allows `because` as a second argument or via `.BecauseOf()`. The prompt says "Use `because` strings like the example." The example shows: `disk.Cwd.Should().Be(CwdKind.Snapshot, "M-V11: ...");` So I'll stick to that pattern.

   Refined Task B:
   ```csharp
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
   ```
   Wait, the prompt says `Confinement.Planned` check is already done by `Plan()` helper, but I can add explicit checks or rely on helper. The helper does `.Should().BeOfType<Confinement.Planned>().Which`. I'll just use `none.Cwd...` etc. It's fine.

4.  **Deconstruct Task C (Tests):**
   - Class: `CapabilityMatrixTests`
   - Fact 1: `KimiHasThreeRows_EachCitingItsCells`
     - `RuntimeCapabilities.Builtin.Of("kimi", Capability.None)` / `Disk` / `Web` each not null
     - none: Standing Unconfined, Cells contain "M-K6"
     - disk: Unconfined, Cells contain "M-K6" and "M-K9"
     - web: Unmeasured
     - every row's MeasuredWith.Version is "2.1.1"
     - ResultRef of every row contains "RESULTS_kimi_confinement.md"
   - Fact 2: `AdmissionFlagsBothRows`
     - `CapabilityMatrix.Admit("kimi", Capability.None)` and `(…, Capability.Disk)` are `Admission.Admitted` with `.Flag` equal `AdmissionFlag.Unconfined`.

   Draft Fact 1:
   ```csharp
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
   ```

   Draft Fact 2:
   ```csharp
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
   ```

   Check constraints: "Row objects have `.Standing` (enum `Standing` with members including `Unconfined`, `Unmeasured`; check as `Standing.Unconfined`) and `.MeasuredWith.Version`, `.MeasuredWith.Cells` (list of string), `.MeasuredWith.ResultRef`."
   All matches. I'll ensure syntax is correct for latest C# (collection expressions `[]` are supported).

   One minor thing: `diskRow.MeasuredWith.Cells.Should().Contain(["M-K6", "M-K9"])` works in FluentAssertions with collection expressions. I'll keep it.

   Let's verify Task B's `Plan` helper usage. The prompt says:
   `private static Confinement.Planned Plan(string runtime, CapabilityGrant grant) => ConfinementPlanner.Plan(runtime, grant).Should().BeOfType<Confinement.Planned>().Which;`
   So `Plan("kimi", ...)` returns `Confinement.Planned`. That's fine.

   For Task B, I should make sure I'm using the exact names and structure requested.
   "Write ONE `[Fact]` named exactly `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`:"
   I'll format it cleanly.

   One detail: `Confinement.Refused` has `.Reason`. The prompt says: `web: ConfinementPlanner.Plan("kimi", CapabilityGrant.Web) is Confinement.Refused whose Reason contains "not been measured" and "RESULTS_kimi_confinement.md".`
   I'll write:
   ```csharp
   var web = ConfinementPlanner.Plan("kimi", CapabilityGrant.Web);
   web.Should().BeOfType<Confinement.Refused>()
       .Which.Reason.Should().Contain("not been measured")
       .And.Contain("RESULTS_kimi_confinement.md", "the matrix refuses web as unmeasured");
   ```
   This matches FluentAssertions chaining.

   All tasks seem ready. I will output exactly as requested: code blocks preceded by one line naming where it goes. No explanations.

   Structure:
   // ConfinementPlanner.cs
   [code]
   // ConfinementPlannerTests.cs
   [code]
   // CapabilityMatrixTests.cs
   [code]

   Double-check Task A spec: "Give me the two edits: the new switch arm line, and the new method."
   I will output exactly those two pieces.

   Double-check Task B spec: "Write ONE [Fact] named exactly..."
   I will output just that fact.

   Double-check Task C spec: "Write two [Fact]s: ..."
   I will output both facts.

   All good. I will generate the response now.
   One minor adjustment: In Task B, the prompt says `none.Flags.Should().BeEmpty(...)`, but `Confinement.Planned` might not have a `Flags` property directly exposed in the prompt's context, but it's standard. I'll stick to what's requested. The prompt explicitly lists: "Flags, Leading, AddDirs, SnapshotSources all empty." So I'll check them.
   Also, `Confinement.Planned` constructor in Task A uses `[], []` for flags and leading? Wait, the constructor signature in the prompt is: `new("kimi", grant, [], [], CwdKind.Snapshot, [], flag)`. That's 7 arguments. The prompt says `Flags, Leading, AddDirs, SnapshotSources`. I'll assume the order matches typical patterns or just follow the prompt's explicit checks. I'll check `Flags`, `Leading`, `AddDirs`, `SnapshotSources` as requested.

   Actually, looking at the prompt's `Vibe` method: `new("vibe", grant, [], VibeReadOnly, CwdKind.Snapshot, [], flag)`. It has 6 args. The prompt says `new("kimi", grant, [], [], CwdKind.Snapshot, [], flag)`. That's 7 args. I'll trust the prompt's exact signature for Task A. For tests, I'll just check the properties mentioned.

   I'll produce the code now. 
   One thing: xUnit v3 uses `[Fact]` still, but sometimes `[Theory]` etc. The prompt says xUnit v3, but `[Fact]` is fine.
   I will ensure FluentAssertions syntax is correct for .NET 10 / latest C#. Collection expressions `[]` are valid.
   All set. 
   Output matches request exactly.✅
   Proceeds. 
   Self-Correction/Refinement during thought:
   In Task B, the prompt says: "`none`: Planned; Cwd Scratch; Flags, Leading, AddDirs, SnapshotSources all empty."
   I'll write exactly that.
   In Task C, `RuntimeCapabilities.Builtin.Of` returns nullable, so I'll handle nulls gracefully or just assert `.Should().NotBeNull()` first, then access properties. FluentAssertions handles nulls gracefully if chained properly, but explicit `.Should().NotBeNull()` is safer. I'll keep it simple.
   The prompt says: `Row objects have .Standing ... .MeasuredWith.Version, .MeasuredWith.Cells ... .MeasuredWith.ResultRef.`
   I'll access them directly after null checks or just chain `.Should().NotBeNull()` then access. Actually, FluentAssertions allows `.Should().NotBeNull().Which.Standing...`
   I'll write it cleanly.

   Final check of the prompt: "Output ONLY code blocks, each preceded by one line naming where it goes. No explanations."
   I will strictly follow this.
   Ready. 
   Generating. 
   [Output Generation] -> matches the refined code blocks. 
   Note: I'll make sure the formatting matches exactly. No extra text. Only code blocks with filenames/comments above them.
   Task A: `// ConfinementPlanner.cs`
   Task B: `// ConfinementPlannerTests.cs`
   Task C: `// CapabilityMatrixTests.cs`
   All good. 
   Proceeds. 
   *(Self-Correction/Verification during drafting)*
   Wait, Task A says: `new("kimi", grant, [], [], CwdKind.Snapshot, [], flag)`. The prompt explicitly gives this signature. I will copy it exactly.
   Task B: `Confinement.Planned` helper `Plan` is used. I'll use it.
   Task C: `CapabilityMatrix.Admit` returns `Admission.Admitted` or `Refused`. I'll cast/check as requested.
   Everything aligns. Output matches. 
   Proceeds. 
   [Output Generation] (matches the final refined version)
   ```csharp
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
   ```
   ```csharp
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
   ```
   ```csharp
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
   ```
   All matches requirements. Output matches exactly. No extra text. Proceeds. 
   One minor point: `Capability.None` vs `CapabilityGrant.None`? The prompt says `CapabilityGrant.None` for tests, but `Capability.None` for matrix tests. I used exactly what was specified.
   The prompt says `Capability.None` / `Capability.Disk` / `Capability.Web` for matrix tests. I used that.
   All good.✅
   Output generation. 
   [Done] 
   *Self-Correction/Note during thought*: 
   The prompt says `CapabilityGrant.None`, `CapabilityGrant.Disk(RootA, RootB)`, `CapabilityGrant.Web` exist. And for matrix tests: `Capability.None`, `Capability.Disk`, `Capability.Web`. I'll stick to that.
   The `Plan` helper in Task B expects `CapabilityGrant`. I used `CapabilityGrant.None`, etc. Correct.
   Everything is aligned. Output matches. 
   Proceeds. 
   [Final Output Generation] -> matches the refined code blocks exactly. 
   *(Done.)*
