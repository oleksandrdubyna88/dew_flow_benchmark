# DR1 — review of E5.5 and E5.6 (Haiku reviewer, 2026-10-27)

Branch exp/kimi-e56-haiku-direct-review-1. Reviewed: 564e4d55, c15e4df1, 111f205b against b4e2ea96.
Findings written BEFORE any code change. Each: id, severity, file:line, one sentence, fix.

## Findings

H1. major — src_mcp/tests/AKimiConsultantCheckTests.cs:50,87,117,154 and src_mcp/tests/AKimiDiskQuestionRowTests.cs:145,187.
Spec (E5.5 S5.5.1–S5.5.4, E5.6 S5.6.7) says the fake person's folder is intact "bytes and ReadOnly"; the tests only assert `Directory.Exists`, so a walk that deleted the person's files would pass.
Fix: take `person.Hash()` before the run and assert it equal after (the fixture's helper, which exists for this).

H2. major — src_mcp/tests/AKimiDiskQuestionRowTests.cs:118–188.
S5.6.7's Then says "the recorder never invoked" (junction-first); no test there sets `GitScratch.AfterUnlink`, so a bare-delete regression in the disk row would not be caught.
Fix: install the recorder in the answered and cancelled facts and assert it stays empty.

H3. major — src_mcp/tests/AKimiConsultantCheckTests.cs (all four facts) and the four S5.5 scenarios.
S5.5.1–S5.5.4 were green on arrival and no teeth run was done; the plan requires red first for every behaviour and a teeth check for green-on-arrival tests.
Fix: break the disposal in ConsultantCheck (a bare delete instead of junction-first) and show S5.5.2/S5.5.4 go red for the recorder reason; restore; record in notes.

H4. major — AKimiConsultantCheckTests.cs:64,98,139 and AKimiDiskQuestionRowTests (hook use via the service); also pre-existing KimiCanaryTests.cs:132, JunctionsTests.cs:384, AOneShotKimiHomeIsDisposedJunctionFirstTests.cs:38.
`GitScratch.AfterUnlink` is a process-wide static and `xunit.runner.json` runs classes in parallel (maxParallelThreads 8), so one class's recorder can fire in another class's test.
Fix: put the new classes in one `[Collection]` with `DisableParallelization` that the other hook setters also share; if that grows past the cheap set, report it as pre-existing and leave the old classes alone.

H5. minor — shared/runtime-capabilities.json (kimi rows' resultRef) and the generated TS mirror.
The section numbers are wrong: M-K6 is RESULTS §9 (not §5/§11), M-K3 is §6, M-K4 is §7, M-K9 is §12 and §21 (not §19); §5 is M-K2 and §11 is M-K8.
Fix: correct the section numbers in the seed and regenerate the TS file with its generator.

H6. minor — src_mcp/tests/AKimiConsultantCheckTests.cs:120–155 (S5.5.3).
The spec's "the record says cancelled" is asserted only as `NotBe("answered")`; the check's cancelled state is `ConsultFailure.Cancelled` (ConsultantCheck.cs:188).
Fix: assert the state (or the failure) names the cancel, if the output carries it cheaply.

H7. minor — spec naming: S5.6.2 is the existing `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`, S5.6.7 is three facts `ADiskRow…`, not the spec's names.
Not recorded as a deviation in the plan.
Fix: note in the plan's as-built list (no code change).

H8. minor — src_mcp/tests/AKimiConsultantCheckTests.cs:41–50 and :44 (S5.5.1).
The spec's "scratch repository and its canary unchanged" is not asserted directly; it holds only because `Judged` would mark a breach as not answered.
Fix: leave as is, or assert the record; cheap to add only if time allows.

H9. minor — AKimiQuestionRowTests.cs:21–25 and AKimiDiskQuestionRowTests.cs:195–204 (duplicated `Stood`/`StandIn` helper across two files).
Duplicated test scaffolding.
Fix: not done (time); note only.

H10. minor — src_mcp/tests/CapabilityMatrixTests.cs:203 (`TheFlags_AreD13s…`).
The D13 flag test does not mention kimi, though kimi is now flagged.
Fix: not done; the flag is asserted in AKimiQuestionRowTests.AdmissionFlagsBothRows and the TS test.

## Checked and found right (no finding)
- All 12 scenario names exist (S5.6.2, S5.6.7 as in H7); S5.6.1, S5.6.3–S5.6.6, S5.6.8 assert what the table says.
- The planner arm (ConfinementPlanner.cs:205–213): disk → snapshot of roots, none → scratch, no flags; web never reaches it.
- The refusal of flags (KimiConsultant.cs:62–70) is before the launch, and is caught by QuestionRowLaunch's ArgumentException path.
- The seed's cells M-K6, M-K3, M-K4, M-K9, M-K9 (e) exist in RESULTS_kimi_confinement.md; M-K3 "0 of 9" and M-K4 "0 of 6" are correct.

## Status after the fixes (commits 4e7d8e75, 22399893)

- H1 — FIXED. The four S5.5 facts and the two S5.6.7 facts now compare `person.Hash()` (entries, attributes, bytes) before and after, not only `Directory.Exists`. Commit 4e7d8e75.
- H2 — FIXED. The disk row's answered and cancelled facts install a `GitScratch.AfterUnlink` recorder (`RecordingUnlinks`) and assert it stays empty. Commit 4e7d8e75.
- H3 — NOT YET (see the end of this file for the teeth run, if it was done in time).
- H4 — FIXED. The six classes that set a process-wide unlink hook now share `[Collection("kimi-deleter-hook")]` with `DisableParallelization`, defined in the new file `src_mcp/tests/KimiDeleterHookCollection.cs`. The existing classes KimiCanaryTests, JunctionsTests and AOneShotKimiHomeIsDisposedJunctionFirstTests were changed by one attribute each, because the race is with them too. Commit 4e7d8e75.
- H5 — FIXED. The seed's `resultRef` now cites M-K6 at §9, M-K3 at §6, M-K4 at §7, M-K9 at §12 and §21. The TypeScript copy was regenerated, and `--check` says "up to date". Commit 22399893.
- H6 — NOT FIXED (time). The S5.5.3 assertion is still `NotBe("answered")`. The cancelled state is written as `cancelled` by AskAcpMode (AskAcpMode.cs:408), and a stricter assertion can follow in a later pass.
- H7 — NOT FIXED. A note only: the plan's as-built list does not record the test-name differences. No code change.
- H8 — NOT FIXED. The scratch repository and canary are checked only through `Judged`; the test does not assert them directly. Accepted as is.
- H9 — NOT FIXED. The duplicated `Stood`/`StandIn` helper in two test files. Note only.
- H10 — NOT FIXED. The D13 flag test does not name kimi; kimi's flag is asserted in AdmissionFlagsBothRows and in the TS test. Accepted as is.

## Checks run

- Build (Debug, -m:4 --disable-build-servers): succeeded, 0 warnings, 0 errors (after one fix: a missing `using Xunit;` in the new collection file, found by the build).
- Generator `--check`: "up to date", 21 rows over 7 runtimes × 3 capabilities.
- Changed classes (`--filter-class "*AKimi*"`): total 42, passed 30, failed 0, skipped 12. The skipped are the Windows-only Kimi tests (D18), as on arrival.

## H3 — teeth run (done after the suite)

- Mutation: `ConsultantCheck.TurnAsync` `held.Home.Dispose()` removed (temporary; file restored, `git status` clean).
- Result: 4 of 5 `AKimiConsultantCheckTests` facts went red with "the home is gone" messages (`ACheckCancelledMidTurn…`, `AfterTheCheck_TheHomeIsGone_JunctionFirst` ×2, `AKimiCheck_RunsInACopy…`). `ARootlessCheck…` stayed green, correctly: its disposal is in `ConsultCheckScratch.Delete`, which the mutation did not touch.
- Not shown: the junction-first ORDER (a bare delete that walks the junction). The recorder assertions were added (H2) but were not given their own teeth run. Left open; the recorder is the test for it.
- Restored; rebuilt; `--filter-class "*AKimi*"`: 42 total, 30 passed, 0 failed, 12 skipped (Windows-only).

## Final results

- Build (Debug, -m:4 --disable-build-servers): succeeded, 0 warnings, 0 errors.
- Whole MCP suite (`CoaiMcp.Tests`, log in notes/DR1-review-full-suite.txt): total 10557, failed 3, succeeded 10470, skipped 84.
  - 2 are the known ones: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_…`.
  - 1 is NOT on the brief's list: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. It fails the same way run alone (deterministic). The branch does not touch the rounds numstats code, but I did NOT run it at the base commit, so I cannot say it is pre-existing. Open.
- Format (`dotnet format --verify-no-changes --no-restore`, log notes/DR1-review-format.txt): exit 2. Only IL2026/IL3050 analyzer errors, all in src_bugs/src/*.cs and src_server/src/*.cs. No format complaint in any file this branch changed. These files are not in this branch's diff, so the errors come from the base (not verified at base).
- Generator `--check`: "up to date", 21 rows over 7 runtimes × 3 capabilities.
- TS tests (`npm test` in src_vs_code): 6290 tests, 6270 pass, 1 fail: "the baseline names no release that was never tagged" (the known no-tags failure).

## Commits on this review
- 4e7d8e75 test(kimi): person's folder compared whole, disk row records unlinks, hook classes in one non-parallel collection (H1, H2, H4)
- 22399893 docs(kimi): Kimi rows cite the RESULTS sections their cells are in (H5)
