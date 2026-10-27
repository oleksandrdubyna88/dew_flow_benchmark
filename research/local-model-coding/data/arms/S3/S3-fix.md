# S3-fix — review fixes for E5.5 and E5.6 (coder: Haiku)

Branch: exp/kimi-e56-sonnet-split-haiku-3
Commits: 3a350ed3 (test fixes), b9b88a4a (plan deviation 54). Not pushed.
Review: notes/S3-review.json (verdict fix-then-ready; no blocking, no major; five minor findings R1–R5).

## Per finding

- R1 (minor) — FIXED. Renamed the S5.6.3 test to `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`, the name the plan gives.
- R2 (minor) — FIXED. Added `KimiEnvironment.Refusal(request, KimiPlaces.ForShim(FlagOf(request, HomeFlag), RowFolder/persons-bin))` must be empty, for each built request, in the S5.6.5 test. Corrected deviation 54/t2: no `KimiFixture` is needed.
- R3 (minor) — NOT FIXED (reason). The refused arm has no shim request, so there is no `coai-question-*` folder path to find. The only other way is to enumerate the shared temp root before and after. That races with the other tests, which also make `coai-question-*` folders. The plan's deviation 54/t3 now records this.
- R4 (minor) — FIXED. Removed the unused `Other` property in `AKimiQuestionRowTests.cs`. Deviation 54/t3 updated.
- R5 (minor) — FIXED. Added `private static Runners.Kimi.KimiConsultant KimiAt()` in `ConfinementPlannerTests.cs`. It is used by the S5.6.5 test, the S5.6.6 test and the `Adapters()` list. The plan's copy of the expression is gone.

No finding was wrong. No defect was found, so no red-first test was needed. All edits are in tests and the plan.

## Results

- Build (`dew_flow_connect_other_ais.slnx`, Debug, `-m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors. Log: notes/S3-fix-build.log.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.ConfinementPlannerTests`: 34 of 34 passed. Log: notes/S3-fix-planner.log.
- `CoaiMcp.Tests --filter-class CoaiMcp.Tests.AKimiQuestionRowTests`: 3 of 3 passed. Log: notes/S3-fix-row.log.
- Whole MCP suite (once): see the final section below.
- `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes`: exit 0. Log: notes/S3-fix-format.log.
- `node src_vs_code/scripts/generate-runtime-capabilities.mjs --check`: up to date, 21 rows, exit 0.
- plan-lifecycle.mjs: clean.
- Extension `npm test`: see the final section below.

## Known failures (not from this change)

- KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn
- KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed
- Extension: the baseline names no release that was never tagged (the clone has no git tags)

## Final results

- Whole MCP suite (once): 10554 total, 10468 passed, 2 failed, 84 skipped. The two failures are the known host failures named above (KimiHomeTests.AHomeWhoseDataPathPasses260, KimiRuntimeTests.ALaunchThatNamesItsHome). Log: notes/S3-fix-suite.log.
- Extension npm test: 6290 tests, 6270 pass, 1 fail (the known tagless-checkout case), 19 skipped. Log: notes/S3-fix-ts.log.
- The suite log shows "the log could not be flushed: the log's disk is full" once. The root disk has 860G free, so I read this as a test that simulates a full disk, not a real fault. Not verified further.
