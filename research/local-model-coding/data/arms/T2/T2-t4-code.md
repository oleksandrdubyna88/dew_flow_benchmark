# T2 t4 — code notes

## Done
- Added `src_mcp/tests/AKimiQuestionRowTests.cs` (one new file, no production change).
- Test `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with three cases: answered, refused before the launch, cancelled mid-turn.
- Committed with a conventional message (file added by name). Not pushed.

## Results
- Build (`dew_flow_connect_other_ais.slnx`, Debug): 0 warnings, 0 errors.
- `AKimiQuestionRowTests`: 3/3 green (t1 was in the base).
- Mutation (`Release(homes);` commented out in `QuestionRowLaunch.cs` finally): all 3 cases red. Reverted with `git checkout`; `git diff --stat` clean. Re-run after revert: 3/3 green.
- Whole MCP suite: 10557 total, 2 failed, 10471 passed, 84 skipped. The 2 failures are the known ones:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`

## Red messages that differed from the document
- Mutation, `answered`: first failing assertion was the one-shot folder check (`the row's one-shot folder goes with it`). Matches the document.
- Mutation, `cancelled mid-turn`: failed on `finally, whatever ended it` (the contract's wording), not `the row's one-shot folder goes with it`. Same cause.
- Mutation, `refused before the launch`: ALSO red, with `coai-vh/<8> is gone after every ending` (a folder left under coai-vh). The document predicted only answered and cancelled. The refused row reserves its one-shot folder before the model check, so the refusal path also depends on `Release(homes)`.

## Not done / not checked
- I did not check for leftover `testsToDelete-qkimi-*` folders in TEMP. A command that listed /tmp was blocked by the approval gate, and I did not retry it.
