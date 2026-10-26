Write production C# code. Style: C# 14/.NET 10, expression-bodied members, target-typed new, collection expressions, XML <remarks>/<summary> docs in the density shown below. Output the changed members in one ```csharp block per file, each preceded by a line `// FILE: <path>`.

Task 1 — file src_mcp/core/QuestionConsult/ConfinementPlanner.cs. Existing code:
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
    /// D2's pair on every capability, never <c>--trust</c> or <c>--add-dir</c> (D3: a trusted folder runs its hooks). Vibe reads only its working folder (M-V11, 6 of 6), so a disk row's roots are
    /// COPIED into the snapshot it stands in (D6) and named to it there; a none row stands in an empty folder.
    /// </remarks>
    private static Confinement.Planned Vibe(CapabilityGrant grant, AdmissionFlag flag) => grant.Capability switch
    {
        Capability.Disk => new("vibe", grant, [], VibeReadOnly, CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots },
        // `web` never reaches here: the matrix refuses it (no Vibe cell exercises the web).
        _ => new("vibe", grant, [], VibeReadOnly, CwdKind.Scratch, [], flag),
    };
```
Add the arm `"kimi" => Kimi(grant, flag),` (after vibe) and a method `Kimi` of the same shape as `Vibe`: disk -> `new("kimi", grant, [], [], CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots }`; none (default arm) -> `new("kimi", grant, [], [], CwdKind.Scratch, [], flag)`. No flags ever. Remarks: Kimi's confinement is the ACP shim's (D15), not its argv, so a plan carries no flag; its Read is bounded by the shim, but Grep and Glob read the disk directly (M-K6), so both rows are admitted FLAGGED; the roots are COPIED because a branch's AGENTS.md, skills, sub-agents and .mcp.json load from disk (M-K9). `web` never reaches here: the matrix refuses it (unmeasured).

Task 2 — file src_mcp/runners/Kimi/KimiConsultant.cs, method Build. Existing:
```csharp
    public ReviewerInvocation Build(ConsultantLaunch launch)
    {
        ConsultantLaunches.MustBeLaunchable(launch);
        var settings = launch.Settings with { OwnedScratch = OwnedFolder(launch), Model = ModelOf(launch.Settings) };
        // ...
    }
```
`launch.Confinement` is a `LaunchConfinement`; the planned case is `LaunchConfinement.Planned planned` with `planned.Plan` of type `Confinement.Planned` having `.Flags` and `.Leading` (IReadOnlyList<string>). Give: a private static method `MustCarryNoFlag(ConsultantLaunch launch)` that throws `ArgumentException` (message: "a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", paramName nameof(launch)) when the launch is Planned and its plan has any Flags or Leading entry; and the one line to insert in Build after MustBeLaunchable to call it. Give both as snippets.
