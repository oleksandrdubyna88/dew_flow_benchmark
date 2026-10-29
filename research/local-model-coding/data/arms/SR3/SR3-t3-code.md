# SR3 t3 — code notes (S5.6.7, E5.6 decision 5)

## What I did

- Added `src_mcp/tests/AKimiQuestionRowTests.cs` with the four tests named in the task document:
  1. `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind_WhenAnswered`
  2. `AKimiDiskRow_RefusedBeforeTheLaunch_LeavesNothingBehind`
  3. `AKimiDiskRow_CancelledMidTurn_LeavesNothingBehind`
  4. `ANoneKimiRow_StandsInAnEmptyFolderBesideItsHome_AndLeavesNothingBehind` (prompt `question-opinion`, no roots)
- Commit `4a02817b` (test file only, added by name).
- No production change. `src_mcp/src` and `src_mcp/runners` are unchanged against HEAD.
- Helper `Service(...)` takes `(PlayedKimi, homesRoot, params roots)` and delegates to `ServiceWithPrompt(...)`, which takes the prompt id. Test 4 needs a different prompt, so the prompt is a parameter; the default is `question-disk`.

## Red / green

- On arrival (t1/t2 present in the tree): 4/4 green.
- Teeth checks, one run each, then reverted with `git checkout --` on the touched files:
  - (i) `Release(homes)` commented out in `QuestionRowLaunch.RunAsync` `finally`: all 4 red. Message: `Expected Directory.EnumerateFileSystemEntries(... coai-vh) to be empty ... but found at least one item {".../coai-vh/<8>"}`. The document's suggested message was `Directory.Exists(...) to be False`; the "not empty" message is the one the document also allowed.
  - (ii) `Remove(scratch)` commented out: 3 red (answered, cancelled, none-row). Message: `Expected Directory.Exists(Path.GetDirectoryName(cwd!)!) to be False because the row's scratch is gone, but found True.` Test 2 (refused) stays green: its contract has no scratch assertion.
  - (iii) Kimi junction-first call in `VibeHomes.EmptiedUnderItsLock` made unreachable (`if (folder.Length < 0)`): 3 red (answered, cancelled, none-row). Message: `Expected recorded to be empty because KimiHome unlinked the junction first, so the deleter never met it, but found at least one item {".../kh-f427d4937a/home/credentials"}`. Test 2 stays green: no Kimi home is made before the refusal.
- After reverting: class re-run was 4/4 green (`--filter-class`, run once before break (i) as the initial pass).

## Full MCP suite (after revert, solution rebuilt)

Total 10557, failed 3, succeeded 10470, skipped 84, duration 7m 57s.

- `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — on the known-failure list.
- `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — on the known-failure list.
- `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT on the list. Re-run alone: 10/10 passed. It looks load-dependent (it ran alongside the full suite). Not investigated further, and not touched by this change.

## dotnet format

`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` exits 2 on this host. All reported errors are IL3050/IL2026 analyzer errors in `src_server/src/CoaiServer.csproj` (`SessionEndpoints.cs`, `CatalogEndpoints.cs`, `UsageEndpoints.cs`), which format's build step hits. Nothing from `AKimiQuestionRowTests.cs` is in the log. I did not change `src_server`. So format could not complete its check on this host; this is not verified clean.

## Left / for the reviewer

- Test 2 (refused before launch) does not assert that the row's scratch folder is gone. The contract did not list that assertion; if wanted, it is a one-line addition.
- Test 1 asserts the copies by name from `Directory.GetDirectories(cwd)` recorded during the shim run, not from a fixed name.
- Two unrelated items for the reviewer: the `TheRoundsNumstatsAreCountedTests` failure above, and the `dotnet format` analyzer errors in `CoaiServer.csproj`.
