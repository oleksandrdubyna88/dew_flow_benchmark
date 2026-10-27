# O1 t2 — code notes (E5.6: planner Kimi arm, KimiConsultant flags refusal)

## Done
- `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`: `"kimi" => Kimi(grant, flag)` in `Fragments`; `Kimi(...)` after `Vibe(...)`, with its `<remarks>`. Disk gives Snapshot with `SnapshotSources = grant.Roots`; none gives Scratch; no flags. `SandboxVocabulary` unchanged.
- `src_mcp/runners/Kimi/KimiConsultant.cs`: `MustCarryNoFlags(launch)` called after `MustBeLaunchable`; method placed after `OwnedFolder`.
- `src_mcp/tests/ConfinementPlannerTests.cs`: S5.6.3, S5.6.5, S5.6.6 tests as the contracts give them.
  One deviation: the `AcpWords` reference in S5.6.5 is written `Runners.Acp.AcpWords.SessionFlag`, because the bare name did not compile in the test namespace.
- Commit `3dba02fd` on `exp/kimi-e56-opus-split-haiku-1`, three files only. Not pushed.

## Red (before production code)
- S5.6.3: failed as the document says (`disk.Cwd` expected Snapshot, found Scratch).
- S5.6.6: failed as the document says (no ArgumentException thrown).
- S5.6.5: NOT green before the change, contrary to the document's "may already be green". It failed at the disk row's WorkingDirectory (Scratch instead of the copy), which is the missing planner arm. Green after the planner arm.

## Green (after)
- Build: succeeded.
- `ConfinementPlannerTests`: 34/34.
- `KimiConsultantTests`: 12/12.
- Full MCP suite: 10551 total, 3 failed, 10464 passed.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known, not mine.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known, not mine.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT in the document's known list. It failed in both full runs and passes when its class runs alone (10/10). The assertion is on `DeltaAsync` with a 1 ms deadline (state "recorded" instead of "unavailable: git…"), so it looks timing-sensitive, and it lives in a file I did not touch. I did not check it against the pre-change tree, so I cannot say it is pre-existing. Its owner should look.

## Left / problems
- The 20:48 UTC deadline had passed by the time of the commit (the clock read 20:55).
