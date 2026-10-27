# DR3 — coder notes: E5.5 and E5.6 (branch exp/kimi-e56-haiku-direct-review-3)

Start: b4e2ea96. Commits: 8146e6eb (E5.6), b20c9f7b (E5.5 check tests, S5.6.7 row tests), plus the plan status and deviations commit.
Spec: todo/PLAN_vibe_and_kimi_runtimes_E5.md, E5.5 and E5.6 sections. Deviations are recorded in the plan (*E5.5–E5.6 as built*, 50–58).

## Scenarios

| # | Test | Red seen? | Green? | Note |
|---|------|-----------|--------|------|
| S5.5.1 | AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome | no (green on arrival) | yes | table's red ("before E5.4") no longer occurs; copy is own\tree\snapshot-<hex> |
| S5.5.2 | AfterTheCheck_TheHomeIsGone_JunctionFirst | yes, by teeth run (disposal removed): folder found with an item | yes | table's red ("before E5.3 g") no longer occurs |
| S5.5.3 | ACheckCancelledMidTurn_StillDisposesItsHome | yes, by teeth run | yes | record is state failed, failureKind cancelled |
| S5.5.4 | ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst | no (green on arrival) | yes | teeth: not run (no disposal dependence) |
| S5.6.1 | CapabilityMatrixTests.KimiHasThreeRows_EachCitingItsCells | yes: *Expected … not to be <null>* (before the seed rows) | yes | |
| S5.6.2 | CapabilityMatrixTests.TheVectorsActuallyLoaded_… (existing) | NOT observed | yes | red exists only between seed and vectors; not run |
| S5.6.3 | ConfinementPlannerTests.ThePlannerArm_… | yes: planner refuses kimi as unknown | yes | table predicted a CwdKind.Scratch default arm; the real red was the refusal |
| S5.6.4 | CapabilityMatrixTests.AdmissionFlagsBothKimiRows_… | yes: refused by name | yes | |
| S5.6.5 | AKimiQuestionRowTests.AKimiQuestionLaunch_… | yes: plan cast failure (refused) | yes | |
| S5.6.6 | AKimiQuestionRowTests.APlanWithFlags_… | yes: no exception (stub) | yes | |
| S5.6.7 | AKimiQuestionRowTests.AKimiDiskRow_… (3 facts) | NOT observed | yes | green on arrival; no teeth run |
| S5.6.8 | capabilityAdmission.test.ts: kimi rows admit flagged, web refuses … | NOT observed | yes | written before the seed change, not run first |

## Red messages that differ from the tables

- S5.6.3: the table predicted *Expected plan.Cwd to be CwdKind.Snapshot … found CwdKind.Scratch* (default arm). The real red was the planner refusing `kimi`, because the planner had no kimi arm and no kimi row.
- S5.5.1 to S5.5.4 and S5.6.5 to S5.6.7: the tables' reds name states before E5.3 g / E5.4, which are already in place.

## Decisions I made

- Kimi plans carry no flags, ever (E5.6 decision 1). `KimiConsultant.RefuseAFlaggedPlan` refuses any plan with `Flags` or `Leading` (decision 3).
- Kimi's `none` and `disk` rows are `unconfined`, and `web` is `unmeasured`, as E5.6 decision 2 says. Citations use the record's own section numbers (M-K3 §6, M-K4 §7, M-K6 §9, M-K9 §12; web §7 and §14).
- The vectors and the generated table are data. The table was regenerated with its generator, and `--check` is clean.
- The record's `kimi` cells are M-K3, M-K4, M-K6 and M-K9. No web cell is cited.

## Test results

- Generator: `node scripts/generate-runtime-capabilities.mjs` written, then `--check`: up to date, 21 rows over 7 runtimes × 3 capabilities.
- Extension (`npm test`) before the E5.5 commit: 6290 tests. One failure, the known one at the start: "the baseline names no release that was never tagged" (no tags in this clone). S5.6.8 green. The generated-file checks are green.
- Format: `dotnet format` on the solution exits 2 on analyzer errors in src_server (38) and src_bugs (16), which this branch does not touch. `src_mcp/tests`, `src_mcp/core` and `src_mcp/runners` verify clean.
- Whole MCP suite: see the result line below.
- plan-lifecycle: clean.

## Remains

- S5.6.2 and S5.6.8 were not observed red. A red run would need the kimi rows without their vectors (C#) and the seed without the regenerated TS (TS).
- S5.5.1, S5.5.4 and S5.6.7 have no teeth runs of their own.
- `StandIn` is copied into two test classes; a shared helper is a follow-up.
- The live checks (J2 in the plan: one three-turn consult, one question row per capability, one `--check-consultant`, in RESULTS §31) are out of scope and not run. Nothing that spends money was run.
- research/module_*.md and RESULTS files were not edited (out of scope).
- Merge to main, push, PR, release: not done.

## Whole MCP suite (final)

- First full run: 10555 total, 5 failed, 10466 passed, 84 skipped. Failures: the two known ones, plus three new ones (AKimiConsultantCheckTests.AfterTheCheck_TheHomeIsGone_JunctionFirst, ACheckCancelledMidTurn_StillDisposesItsHome, and ConsultantCheckTests.AVibeCheckThatEndsWithoutAnAnswer_LeavesNoFolderUnderCoaiVh). Each left a one-shot folder behind. All three passed alone.
- Cause (likely, not proven): GitScratch.AfterUnlink is a process-wide static that several classes set, and most of them have no collection, so they run in parallel.
- Fix: AKimiConsultantCheckTests and AKimiQuestionRowTests moved into the "fakecli-env" collection (commit d75d9edd).
- Rerun: 10555 total, 2 failed, 10469 passed, 84 skipped. The two failures are the known ones: KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed and KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn.
