// FILE: src_mcp/core/QuestionConsult/ConfinementPlanner.cs
```csharp
    private static Confinement Fragments(string runtime, CapabilityGrant grant, AdmissionFlag flag) => runtime switch
    {
        "claude" => Claude(grant, flag),
        "codex" => Codex(grant, flag),
        "antigravity" => Antigravity(grant, flag),
        "vibe" => Vibe(grant, flag),
        "kimi" => Kimi(grant, flag),
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

    /// <remarks>
    /// Kimi's confinement is the ACP shim's (D15), not its argv, so a plan carries no flag; its Read is bounded by the shim, but Grep and Glob read the disk directly (M-K6), so both rows are admitted FLAGGED; the roots are COPIED because a branch's AGENTS.md, skills, sub-agents and .mcp.json load from disk (M-K9). <c>web</c> never reaches here: the matrix refuses it (unmeasured).
    /// </remarks>
    private static Confinement.Planned Kimi(CapabilityGrant grant, AdmissionFlag flag) => grant.Capability switch
    {
        Capability.Disk => new("kimi", grant, [], [], CwdKind.Snapshot, [], flag) { SnapshotSources = grant.Roots },
        _ => new("kimi", grant, [], [], CwdKind.Scratch, [], flag),
    };
```

// FILE: src_mcp/runners/Kimi/KimiConsultant.cs
```csharp
    private static void MustCarryNoFlag(ConsultantLaunch launch)
    {
        if (launch.Confinement is LaunchConfinement.Planned planned && (planned.Plan.Flags.Count > 0 || planned.Plan.Leading.Count > 0))
            throw new ArgumentException("a Kimi row's confinement is the shim's; a plan with flags is not one coai made for kimi", nameof(launch));
    }

    public ReviewerInvocation Build(ConsultantLaunch launch)
    {
        ConsultantLaunches.MustBeLaunchable(launch);
        MustCarryNoFlag(launch);
        var settings = launch.Settings with { OwnedScratch = OwnedFolder(launch), Model = ModelOf(launch.Settings) };
        // ...
    }
```