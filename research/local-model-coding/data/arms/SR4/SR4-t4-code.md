# SR4 t4 — code notes (S5.5.1–S5.5.4, E5.5)

## What I did

- Added `src_mcp/tests/AKimiConsultantCheckTests.cs` (new). Only this file. No production code.
- Five tests:
  - `AKimiCheck_RunsInACopy_OnAOneShotHome` (S5.5.1)
  - `AfterTheCheck_TheHomeIsGone_JunctionFirst`, two arms: `answered`, `failed-exit-70` (S5.5.2)
  - `ACheckCancelledMidTurn_StillDisposesItsHome` (S5.5.3)
  - `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` (S5.5.4)
- Commit `3451b066` (only the test file, added by name).

## Deviation from the document

- S5.5.3: the document expects state `cancelled`. That word does not exist in `ConsultCheckStates`.
  The real output is `state: "failed"` with `failureKind: "cancelled"`
  (`failureWhat`: "the call was cancelled while the consultant (my-kimi) was running").
  The test asserts that real shape. The check did not throw `OperationCanceledException`;
  it answered with the failed record. This is not a production breach.

## Teeth runs (temporary breaks, all reverted)

All five tests were red at once, each for its own reason:

- S5.5.1: `own\tree` changed to `own\nothing` → red (WorkingDirectory assertion).
- S5.5.2 (both arms): `left-behind` folder created under `coai-vh` → red (folder-empty assertion).
- S5.5.2 and S5.5.4: `recorded.Add("control")` → red (recorder assertion).
- S5.5.3: `cancel.CancelAsync()` removed from `During` → red (state assertion, state was not `failed`).

After the reverts: the file in git has no break in it (grep checked).

## Test results (Linux host, Debug build, 0 warnings)

- `AKimiConsultantCheckTests`: 5 / 5 green.
- `ConsultantCheckTests`: 30 passed, 2 skipped (Windows-only Vibe tests, `Assert.SkipUnless`), 0 failed.
- `NoTestHoldsAnInheritableHandleTests`: 2 / 2 green.
- No leftover `testsToDelete-kcheck-*` folder under `/tmp`.

## Open points

- The document says "Do not commit" (Files / Out of bounds / Done when). The coder rules say commit with
  the files named. I followed the coder rules and committed one file. If the commit is not wanted,
  `git reset --soft HEAD~1` undoes it (the file stays in the tree).
- The document's Commands list names `dew_flow_connect_other_ais.slnx` and `CoaiMcp.Tests.exe`. On this host
  the test binary is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (no `.exe`), and the low-priority wrapper
  could not spawn the `.exe` path. I ran the binary without `.exe`.
- The two known failing tests (`KimiRuntimeTests...`, `KimiHomeTests...`) were not run; they are outside this task.
- Nothing left undone for S5.5.1–S5.5.4.
