# CR4 — review notes (Haiku), stories E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-4`, HEAD `eec894ae` at review start. Written BEFORE any code change (Step 1).
Deadline 15:41 UTC; review started about 15:10 UTC.

## What was checked

- Rules: `rules.mjs explain --task audit --task implement --task test` on the changed files; selected rules read.
- Plan: §0, E5.4, E5.5, E5.6, *Where the parent plan and the code disagree*, *E5.5–E5.6 as built*.
- Product diff: `shared/runtime-capabilities.json`, `shared/capability-matrix-vectors.json`, `ConfinementPlanner.Kimi`, `RuntimeCapabilities.Runtimes`, `KimiConsultant.MustCarryNoFlag`, the generated TS.
- Tests: `AKimiConsultantCheckTests` (S5.5.1–4), `AKimiQuestionRowTests` (S5.6.7), `CapabilityMatrixTests` (S5.6.1, S5.6.4), `ConfinementPlannerTests` (S5.6.3, S5.6.5, S5.6.6), `capabilityAdmission.test.ts` (S5.6.8).
- Facts opened in the repo: the check's state words (`ConsultCheckStates.Answered`, `CanaryReadings.NotAttempted`), the scratch prefix (`coai-check-`, `ConsultCheckScratch.cs:42`), the Vibe planner arm (`ConfinementPlanner.cs:198-202`, same shape as Kimi's), the RESULTS sections cited by the rows (§6 M-K3, §7 M-K4, §9 M-K6, §12 M-K9, §14, §21 M-K9 (e) all exist in `research/RESULTS_kimi_confinement.md`).

Scenario names: all 12 tests exist with the table's names (S5.5.1–4 in `AKimiConsultantCheckTests`, S5.6.1 `KimiHasThreeRows_EachCitingItsCells`, S5.6.2 the existing `TheVectorsCoverKimi`, S5.6.3 `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`, S5.6.4 `AdmissionFlagsBothRows`, S5.6.5 `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`, S5.6.6 `APlanWithFlags_IsRefusedForKimi`, S5.6.7 `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind`, S5.6.8 `kimi rows admit flagged, web refuses naming its record`).

Decisions checked: planner arm (disk = Snapshot with copied roots, none = Scratch, web refused by the matrix, no flags); no flags for kimi (the arm passes none, `MustCarryNoFlag` refuses a plan with `Leading` or `Flags`); the rows and vectors match the plan's cells (none: M-K6, M-K3, M-K4; disk: M-K6, M-K3, M-K4, M-K9, M-K9 (e); web: unmeasured, §14); the generated table is the generator's output (checked by the generator run in Step 2).

## Findings

**H1 — minor.** `src_mcp/tests/AKimiQuestionRowTests.cs:150-154` (S5.6.7, the `cancelled` arm). The arm never asserts that the shim started, so the cancellation may not have happened mid-turn. The cancel fires inside the shim's `During` callback, so the test is not vacuous, but the table says "cancelled mid-turn" and S5.5.3 asserts `played.Shims.Should().ContainSingle("the turn had started")`.
Fix: assert the shim started in the cancelled arm, the same as S5.5.3.

**H2 — minor.** `shared/runtime-capabilities.json` (the three `kimi` rows' `note`) and `src_vs_code/src/runtimeCapabilities.generated.ts`. E5.6 decision 2 says each row's `note` carries the version (2.1.1). The version is in `measuredWith.version` only. The notes say "WebSearch and FetchURL are announced" but not the version.
Fix: add `kimi 2.1.1` to all three notes, then regenerate the TS with the generator.

**H3 — major.** `src_mcp/tests/AKimiConsultantCheckTests.cs:31-35`, `src_mcp/tests/AKimiQuestionRowTests.cs:49-52`. Both new classes set the process-wide statics `GitScratch.AfterUnlink` and `KimiHome.AfterUnlink`, and carry no `[Collection]`. `KimiCanaryTests` (`:119-143`, `:205-212`) and `JunctionsTests` (`:384-392`) set the same statics. xUnit runs classes in parallel, so one class's recorder can capture another class's deletion (a spurious red) or be nulled while a deletion is in flight (a vacuous green in the "junction first" check).
Fix: put the four classes in one shared `[Collection]` (`DisableParallelization` is not needed), in the same style as the `fakecli-env` collections. Check first what class `JunctionsTests.cs:384` is in. If that is a bigger change than the time allows, report it as not fixed.

**H4 — not a defect (wrong on a second look).** S5.5.1's table says the check is "passed". The check's state word for a turn that answered is `answered` (`ConsultCheckStates.Answered`), and the test asserts `answered`. The table's word is loose, the test is right. No code change.

**H5 — noted, not re-run.** E5.5 needed no product code, so S5.5.1–4 and S5.6.7 were green on arrival. The coder's two temporary breaks are recorded in the coder notes (`CR4-code.md`, decision 1). I did not re-run the breaks: the time left does not allow a mutation run plus the full suite. The "fail without the change" check for E5.5 therefore rests on the coder's note.

## Fix log

- **H1 — fixed.** `AKimiQuestionRowTests.cs`: the cancelled arm now asserts `played.Shims.Should().ContainSingle(...)`. This is a pin on an existing behaviour, so it was green on the first run; no red was produced for it.
- **H2 — fixed.** `shared/runtime-capabilities.json`: the three `kimi` notes start with `kimi 2.1.1 —`. Regenerated with `generate-runtime-capabilities.mjs`; `--check` is clean (21 rows over 7 runtimes × 3 capabilities).
- **H3 — fixed.** `[Collection("unlink-hooks")]` on `AKimiConsultantCheckTests`, `AKimiQuestionRowTests`, `KimiCanaryTests` and `JunctionsTests`, the four classes that set `GitScratch.AfterUnlink` or `KimiHome.AfterUnlink`. No test changed behaviour. Whether the race was real in this run is not known; the collection removes the possibility.
- **H4 — not a defect.** See above.
- **H5 — not re-run.** See above.

Results (on this host, Linux WSL2, `WindowsStandIn`):
- Build: 0 warnings, 0 errors (after the fixes).
- Generator `--check`: up to date.
- Format: `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes --no-restore` exits 0. The whole-solution format check was not run in this review.
- TS (`npm --prefix src_vs_code test`): 6270 pass, 1 fail — `the baseline names no release that was never tagged` (known, no tags on this clone).
- Whole MCP suite (after the fixes, 7m 23s): total 10555, failed 19, succeeded 10452, skipped 84. Log: `/home/jinx/git/coai-exp/notes/CR4/suite-after.log`. The 19 failures are the coder's known set: 7 `ACodeRoundRecordsItsSizeTests`, 2 `ARoundIsMintedAKeyTests`, 3 `ARoundsNumberComesFromTheJournalTests`, 3 `EndToEndTests`, 1 `SplitOrderTests`, 1 `ThePersonsCommandsReachTheCallerTests` (rounds database empty on this host), plus the 2 known Kimi failures (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_…`). None of the new Kimi scenario tests failed.

Commits: `a01f6a0b` (H1, H3), `2790f239` (H2). Nothing pushed.

Not done: whole-solution `dotnet format` (the coder saw AOT analyzer errors in `src_server`, not touched here); re-running the coder's temporary breaks for S5.5; a reproduction of the H3 race.
