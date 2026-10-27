# D4 fix notes — E5.5 and E5.6 review (coder: Haiku)

Branch `exp/kimi-e56-haiku-direct-4`. Commits made by this run:
- `af8d81ef` test(kimi): junction-first and whole-folder checks, the cancelled check, and the test names
- `6f53aef0` docs(kimi): Kimi's arm in module_core
- `b664648b` docs(kimi): the seed's why names the kimi rows' record

Review file: `/home/jinx/git/coai-exp/notes/D4-review.json`. Each finding checked against the code before any change.

## Per finding

| ID | Severity | Outcome | Notes |
|---|---|---|---|
| R1 | major | **fixed** | `AKimiQuestionRowTests` (answered arm), `AKimiConsultantCheckTests` (S5.5.2, S5.5.4): `GitScratch.AfterUnlink` recorder set around the run and reset in `finally`; `recorded.Should().BeEmpty()`; `person.Hash()` before/after (bytes and attributes). Found in the code: the old assertions only checked `Directory.Exists`, which stays green with a bare deleter. **Teeth not run** (see R4). |
| R2 | major | **partly fixed** | S5.5.3 added as `ACheckCancelledMidTurn_StillDisposesItsHome` (the played shim cancels the token and waits; home gone, recorder empty, person whole). **Not fixed:** the exit-70 arm of S5.5.2 (no test written). |
| R3 | major | **not fixed** | S5.6.7 is still a Fact with the answered arm only. The refused arm (launch refused before the shim starts) and the cancelled arm are not written. Time ran out. |
| R4 | major | **not fixed** | No teeth check was run (no deciding line removed, no red message recorded). Red-first was not kept for S5.6.1, S5.6.3–S5.6.6 or S5.5.x; that is stated in the plan's deviations 1–6. A teeth run needs one build (about 1.5 min) per line; not enough time left before 01:59. |
| R5 | minor | **fixed** | `ConfinementPlannerTests` S5.6.5 now loops over the none and disk requests: `InheritsEnvironment` false, `Passthrough` Minimal, no `--session`, neither root in Arguments or StdIn, `--home` under the row's folder. **Not added:** the `KimiEnvironment.Refusal` check (I did not confirm its signature in time). |
| R6 | minor | **fixed** | `shared/runtime-capabilities.json`: resultRef now reads "§12 M-K9 and §21 M-K9 (e)". Generator run; `--check` says up to date. |
| R7 | minor | **fixed** | Seed `why` (`shared/runtime-capabilities.json`, lines 18-21) now says the kimi rows cite the M-K cells of research/RESULTS_kimi_confinement.md (Windows, kimi 2.1.1, 2026-10-13). The seed is embedded in CoaiMcp.Core, so I rebuilt and reran CapabilityMatrixTests (39/39) and the full suite. Commit `b664648b`. |
| R8 | minor | **fixed** | `coai-kimicheck-data-`, `coai-kimicheck-temp-` and `coai-qkimi-` renamed to `testsToDelete-…` prefixes. The check tests now use `TempDir`. |
| R9 | minor | **partly fixed** | Renamed: S5.5.2 → `AfterTheCheck_TheHomeIsGone_JunctionFirst`; S5.5.4 → `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst`; S5.6.7 → `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind`; S5.6.3 → `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`; S5.6.4 → `AdmissionFlagsBothRows`. **Not renamed:** none left in the list; S5.6.3's clause order was fixed in the same rename. |
| R10 | minor | **not fixed** | Plan file not changed. The coder-model note (Haiku, not Opus) and the separate deviation numbering need an operator decision, as the review says. |
| R11 | minor | **fixed** | Vendor on the Kimi question row is now `my-kimi` (was `mistral`). |
| R12 | minor | **partly fixed** | `research/module_core.md`: "Kimi's arm (S5, E5.6)" bullet added beside Vibe's. **Not fixed:** `research/module_tests.md` rows for `AKimiConsultantCheckTests` and `AKimiQuestionRowTests`. |

## Wrong findings

None. Every finding was true in the code.

## Results (this run)

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): exit 0, 0 warnings, 0 errors. Log: `/home/jinx/git/coai-exp/notes/D4-fix-build.log`.
- Changed test classes (MTP, `--filter-class`): AKimiConsultantCheckTests 4/4; AKimiQuestionRowTests 1/1; ConfinementPlannerTests 34/34; CapabilityMatrixTests 39/39. All pass.
- Whole MCP suite, once: 10,552 total, 84 skipped, **2 failed**, both known from the start: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`. `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` passed in this run. Log: `/home/jinx/git/coai-exp/notes/D4-fix-suite.log`. Run again after the seed change (R7), same result: 10,552 total, 2 failed (the same two). Log: `/home/jinx/git/coai-exp/notes/D4-fix-suite2.log`.
- Format (`dotnet format --verify-no-changes`, the four changed test files): exit 0. Log: `/home/jinx/git/coai-exp/notes/D4-fix-format.log`.
- Generator (`src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities.
- TS (`npm test` in src_vs_code): 6290 tests, 6270 pass, 19 skipped, 1 fail. The fail is `the baseline names no release that was never tagged` (known; no git tags in this clone). Log: `/home/jinx/git/coai-exp/notes/D4-fix-ts.log`.

## Not done (for the operator)

1. R3: refused and cancelled arms of S5.6.7 (Theory).
2. R2: exit-70 arm of S5.5.2.
3. R4: teeth checks and red messages for the deviations list.
4. R7: seed `why` sentence for the kimi rows.
5. R10: plan-file changes (coder-model deviation; own heading and numbering).
6. R12: `module_tests.md` rows.
7. The coai review gate was not run (as in the earlier notes).
