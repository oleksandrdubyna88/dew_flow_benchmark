# S3 t2 — code notes (E5.5/E5.6, planner's Kimi arm and KimiConsultant refusal)

Branch: exp/kimi-e56-sonnet-split-haiku-3. Commit: c3132170 (not pushed).

## What I did

1. Wrote the four tests (red first) in `src_mcp/tests/ConfinementPlannerTests.cs`:
   - `KimiPlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (S5.6.3)
   - `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` (S5.6.5)
   - `APlanWithFlags_IsRefusedForKimi` (S5.6.6)
   - Line ~167 `AnUnknownRuntime_IsRefused_NamingTheOnesItKnows`: added `, kimi` to the expected list.
2. Added `"kimi" => Kimi(grant, flag),` and the `Kimi` method in `ConfinementPlanner.cs`, as the contract says. `SandboxVocabulary` unchanged.
3. Added `MustCarryNoFlags` and its call in `KimiConsultant.Build`, as the contract says.
4. Teeth: removed the `MustCarryNoFlags` call → S5.6.6 red; reverted at once.

## Red messages (differences from the document)

- S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot ... but found CwdKind.Scratch.` The document says `plan.Cwd`; my local variable is named `disk`. Same failure, different name.
- S5.6.5: `Expected disk.Request.WorkingDirectory to be "<RowTree>" ... but they differ at index 10` (FluentAssertions string diff, actual `/tmp/coai-scratch-…`). Same cause as the document (the default arm gives Scratch).
- S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` Matches the document.
- Line ~167 test was NOT red before my edit: t1 had already put `kimi` in the runtime list, so the old expected text passed. I made the edit anyway, as the contract asks.

## Test results

- Build (`dew_flow_connect_other_ais.slnx`, Debug): 0 warnings, 0 errors.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.ConfinementPlannerTests`: 34 total, 34 passed.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.KimiConsultantTests`: 12 total, 12 passed.
- Known host failures (not mine, not run here): `KimiRuntimeTests.ALaunchThatNamesItsHome...`, `KimiHomeTests.AHomeWhoseDataPathPasses260...`, the extension tag test. I did not run the whole suite.

## Deviations and gaps

- S5.6.5: I did NOT add the optional `KimiEnvironment.Refusal(request, places)` assertion. It needs a `KimiPlaces` for a real person's home, which `KimiFixture` provides and `ConfinementPlannerTests` does not have. The document said to add it only if reachable; it is not reachable without the fixture, and I did not use the fixture because the document named only the files above.
- The test binary on Linux is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (an apphost-style runner); `CoaiMcp.Tests.exe` in the document does not exist on this host, so I ran the DLL-backed runner.
- Nothing left undone from the steps.
