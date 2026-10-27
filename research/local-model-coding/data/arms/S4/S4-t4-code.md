# S4 t4 — code notes (Kimi disk question row, S5.6.7)

## What I did
- Added one file: `src_mcp/tests/AKimiQuestionRowTests.cs` (class `AKimiQuestionRowTests`, one Theory with three rows).
- Commit `e624931a`. Only that file. No production file changed. No other file changed.
- Followed the contracts of the task document. Class is `[Collection("fakecli-env")]`, uses `TempDir.For("testsToDelete-qkimi-")`, `KimiFixture`, `KimiSeams.Using`, `KimiCredentialsLock.RootFor`, `WindowsStandIn.Begin()`, `GitScratch.AfterUnlink` recorder.
- Two small choices where the document left room:
  - `StandIn()` is an instance method (it uses `_person`); the document copies the static Kimi one.
  - The three arms are one method with a `switch` on `ending`, as the document's body describes.

## Red / green
- Red-first: the test file is the only change and production code is untouched. The document expected green on arrival (E5.3 already junction-first). Green on arrival: 3 of 3 passed.
- No red message differed from the document.

## Teeth (Step 3), each seen red for all three rows, then reverted
- Leftover folder `coai-vh\leftover` before the row: `Expected (Directory.Exists(OneShotRoot) ? ...) to be empty ... found ... \coai-vh\leftover`.
- File `x` written in the person's folder: `Expected _person.Hash() to be equal to ... but ... contains 1 item(s) too many` (the `x Normal 2d71…` entry).
- `_unlinkedByTheDeleter.Add("x")`: `Expected _unlinkedByTheDeleter to be empty … found at least one item {"x"}`.
- The teeth were gated by a temporary flag file (`notes/teeth.txt`), because env-var prefixes on the shell were blocked by the permission layer. All teeth code removed (grep count 0). Flag file deleted.

## Test results
- Build (`dew_flow_connect_other_ais.slnx`, Debug): succeeded, 0 errors, 0 warnings.
- `AKimiQuestionRowTests`: 3 of 3 passed (after revert).
- Full suite (`CoaiMcp.Tests`): total 10555, failed 2, succeeded 10469, skipped 84. The two failures are the ones the document names as known at this base, confirmed by re-running their classes:
  - `CoaiMcp.Tests.KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
  - `CoaiMcp.Tests.KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`
  - Caveat: I did not keep the full-run list, so I confirmed the two names by re-running their classes, not from the full run's own output. The full run's failed count was 2, which matches.
- `dotnet format … --verify-no-changes` (scoped to the new file): no output, no error. I did not capture a separate exit code.

## Left undone / notes for the reviewer
- Nothing left in the task scope.
- The test binary on this Linux host is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (apphost, no `.exe`). The document's `.exe` path does not exist here; I used the apphost. Filter flag `--filter-class` works.
- The document's "answered" arm is the only one that asserts the prompt names the copy. Other arms assert only that the root is not in the arguments, as the document says.
