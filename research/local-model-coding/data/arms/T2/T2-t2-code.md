# T2-t2 — code notes

Commit: `5f76113b` on `tmp/T2-t2` (not pushed).
Files changed: `src_mcp/runners/Kimi/KimiConsultant.cs`, `src_mcp/tests/KimiConsultantTests.cs`.

## What I did

1. Added the E5.6 helpers and the two tests (S5.6.5, S5.6.6) to `KimiConsultantTests.cs`, and the `Core.QuestionConsult` using.
2. Build, then red run of `KimiConsultantTests` and `ConfinementPlannerTests`:
   - `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` was GREEN at once, as the document expected.
   - `APlanWithFlags_IsRefusedForKimi` was RED for both cases (`capability: None` and `capability: Disk`) with
     "Expected a <System.ArgumentException> to be thrown, but no exception was thrown." This matches the document.
3. Added the `<para>` to the class `<remarks>`, the `MustCarryNoFlags` method, and its call in `Build` after `MustBeLaunchable`.
4. Build succeeded (0 errors, 0 warnings). `KimiConsultantTests` and `ConfinementPlannerTests`: 46/46 green.
5. Whole MCP suite: 10536 total, 10450 passed, 2 failed. Both failures are the known ones at `b4e2ea96`:
   - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
   - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`

## Differences from the document

- None in the red messages.
- `MustCarryNoFlags` checks `Leading` as well as `Flags`, as the contract's code shows. The document's Goal talks only about flags. I kept the contract's code.

## Left undone

- Nothing in scope.
- The untracked `src_vs_code/node_modules` was already present before I started. I did not touch it.
