# S4-t3 — code notes

Commit: e1b965e9 (three files: ConfinementPlanner.cs, KimiConsultant.cs, ConfinementPlannerTests.cs). Not pushed.

## Done
- Tests written first (items 1–5 of the task document). Red against b4e2ea96 + t1:
  - ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb (S5.6.3):
    `Expected disk.Cwd to be CwdKind.Snapshot {value: 2} ... but found CwdKind.Scratch {value: 0}.` — as documented.
  - AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome (S5.6.5):
    `Expected disk.Request.WorkingDirectory to be the same string ... but they differ at index 10` (the document's
    "<RowTree> vs <Scratch>" shape; FluentAssertions reports the first differing index). Same reason: default arm planned Scratch.
  - APlanWithFlags_IsRefusedForKimi (S5.6.6):
    `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` — as documented.
  - The Theory row kimi/Web and the web-refusal part of S5.6.3 were already green (t1).
- Production: `"kimi" => Kimi(grant, flag)` arm and `Kimi` method (ConfinementPlanner.cs); `MustCarryNoFlag` called in
  `KimiConsultant.Build` and defined as a private static (KimiConsultant.cs). Written as the two-property pattern in the document.
- Green: ConfinementPlannerTests 35/35, KimiConsultantTests 12/12.
- Build: succeeded, 0 warnings, 0 errors.

## Whole suite (10547 run, 84 skipped)
Failed 3:
- KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed — known, Linux/WSL, not mine.
- KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn — known, not mine.
- TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — NOT in the document's known list. It expects a
  1 ms git deadline to yield "unavailable: git…" and got "recorded". A timing race on git; it touches no file of this task.
  Not verified against the base commit (no stash used, per the shared-stash rule). Needs a look by whoever owns the rounds code.

## dotnet format --verify-no-changes
Exit 2. All 54 output lines are IL2026/IL3050 trimming diagnostics in src_server (Program.cs, ReviewEndpoints.cs, SessionEndpoints.cs,
PeopleEndpoints.cs, UsageEndpoints.cs, CatalogEndpoints.cs) and src_bugs (AdminApi.cs, Program.cs). None mention my three files.
Not verified against the base commit. So format is NOT clean at repo level; my files are not the cause.

## Left
- Nothing of t3's own. Open: the numstats failure above, and the format diagnostics in src_server/src_bugs (outside this task).
