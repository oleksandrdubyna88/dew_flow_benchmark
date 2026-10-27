# S1-t3 - code notes (AKimiConsultantCheckTests)

## Done
- New file `src_mcp/tests/AKimiConsultantCheckTests.cs`: S5.5.1, S5.5.2 (theory: passed, fails), S5.5.3, S5.5.4. Five test cases in total.
- No production file changed. Only the new test file is committed.

## Results (Linux host)
- Build of `dew_flow_connect_other_ais.slnx`: succeeded, 0 errors, 0 warnings.
- `AKimiConsultantCheckTests`: 5 of 5 green on arrival, as the document expected (E5.3 and E5.4 are in).
- `ConsultantCheckTests`: 30 passed, 0 failed, 2 skipped. The two skipped are the Vibe tests, Windows-only by their own `Assert.SkipUnless`.
- `dotnet format --verify-no-changes`: exit 2. Every error it reports is an IL2026/IL3050 analyzer error in `src_server/src/*.cs`. None mention `AKimiConsultantCheckTests`. Not checked against base `b4e2ea96`; left as found.

## Temporary breaks (each reverted; `git status` shows only the new file)
- a. `ConsultantCheck.cs` PlaceAsync: condition forced to `false`, so Checkout is returned instead of the copy. RED: 4 of 5. S5.5.1 failed with `record-failed` ("a Kimi consultant stands in the conversation's sanitised snapshot ... none was made"). S5.5.2 both arms and S5.5.3 also failed; S5.5.4 passed.
- b. `ConsultantCheck.cs` TurnAsync finally: `held.Home.Dispose();` removed. RED: S5.5.2 both arms and S5.5.3, on `TheOneShotFolderIsEmpty` ("found at least one item ... coai-vh/<8 hex>").
- c. The document named `VibeHomes.cs` `OneShotHome.Dispose`. That method has no `KimiHome.DisposeAllUnder` call; it reaches it through `Emptied()` and `VibeHomes.EmptiedUnderItsLock`. I broke the call in `EmptiedUnderItsLock` instead (`KimiHome.DisposeAllUnder(...)` replaced by `GitScratch.DeleteEvenIfReadOnly(folder)`). RED: S5.5.2 both arms and S5.5.3, on `TheDeleterNeverMetTheJunction` ("...kh-.../home/credentials" recorded).
- d. `ConsultCheckScratch.cs` Delete: `KimiHomesDisposed(root, warn)` replaced by `true`. RED: S5.5.4 only, on `TheDeleterNeverMetTheJunction` ("...own/kh-.../home/credentials" recorded).

## Deviations and open items
- Break c is at a different location from the document (see above).
- The document's out-of-bounds list says "Do not run git commit". The coder instructions say to commit with a conventional message. I followed the coder instructions, committed the new file only, and did not push.
- S5.5.3 expected `failureKind` = `cancelled`. On the unbroken run it is `cancelled`, so no assertion was weakened.
- No test was skipped by a platform guard; the stand-in runs Kimi on Linux.
- The known failures at `b4e2ea96` (KimiRuntimeTests, KimiHomeTests) are outside the classes this task runs, so they were not run.
