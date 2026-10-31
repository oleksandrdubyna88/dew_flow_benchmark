# SR1 t5 — code notes (AKimiQuestionRowTests)

## Done
- Added `src_mcp/tests/AKimiQuestionRowTests.cs` (commit 7e8fd4d4). No production change: `QuestionRowLaunch.cs` is unchanged (expected).
- Class run: 4 of 4 green (3 theory arms + none row).
- `AVibeQuestionRowReadsACopyOfItsRootsTests`: 6 passed, 12 skipped (Windows-only on this host).
- Format check on the new file: clean (exit 0).

## Deviations from the document
- None row prompt: the document says `question-none`. No such prompt exists; the shipped none prompt is `question-opinion` (shared/question-prompts.json). The test uses it.
- Added a private `ServiceFor(played, homesRoot, prompt, roots)` beside `Service` so the none row can name its prompt. `Service` keeps its contracted signature.
- Cancelled arm: the ask is started directly (not inside a lambda), and the assertion is `BeAssignableTo<OperationCanceledException>` because the ask throws `TaskCanceledException`.
- Windows-only skip attributes were not added; the Kimi tests run on Linux through the stand-in, as the document's expected-green note implies.

## Teeth runs (each reverted; production files show empty diff against HEAD)
- (a) `QuestionRowLaunch.Release` skips `reserved.Home.Dispose()`: all 4 red on
  `Expected Directory.EnumerateFileSystemEntries(oneShot) to be empty because coai-vh\<8> is gone, but found at least one item {…\homes\coai-vh\<8>}`.
- (b) Snapshot plan given `Workdir = input.Repo`: answered arm red on
  `Expected launched.StdIn "…" to contain "…\repo" because the prompt names the copy the shim reads.`; cancelled arm red on
  `Expected Directory.Exists(...--cwd dirname) to be False because the row's scratch folder is gone, but found True.`
- (c) `VibeHomes.EmptiedUnderItsLock` skips the junction-first `KimiHome.DisposeAllUnder` (the one-shot disposal path): 3 launched arms red on
  `Expected _unlinkedByTheDeleter to be empty … but found at least one item {…\coai-vh\<8>\kh-…\home\credentials}`. The refused arm launches nothing, so it stays green.

## Full suite — finished 11:43 UTC (after the deadline)
- Summary: total 10561, failed 3, succeeded 10474, skipped 84. Output: `scratchpad/t5-full-suite.txt`.
- The 3 failures are the two known ones plus `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (see below). Not a clean "only the two known failures".

## Earlier state (mid-run)
- Run started; at 11:41 UTC it was stuck at 10090 completed, 3 failed, with `AFeatureIsReviewedEndToEndTests.AQuestionLeftOpenOnAnEarlierHold_…` active. No summary line was written.
- Failures seen so far:
  1. `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — known (not mine).
  2. `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known (not mine).
  3. `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT in the document's known list. Expected `"unavailable: git…"`, actual `"recorded"`, on a 1 ms git deadline. Looks timing-sensitive; not touched by this change. Rerun in isolation (11:42 UTC): fails again, 1 of 10 in its class, so it is not a load flake. My commit adds only a test file, so it cannot change this test, but I did not check the parent commit (no time).
- Console line seen during the run: `[coai-mcp] the log could not be flushed: the log's disk is full` (root filesystem has 838G free; cause not found).
- Still to do: full suite summary (it did not finish by the deadline); check item 3 at the parent commit to see whether it is a pre-existing host failure.

## Left
- Full-suite result and the rerun of `TheRoundsNumstatsAreCountedTests`.
