# SR1-t4 — consultant check on Kimi (AKimiConsultantCheckTests)

## What I did
- Added `src_mcp/tests/AKimiConsultantCheckTests.cs` with the five test cases the document names:
  - `AKimiCheck_RunsInACopy_OnAOneShotHome` (S5.5.1)
  - `AfterTheCheck_TheHomeIsGone_JunctionFirst`, two arms: "answered" and "the turn fails exit 70" (S5.5.2)
  - `ACheckCancelledMidTurn_StillDisposesItsHome` (S5.5.3)
  - `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` (S5.5.4)
- Production code: none changed. The document expected this.
- Commit `60eb05f1` on `tmp/SR1-t4`. It adds only the new test file. Not pushed.

## Results
- Build: succeeded, 0 warnings, 0 errors.
- `AKimiConsultantCheckTests`: 5 of 5 passed (first run, before any break).
- `ConsultantCheckTests`: 30 passed, 2 skipped (Windows-only skips, as the document says).
- `AOneShotKimiHomeIsDisposedJunctionFirstTests`: 10 of 10 passed.
- No known failures from the document appeared in these three classes.
- `dotnet format --verify-no-changes` on the whole solution: exit 2. Every message is an IL2026/IL3050 trimming analyzer error in `src_bugs` (32) and `src_server` (76). None is in my files, and none is a whitespace finding. A targeted `dotnet format whitespace` on `src_mcp/tests/CoaiMcp.Tests.csproj` with `--include` my file: exit 0.

## Teeth runs (each reverted; tree clean after)
- (a) `ConsultantCheck.TurnAsync`: `held.Home.Dispose()` skipped. 3 red:
  - test 2 "answered": `coai-vh\<8>` not empty
  - test 2 "the turn fails exit 70": `coai-vh\<8>` not empty
  - test 3: `coai-vh\<8>` not empty
  - Test 1 stayed green. The document did not predict it would go red.
- (b) `ConsultCheckScratch.KimiHomesDisposed`: loop over the two folders replaced by an empty array (no `DisposeAllUnder`). 1 red:
  - test 4: `_unlinkedByTheDeleter` not empty, first item `…/own/kh-…/home/credentials`. This matches the document.
- (c) `ConsultantCheck.PlaceAsync`: `NeedsASanitisedSnapshot` check replaced by `false && …`, so `Checkout` is always used. 5 of 5 red. This DIFFERS from the document, which said test 1 goes red on `--cwd`:
  - Test 1 and test 4 failed on `state`: `failed`, `failureKind: record-failed`. The message says: "a Kimi consultant stands in the conversation's sanitised snapshot (Workdir), and none was made — the checkout is never its working folder (D6) (Parameter 'launch')". The Kimi adapter refuses a launch with no snapshot, so no shim starts, and the `--cwd` assertion is never reached.
  - Tests 2 (both arms): `Expected played.Shims to contain a single item, but the collection is empty`. This assertion is one I added; the document did not list it.
  - Test 3: 30 s wait timeout (`TimeoutException`), because the shim never started and `started` was never set. This is a weak point of the test: its wait is 30 s before it fails.
  - Conclusion: the `--cwd` assertion in test 1 is not reached under break (c). A second guard in the Kimi adapter catches the break first. The test still goes red, but not for the reason the document gives.

## Deviations from the document
1. The document says "No commit" in its Out of bounds section. The task brief says to commit with a conventional message. I followed the task brief and committed the one file.
2. Test 2 has an extra assertion: `played.Shims.Should().ContainSingle()`. It proves a shim ran before the disposal checks. It is stricter, not weaker.
3. Test 4 does not call `ConsultCheckScratch.Delete` by hand, and does not build `ConsultantCheckTurn` by hand. `ConsultantCheckMode.AnswerAsync` already runs the root-less shape (an empty `ConsultantHomesRoot` makes `ReserveOneShot` return None, so the home goes to `<scratch>\own\kh-*`). It also deletes the scratch itself. The test checks the `--home` path under a `coai-check-` scratch and the `own` folder name. It then checks that no `coai-check-*` folder is left.
4. On Linux the test binary is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (no `.exe`). The low-priority wrapper gave ENOENT with the `.exe` path.

## Left undone
- Nothing from the task list is left open.
- Not checked: the two known-red tests from the document (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`). I did not run those classes, because the document scopes my run to the three classes above.
- The format gate is not clean at solution level, because of the analyzer errors in `src_bugs` and `src_server`. Those files are outside my scope and I did not touch them.
- No files were left outside a `testsToDelete-` folder. The tests make their folders under `testsToDelete-kimi-check-` and `testsToDelete-kimi-`.
