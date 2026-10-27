# T3 / t4 — code notes

Task: `vllm/work/T3/t4.md` (tests only; E5.6 / S5.6.7). Branch `exp/kimi-e56-tasks-3`.

## Done
- New file `src_mcp/tests/AKimiQuestionRowTests.cs` (class `AKimiQuestionRowTests`, one Theory
  `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with InlineData answered / refused / cancelled).
- Commit `716bef8d` (only that file). Not pushed.
- No production change.

## Deviations from the document
1. `TheOneShotFolderIsEmpty` guards with `Directory.Exists` before enumerating. The document's line
   `Directory.EnumerateFileSystemEntries(...)` throws if a refused row never made `coai-vh`. The assertion
   message is the one the document gives.
2. The homes root is its own field, `_homes = TempDir.For("testsToDelete-kimi-qvh-")`, and it is disposed in
   `DisposeAsync`. The document names this folder but does not list the field.
3. The theory keeps the three endings in helpers (`AskTheRowAsync`, `DuringFor`-style ternary) to hold the
   method under complexity 4. `CancelDuringTurn` is a helper.
4. The cancelled arm uses a named `asking` local. A lambda cannot take `.Should()` (first build error, CS0023).

## Red / green
- Red: NOT observed. Every case skips on Linux with the D18 reason:
  `D18: Kimi is refused on every host but Windows`. As the document says.
- Build: succeeded, 0 errors, 0 warnings.
- `--filter-class CoaiMcp.Tests.AKimiQuestionRowTests`: 3 skipped, 0 failed.

## Whole suite (CoaiMcp.Tests, Linux)
Total 10556, failed 4, succeeded 10460, skipped 92.
- `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known, not mine.
- `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known, not mine. Re-run alone: 1 failed.
- `QuestionRowOnAgyScenarioTests.ADeniedCommand_IsContinuedInTheSameConversation_AndTheRowAnswers`
- `QuestionRowOnAgyScenarioTests.AFollowUpCutShortByTheCaller_StillBillsTheFirstLaunch_AndNeverAsOk`
  These two are NOT in the document's known list. Run alone, the class is 5 succeeded, 0 failed. They look
  timing-dependent (`WaitUntilAsync`, a 20 s wait) and this change does not touch them. Not investigated further.
  Confirmation would need a second full-suite run, which I did not do before the deadline.
- So the document's "green except the two known failures" is NOT met on this run: two extra failures appear,
  both in a class that passes alone.

## `dotnet format`
`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` printed IL2026/IL3050 analyzer errors in
`src_server` (`ReviewEndpoints.cs`, `CatalogEndpoints.cs`). Those are not mine. No format finding names
`AKimiQuestionRowTests.cs` (checked with `--include`). The solution-wide format check did not finish clean.

## Left
- Red/green on Windows: needs a Windows host to see the three cases run.
- The two `QuestionRowOnAgyScenarioTests` failures: check on a second full run.
- The `coai-vh` existence guard (deviation 1): if the Windows run shows a refused row never reserves the folder,
  the guard is what keeps it green; otherwise the guard can be removed to match the document exactly.
