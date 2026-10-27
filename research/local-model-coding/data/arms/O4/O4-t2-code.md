# O4 t2 — code notes (E5.6: planner kimi arm, KimiConsultant refusing flags)

Commit: eeb39060 on exp/kimi-e56-opus-split-haiku-4 (only the three named files).

## What I did
- Tests first (ConfinementPlannerTests.cs): S5.6.3 fact, widened refusal theory (kimi/web), unknown-runtime sentence
  with `kimi`, S5.6.5 fact, S5.6.6 fact, `KimiAdapter()` helper (Adapters() kimi entry calls it).
- Red run, then production code: `"kimi" => Kimi(grant, flag)` arm and `Kimi(...)` in ConfinementPlanner.cs;
  `MustCarryNoFlags` and its call in KimiConsultant.cs. Nothing else changed in KimiConsultant.cs.

## Red messages (before the production change)
- S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot ... but found CwdKind.Scratch`.
  The document said `plan.Cwd`; the variable in the test is `disk`. Same failure.
- S5.6.5: NOT green before the change, as the document feared it might be. It failed with
  `Expected disk.Request.WorkingDirectory to be ... RowTree ... but they differ at index 10: "/tmp/coai-scratch-…" vs "/tmp/coai-question-…"`.
  The cause is the planner's default arm (Scratch for a disk row), not KimiConsultant. Green after the planner arm.
  So no "pin" mutation was needed or run.
- S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` as documed.

## Refusal places in S5.6.5
The document said to pass the same value the seams' lambda receives. That value is `root`, the home folder
(`KimiHome.HomeFor`, the same as the `--home` argv value), so the test uses `KimiPlaces.ForShim(home, persons-bin)`.
The `Refusal` call passes with this, so the first argument is the home, not the owned root.

## Results (after the production change)
- Build: succeeded, 0 errors.
- ConfinementPlannerTests: 35/35 pass. KimiConsultantTests: 12/12. CapabilityMatrixTests: 39/39.
- Whole MCP suite: 10552 total, 10465 passed, 84 skipped, 3 failed:
  - KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn (known)
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed (known)
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow: NOT in the known list. It failed on the
    first full run. It is a git timing test (a 1 ms bound). Re-run: 10/10 pass. Judged flaky and unrelated to my files.
- dotnet format: the full-solution `--verify-no-changes` exits 2, with only IL2026/IL3050 analyzer errors in
  CoaiServer (src_server), not in my files. Format restricted to my three files: exit 0.
  I did not stash to check the baseline (other tasks share the worktree).
- Cyclomatic complexity: each new method is a single expression or a single if, within the limit of 4.

## Left undone
- Nothing from the task. The full-solution format run is not clean because of the CoaiServer analyzer errors above;
  these are outside this task's files.
