# T1-t4 — code notes (E5.5, the consultant check for Kimi)

## Done
- Added `src_mcp/tests/AKimiConsultantCheckTests.cs` (commit 1ea41c10). Class, helpers and the four tests named as in the task document:
  - S5.5.1 `AKimiCheck_RunsInACopy_OnAOneShotHome`
  - S5.5.2 `AfterTheCheck_TheHomeIsGone_JunctionFirst` (Theory, 2 cases: answered, lost)
  - S5.5.3 `ACheckCancelledMidTurn_StillDisposesItsHome`
  - S5.5.4 `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst`
- No production code changed (E5.5 decision 1).

## Deviations from the document
- First build failed: `PlayedKimi.During` is init-only (CS8852). In S5.5.3 I set `Turn` and `During` in one object initializer, with `Turn = Answering().Turn`, as the document asks.
- S5.5.3 has the branch the document allows: if `played.Shims` is empty, assert the home folder was reserved; otherwise assert one shim launch. I did not instrument the run, so I did not record which branch ran.

## Results (host: Linux, WSL2)
- `AKimiConsultantCheckTests`: total 5, passed 5, failed 0, skipped 0. None of my tests skipped.
- `ConsultantCheckTests`: total 32, passed 30, skipped 2. The skips are `Assert.SkipUnless(OperatingSystem.IsWindows())` tests; I did not identify the second skipped test.

## Bite checks (Step 2), both undone
The file was restored from a saved copy (diff clean), rebuilt and rerun green.
- S5.5.1 J3 line, `OneShotRoot` changed to `_homes.Path`. Red:
  `Expected Path.GetDirectoryName(Path.GetDirectoryName(home)!) to be the same string because J3: under the homes root's coai-vh, never TEMP, but they differ at index 36`
- Deleter line, `BeEmpty` changed to `NotBeEmpty` in `TheDeleterNeverMetTheJunction()`. All 5 test cases red, reason:
  `Expected _unlinkedByTheDeleter not to be empty because the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first.`
  S5.5.1 failed at its J3 assertion before reaching the deleter line, so under this bite it was red for the J3 reason.

## Left
- Nothing left for t4. Not pushed. No other files touched.
