# SR2 t4 — Kimi disk question row reads its copies and leaves nothing behind (S5.6.7)

## What I did

- Added `src_mcp/tests/AKimiQuestionRowTests.cs` (commit `89b75459`): class `AKimiQuestionRowTests : IAsyncLifetime`, one `[Theory]` `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with rows `answered`, `refused before the launch`, `cancelled mid-turn`.
- No production code changed. `QuestionRowLaunch.cs` is clean at HEAD.
- Model: copied (not inherited) from `AVibeQuestionRowReadsACopyOfItsRootsTests`. Kimi played by `PlayedKimi`; the three per-flow stand-ins are held with `using var stood = StandIn();`. No `[Collection]` (no environment variables set).
- The cancelled arm starts the ask, waits for the shim's `started` signal, cancels the ask's token, and asserts `OperationCanceledException` (the Vibe cancelled arm's expectation).

## Red / teeth runs

1. Arrival (no break): all 3 rows GREEN, none skipped.
2. Teeth break A (`QuestionRowLaunch.Launch`: `input.Prompt` instead of `input.PromptNaming(...)`):
   - `answered` RED: `Expected request.StdIn "...<prompt>..." to contain "/tmp/coai-question-vE09Ac/tree" because the prompt names the copy Kimi can read.`
   - Other two rows unaffected (as expected).
   - Restored with `git checkout`.
3. Teeth break B (`QuestionRowLaunch.Release`: `reserved.Home.Dispose()` replaced by `_ = reserved.Home;`):
   - All 3 rows RED on the one-shot home check. Message differs from the document's example. The document expected `Directory.Exists("…\coai-vh\<8>") to be False, but found True`. The test asserts the folder listing instead, so the message is:
     `Expected Directory.EnumerateFileSystemEntries(Path.Combine(Homes, VibeHomes.OneShotFolder)) to be empty because the row's one-shot home is gone, but found at least one item {".../homes/coai-vh/<8>"}.`
   - Restored with `git checkout`; `git status` clean.
   - Note: with the break, the `coai-vh/<8>` folder survives, so the scratch and the person's folder assertions were not reached in that run.
   - The recorder assertion (`_unlinked` empty) was not separately seen red: it was not reached, since the earlier assertion failed first. It was not broken on purpose.

## Results after restore

- `AKimiQuestionRowTests`: 3/3 passed.
- `AVibeQuestionRowReadsACopyOfItsRootsTests`: passed 6, skipped 12 (all Vibe tests are Windows-only, D18, so skipped on this Linux host; not a failure).
- Whole MCP suite (`CoaiMcp.Tests`): total 10558, succeeded 10472, skipped 84, failed 2. The two failures are the known ones and nothing else:
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` (PathTooLongException on Linux)
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
- `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes`: does NOT finish on this host. It aborts with IL2026/IL3050 analyzer errors in `src_server/src/*Endpoints.cs` (not my files, not touched). Scoped check `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes --include src_mcp/tests/AKimiQuestionRowTests.cs --no-restore` exits 0, so my file is format-clean.
- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: succeeded, 0 warnings, 0 errors.

## Notes and risks

- The Kimi test's temp-folder assertion compares the set of `coai-question-*` folders before/after. Other test classes running in parallel that create such folders could make it flaky. Not observed in the run.
- `GitScratch.AfterUnlink` is a static recorder. `AOneShotKimiHomeIsDisposedJunctionFirstTests` also sets it, and neither class is in a collection, as the contract specified. Not observed in the run, but a possible race.
- Nothing left undone from the task. Did not push.
